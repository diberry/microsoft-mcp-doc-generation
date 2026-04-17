// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;

namespace ToolFamilyCleanup.Services;

/// <summary>
/// Escapes bare angle-bracket placeholders in generated markdown content.
///
/// Generated example prompts contain bare &lt;placeholder&gt; values
/// (e.g., &lt;resource-name&gt;, &lt;subscription-id&gt;) that MS Learn
/// build validation flags as disallowed HTML tags.
///
/// Converts bare placeholders to backslash-escaped form per MS Learn convention:
///   &lt;resource-name&gt;  →  \&lt;resource-name\&gt;
///
/// Protected content (not escaped):
///   - Code fences (``` ... ```)
///   - Inline code spans (`...`)
///   - HTML comments (&lt;!-- ... --&gt;)
///   - Frontmatter (--- ... ---)
///   - Already-escaped placeholders (\&lt;...\&gt;)
///   - Already-backtick-wrapped placeholders (`&lt;...&gt;`)
///
/// Fixes: #416
/// </summary>
public static class PlaceholderEscaper
{
    // ── Protection patterns ─────────────────────────────────────────

    private static readonly Regex CodeBlockPattern = new(
        @"```[\s\S]*?```",
        RegexOptions.Compiled);

    private static readonly Regex InlineCodePattern = new(
        @"`[^`]+`",
        RegexOptions.Compiled);

    private static readonly Regex HtmlCommentPattern = new(
        @"<!--[\s\S]*?-->",
        RegexOptions.Compiled);

    // ── Placeholder pattern ─────────────────────────────────────────
    // Matches <word> where word is lowercase letters, digits, hyphens, underscores.
    // Negative lookbehind: not preceded by backslash (already escaped) or backtick.
    // Negative lookahead after closing >: not followed by closing backtick.
    private static readonly Regex PlaceholderPattern = new(
        @"(?<![\\\x00`])<([a-z][a-z0-9_-]*)>(?![\x00`])",
        RegexOptions.Compiled);

    /// <summary>
    /// Escapes bare angle-bracket placeholders in markdown content.
    /// Protects frontmatter, code blocks, inline code, and HTML comments.
    /// </summary>
    public static string Escape(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
            return markdown ?? "";

        // Separate frontmatter from body
        string frontmatter = "";
        string body = markdown;
        if (markdown.StartsWith("---"))
        {
            int endFm = markdown.IndexOf("\n---", 3, StringComparison.Ordinal);
            if (endFm > 0)
            {
                int fmEnd = markdown.IndexOf('\n', endFm + 4);
                if (fmEnd > 0)
                {
                    frontmatter = markdown[..(fmEnd + 1)];
                    body = markdown[(fmEnd + 1)..];
                }
            }
        }

        // Protect code blocks, inline code, and HTML comments with placeholders
        var placeholders = new Dictionary<string, string>();
        int placeholderIndex = 0;

        body = CodeBlockPattern.Replace(body, m =>
        {
            var key = $"\x00PB{placeholderIndex++}\x00";
            placeholders[key] = m.Value;
            return key;
        });

        body = InlineCodePattern.Replace(body, m =>
        {
            var key = $"\x00PI{placeholderIndex++}\x00";
            placeholders[key] = m.Value;
            return key;
        });

        body = HtmlCommentPattern.Replace(body, m =>
        {
            var key = $"\x00PH{placeholderIndex++}\x00";
            placeholders[key] = m.Value;
            return key;
        });

        // Escape bare placeholders: <word> → \<word\>
        body = PlaceholderPattern.Replace(body, @"\<$1\>");

        // Restore protected content
        foreach (var (key, value) in placeholders)
        {
            body = body.Replace(key, value);
        }

        return frontmatter + body;
    }
}
