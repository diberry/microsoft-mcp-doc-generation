// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Text.RegularExpressions;
using ToolFamilyCleanup.Models;

namespace ToolFamilyCleanup.Services;

/// <summary>
/// Handles generation of large tool namespaces by batching tools into groups,
/// generating each batch separately, and merging results with H2 deduplication.
/// Prevents silent truncation when tool metadata exceeds LLM output window.
/// </summary>
public class ChunkedGenerationService
{
    /// <summary>
    /// Tools per batch for chunked generation.
    /// </summary>
    public const int BatchSize = 5;

    /// <summary>
    /// Maximum number of retries per batch before failing the pipeline.
    /// </summary>
    public const int MaxRetriesPerBatch = 3;

    /// <summary>
    /// Tool count threshold above which chunked generation is used.
    /// </summary>
    public const int ToolCountThreshold = 10;

    /// <summary>
    /// Metadata size threshold (bytes) above which chunked generation is used.
    /// </summary>
    public const int MetadataSizeThresholdBytes = 30 * 1024; // 30KB

    private static readonly Regex H2Regex = new(
        @"^##\s+(.+)$",
        RegexOptions.Multiline | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex H3Regex = new(
        @"^###\s+(.+)$",
        RegexOptions.Multiline | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly Func<List<ToolContent>, Task<string>>? _generateBatchFunc;

    /// <summary>
    /// Creates a new instance of ChunkedGenerationService.
    /// </summary>
    /// <param name="generateBatchFunc">
    /// Function that generates markdown for a batch of tools.
    /// Accepts a list of ToolContent and returns the generated markdown string.
    /// </param>
    public ChunkedGenerationService(Func<List<ToolContent>, Task<string>>? generateBatchFunc = null)
    {
        _generateBatchFunc = generateBatchFunc;
    }

    /// <summary>
    /// Determines whether chunked generation should be used based on tool count and metadata size.
    /// </summary>
    /// <param name="tools">List of tools to evaluate.</param>
    /// <returns>True if chunked generation should be used.</returns>
    public static bool ShouldUseChunkedGeneration(IReadOnlyList<ToolContent> tools)
    {
        ArgumentNullException.ThrowIfNull(tools);

        if (tools.Count > ToolCountThreshold)
            return true;

        var totalMetadataSize = tools.Sum(t => Encoding.UTF8.GetByteCount(t.Content ?? string.Empty));
        return totalMetadataSize > MetadataSizeThresholdBytes;
    }

    /// <summary>
    /// Splits tools into batches of the configured batch size.
    /// </summary>
    /// <param name="tools">Tools to batch.</param>
    /// <returns>List of tool batches.</returns>
    public static List<List<ToolContent>> CreateBatches(IReadOnlyList<ToolContent> tools)
    {
        ArgumentNullException.ThrowIfNull(tools);

        var batches = new List<List<ToolContent>>();
        for (int i = 0; i < tools.Count; i += BatchSize)
        {
            var batch = tools.Skip(i).Take(BatchSize).ToList();
            batches.Add(batch);
        }
        return batches;
    }

    /// <summary>
    /// Generates content for all batches with retry logic, then merges results.
    /// </summary>
    /// <param name="tools">All tools to generate.</param>
    /// <returns>Merged markdown content for all tools.</returns>
    /// <exception cref="InvalidOperationException">Thrown when a batch fails after max retries.</exception>
    public async Task<string> GenerateChunkedAsync(IReadOnlyList<ToolContent> tools)
    {
        if (_generateBatchFunc == null)
            throw new InvalidOperationException("No generation function provided.");

        var batches = CreateBatches(tools);
        var batchResults = new List<string>(batches.Count);

        for (int batchIndex = 0; batchIndex < batches.Count; batchIndex++)
        {
            var batch = batches[batchIndex];
            string? result = null;
            int retryCount = 0;

            while (retryCount < MaxRetriesPerBatch)
            {
                try
                {
                    result = await _generateBatchFunc(batch);

                    // Validate the batch result
                    var validation = ChunkedGenerationValidator.ValidateBatch(
                        result,
                        batch.Select(t => t.ToolName).ToList());

                    if (validation.IsValid)
                        break;

                    retryCount++;
                    if (retryCount >= MaxRetriesPerBatch)
                    {
                        var missingNames = string.Join(", ", validation.MissingTools.Select(t => $"'{t}'"));
                        throw new InvalidOperationException(
                            $"Batch {batchIndex + 1}/{batches.Count} failed after {MaxRetriesPerBatch} retries. " +
                            $"Missing tools: {missingNames}. Reason: {validation.FailureReason}");
                    }
                }
                catch (InvalidOperationException) when (retryCount >= MaxRetriesPerBatch - 1)
                {
                    throw;
                }
                catch (Exception) when (retryCount < MaxRetriesPerBatch - 1)
                {
                    retryCount++;
                }
            }

            if (string.IsNullOrEmpty(result))
            {
                var toolNames = string.Join(", ", batch.Select(t => $"'{t.ToolName}'"));
                throw new InvalidOperationException(
                    $"Batch {batchIndex + 1}/{batches.Count} returned empty after {MaxRetriesPerBatch} retries. " +
                    $"Tools: {toolNames}");
            }

            batchResults.Add(result);
        }

        return MergeBatchResults(batchResults);
    }

    /// <summary>
    /// Merges multiple batch results, consolidating duplicate H2 resource group headings.
    /// Tools (H3) under the same H2 group are appended together.
    /// </summary>
    /// <param name="batchResults">List of generated markdown strings from each batch.</param>
    /// <returns>Merged markdown with deduplicated H2 headings.</returns>
    public static string MergeBatchResults(IReadOnlyList<string> batchResults)
    {
        ArgumentNullException.ThrowIfNull(batchResults);

        if (batchResults.Count == 0)
            return string.Empty;

        if (batchResults.Count == 1)
            return batchResults[0];

        // Parse each batch into H2 groups
        var mergedGroups = new List<(string H2Heading, List<string> Content)>();

        foreach (var batchResult in batchResults)
        {
            var groups = ParseH2Groups(batchResult);

            foreach (var (heading, content) in groups)
            {
                var existingGroup = mergedGroups.FirstOrDefault(
                    g => string.Equals(g.H2Heading, heading, StringComparison.OrdinalIgnoreCase));

                if (existingGroup.H2Heading != null)
                {
                    existingGroup.Content.AddRange(content);
                }
                else
                {
                    mergedGroups.Add((heading, new List<string>(content)));
                }
            }
        }

        // Reassemble the merged output
        var sb = new StringBuilder(batchResults.Sum(r => r.Length));
        foreach (var (heading, content) in mergedGroups)
        {
            if (!string.IsNullOrEmpty(heading))
            {
                sb.AppendLine($"## {heading}");
                sb.AppendLine();
            }

            foreach (var section in content)
            {
                sb.AppendLine(section.TrimEnd());
                sb.AppendLine();
            }
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Parses markdown content into H2-headed groups.
    /// Content before the first H2 goes into a group with empty heading.
    /// </summary>
    internal static List<(string H2Heading, List<string> Content)> ParseH2Groups(string markdown)
    {
        var groups = new List<(string H2Heading, List<string> Content)>();
        if (string.IsNullOrEmpty(markdown))
            return groups;

        var lines = markdown.Split('\n');
        string currentH2 = "";
        var currentContent = new List<string>();
        var sectionBuilder = new StringBuilder();

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var h2Match = Regex.Match(line, @"^##\s+(.+)$");

            if (h2Match.Success)
            {
                // Flush previous section
                if (sectionBuilder.Length > 0)
                {
                    currentContent.Add(sectionBuilder.ToString());
                    sectionBuilder.Clear();
                }

                // If we had content in the current group, save it
                if (currentContent.Count > 0 || !string.IsNullOrEmpty(currentH2))
                {
                    groups.Add((currentH2, currentContent));
                }

                currentH2 = h2Match.Groups[1].Value.Trim();
                currentContent = new List<string>();
            }
            else if (Regex.IsMatch(line, @"^###\s+") && sectionBuilder.Length > 0)
            {
                // New H3 starts a new content section within the same H2 group
                currentContent.Add(sectionBuilder.ToString());
                sectionBuilder.Clear();
                sectionBuilder.AppendLine(line);
            }
            else
            {
                sectionBuilder.AppendLine(line);
            }
        }

        // Flush final section
        if (sectionBuilder.Length > 0)
        {
            currentContent.Add(sectionBuilder.ToString());
        }

        if (currentContent.Count > 0 || !string.IsNullOrEmpty(currentH2))
        {
            groups.Add((currentH2, currentContent));
        }

        return groups;
    }
}
