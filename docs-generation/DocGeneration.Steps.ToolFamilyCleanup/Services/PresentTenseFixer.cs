// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;

namespace ToolFamilyCleanup.Services;

/// <summary>
/// Converts future tense ("will ...") to present tense per Microsoft
/// style guide (Acrolinx GR-1: "Use present tense"). Fixes: #145
///
/// Handles three patterns:
///   1. "will not be <past-participle>" → "is not <past-participle>"
///   2. "will be <past-participle>"     → "is <past-participle>"  /  "are <past-participle>"
///   3. "will <verb>"                   → "<verb>s"
///
/// Skips content inside backticks and code blocks.
/// Idempotent — already present-tense text passes through unchanged.
/// </summary>
public static class PresentTenseFixer
{
    // "will not be <past-participle>" → "is not <past-participle>"
    private static readonly Regex WillNotBePattern = new(
        @"\bwill not be (\w+ed)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // "will be <past-participle>" → "is/are <past-participle>"
    // Uses lookbehind for plural subject hints (Results/Resources/...)
    private static readonly Regex WillBePluralPattern = new(
        @"(?<=\b\w+s) will be (\w+ed)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex WillBeSingularPattern = new(
        @"\bwill be (\w+ed)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // "will <verb>" → "<verb>s" — only matches common verb patterns
    private static readonly Regex WillVerbPattern = new(
        @"\bwill (return|create|list|display|delete|provide|generate|show|update|remove|filter|set|get|send|retrieve|produce|include|contain|output)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Matches fenced code blocks (```...```)
    private static readonly Regex CodeBlockPattern = new(
        @"```[\s\S]*?```",
        RegexOptions.Compiled);

    // Matches inline code spans (`...`)
    private static readonly Regex InlineCodePattern = new(
        @"`[^`]+`",
        RegexOptions.Compiled);

    /// <summary>
    /// Converts future tense constructions to present tense.
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

        // Order matters: "will not be" before "will be" before "will <verb>"

        // 1. "will not be <past-participle>" → "is not <past-participle>"
        markdown = WillNotBePattern.Replace(markdown, "is not $1");

        // 2. "will be <past-participle>" — plural subjects get "are", else "is"
        markdown = WillBePluralPattern.Replace(markdown, " are $1");
        markdown = WillBeSingularPattern.Replace(markdown, "is $1");

        // 3. "will <verb>" → "<verb>s"
        markdown = WillVerbPattern.Replace(markdown, m =>
        {
            var verb = m.Groups[1].Value.ToLowerInvariant();
            return AddThirdPersonS(verb);
        });

        // Restore placeholders
        foreach (var (key, value) in placeholders)
        {
            markdown = markdown.Replace(key, value);
        }

        return markdown;
    }

    /// <summary>
    /// Adds third-person singular -s/-es to a verb.
    /// </summary>
    private static string AddThirdPersonS(string verb)
    {
        if (verb.EndsWith("sh") || verb.EndsWith("ch") || verb.EndsWith("s") ||
            verb.EndsWith("x") || verb.EndsWith("z"))
        {
            return verb + "es";
        }

        if (verb.EndsWith("y") && verb.Length > 1 && !IsVowel(verb[^2]))
        {
            return verb[..^1] + "ies";
        }

        return verb + "s";
    }

    private static bool IsVowel(char c) => "aeiou".Contains(c);
}
