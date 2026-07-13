// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Text.RegularExpressions;

namespace ToolFamilyCleanup.Services;

/// <summary>
/// Strips non-canonical duplicate example blocks from tool-family articles.
/// 
/// The canonical format is "Example prompts include:" followed by bullet items.
/// AI steps (3 and 4) sometimes inject additional formats:
///   - "> Example: ..." (blockquote single example)
///   - "Examples:\n- ..." (raw bullet list)
///   - "Example prompts:\n- ..." (variant without "include")
/// 
/// This post-processor removes those duplicates while preserving the canonical block.
/// </summary>
public static class DuplicateExampleStripper
{
    // Pattern 1: "> Example: <text>" — blockquote single example line
    // Matches "> Example:" followed by text, NOT "> Example prompts"
    private static readonly Regex BlockquoteExamplePattern = new(
        @"^> Example:(?! prompts).*$",
        RegexOptions.Multiline | RegexOptions.Compiled);

    // Pattern 2: "Examples:" followed by bullet items (not a heading)
    // Matches "Examples:" at start of line (not "## Examples") followed by "- " lines
    private static readonly Regex RawExamplesBlockPattern = new(
        @"^(?<!#+ )Examples:\s*\n(?:- .+\n?)+",
        RegexOptions.Multiline | RegexOptions.Compiled);

    // Pattern 2b: bare "Examples" header WITHOUT a colon and WITHOUT a "###"/"##" heading,
    // followed by a blank line and bullet items. This is the leak variant from #709 that
    // slipped past RawExamplesBlockPattern (which requires a colon). Anchored on a line that
    // is exactly "Examples" so prose like "Examples of supported regions ..." is preserved.
    private static readonly Regex BareExamplesBlockPattern = new(
        @"^Examples[ \t]*\r?\n(?:[ \t]*\r?\n)*(?:- .+\r?\n?)+",
        RegexOptions.Multiline | RegexOptions.Compiled);

    // Pattern 3: "Example prompts:" (without "include") followed by bullet items
    // Must NOT match "Example prompts include:"
    private static readonly Regex ExamplePromptsNoIncludePattern = new(
        @"^Example prompts:(?! include)\s*\n(?:- .+\n?)+",
        RegexOptions.Multiline | RegexOptions.Compiled);

    /// <summary>
    /// Strips non-canonical example blocks from the content.
    /// Preserves "Example prompts include:" canonical blocks.
    /// </summary>
    public static string Strip(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return content;
        }

        var result = content;

        // Remove blockquote examples ("> Example: ...")
        result = BlockquoteExamplePattern.Replace(result, "");

        // Remove raw "Examples:" bullet lists when they duplicate the canonical block.
        // If an AI step rewrote the canonical heading, restore it instead of dropping
        // the only example prompts for the tool section.
        result = RawExamplesBlockPattern.Replace(
            result,
            match => CanonicalizeOrRemoveExampleBlock(result, match, @"^Examples:\s*", "Example prompts include:"));

        // Remove bare "Examples" (no colon) bullet lists (#709), unless this is the
        // section's only example-prompt block.
        result = BareExamplesBlockPattern.Replace(
            result,
            match => CanonicalizeOrRemoveExampleBlock(result, match, @"^Examples[ \t]*", "Example prompts include:"));

        // Remove "Example prompts:" (without "include") bullet lists when duplicate;
        // otherwise normalize to the canonical label.
        result = ExamplePromptsNoIncludePattern.Replace(
            result,
            match => CanonicalizeOrRemoveExampleBlock(result, match, @"^Example prompts:(?! include)\s*", "Example prompts include:"));

        // Clean up excessive blank lines left by removals (3+ → 2)
        result = Regex.Replace(result, @"\n{3,}", "\n\n");

        return result;
    }

    private static string CanonicalizeOrRemoveExampleBlock(
        string fullContent,
        Match match,
        string headingPattern,
        string canonicalHeading)
    {
        return SectionContainsCanonicalExampleHeader(fullContent, match.Index)
            ? string.Empty
            : Regex.Replace(
                match.Value,
                headingPattern,
                canonicalHeading + Environment.NewLine,
                RegexOptions.IgnoreCase);
    }

    private static bool SectionContainsCanonicalExampleHeader(string content, int matchIndex)
    {
        var sectionStart = FindPreviousH2Start(content, matchIndex);
        var sectionEnd = FindNextH2Start(content, matchIndex);
        var sectionLength = sectionEnd < 0 ? content.Length - sectionStart : sectionEnd - sectionStart;
        var section = content.Substring(sectionStart, sectionLength);

        return section.Contains("Example prompts include:", StringComparison.Ordinal);
    }

    private static int FindPreviousH2Start(string content, int index)
    {
        var previous = content.LastIndexOf("\n## ", Math.Max(0, index), StringComparison.Ordinal);
        return previous < 0 ? 0 : previous + 1;
    }

    private static int FindNextH2Start(string content, int index)
        => content.IndexOf("\n## ", index + 1, StringComparison.Ordinal);
}
