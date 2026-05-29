// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Text.RegularExpressions;

namespace Shared;

/// <summary>
/// Wraps tool sections in family articles with tabbed conceptual tags
/// for MCP Server and CLI content.
/// </summary>
public static class CliTabWrapper
{
    private static readonly Regex McpCliMarkerPattern = new(
        @"<!--\s*@mcpcli\s+(.+?)\s*-->", RegexOptions.Compiled);

    // Matches the annotation block: "[Tool annotation hints](...):"\n\n"Destructive: ..."
    // The block starts with the link line and includes the values line that follows.
    private static readonly Regex AnnotationBlockPattern = new(
        @"(\[Tool annotation hints\]\([^\)]+\):\s*\n\s*\n[^\n]*(?:Destructive|Idempotent|Read Only)[^\n]*)",
        RegexOptions.Compiled);

    /// <summary>
    /// Wraps a tool section with MCP/CLI tabs.
    /// If no CLI content is available for the tool, returns the original content unchanged.
    /// Annotation blocks are stripped from tab content and placed once after the --- separator.
    /// </summary>
    /// <param name="mcpContent">The existing MCP tool section content (without the H2 heading)</param>
    /// <param name="cliContent">The CLI content block for this tool, or null if unavailable</param>
    /// <returns>Tab-wrapped content, or original content if no CLI available</returns>
    public static string WrapWithTabs(string mcpContent, string? cliContent)
    {
        var (tabBlock, _) = WrapWithTabsAndExtractDescription(mcpContent, cliContent);
        return tabBlock;
    }

    public static (string TabBlock, string? Description) WrapWithTabsAndExtractDescription(string mcpContent, string? cliContent)
    {
        if (cliContent is null)
            return (mcpContent, null);

        var (mcpContentWithoutDescription, description) = ExtractDescription(mcpContent);
        var cliContentWithoutDescription = StripMatchingDescriptionFromCli(cliContent, description);
        return (BuildTabBlock(mcpContentWithoutDescription, cliContentWithoutDescription), description);
    }

    private static string BuildTabBlock(string mcpContent, string cliContent)
    {
        // Extract and strip annotation block from MCP content
        var annotationBlock = ExtractAnnotationBlock(mcpContent);
        var mcpWithoutAnnotation = annotationBlock != null
            ? StripAnnotationBlock(mcpContent)
            : mcpContent;

        var sb = new StringBuilder();

        // CLI tab first (ground truth, deterministic from tools JSON)
        sb.AppendLine("#### [Azure MCP CLI](#tab/azure-mcp-cli)");
        sb.AppendLine();
        sb.AppendLine(cliContent.TrimEnd());
        sb.AppendLine();

        // MCP Server tab second (AI-improved derivative)
        sb.AppendLine("#### [MCP Server](#tab/mcp-server)");
        sb.AppendLine();
        sb.AppendLine(mcpWithoutAnnotation.TrimEnd());
        sb.AppendLine();

        // Tab group terminator
        sb.AppendLine("---");

        // Annotations appear once, after the separator, outside all tabs
        if (annotationBlock != null)
        {
            sb.AppendLine();
            sb.AppendLine(annotationBlock);
        }

        return sb.ToString();
    }

    internal static (string ContentWithoutDescription, string? Description) ExtractDescription(string content)
    {
        var normalizedContent = content.ReplaceLineEndings("\n");
        var lines = normalizedContent.Split('\n');
        var descriptionStart = 0;

        while (descriptionStart < lines.Length && string.IsNullOrWhiteSpace(lines[descriptionStart]))
        {
            descriptionStart++;
        }

        if (descriptionStart < lines.Length && IsMcpMarker(lines[descriptionStart]))
        {
            descriptionStart++;

            while (descriptionStart < lines.Length && string.IsNullOrWhiteSpace(lines[descriptionStart]))
            {
                descriptionStart++;
            }
        }

        if (descriptionStart >= lines.Length || IsDescriptionBoundary(lines[descriptionStart]))
        {
            return (content, null);
        }

        var descriptionEnd = descriptionStart;
        while (descriptionEnd < lines.Length
            && !string.IsNullOrWhiteSpace(lines[descriptionEnd])
            && !IsDescriptionBoundary(lines[descriptionEnd]))
        {
            descriptionEnd++;
        }

        var description = string.Join(" ", lines[descriptionStart..descriptionEnd].Select(line => line.Trim())).Trim();
        if (string.IsNullOrWhiteSpace(description))
        {
            return (content, null);
        }

        var remainderStart = descriptionEnd;
        while (remainderStart < lines.Length && string.IsNullOrWhiteSpace(lines[remainderStart]))
        {
            remainderStart++;
        }

        var remainingLines = lines[..descriptionStart].ToList();
        while (remainingLines.Count > 0 && string.IsNullOrWhiteSpace(remainingLines[^1]))
        {
            remainingLines.RemoveAt(remainingLines.Count - 1);
        }

        if (remainingLines.Count > 0 && remainderStart < lines.Length)
        {
            remainingLines.Add(string.Empty);
        }

        remainingLines.AddRange(lines[remainderStart..]);

        return (string.Join("\n", remainingLines).TrimEnd(), description);
    }

    internal static string StripMatchingDescriptionFromCli(string cliContent, string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return cliContent;
        }

        var normalizedContent = cliContent.ReplaceLineEndings("\n");
        var lines = normalizedContent.Split('\n');
        var descriptionStart = 0;

        while (descriptionStart < lines.Length && string.IsNullOrWhiteSpace(lines[descriptionStart]))
        {
            descriptionStart++;
        }

