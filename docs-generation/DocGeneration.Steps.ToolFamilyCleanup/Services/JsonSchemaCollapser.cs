// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Text.RegularExpressions;

namespace ToolFamilyCleanup.Services;

/// <summary>
/// Collapses inline JSON schema blocks in parameter table cells into concise
/// prose descriptions. The Deploy article's 151-line schema (314 &amp;quot; entities)
/// was the primary cause of its 61/100 Acrolinx score.
///
/// Handles:
///   Multi-line JSON schema with &amp;quot; HTML entities in table cells → prose
///   Code blocks (triple backticks) are protected — schemas inside them are NOT collapsed
///   Already-collapsed text passes through unchanged (idempotent)
/// </summary>
public static class JsonSchemaCollapser
{
    private const string Replacement = "JSON object that defines the input structure for this tool.";

    // Matches a table row whose Description cell starts with { on the same line as the | delimiters,
    // continues across multiple lines containing &quot;type&quot; and &quot;properties&quot;,
    // and ends with }. | or } | (closing brace, optional dot, optional whitespace, pipe).
    //
    // Group 1: row prefix up to and including the last ` | ` before the schema
    // Group 2: the entire JSON schema block (to be replaced)
    // Group 3: trailing ` |` or `. |`
    private static readonly Regex SchemaInTableCellPattern = new(
        @"^(\|[^|]*\|[^|]*\| )(\{\s*\n(?:.*\n)*?.*\})(\.? \|)\r?$",
        RegexOptions.Multiline | RegexOptions.Compiled);

    /// <summary>
    /// Collapses inline JSON schemas in parameter table cells into prose.
    /// </summary>
    public static string Collapse(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
            return "";

        return ProcessWithCodeBlockProtection(markdown);
    }

    private static string ProcessWithCodeBlockProtection(string markdown)
    {
        // Split into code-block-protected and unprotected segments.
        // Process only unprotected segments.
        var result = new StringBuilder(markdown.Length);
        int i = 0;

        while (i < markdown.Length)
        {
            // Check for fenced code block (``` at line start)
            if (i == 0 || (i > 0 && markdown[i - 1] == '\n'))
            {
                if (i + 2 < markdown.Length && markdown[i] == '`' && markdown[i + 1] == '`' && markdown[i + 2] == '`')
                {
                    int fenceEnd = markdown.IndexOf("\n```", i + 3, StringComparison.Ordinal);
                    if (fenceEnd >= 0)
                    {
                        int blockEnd = fenceEnd + 4;
                        if (blockEnd < markdown.Length && markdown[blockEnd] == '\n')
                            blockEnd++;
                        result.Append(markdown, i, blockEnd - i);
                        i = blockEnd;
                        continue;
                    }
                    // No closing fence — treat rest as code block
                    result.Append(markdown, i, markdown.Length - i);
                    return result.ToString();
                }
            }

            // Find the next code fence or end of string
            int nextFence = FindNextCodeFence(markdown, i);
            int segmentEnd = nextFence >= 0 ? nextFence : markdown.Length;

            // Extract the non-code segment and apply schema collapsing
            string segment = markdown.Substring(i, segmentEnd - i);
            string replaced = CollapseSchemas(segment);
            result.Append(replaced);
            i = segmentEnd;
        }

        return result.ToString();
    }

    private static int FindNextCodeFence(string markdown, int startIndex)
    {
        int pos = startIndex;
        while (pos < markdown.Length)
        {
            int idx = markdown.IndexOf("```", pos, StringComparison.Ordinal);
            if (idx < 0) return -1;

            // Must be at line start
            if (idx == 0 || markdown[idx - 1] == '\n')
                return idx;

            pos = idx + 3;
        }
        return -1;
    }

    private static string CollapseSchemas(string segment)
    {
        return SchemaInTableCellPattern.Replace(segment, match =>
        {
            var schemaBlock = match.Groups[2].Value;

            // Only collapse if it looks like a JSON schema (has &quot;type&quot; and &quot;properties&quot;)
            if (schemaBlock.Contains("&quot;type&quot;") && schemaBlock.Contains("&quot;properties&quot;"))
            {
                return $"{match.Groups[1].Value}{Replacement} |";
            }

            // Not a schema — return unchanged
            return match.Value;
        });
    }
}
