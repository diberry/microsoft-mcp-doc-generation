// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ToolFamilyCleanup.Models;

namespace ToolFamilyCleanup.Services;

/// <summary>
/// Stitches together metadata, tool sections, and related content into a complete tool family markdown file.
/// No AI involved - pure string assembly.
/// </summary>
public class FamilyFileStitcher
{
    // Known acronyms that should remain uppercase in display names
    private static readonly HashSet<string> KnownAcronyms = new(StringComparer.OrdinalIgnoreCase)
    {
        "vm", "vmss", "db", "sql", "api", "aks", "acr", "dns", "ip", "nsg",
        "vpn", "vnet", "hpc", "gpu", "cpu", "ssd", "hdd", "cdn", "waf", "rbac"
    };

    private static readonly Regex HeadingRegex = new(@"^(#{2,6})\s", RegexOptions.Multiline | RegexOptions.Compiled);

    /// <summary>
    /// Assembles a complete tool family markdown file from its parts.
    /// </summary>
    /// <param name="familyContent">Family content with all parts</param>
    /// <returns>Complete markdown string</returns>
    public string Stitch(FamilyContent familyContent)
    {
        var sb = new StringBuilder();

        // 1. Metadata section (frontmatter + H1 + intro - strip any H2s the AI may have generated)
        var metadataLines = familyContent.Metadata.Split('\n')
            .Where(line => !line.StartsWith("## "));
        sb.AppendLine(string.Join('\n', metadataLines));
        sb.AppendLine();

        // 2. Tool sections - group by resource type if multi-resource (#412)
        var isMultiResource = IsMultiResourceFamily(familyContent.Tools);

        if (isMultiResource)
        {
            StitchMultiResource(sb, familyContent.Tools);
        }
        else
        {
            // Sort tools for consistent single-resource presentation order (#503).
            // Ordering delegated to ToolOrderingPolicy (alphabetical by ToolName).
            foreach (var tool in ToolOrderingPolicy.OrderForSingleResource(familyContent.Tools))
            {
                sb.AppendLine(tool.Content);
                sb.AppendLine();
            }
        }

        // 3. Related content section
        sb.AppendLine(familyContent.RelatedContent);

        // 4. Post-processing: expand all acronyms on first body mention (#142, #215)
        var markdown = sb.ToString().TrimEnd();
        markdown = AcronymExpander.ExpandAll(markdown);

        // 5. Post-processing: inject required frontmatter fields (#155)
        markdown = FrontmatterEnricher.EnrichWithDefaults(markdown);

        // 6. Post-processing: strip duplicate example blocks (#153)
        markdown = DuplicateExampleStripper.Strip(markdown);

        // 6a. Post-processing: strip engineering-authored example patterns (#278)
        markdown = EngineeringExampleStripper.Strip(markdown);

        // 7. Post-processing: ensure blank line between annotation link and values (#151)
        markdown = AnnotationSpaceFixer.Fix(markdown);

        // 7a. Post-processing: strip trailing pipe from annotation value lines (#281)
        markdown = AnnotationTrailingPipeFixer.Fix(markdown);

        // 8. Post-processing: convert future tense to present tense (#145, #215)
        markdown = PresentTenseFixer.Fix(markdown);

        // 9. Post-processing: apply contractions per Microsoft style guide (#145)
        markdown = ContractionFixer.Fix(markdown);

        // 9a. Post-processing: compound words, double-plurals, and wordy phrases (#393)
        markdown = StyleGuidePostProcessor.Fix(markdown);

        // 10. Post-processing: insert commas after introductory phrases (#146, #215)
        markdown = IntroductoryCommaFixer.Fix(markdown);

        // 10a. Post-processing: insert colon after "including" in intro paragraphs (#282)
        markdown = IncludingColonFixer.Fix(markdown);

        // 11. Post-processing: wrap bare example values in backticks (#152)
        markdown = ExampleValueBackticker.Fix(markdown);

        // 12. Post-processing: convert full learn.microsoft.com URLs to site-root-relative paths (#220, AD-017)
        markdown = LearnUrlRelativizer.Relativize(markdown);

        // 13. Post-processing: collapse inline JSON schemas in parameter tables (Acrolinx P1)
        markdown = JsonSchemaCollapser.Collapse(markdown);

        // 14. Post-processing: strip HTML scaffolding comments (preserve @mcpcli markers)
        markdown = ScaffoldingCommentStripper.Strip(markdown);

        // 15. Post-processing: escape bare <placeholder> values for MS Learn validation (#416)
        markdown = PlaceholderEscaper.Escape(markdown);

        return markdown;
    }

    /// <summary>
    /// Determines whether a set of tools spans multiple distinct resource types.
    /// Returns true when there are 2+ distinct non-empty resource types.
    /// </summary>
    internal static bool IsMultiResourceFamily(List<ToolContent> tools)
    {
        var distinctResourceTypes = tools
            .Select(t => t.ResourceType)
            .Where(rt => !string.IsNullOrEmpty(rt))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return distinctResourceTypes.Count >= 2;
    }