        if (descriptionStart >= lines.Length || IsDescriptionBoundary(lines[descriptionStart]))
        {
            return cliContent;
        }

        var descriptionEnd = descriptionStart;
        while (descriptionEnd < lines.Length
            && !string.IsNullOrWhiteSpace(lines[descriptionEnd])
            && !IsDescriptionBoundary(lines[descriptionEnd]))
        {
            descriptionEnd++;
        }

        var cliDescription = string.Join(" ", lines[descriptionStart..descriptionEnd].Select(line => line.Trim())).Trim();
        if (!string.Equals(cliDescription, description, StringComparison.Ordinal))
        {
            return cliContent;
        }

        var remainderStart = descriptionEnd;
        while (remainderStart < lines.Length && string.IsNullOrWhiteSpace(lines[remainderStart]))
        {
            remainderStart++;
        }

        return string.Join("\n", lines[..descriptionStart].Concat(lines[remainderStart..])).TrimEnd();
    }

    private static bool IsMcpMarker(string line)
        => line.TrimStart().StartsWith("<!-- @mcpcli ", StringComparison.Ordinal);

    private static bool IsDescriptionBoundary(string line)
    {
        var trimmed = line.TrimStart();
        return trimmed.StartsWith("|", StringComparison.Ordinal)
            || trimmed.StartsWith("#", StringComparison.Ordinal)
            || trimmed.StartsWith("{{", StringComparison.Ordinal)
            || trimmed.StartsWith("[Tool annotation", StringComparison.Ordinal)
            || trimmed.StartsWith("```", StringComparison.Ordinal)
            || trimmed.StartsWith("- ", StringComparison.Ordinal)
            || trimmed.StartsWith("* ", StringComparison.Ordinal)
            || trimmed.StartsWith("+ ", StringComparison.Ordinal)
            || Regex.IsMatch(trimmed, @"^\d+\.\s");
    }

    /// <summary>
    /// Extracts the annotation block from tool content.
    /// Returns the full block ("[Tool annotation hints](...):"\n\nDestructive: ...)
    /// or null if not found.
    /// </summary>
    internal static string? ExtractAnnotationBlock(string content)
    {
        var match = AnnotationBlockPattern.Match(content);
        return match.Success ? match.Groups[1].Value.TrimEnd() : null;
    }

    /// <summary>
    /// Removes the annotation block from tool content, returning the content without it.
    /// </summary>
    internal static string StripAnnotationBlock(string content)
    {
        return AnnotationBlockPattern.Replace(content, "").TrimEnd();
    }

    /// <summary>
    /// Applies tab wrapping to a complete family article.
    /// Finds each ## Tool section by the &lt;!-- @mcpcli {command} --&gt; marker,
    /// looks up CLI content for that command, and wraps with tabs.
    /// </summary>
    /// <param name="familyMarkdown">The complete family article markdown</param>
    /// <param name="cliContentByCommand">CLI content blocks keyed by normalized command</param>
    /// <returns>The family article with tab-wrapped tool sections</returns>
    public static string ApplyTabsToFamilyArticle(
        string familyMarkdown,
        IReadOnlyDictionary<string, string> cliContentByCommand)
    {
        if (cliContentByCommand.Count == 0)
            return familyMarkdown;

        var lines = familyMarkdown.Split('\n');
        var result = new StringBuilder();
        var currentToolContent = new StringBuilder();
        string? currentCommand = null;
        bool inToolSection = false;
        string? currentHeading = null;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmedLine = line.TrimEnd('\r');

            // Check if this is a ## heading (new tool section or end of current)
            if (trimmedLine.StartsWith("## ") && !trimmedLine.StartsWith("## Quick Navigation", StringComparison.OrdinalIgnoreCase))
            {
                // Flush any previous tool section
                if (inToolSection)
                {
                    FlushToolSection(result, currentHeading!, currentCommand, currentToolContent, cliContentByCommand);
                    currentToolContent.Clear();
                }

                currentHeading = trimmedLine;
                currentCommand = null;
                inToolSection = true;
                continue;
            }

            // Check for @mcpcli marker to get command
            if (inToolSection && trimmedLine.Contains("<!-- @mcpcli "))
            {
                var match = McpCliMarkerPattern.Match(trimmedLine);
                if (match.Success)
                    currentCommand = match.Groups[1].Value;
            }

            if (inToolSection)
            {
                currentToolContent.AppendLine(trimmedLine);
            }
            else
            {
                result.AppendLine(trimmedLine);
            }
        }

        // Flush final tool section
        if (inToolSection)
        {
            FlushToolSection(result, currentHeading!, currentCommand, currentToolContent, cliContentByCommand);
        }

        return result.ToString();
    }

    private static void FlushToolSection(
        StringBuilder result,
        string heading,
        string? command,
        StringBuilder toolContent,
        IReadOnlyDictionary<string, string> cliContentByCommand)
    {
        var content = toolContent.ToString();

        if (command != null)
        {
            var normalizedCmd = CliJsonMapper.NormalizeCommand(command);
            if (cliContentByCommand.TryGetValue(normalizedCmd, out var cliContent))
            {
                var (tabBlock, description) = WrapWithTabsAndExtractDescription(content, cliContent);
                result.AppendLine(heading);
                if (!string.IsNullOrWhiteSpace(description))
                {
                    result.AppendLine();
                    result.AppendLine(description);
                    result.AppendLine();
                }

                result.AppendLine(tabBlock);
                return;
            }
        }

        result.AppendLine(heading);
        result.Append(content);
    }
}
