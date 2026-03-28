// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;

namespace ToolFamilyCleanup.Services;

/// <summary>
/// Strips engineering-authored example patterns from MCP CLI source descriptions
/// that leak into tool-family articles.
///
/// Three patterns are removed:
///   1. H3 "### Examples" / "### Example" heading blocks with bullet items
///   2. Standalone "Example: ..." lines (not blockquotes, not in paragraphs)
///   3. Inline "Example prompt: ..." sentences (at end of paragraph or standalone)
///      Including multi-line variant: "Example prompt:\n"text""
///
/// The canonical format "Example prompts include:" is always preserved.
/// </summary>
public static class EngineeringExampleStripper
{
    // Pattern 1: ### Examples / ### Example heading followed by bullet items or text
    // Captures the heading + optional blank line + bullet items (or any non-heading text until next blank line pair or heading)
    private static readonly Regex H3ExamplesBlockPattern = new(
        @"^###\s+Examples?\s*\n\s*\n(?:- .+\n?)+",
        RegexOptions.Multiline | RegexOptions.Compiled);

    // Pattern 2: Standalone "Example: ..." line (NOT a blockquote "> Example:")
    // Must be at start of line, not preceded by "> "
    // Must NOT match "Example prompts" (handled separately)
    private static readonly Regex StandaloneExampleLinePattern = new(
        @"^Example:(?! prompts) .+$",
        RegexOptions.Multiline | RegexOptions.Compiled);

    // Pattern 3a: Standalone "Example prompt: <text>" on a single line
    private static readonly Regex StandaloneExamplePromptSingleLinePattern = new(
        @"^Example prompt:(?! include) .+$",
        RegexOptions.Multiline | RegexOptions.Compiled);

    // Pattern 3a-multi: "Example prompt:" alone on one line, with text on the next line
    // Uses \r?\n to handle both Unix and Windows line endings
    private static readonly Regex StandaloneExamplePromptMultiLinePattern = new(
        @"^Example prompt:(?! include)[ \t]*\r?\n[^\r\n]+",
        RegexOptions.Multiline | RegexOptions.Compiled);

    // Pattern 3b: Inline " Example prompt: ..." at end of a sentence within a paragraph
    // Captures from " Example prompt:" to end of line, preserving text before it
    // Uses [ \t]+ (not \s*) to stay on the same line — must not match across blank lines
    private static readonly Regex InlineExamplePromptPattern = new(
        @"(?<=\S[.!?])[ \t]+Example prompt:(?! include).+$",
        RegexOptions.Multiline | RegexOptions.Compiled);

    /// <summary>
    /// Strips engineering-authored example patterns from content.
    /// Preserves "Example prompts include:" canonical blocks.
    /// </summary>
    public static string Strip(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return content;
        }

        var result = content;

        // Remove H3 Examples blocks (### Examples + bullet items)
        result = H3ExamplesBlockPattern.Replace(result, "");

        // Remove inline "Example prompt: ..." from within paragraphs (before standalone patterns)
        result = InlineExamplePromptPattern.Replace(result, "");

        // Remove standalone "Example prompt: ..." lines (single-line)
        result = StandaloneExamplePromptSingleLinePattern.Replace(result, "");

        // Remove multi-line "Example prompt:\n<text>" blocks
        result = StandaloneExamplePromptMultiLinePattern.Replace(result, "");

        // Remove standalone "Example: ..." lines
        result = StandaloneExampleLinePattern.Replace(result, "");

        // Clean up excessive blank lines left by removals (3+ → 2)
        result = Regex.Replace(result, @"\n{3,}", "\n\n");

        return result;
    }
}
