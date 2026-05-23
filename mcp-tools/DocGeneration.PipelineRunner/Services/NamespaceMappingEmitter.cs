using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PipelineRunner.Services;

/// <summary>
/// Builds and writes <c>namespace-mapping.json</c> to the root of the global output directory.
/// The file is a versioned snapshot of the brand mapping + tool assignment, consumed by downstream
/// validation agents and coverage audits (see PRD issue #618).
/// </summary>
public sealed class NamespaceMappingEmitter : INamespaceMappingEmitter
{
    /// <summary>Output filename, placed at the root of the global output directory.</summary>
    internal const string OutputFileName = "namespace-mapping.json";

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> EmitAsync(
        IReadOnlyList<BrandMappingEntry> brandMappings,
        CliMetadataSnapshot cliOutput,
        string cliVersion,
        string outputPath,
        CancellationToken cancellationToken)
    {
        // F2 fix: sort by McpServerName length descending so that longer (more-specific) prefixes
        // like "extension_azqr" are evaluated before shorter ones like "extension". Once a tool
        // is claimed by a namespace it is removed from the candidate pool, preventing duplication.
        var sortedMappings = brandMappings
            .Where(m => !string.IsNullOrWhiteSpace(m.McpServerName))
            .OrderByDescending(m => m.McpServerName.Length)
            .ToArray();

        // Track which tools have already been matched so each tool appears in at most one namespace.
        var unclaimedTools = cliOutput.Tools.ToList();
        var namespaces = new Dictionary<string, NamespaceEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (var mapping in sortedMappings)
        {
            var normalizedNs = mapping.McpServerName.Replace('_', ' ');

            var matchedIndices = new List<int>();
            var matchedNames = new List<string>();

            for (var i = 0; i < unclaimedTools.Count; i++)
            {
                var tool = unclaimedTools[i];
                if (string.Equals(tool.Command, normalizedNs, StringComparison.OrdinalIgnoreCase)
                    || tool.Command.StartsWith($"{normalizedNs} ", StringComparison.OrdinalIgnoreCase))
                {
                    matchedIndices.Add(i);
                    matchedNames.Add(string.IsNullOrEmpty(tool.Name) ? tool.Command : tool.Name);
                }
            }

            // Remove matched tools from the pool (iterate in reverse to preserve indices).
            for (var i = matchedIndices.Count - 1; i >= 0; i--)
            {
                unclaimedTools.RemoveAt(matchedIndices[i]);
            }

            matchedNames.Sort(StringComparer.OrdinalIgnoreCase);

            namespaces[mapping.McpServerName] = new NamespaceEntry(
                mapping.BrandName,
                mapping.FileName,
                mapping.ShortName,
                mapping.MergeGroup,
                matchedNames.ToArray());
        }

        // F1 fix: collect any tools that were not matched to any namespace prefix.
        // Emit them in a top-level unmatched_tools array and log a warning so the
        // inconsistency is visible without breaking the pipeline.
        var unmatchedTools = unclaimedTools
            .Select(t => string.IsNullOrEmpty(t.Name) ? t.Command : t.Name)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var document = new NamespaceMappingDocument(
            DateTime.UtcNow.ToString("O"),
            cliVersion,
            namespaces.Count,
            namespaces.Values.Sum(entry => entry.Tools.Length),
            namespaces,
            unmatchedTools);

        var outputFilePath = Path.Combine(outputPath, OutputFileName);
        var json = JsonSerializer.Serialize(document, WriteOptions);
        await File.WriteAllTextAsync(outputFilePath, json, Encoding.UTF8, cancellationToken);

        return unmatchedTools;
    }

    private sealed record NamespaceMappingDocument(
        [property: JsonPropertyName("generated_at")] string GeneratedAt,
        [property: JsonPropertyName("source_version")] string SourceVersion,
        [property: JsonPropertyName("namespace_count")] int NamespaceCount,
        [property: JsonPropertyName("tool_count")] int ToolCount,
        [property: JsonPropertyName("namespaces")] Dictionary<string, NamespaceEntry> Namespaces,
        [property: JsonPropertyName("unmatched_tools")] string[] UnmatchedTools);

    private sealed record NamespaceEntry(
        [property: JsonPropertyName("display_name")] string DisplayName,
        [property: JsonPropertyName("file_name")] string FileName,
        [property: JsonPropertyName("short_name")] string ShortName,
        [property: JsonPropertyName("merge_group")] string? MergeGroup,
        [property: JsonPropertyName("tools")] string[] Tools);
}
