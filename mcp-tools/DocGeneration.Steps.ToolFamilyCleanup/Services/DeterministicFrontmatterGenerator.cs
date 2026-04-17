// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;

namespace ToolFamilyCleanup.Services;

/// <summary>
/// Generates deterministic frontmatter YAML + H1 heading for tool-family articles.
/// Replaces the frontmatter portion of FamilyMetadataGenerator's AI output with
/// template-driven content that is consistent across runs.
/// Fixes: #163 Tier 1b
///
/// AI is still used for the 2 intro paragraphs (capabilities + service description).
/// This generator handles: frontmatter fields, H1 heading, and INCLUDE statement.
/// </summary>
public static class DeterministicFrontmatterGenerator
{
    private const string IncludeStatement =
        "[!INCLUDE [tip-about-params](../includes/tools/parameter-consideration.md)]";

    /// <summary>
    /// Generates deterministic frontmatter YAML block and H1 heading.
    /// </summary>
    /// <param name="brandName">Display name (e.g., "Azure Storage")</param>
    /// <param name="toolCount">Number of tools in the family</param>
    /// <param name="cliVersion">MCP CLI version string</param>
    /// <param name="seoDescription">Human-curated SEO description, or null for generic fallback</param>
    /// <returns>Markdown string: frontmatter block + blank line + H1 heading</returns>
    public static string Generate(
        string brandName,
        int toolCount,
        string cliVersion,
        string? seoDescription = null)
    {
        var title = $"Azure MCP Server tools for {brandName}";
        var description = seoDescription
            ?? $"Use Azure MCP Server tools to manage {brandName} resources with natural language prompts from your IDE.";

        var sb = new StringBuilder();
        sb.AppendLine("---");
        sb.AppendLine($"title: {title}");
        sb.AppendLine($"description: {description}");
        sb.AppendLine($"ms.date: {DateTime.UtcNow:MM/dd/yyyy}");
        sb.AppendLine("ms.service: azure-mcp-server");
        sb.AppendLine("ms.topic: concept-article");
        sb.AppendLine($"tool_count: {toolCount}");
        sb.AppendLine($"mcp-cli.version: {cliVersion}");
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine($"# {title}");

        return sb.ToString();
    }

    /// <summary>
    /// Extracts the intro paragraphs from AI-generated metadata output.
    /// Returns content between the H1 heading and the INCLUDE statement.
    /// </summary>
    public static string ExtractIntroParagraphs(string aiMetadata)
    {
        var lines = aiMetadata.Replace("\r\n", "\n").Split('\n');
        var introLines = new List<string>();
        var foundH1 = false;

        foreach (var line in lines)
        {
            if (!foundH1)
            {
                if (line.StartsWith("# "))
                {
                    foundH1 = true;
                }
                continue;
            }

            if (line.Contains("[!INCLUDE"))
            {
                break;
            }

            introLines.Add(line);
        }

        return string.Join('\n', introLines).Trim();
    }

    /// <summary>
    /// Assembles the complete metadata section from deterministic header + AI intros.
    /// </summary>
    public static string Assemble(string frontmatterAndH1, string introParagraphs)
    {
        var sb = new StringBuilder();
        sb.Append(frontmatterAndH1);
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(introParagraphs))
        {
            sb.AppendLine(introParagraphs);
            sb.AppendLine();
        }

        sb.AppendLine(IncludeStatement);

        return sb.ToString();
    }
}
