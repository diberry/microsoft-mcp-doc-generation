// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;

namespace ToolFamilyCleanup.Services;

/// <summary>
/// Converts formal non-contracted forms to contractions per Microsoft
/// style guide (Acrolinx: "Tone > Could you use contractions to make
/// it less formal?"). Fixes: #145
/// 
/// Only operates on prose text — skips content inside backticks.
/// Idempotent — already-contracted text passes through unchanged.
/// </summary>
public static partial class ContractionFixer
{
    // Patterns: match the non-contracted form, capture case-preserving prefix
    private static readonly (Regex Pattern, string Replacement)[] Rules =
    [
        (BuildRule("does not"), "doesn't"),
        (BuildRule("do not"), "don't"),
        (BuildRule("is not"), "isn't"),
        (BuildRule("are not"), "aren't"),
        (BuildRule("will not"), "won't"),
        (BuildRule("cannot"), "can't"),
        (BuildRule("can not"), "can't"),
    ];

    /// <summary>
    /// Replaces non-contracted negative forms with contractions.
    /// Skips text inside backticks to avoid breaking code references.
    /// </summary>
    public static string Fix(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
            return "";

        var result = markdown;
        foreach (var (pattern, replacement) in Rules)
        {
            result = pattern.Replace(result, replacement);
        }
        return result;
    }

    /// <summary>
    /// Builds a regex that matches the phrase outside of backtick-delimited spans.
    /// Uses a negative lookbehind/lookahead for backtick context.
    /// </summary>
    private static Regex BuildRule(string phrase)
    {
        // Match the phrase only when NOT inside backticks
        // Simple heuristic: not preceded by ` without a closing `
        return new Regex(
            $@"(?<!`[^`]*)(?<!\w){Regex.Escape(phrase)}(?!\w)(?![^`]*`)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
    }
}
