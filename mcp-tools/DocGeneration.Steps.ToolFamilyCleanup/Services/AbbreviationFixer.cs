// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;

namespace ToolFamilyCleanup.Services;

/// <summary>
/// Replaces informal Latin abbreviations with their spelled-out forms per
/// Microsoft style guide (Acrolinx: "Clarity > Avoid Latin abbreviations").
///
/// Rules:
///   - "e.g." → "for example" (with comma handling)
///   - "i.e." → "that is" (with comma handling)
///   - "etc." → remove or replace with "and more"
///
/// Skips content inside backticks and code blocks.
/// Idempotent — already spelled-out text passes through unchanged.
/// </summary>
public static class AbbreviationFixer
{
    // Matches fenced code blocks (```...```)
    private static readonly Regex CodeBlockPattern = new(
        @"```[\s\S]*?```",
        RegexOptions.Compiled);

    // Matches inline code spans (`...`)
    private static readonly Regex InlineCodePattern = new(
        @"`[^`]+`",
        RegexOptions.Compiled);

    // Matches markdown link destinations [text](url) to protect URLs from replacement
    private static readonly Regex MarkdownLinkPattern = new(
        @"\[([^\]]*)\]\(([^)]+)\)",
        RegexOptions.Compiled);

    // "e.g." → "for example"
    // Handles: "e.g., X", "(e.g., X)", "e.g. X"
    // Uses [ \t] (horizontal whitespace) to avoid consuming newlines
    private static readonly Regex EgPattern = new(
        @"\be\.g\.[ \t]*,?[ \t]*",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // "i.e." → "that is"
    // Handles: "i.e., X", "(i.e., X)", "i.e. X"
    private static readonly Regex IePattern = new(
        @"\bi\.e\.[ \t]*,?[ \t]*",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // "etc." at end of line (list terminator) → replace with "."
    // Consumes optional preceding comma. Multiline so $ matches end of each line.
    // \r? handles both \n and \r\n line endings.
    private static readonly Regex EtcAtEndPattern = new(
        @",?[ \t]+etc\.\r?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);

    // "etc." in middle of text → replace with "and more"
    // Uses [ \t] (horizontal whitespace) to avoid consuming newlines
    private static readonly Regex EtcInMiddlePattern = new(
        @"[ \t]+etc\.[ \t]+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Replaces Latin abbreviations with spelled-out forms.
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

        // Apply replacements
        markdown = EgPattern.Replace(markdown, "for example, ");
        markdown = IePattern.Replace(markdown, "that is, ");
        
        // For etc., handle end-of-line first (list terminator), then mid-text
        markdown = EtcAtEndPattern.Replace(markdown, ".");
        markdown = EtcInMiddlePattern.Replace(markdown, " and more ");

        // Restore placeholders
        foreach (var (key, value) in placeholders)
        {
            markdown = markdown.Replace(key, value);
        }

        return markdown;
    }
}
