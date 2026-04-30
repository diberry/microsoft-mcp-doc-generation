// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;

namespace ToolFamilyCleanup.Services;

/// <summary>
/// Validates generated content against expected tool counts to detect
/// silent LLM truncation and missing tools after chunked generation.
/// </summary>
public static class ChunkedGenerationValidator
{
    private static readonly Regex H3Regex = new(
        @"^###\s+(.+)$",
        RegexOptions.Multiline | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex H2Regex = new(
        @"^##\s+(.+)$",
        RegexOptions.Multiline | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Validates a single batch result against expected tool names.
    /// Checks H3 count (for multi-resource format) and H2 count (for single-resource format).
    /// </summary>
    /// <param name="generatedContent">The generated markdown content.</param>
    /// <param name="expectedToolNames">Expected tool names in this batch.</param>
    /// <returns>Validation result with details.</returns>
    public static BatchValidationResult ValidateBatch(
        string generatedContent,
        IReadOnlyList<string> expectedToolNames)
    {
        ArgumentNullException.ThrowIfNull(expectedToolNames);

        if (string.IsNullOrEmpty(generatedContent))
        {
            return new BatchValidationResult
            {
                IsValid = false,
                ExpectedToolCount = expectedToolNames.Count,
                ActualToolCount = 0,
                MissingTools = expectedToolNames.ToList(),
                FailureReason = "Generated content is empty"
            };
        }

        // Check for truncation (incomplete final section)
        if (IsTruncated(generatedContent))
        {
            return new BatchValidationResult
            {
                IsValid = false,
                ExpectedToolCount = expectedToolNames.Count,
                ActualToolCount = CountToolSections(generatedContent),
                MissingTools = FindMissingTools(generatedContent, expectedToolNames),
                FailureReason = "Output appears truncated (incomplete final section)"
            };
        }

        // Count actual tool sections (H2 or H3 depending on format)
        var actualCount = CountToolSections(generatedContent);
        var missingTools = FindMissingTools(generatedContent, expectedToolNames);

        if (missingTools.Count > 0)
        {
            return new BatchValidationResult
            {
                IsValid = false,
                ExpectedToolCount = expectedToolNames.Count,
                ActualToolCount = actualCount,
                MissingTools = missingTools,
                FailureReason = $"Expected {expectedToolNames.Count} tools, found {actualCount}. Missing: {string.Join(", ", missingTools)}"
            };
        }

        return new BatchValidationResult
        {
            IsValid = true,
            ExpectedToolCount = expectedToolNames.Count,
            ActualToolCount = actualCount,
            MissingTools = new List<string>(),
            FailureReason = null
        };
    }

    /// <summary>
    /// Validates the final merged output against all expected tool names.
    /// </summary>
    /// <param name="mergedContent">The fully merged markdown content.</param>
    /// <param name="expectedToolNames">All expected tool names across all batches.</param>
    /// <returns>Validation result.</returns>
    public static BatchValidationResult ValidateFinalOutput(
        string mergedContent,
        IReadOnlyList<string> expectedToolNames)
    {
        return ValidateBatch(mergedContent, expectedToolNames);
    }

    /// <summary>
    /// Detects if the output was truncated by checking for incomplete sections.
    /// A section is considered truncated if it ends abruptly without proper closure.
    /// </summary>
    /// <param name="content">The generated content to check.</param>
    /// <returns>True if truncation is detected.</returns>
    public static bool IsTruncated(string content)
    {
        if (string.IsNullOrEmpty(content))
            return false;

        var trimmed = content.TrimEnd();

        // Truncation indicators:
        // 1. Content ends mid-word (no trailing newline after content)
        // 2. Content ends with an incomplete markdown table row
        // 3. Content ends with an unclosed code block
        if (trimmed.EndsWith('|') && !trimmed.EndsWith("--|"))
            return true;

        // Check for unclosed code fences
        var fenceCount = Regex.Matches(trimmed, @"^```", RegexOptions.Multiline).Count;
        if (fenceCount % 2 != 0)
            return true;

        // Check if last line is a heading with no content after it (suspicious if it's the very end)
        var lines = trimmed.Split('\n');
        if (lines.Length > 0)
        {
            var lastLine = lines[^1].Trim();
            if (Regex.IsMatch(lastLine, @"^#{2,6}\s+") && lines.Length > 1)
            {
                // A heading as the very last line suggests truncation
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Counts tool sections in the content (H2 or H3 headings, excluding "Related content").
    /// </summary>
    internal static int CountToolSections(string content)
    {
        if (string.IsNullOrEmpty(content))
            return 0;

        // Count H3 sections (multi-resource format) or H2 sections (single-resource format)
        var h3Matches = H3Regex.Matches(content);
        var h2Matches = H2Regex.Matches(content)
            .Cast<Match>()
            .Where(m => !string.Equals(m.Groups[1].Value.Trim(), "Related content", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // If we have H3s under H2 resource groups, count H3s as tools
        // Otherwise count H2s as tools
        return h3Matches.Count > 0 ? h3Matches.Count : h2Matches.Count;
    }

    /// <summary>
    /// Finds tool names that don't appear in any heading in the generated content.
    /// Uses exact match against heading text (case-insensitive).
    /// </summary>
    internal static List<string> FindMissingTools(
        string content,
        IReadOnlyList<string> expectedToolNames)
    {
        if (string.IsNullOrEmpty(content))
            return expectedToolNames.ToList();

        var allHeadings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Collect all H2 and H3 heading texts
        foreach (Match m in H2Regex.Matches(content))
            allHeadings.Add(m.Groups[1].Value.Trim());
        foreach (Match m in H3Regex.Matches(content))
            allHeadings.Add(m.Groups[1].Value.Trim());

        return expectedToolNames
            .Where(tool => !allHeadings.Contains(tool.Trim()))
            .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

/// <summary>
/// Result of validating a generated batch or final output.
/// </summary>
public class BatchValidationResult
{
    /// <summary>
    /// Whether the validation passed (all expected tools present, no truncation).
    /// </summary>
    public required bool IsValid { get; init; }

    /// <summary>
    /// Number of tools expected in this batch.
    /// </summary>
    public required int ExpectedToolCount { get; init; }

    /// <summary>
    /// Number of tool sections actually found in the generated output.
    /// </summary>
    public required int ActualToolCount { get; init; }

    /// <summary>
    /// List of tool names that were expected but not found in the output.
    /// </summary>
    public required List<string> MissingTools { get; init; }

    /// <summary>
    /// Human-readable reason for failure, or null if valid.
    /// </summary>
    public required string? FailureReason { get; init; }
}
