// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;

namespace ToolFamilyCleanup.Services;

/// <summary>
/// Ensures a blank line between the "[Tool annotation hints]" link
/// and the annotation values line (Destructive: ❌ | ...).
/// Without the blank line, Markdown renders them as one paragraph.
/// Fixes: #151
/// </summary>
public static partial class AnnotationSpaceFixer
{
    // Matches: annotation link line + single newline + non-blank content (no blank line between)
    [GeneratedRegex(
        @"(\[Tool annotation hints\]\([^\)]+\):)\n(?!\n)",
        RegexOptions.Compiled)]
    private static partial Regex MissingBlankLinePattern();

    /// <summary>
    /// Inserts a blank line after each "[Tool annotation hints](...):' line
    /// that is immediately followed by content (no blank line).
    /// Idempotent — already-correct documents pass through unchanged.
    /// </summary>
    public static string Fix(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
            return "";

        return MissingBlankLinePattern().Replace(markdown, "$1\n\n");
    }
}
