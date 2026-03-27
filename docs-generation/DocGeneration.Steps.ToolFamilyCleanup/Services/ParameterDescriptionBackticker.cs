// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;

namespace ToolFamilyCleanup.Services;

/// <summary>
/// Wraps technical values in parameter description text with backticks.
/// Targets enum names, boolean literals, date formats, and CLI switches
/// to improve Acrolinx Spelling &amp; Grammar scores.
/// </summary>
public static class ParameterDescriptionBackticker
{
    private static readonly Regex BacktickedSpan = new(@"`[^`]+`", RegexOptions.Compiled);
    private static readonly Regex BooleanLiteral = new(@"\b(true|false)\b", RegexOptions.Compiled);
    private static readonly Regex CliSwitch = new(@"--[a-z][a-z0-9-]*", RegexOptions.Compiled);
    private static readonly Regex DateFormat = new(
        @"\b[Yy]{2,4}-[Mm]{2}-[Dd]{2}(?:T[Hh]{2}:[Mm]{2}:[Ss]{2})?\b", RegexOptions.Compiled);
    private static readonly Regex EnumListAfterColon = new(
        @"(?<=:\s)([A-Z][a-zA-Z_]*(?:(?:\s*,\s*(?:or\s+)?|\s+or\s+)[A-Z][a-zA-Z_]*)+)",
        RegexOptions.Compiled);
    private static readonly Regex EnumItem = new(@"[A-Z][a-zA-Z_]*", RegexOptions.Compiled);
    private static readonly Regex Placeholder = new(@"\x00(\d+)\x00", RegexOptions.Compiled);

    /// <summary>
    /// Applies backtick wrapping to technical values in a parameter description string.
    /// </summary>
    /// <param name="description">The raw parameter description text.</param>
    /// <returns>The description with technical values wrapped in backticks.</returns>
    public static string Apply(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return description;

        var result = description;

        // Protect already-backticked spans from double-wrapping
        var protectedSpans = new List<string>();
        result = BacktickedSpan.Replace(result, m =>
        {
            protectedSpans.Add(m.Value);
            return $"\x00{protectedSpans.Count - 1}\x00";
        });

        // Boolean literals (case-sensitive, lowercase only)
        result = BooleanLiteral.Replace(result, "`$1`");

        // CLI switches: --flag-name
        result = CliSwitch.Replace(result, "`$0`");

        // Date format patterns: YYYY-MM-DD, yyyy-MM-ddTHH:mm:ss
        result = DateFormat.Replace(result, "`$0`");

        // Enum values in list context after a colon (2+ PascalCase items)
        result = EnumListAfterColon.Replace(result,
            m => EnumItem.Replace(m.Value, inner => $"`{inner.Value}`"));

        // Restore protected spans
        if (protectedSpans.Count > 0)
            result = Placeholder.Replace(result,
                m => protectedSpans[int.Parse(m.Groups[1].Value)]);

        return result;
    }
}
