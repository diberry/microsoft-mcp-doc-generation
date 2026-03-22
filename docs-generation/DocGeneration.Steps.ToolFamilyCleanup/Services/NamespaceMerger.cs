// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Text.RegularExpressions;
using Shared;

namespace ToolFamilyCleanup.Services;

/// <summary>
/// Merges tool-family articles from grouped namespaces into a single article.
/// Per AD-011: post-assembly merge using config from brand-to-server-mapping.json.
///
/// Primary namespace contributes: frontmatter, H1, overview, related content.
/// Secondary namespaces contribute: tool H2 sections only.
/// </summary>
public static partial class NamespaceMerger
{
    [GeneratedRegex(@"^tool_count:\s*(\d+)", RegexOptions.Multiline)]
    private static partial Regex ToolCountPattern();

    /// <summary>
    /// Merges articles based on merge group configuration.
    /// Returns a dictionary of outputKey → merged markdown.
    /// Ungrouped namespaces pass through unchanged.
    /// </summary>
    public static Dictionary<string, string> Merge(
        Dictionary<string, string> articles,
        IReadOnlyList<BrandMapping> mappings)
    {
        var result = new Dictionary<string, string>();

        // Find merge groups
        var groups = mappings
            .Where(m => !string.IsNullOrEmpty(m.MergeGroup))
            .GroupBy(m => m.MergeGroup!)
            .ToDictionary(g => g.Key, g => g.OrderBy(m => m.MergeOrder ?? 99).ToList());

        // Track which namespaces are in a merge group
        var groupedNamespaces = new HashSet<string>(
            groups.Values.SelectMany(g => g.Select(m => m.McpServerName)),
            StringComparer.OrdinalIgnoreCase);

        // Pass through ungrouped namespaces unchanged
        foreach (var (ns, content) in articles)
        {
            if (!groupedNamespaces.Contains(ns))
                result[ns] = content;
        }

        // Process each merge group
        foreach (var (groupKey, members) in groups)
        {
            var primary = members.FirstOrDefault(m =>
                string.Equals(m.MergeRole, "primary", StringComparison.OrdinalIgnoreCase));

            if (primary == null || !articles.ContainsKey(primary.McpServerName))
                continue;

            var primaryContent = articles[primary.McpServerName];
            var (header, primaryTools, relatedContent) = ParseArticle(primaryContent);

            // Collect tool sections from all members in order
            var allTools = new List<string>(primaryTools);
            foreach (var member in members)
            {
                if (string.Equals(member.MergeRole, "primary", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!articles.TryGetValue(member.McpServerName, out var secondaryContent))
                    continue;

                var (_, secondaryTools, _) = ParseArticle(secondaryContent);
                allTools.AddRange(secondaryTools);
            }

            // Build merged article
            var totalToolCount = allTools.Count;
            var updatedHeader = UpdateToolCount(header, totalToolCount);

            var sb = new StringBuilder();
            sb.Append(updatedHeader);

            foreach (var tool in allTools)
            {
                sb.AppendLine(tool);
                sb.AppendLine();
            }

            sb.AppendLine("## Related content");
            sb.AppendLine();
            sb.Append(relatedContent);

            result[groupKey] = sb.ToString().TrimEnd() + "\n";
        }

        return result;
    }

    /// <summary>
    /// Parses an article into header (frontmatter + H1 + overview),
    /// tool H2 sections, and related content.
    /// </summary>
    internal static (string Header, List<string> Tools, string RelatedContent) ParseArticle(string markdown)
    {
        var lines = markdown.Split('\n');
        var header = new StringBuilder();
        var tools = new List<string>();
        var relatedContent = new StringBuilder();

        var currentSection = new StringBuilder();
        var inHeader = true;
        var inRelated = false;
        var foundFirstH2 = false;

        foreach (var line in lines)
        {
            if (line.StartsWith("## "))
            {
                if (inHeader)
                {
                    inHeader = false;
                    // Flush header (everything before first H2)
                }

                if (string.Equals(line.TrimEnd(), "## Related content", StringComparison.OrdinalIgnoreCase))
                {
                    // Save any current tool section
                    if (currentSection.Length > 0 && foundFirstH2)
                        tools.Add(currentSection.ToString().TrimEnd());

                    currentSection.Clear();
                    inRelated = true;
                    continue;
                }

                // Save previous tool section
                if (foundFirstH2 && currentSection.Length > 0)
                    tools.Add(currentSection.ToString().TrimEnd());

                currentSection.Clear();
                currentSection.AppendLine(line);
                foundFirstH2 = true;
                continue;
            }

            if (inRelated)
            {
                relatedContent.AppendLine(line);
            }
            else if (inHeader)
            {
                header.AppendLine(line);
            }
            else
            {
                currentSection.AppendLine(line);
            }
        }

        // Flush last section
        if (!inRelated && foundFirstH2 && currentSection.Length > 0)
            tools.Add(currentSection.ToString().TrimEnd());

        return (header.ToString(), tools, relatedContent.ToString().TrimEnd());
    }

    /// <summary>
    /// Updates the tool_count value in frontmatter.
    /// </summary>
    internal static string UpdateToolCount(string header, int newCount)
    {
        return ToolCountPattern().Replace(header, $"tool_count: {newCount}");
    }
}