    /// <summary>
    /// Emits tool sections grouped by resource type. Each group gets an H2 header;
    /// tool headings are demoted from H2 to H3 (and sub-headings shift accordingly).
    /// </summary>
    private static void StitchMultiResource(StringBuilder sb, List<ToolContent> tools)
    {
        // Group tools preserving existing sort order (already sorted by resource type then verb)
        var groups = new List<(string ResourceType, List<ToolContent> Tools)>();
        string currentResourceType = "";
        List<ToolContent>? currentGroup = null;

        foreach (var tool in tools)
        {
            var rt = string.IsNullOrEmpty(tool.ResourceType) ? "" : tool.ResourceType;
            if (currentGroup == null || !string.Equals(rt, currentResourceType, StringComparison.OrdinalIgnoreCase))
            {
                currentGroup = new List<ToolContent>();
                currentResourceType = rt;
                groups.Add((rt, currentGroup));
            }
            currentGroup.Add(tool);
        }

        foreach (var (resourceType, groupTools) in groups)
        {
            // Emit H2 resource group header
            var displayName = FormatResourceTypeDisplayName(resourceType);
            sb.AppendLine($"## {displayName}");
            sb.AppendLine();

            // Sort tools by action verb within each resource group for consistent
            // presentation (#503, #504). Uses action verb (not ToolName) because the
            // displayed heading is rewritten by ReformatToolHeadingForMultiResource.
            foreach (var tool in ToolOrderingPolicy.OrderForMultiResource(groupTools))
            {
                // #416: Reformat H2 heading to "Resource type: action" format before demoting
                var reformattedContent = ReformatToolHeadingForMultiResource(tool);
                
                // Demote all headings in tool content by one level (H2->H3, H3->H4, etc.)
                var demotedContent = DemoteHeadings(reformattedContent);
                sb.AppendLine(demotedContent);
                sb.AppendLine();
            }
        }
    }

    /// <summary>
    /// Reformats the H2 tool heading in multi-resource format: "Resource type: action"
    /// (e.g., "Create disk" -> "Managed disk: create")
    /// </summary>
    private static string ReformatToolHeadingForMultiResource(ToolContent tool)
    {
        if (string.IsNullOrEmpty(tool.ResourceType) || string.IsNullOrEmpty(tool.Command))
            return tool.Content;

        // Extract action from command (last segment)
        var commandParts = tool.Command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (commandParts.Length == 0)
            return tool.Content;

        var action = commandParts[^1];
        
        // Format the new heading using multi-resource formatter
        var newHeading = MultiResourceH2Formatter.FormatToolHeading(tool.ResourceType, action);

        // Replace the first H2 heading in the content with the new format
        var match = Regex.Match(tool.Content, @"^##\s+(.+)$", RegexOptions.Multiline);
        if (match.Success)
        {
            return tool.Content.Substring(0, match.Index) + 
                   $"## {newHeading}" + 
                   tool.Content.Substring(match.Index + match.Length);
        }

        return tool.Content;
    }

    /// <summary>
    /// Demotes all markdown headings in content by one level (## -> ###, ### -> ####, etc.).
    /// Caps at H6 (######) per markdown spec.
    /// </summary>
    internal static string DemoteHeadings(string content)
    {
        return HeadingRegex.Replace(content, match =>
        {
            var hashes = match.Groups[1].Value;
            // Cap at 6 levels
            if (hashes.Length >= 6)
                return match.Value;
            return hashes + "# ";
        });
    }

    /// <summary>
    /// Converts a resource type identifier to a human-readable display name.
    /// Examples: "disk" -> "Disk", "vmss" -> "VMSS", "db container" -> "DB container"
    /// </summary>
    internal static string FormatResourceTypeDisplayName(string resourceType)
    {
        if (string.IsNullOrWhiteSpace(resourceType))
            return "General";

        var words = resourceType.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var result = new string[words.Length];

        for (int i = 0; i < words.Length; i++)
        {
            if (KnownAcronyms.Contains(words[i]))
            {
                result[i] = words[i].ToUpperInvariant();
            }
            else if (i == 0)
            {
                // Title-case the first word
                result[i] = char.ToUpper(words[i][0], CultureInfo.InvariantCulture) + words[i][1..];
            }
            else
            {
                // Lowercase subsequent non-acronym words
                result[i] = words[i].ToLowerInvariant();
            }
        }

        return string.Join(" ", result);
    }

    /// <summary>
    /// Stitches and saves to file in one operation.
    /// </summary>
    /// <param name="familyContent">Family content to stitch</param>
    /// <param name="outputPath">Output file path</param>
    public async Task StitchAndSaveAsync(FamilyContent familyContent, string outputPath)
    {
        var markdown = Stitch(familyContent);
        await File.WriteAllTextAsync(outputPath, markdown, Encoding.UTF8);
    }
}
