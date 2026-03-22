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

        // Remove raw "Examples:" bullet lists
        result = RawExamplesBlockPattern.Replace(result, "");

        // Remove "Example prompts:" (without "include") bullet lists
        result = ExamplePromptsNoIncludePattern.Replace(result, "");

        // Clean up excessive blank lines left by removals (3+ → 2)
        result = Regex.Replace(result, @"\n{3,}", "\n\n");

        return result;
    }
}
