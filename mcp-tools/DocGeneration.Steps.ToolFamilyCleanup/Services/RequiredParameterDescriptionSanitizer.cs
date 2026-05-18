// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;

namespace ToolFamilyCleanup.Services;

/// <summary>
/// Strips "Defaults to..." language from Required parameter descriptions.
/// Required parameters have no default — users must always provide a value —
/// so including default-value language is contradictory and confusing.
/// </summary>
public static class RequiredParameterDescriptionSanitizer
{
    // Sentence-boundary lookahead: a period that is followed by space+uppercase or end-of-string.
    // This prevents stopping at periods embedded in values like "24.04" or "v1.0".
    private const string SentenceEnd = @"\.(?=\s+[A-Z]|\s*$)";

    // "If not specified, defaults to {value}[...]."
    private static readonly Regex IfNotSpecifiedDefaultsTo = new(
        @"If not specified,\s+defaults to\s+.+?" + SentenceEnd,
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // "Defaults to {value}[ if not specified | when not provided | unless specified | ...]."
    // Also covers plain "Defaults to {value}." — the lazy .+? handles both.
    private static readonly Regex DefaultsTo = new(
        @"Defaults to\s+.+?" + SentenceEnd,
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // "Default is {value}."
    private static readonly Regex DefaultIs = new(
        @"Default is\s+.+?" + SentenceEnd,
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // "Default: {value}."
    private static readonly Regex DefaultColon = new(
        @"Default:\s+.+?" + SentenceEnd,
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex MultipleSpaces = new(@"  +", RegexOptions.Compiled);
    private static readonly Regex DoublePeriod = new(@"\.\.+", RegexOptions.Compiled);
    private static readonly Regex TrailingWhitespace = new(@"\s+$", RegexOptions.Compiled);

    /// <summary>
    /// Strips "Defaults to..." language when the parameter is Required.
    /// Returns the description unchanged when <paramref name="isRequired"/> is false.
    /// </summary>
    /// <param name="description">The parameter description text.</param>
    /// <param name="isRequired">Whether the parameter is marked Required.</param>
    /// <returns>Sanitized description.</returns>
    public static string Apply(string description, bool isRequired)
    {
        if (!isRequired)
            return description;

        if (string.IsNullOrWhiteSpace(description))
            return description;

        var result = description;

        // Order matters: most-specific patterns first to avoid partial matches.
        result = IfNotSpecifiedDefaultsTo.Replace(result, string.Empty);
        result = DefaultsTo.Replace(result, string.Empty);
        result = DefaultIs.Replace(result, string.Empty);
        result = DefaultColon.Replace(result, string.Empty);

        // Clean up artefacts: collapse runs of spaces, fix double periods, trim.
        result = MultipleSpaces.Replace(result, " ");
        result = DoublePeriod.Replace(result, ".");
        result = TrailingWhitespace.Replace(result, string.Empty);
        result = result.Trim();

        return result;
    }
}
