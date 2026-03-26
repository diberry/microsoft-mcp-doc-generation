// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;

namespace ToolFamilyCleanup.Services;

/// <summary>
/// Detects tools that exist in the tool list but have no corresponding H2 section
/// in the assembled article content. Provides actionable information about which
/// tools are missing, enabling clear error messages for content PR workflows.
/// </summary>
public static class MissingToolDetector
{
    private static readonly Regex H2HeadingRegex = new(
        @"^##\s+(.+)$",
        RegexOptions.Multiline | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Identifies tool names that have no corresponding H2 heading in the given article content.
    /// </summary>
    /// <param name="expectedToolNames">List of tool names expected to appear as H2 sections.</param>
    /// <param name="articleContent">The assembled article markdown content.</param>
    /// <returns>
    /// An ordered list of tool names that have no matching H2 heading in the article.
    /// Matching is case-insensitive and ignores leading/trailing whitespace.
    /// </returns>
    public static IReadOnlyList<string> DetectMissingTools(
        IReadOnlyList<string> expectedToolNames,
        string articleContent)
    {
        ArgumentNullException.ThrowIfNull(expectedToolNames);

        if (string.IsNullOrEmpty(articleContent))
        {
            return expectedToolNames
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        var h2Headings = ExtractH2Headings(articleContent);

        var missing = expectedToolNames
            .Where(toolName => !h2Headings.Contains(toolName.Trim(), StringComparer.OrdinalIgnoreCase))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return missing;
    }

    /// <summary>
    /// Extracts all H2 heading texts from the article content, excluding "Related content".
    /// </summary>
    internal static IReadOnlyList<string> ExtractH2Headings(string articleContent)
    {
        if (string.IsNullOrEmpty(articleContent))
        {
            return Array.Empty<string>();
        }

        var matches = H2HeadingRegex.Matches(articleContent);
        return matches
            .Select(m => m.Groups[1].Value.Trim())
            .Where(h => !string.Equals(h, "Related content", StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    /// <summary>
    /// Formats a human-readable warning message listing the missing tools and guidance.
    /// </summary>
    /// <param name="missingTools">The list of missing tool names.</param>
    /// <param name="familyName">The tool family/namespace name.</param>
    /// <returns>A formatted multi-line warning string, or null if no tools are missing.</returns>
    public static string? FormatMissingToolsWarning(
        IReadOnlyList<string> missingTools,
        string familyName)
    {
        if (missingTools.Count == 0)
        {
            return null;
        }

        var toolList = string.Join(", ", missingTools.Select(t => $"'{t}'"));
        var plural = missingTools.Count == 1 ? "tool has" : "tools have";

        return $"{missingTools.Count} {plural} no H2 section in the article: {toolList}. " +
               $"Regenerate the '{familyName}' namespace to include them.";
    }
}
