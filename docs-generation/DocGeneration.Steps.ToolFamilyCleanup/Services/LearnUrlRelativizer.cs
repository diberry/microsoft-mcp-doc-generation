// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;

namespace ToolFamilyCleanup.Services;

/// <summary>
/// Converts full https://learn.microsoft.com URLs to site-root-relative
/// paths per AD-017 (Link Format Convention). Fixes: #220
///
/// Handles:
///   https://learn.microsoft.com/azure/...        → /azure/...
///   https://learn.microsoft.com/en-us/azure/...  → /azure/...
///   https://learn.microsoft.com/cli/azure/...    → /cli/azure/...
///   https://learn.microsoft.com/en-us/dotnet/... → /dotnet/...
///
/// Preserves query params and anchors.
/// Skips URLs inside backtick-delimited spans (inline code) and fenced code blocks.
/// Idempotent — already-relative paths pass through unchanged.
/// </summary>
public static partial class LearnUrlRelativizer
{
    // Matches: https://learn.microsoft.com followed by optional /en-us (or other locale),
    // then the path portion. Stops at whitespace, ), ], ", or '
    // Group 1 = optional locale like /en-us
    // Group 2 = the path we want to keep (e.g., /azure/storage/blobs?view=latest#section)
    private static readonly Regex LearnUrlPattern = BuildLearnUrlRegex();

    [GeneratedRegex(
        @"https://learn\.microsoft\.com(/[a-z]{2}(?:-[a-z]{2,4})?)?(/[^\s\)\]""'<>]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex BuildLearnUrlRegex();

    /// <summary>
    /// Replaces all full learn.microsoft.com URLs with site-root-relative paths.
    /// Skips URLs inside backtick spans and fenced code blocks.
    /// </summary>
    public static string Relativize(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
            return "";

        // Split into protected (backtick/code) and unprotected segments
        // Process only unprotected segments
        return ProcessWithCodeProtection(markdown);
    }

    private static string ProcessWithCodeProtection(string markdown)
    {
        // Strategy: walk the string, identify code spans and fenced blocks,
        // only apply regex replacement outside those regions.
        var result = new System.Text.StringBuilder(markdown.Length);
        int i = 0;

        while (i < markdown.Length)
        {
            // Check for fenced code block (``` at line start)
            if (i == 0 || (i > 0 && markdown[i - 1] == '\n'))
            {
                if (i + 2 < markdown.Length && markdown[i] == '`' && markdown[i + 1] == '`' && markdown[i + 2] == '`')
                {
                    // Find closing fence
                    int fenceEnd = markdown.IndexOf("\n```", i + 3, StringComparison.Ordinal);
                    if (fenceEnd >= 0)
                    {
                        int blockEnd = fenceEnd + 4;
                        // Include trailing newline if present
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

            // Check for inline backtick span
            if (markdown[i] == '`')
            {
                int closeBacktick = markdown.IndexOf('`', i + 1);
                if (closeBacktick >= 0)
                {
                    // Copy the backtick span verbatim (including both backticks)
                    result.Append(markdown, i, closeBacktick - i + 1);
                    i = closeBacktick + 1;
                    continue;
                }
            }

            // Find next code boundary or end of string
            int nextBacktick = markdown.IndexOf('`', i);
            int segmentEnd = nextBacktick >= 0 ? nextBacktick : markdown.Length;

            // Extract the non-code segment and apply URL replacement
            string segment = markdown.Substring(i, segmentEnd - i);
            string replaced = LearnUrlPattern.Replace(segment, "$2");
            result.Append(replaced);
            i = segmentEnd;
        }

        return result.ToString();
    }
}
