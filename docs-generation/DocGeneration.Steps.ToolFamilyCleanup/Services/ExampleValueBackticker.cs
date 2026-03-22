// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;

namespace ToolFamilyCleanup.Services;

/// <summary>
/// Wraps bare example values in backticks within "(for example, ...)" patterns.
/// Parameter descriptions should use inline code formatting for values like
/// resource names, dates, intervals, and SKU names.
/// Fixes: #152
/// 
/// Idempotent — already-backticked values pass through unchanged.
/// </summary>
public static partial class ExampleValueBackticker
{
    // Matches: "(for example, ...)" where content has NO backticks (bare values)
    [GeneratedRegex(
        @"\(for example, ([^)`]+)\)",
        RegexOptions.Compiled)]
    private static partial Regex BareExamplePattern();

    /// <summary>
    /// Finds "(for example, VALUE)" patterns where VALUE is not backticked
    /// and wraps each comma-separated value in backticks.
    /// </summary>
    public static string Fix(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
            return "";

        return BareExamplePattern().Replace(markdown, match =>
        {
            var values = match.Groups[1].Value;

            // Split by ", " to handle comma-separated lists
            var parts = values.Split(", ");
            var backticked = parts.Select(v => $"`{v.Trim()}`");
            return $"(for example, {string.Join(", ", backticked)})";
        });
    }
}
