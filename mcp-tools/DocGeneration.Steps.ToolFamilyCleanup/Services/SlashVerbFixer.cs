// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;

namespace ToolFamilyCleanup.Services;

/// <summary>
/// Replaces slash-stacked verbs/nouns with "or" phrasing per Microsoft
/// style guide (Acrolinx: "Clarity > Avoid using slashes between words").
///
/// Rules:
///   - "Word/word" → "Word or word" (case preserved)
///   - "Word/word/word" → "Word, word, or word"
///
/// Exclusions:
///   - File paths (contain \ or start with / or ./)
///   - URLs (contain :// or www.)
///   - Content inside backticks
///   - Content inside code blocks
///   - Known compound terms: read/write, input/output, client/server, true/false, yes/no, on/off
///
/// Idempotent — already "or" phrased text passes through unchanged.
/// </summary>
public static class SlashVerbFixer
{
    // Matches fenced code blocks (```...```)
    private static readonly Regex CodeBlockPattern = new(
        @"```[\s\S]*?```",
        RegexOptions.Compiled);

    // Matches inline code spans (`...`)
    private static readonly Regex InlineCodePattern = new(
        @"`[^`]+`",
        RegexOptions.Compiled);

    // Known compound terms that should NOT be replaced
    private static readonly HashSet<string> CompoundTerms = new(StringComparer.OrdinalIgnoreCase)
    {
        "read/write",
        "input/output",
        "client/server",
        "true/false",
        "yes/no",
        "on/off",
        "and/or",
        "tcp/ip",
        "udp/tcp",
        "i/o",
        "n/a",
        "sla/slo",
        "rpo/rto",
        "rbac/abac",
        "ci/cd",
        "os/2"
    };

    // Matches slash-stacked words (2 or 3 words separated by slashes)
    // Negative lookahead prevents partial matching of 4+ part paths
    private static readonly Regex SlashPattern = new(
        @"\b([A-Za-z][A-Za-z0-9]*\/[A-Za-z][A-Za-z0-9]*(?:\/[A-Za-z][A-Za-z0-9]*)?)\b(?!\/[A-Za-z])",
        RegexOptions.Compiled);

    // Matches markdown link destinations [text](url) to protect from replacement
    private static readonly Regex MarkdownLinkPattern = new(
        @"\[([^\]]*)\]\(([^)]+)\)",
        RegexOptions.Compiled);

    /// <summary>
    /// Replaces slash-stacked verbs with "or" phrasing.
    /// </summary>
    public static string Fix(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
            return "";

        // Protect code blocks and inline code by replacing with placeholders
        var placeholders = new Dictionary<string, string>();
        int placeholderIndex = 0;

        markdown = CodeBlockPattern.Replace(markdown, m =>
        {
            var key = $"\x00CB{placeholderIndex++}\x00";
            placeholders[key] = m.Value;
            return key;
        });

        markdown = InlineCodePattern.Replace(markdown, m =>
        {
            var key = $"\x00IC{placeholderIndex++}\x00";
            placeholders[key] = m.Value;
            return key;
        });

        // Protect markdown link destinations from replacement
        markdown = MarkdownLinkPattern.Replace(markdown, m =>
        {
            var key = $"\x00ML{placeholderIndex++}\x00";
            placeholders[key] = m.Value;
            return key;
        });

        // Process line by line to avoid replacing in paths/URLs
        var lines = markdown.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            
            // Skip lines that look like paths or URLs
            if (line.Contains("://") || line.Contains("www.") || 
                line.Contains("./") || line.Contains("../") ||
                line.Contains('\\') || line.Contains("C:"))
            {
                continue;
            }

            // Replace slash-stacked words in this line
            lines[i] = SlashPattern.Replace(line, m =>
            {
                var match = m.Groups[1].Value;

                // Check if this is a compound term (case-insensitive)
                if (CompoundTerms.Contains(match))
                    return match;

                // Check if preceded by / (part of a longer path like /usr/local/bin)
                if (m.Index > 0 && line[m.Index - 1] == '/')
                    return match;

                // Check if followed by file extension or trailing slash (path-like context)
                var afterEnd = m.Index + m.Length;
                if (afterEnd < line.Length && (line[afterEnd] == '.' || line[afterEnd] == '/'))
                    return match;

                // Split on slashes
                var parts = match.Split('/');

                // Replace with "or" phrasing
                if (parts.Length == 2)
                {
                    // "Word/word" → "Word or word"
                    return $"{parts[0]} or {parts[1]}";
                }
                else if (parts.Length == 3)
                {
                    // "Word/word/word" → "Word, word, or word"
                    return $"{parts[0]}, {parts[1]}, or {parts[2]}";
                }

                // Shouldn't get here due to regex pattern, but return unchanged
                return match;
            });
        }

        markdown = string.Join('\n', lines);

        // Restore placeholders
        foreach (var (key, value) in placeholders)
        {
            markdown = markdown.Replace(key, value);
        }

        return markdown;
    }
}
