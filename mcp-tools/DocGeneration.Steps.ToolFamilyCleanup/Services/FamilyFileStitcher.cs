// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ToolFamilyCleanup.Models;
using DocGeneration.Steps.ToolFamilyCleanup.Services;

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
    /// Determines whether a family file contains tools from multiple MCP namespaces.
    /// A multi-resource family is defined as a file combining more than one namespace on purpose.
    /// Multiple resource types within a single namespace (e.g., storage has Account, Blob, Table)
    /// does NOT make it multi-resource — each tool still gets a flat H2.
    /// In practice, returns false for all current pipeline runs because the pipeline processes one namespace at a time.
    /// </summary>
    internal static bool IsMultiResourceFamily(List<ToolContent> tools)
    {
        // A family is multi-resource when its tools span 2+ distinct resource types
        // (e.g., "disk" and "vm" within the "compute" family).
        var distinctResourceTypes = tools
            .Select(t => t.ResourceType)
            .Where(r => !string.IsNullOrEmpty(r))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        return distinctResourceTypes >= 2;
    }

    /// <summary>
    /// Emits tool sections for multi-resource families. Each tool gets a flat H2 heading
    /// in "Resource type: action" format. No resource group headers or heading demotion —
    /// the published articles use H2 per tool only.
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
            // Sort tools by action verb within each resource group for consistent
            // presentation (#503, #504). Uses action verb (not ToolName) because the
            // displayed heading is rewritten by ReformatToolHeadingForMultiResource.
            foreach (var tool in ToolOrderingPolicy.OrderForMultiResource(groupTools))
            {
                // #416: Reformat H2 heading to "Resource type: action" format (flat H2, no demotion)
                var reformattedContent = ReformatToolHeadingForMultiResource(tool);
                sb.AppendLine(reformattedContent);
                sb.AppendLine();
            }
        }
    }

    /// <summary>
    /// Reformats the H2 tool heading in multi-resource format: "Resource type: action"
    /// (e.g., "Create disk" -> "Managed disk: Create")
    /// Checks HeadingOverrideProvider first; falls back to MultiResourceH2Formatter.
    /// </summary>
    private static string ReformatToolHeadingForMultiResource(ToolContent tool)
    {
        if (string.IsNullOrEmpty(tool.ResourceType) || string.IsNullOrEmpty(tool.Command))
            return tool.Content;

        // 1. Check for an explicit heading override keyed by the full command
        var newHeading = HeadingOverrideProvider.GetOverride(tool.Command);

        if (newHeading == null)
        {
            // 2. Fall back to algorithmic formatting (title-cased action, hyphens → spaces)
            var commandParts = tool.Command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (commandParts.Length == 0)
                return tool.Content;

            var action = commandParts[^1];
            newHeading = MultiResourceH2Formatter.FormatToolHeading(tool.ResourceType, action);
        }

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
        
        // Validate: no H3 headings except tab markers
        var h3Validation = H3HeadingValidator.Validate(markdown);
        if (!h3Validation.IsValid)
        {
            Console.WriteLine($"⚠ H3 validation warnings for {familyContent.FamilyName}:");
            foreach (var error in h3Validation.Errors)
            {
                Console.WriteLine($"  {error}");
            }
        }
        
        await File.WriteAllTextAsync(outputPath, markdown, Encoding.UTF8);
    }
}
