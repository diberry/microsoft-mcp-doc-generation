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
    /// <summary>
    /// Wraps a tool section with MCP/CLI tabs.
    /// If no CLI content is available for the tool, returns the original content unchanged.
    /// </summary>
    /// <param name="mcpContent">The existing MCP tool section content (without the H2 heading)</param>
    /// <param name="cliContent">The CLI content block for this tool, or null if unavailable</param>
    /// <returns>Tab-wrapped content, or original content if no CLI available</returns>
    public static string WrapWithTabs(string mcpContent, string? cliContent)
    {
        if (string.IsNullOrWhiteSpace(cliContent))
            return mcpContent;

        var sb = new StringBuilder();

        // MCP Server tab
        sb.AppendLine("#### [MCP Server](#tab/mcp-server)");
        sb.AppendLine();
        sb.AppendLine(mcpContent.TrimEnd());
        sb.AppendLine();

        // CLI tab
        sb.AppendLine("#### [CLI](#tab/cli)");
        sb.AppendLine();
        sb.AppendLine(cliContent.TrimEnd());
        sb.AppendLine();

        // Tab group terminator
        sb.AppendLine("---");

        return sb.ToString();
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
            if (trimmedLine.StartsWith("## ") && !trimmedLine.StartsWith("## Quick Navigation"))
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
                result.AppendLine(heading);
                result.AppendLine(WrapWithTabs(content, cliContent));
                return;
            }
        }

        result.AppendLine(heading);
        result.Append(content);
    }
}
