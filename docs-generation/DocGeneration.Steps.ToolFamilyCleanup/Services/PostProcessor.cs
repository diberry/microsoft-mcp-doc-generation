// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;

namespace ToolFamilyCleanup.Services;

/// <summary>
/// Deterministic post-processing transformations applied after AI generation
/// and file stitching. These are reliable, testable fixes that don't depend
/// on AI behavior.
/// </summary>
public static class PostProcessor
{
    private const string McpShort = "Azure MCP Server";
    private const string McpExpanded = "Azure Model Context Protocol (MCP) Server";

    /// <summary>
    /// Expands the first body occurrence of "Azure MCP Server" to
    /// "Azure Model Context Protocol (MCP) Server" per Acrolinx rule:
    /// "Clarity > Did you define the acronym in your content?"
    /// 
    /// Preserves frontmatter and H1 heading — only expands in body text.
    /// Skips if already expanded.
    /// </summary>
    public static string ExpandMcpAcronym(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
        {
            return markdown;
        }

        // If already expanded, don't double-expand
        if (markdown.Contains("Model Context Protocol (MCP)", StringComparison.Ordinal))
        {
            return markdown;
        }

        // Find the body start: after frontmatter (if present) and H1 heading
        int bodyStart = FindBodyStart(markdown);
        if (bodyStart < 0 || bodyStart >= markdown.Length)
        {
            return markdown;
        }

        // Find first "Azure MCP Server" in the body
        int firstMention = markdown.IndexOf(McpShort, bodyStart, StringComparison.Ordinal);
        if (firstMention < 0)
        {
            return markdown;
        }

        // Replace only the first body occurrence
        return string.Concat(
            markdown.AsSpan(0, firstMention),
            McpExpanded,
            markdown.AsSpan(firstMention + McpShort.Length));
    }

    /// <summary>
    /// Finds the start of body content (after frontmatter and H1 heading).
    /// Returns the character index where body text begins.
    /// </summary>
    private static int FindBodyStart(string markdown)
    {
        int pos = 0;

        // Skip frontmatter if present (between --- markers)
        if (markdown.StartsWith("---"))
        {
            int endFrontmatter = markdown.IndexOf("\n---", 3, StringComparison.Ordinal);
            if (endFrontmatter > 0)
            {
                // Move past the closing --- line
                pos = markdown.IndexOf('\n', endFrontmatter + 4);
                if (pos < 0) return -1;
                pos++;
            }
        }

        // Skip the H1 heading line (starts with "# ")
        while (pos < markdown.Length)
        {
            // Skip blank lines
            if (markdown[pos] == '\n' || markdown[pos] == '\r')
            {
                pos++;
                continue;
            }

            // Check if this line is an H1 heading
            if (pos + 1 < markdown.Length && markdown[pos] == '#' && markdown[pos + 1] == ' ')
            {
                // Skip to end of H1 line
                int endOfLine = markdown.IndexOf('\n', pos);
                if (endOfLine < 0) return -1;
                pos = endOfLine + 1;
                break;
            }

            // Not a heading — we're already in the body
            break;
        }

        return pos;
    }
}
