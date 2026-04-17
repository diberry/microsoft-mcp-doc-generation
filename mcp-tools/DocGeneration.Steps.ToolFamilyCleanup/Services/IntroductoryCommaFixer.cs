// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;

namespace ToolFamilyCleanup.Services;

/// <summary>
/// Inserts missing commas after introductory phrases per Microsoft style
/// guide (Acrolinx GR-3: "Comma after introductory phrase"). Fixes: #146
///
/// Only operates at sentence boundaries — skips mid-sentence occurrences.
/// Skips content inside backticks and code blocks.
/// Idempotent — already-correct phrases pass through unchanged.
/// </summary>
public static class IntroductoryCommaFixer
{
    private static readonly string[] IntroductoryPhrases =
    [
        "If not specified",
        "If not provided",
        "For example",
        "In addition",
        "By default",
        "In this case",
        "If not",
    ];

    // Matches fenced code blocks (```...```)
    private static readonly Regex CodeBlockPattern = new(
        @"```[\s\S]*?```",
        RegexOptions.Compiled);

    // Matches inline code spans (`...`)
    private static readonly Regex InlineCodePattern = new(
        @"`[^`]+`",
        RegexOptions.Compiled);

    /// <summary>
    /// Inserts commas after introductory phrases that appear at sentence
    /// boundaries and are missing the required comma.
    /// </summary>
    public static string Fix(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
            return "";

        // Protect code blocks and inline code by replacing with placeholders
        var placeholders = new Dictionary<string, string>();
        int placeholderIndex = 0;

        // Replace code blocks first (they may contain inline code)
        markdown = CodeBlockPattern.Replace(markdown, m =>
        {
            var key = $"\x00CB{placeholderIndex++}\x00";
            placeholders[key] = m.Value;
            return key;
        });

        // Replace inline code spans
        markdown = InlineCodePattern.Replace(markdown, m =>
        {
            var key = $"\x00IC{placeholderIndex++}\x00";
            placeholders[key] = m.Value;
            return key;
        });

        // Apply comma insertion for each introductory phrase
        foreach (var phrase in IntroductoryPhrases)
        {
            markdown = InsertCommaAfterPhrase(markdown, phrase, IntroductoryPhrases);
        }

        // Restore placeholders
        foreach (var (key, value) in placeholders)
        {
            markdown = markdown.Replace(key, value);
        }

        return markdown;
    }

    /// <summary>
    /// Inserts a comma after the given phrase when it appears at a sentence
    /// boundary (start of string, after a period+space, after newline, after
    /// bullet marker) and is NOT already followed by a comma.
    /// If this phrase is a prefix of a longer phrase in the list, a negative
    /// lookahead prevents matching the longer phrase (fixes #280).
    /// </summary>
    private static string InsertCommaAfterPhrase(string markdown, string phrase, string[] allPhrases)
    {
        // Build negative lookahead so short phrases don't corrupt longer ones
        var extensions = allPhrases
            .Where(p => p.Length > phrase.Length &&
                   p.StartsWith(phrase + " ", StringComparison.OrdinalIgnoreCase))
            .Select(p => Regex.Escape(p[phrase.Length..]))
            .ToList();

        var extensionGuard = extensions.Count > 0
            ? $"(?!{string.Join("|", extensions)})"
            : "";

        // Match the phrase at sentence boundaries, NOT already followed by comma
        // Sentence boundaries: start of string, after ". ", after newline, after "- "
        var pattern = $@"(?<=^|(?<=\.)\s|(?<=\n)|- )({Regex.Escape(phrase)}){extensionGuard}(?!,)\s";
        return Regex.Replace(markdown, pattern, "$1, ", RegexOptions.Multiline);
    }
}
