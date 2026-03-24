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

    // Positive contractions with case-preserving replacement
    private static readonly (Regex Pattern, string Contraction)[] PositiveRules =
    [
        (BuildRule("it is"), "it's"),
        (BuildRule("you are"), "you're"),
        (BuildRule("we have"), "we've"),
        (BuildRule("that is"), "that's"),
        (BuildRule("there is"), "there's"),
        (BuildRule("here is"), "here's"),
        (BuildRule("what is"), "what's"),
        (BuildRule("who is"), "who's"),
    ];

    /// <summary>
    /// Replaces non-contracted forms with contractions.
    /// Negative rules run first, then positive rules with case preservation.
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
        foreach (var (pattern, contraction) in PositiveRules)
        {
            result = pattern.Replace(result, m =>
                char.IsUpper(m.Value[0])
                    ? char.ToUpper(contraction[0]) + contraction[1..]
                    : contraction);
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
