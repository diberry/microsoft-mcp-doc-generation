// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;

namespace ToolFamilyCleanup.Services;

/// <summary>
/// Ensures a colon follows "including" in intro paragraphs that list capabilities.
/// The AI-generated intro paragraph pattern is:
///   "The Azure MCP Server lets you manage {desc}, including {capabilities}, with natural language prompts."
/// This fixer changes "including {capabilities}" to "including: {capabilities}".
/// Only targets intro paragraphs (lines containing "with natural language prompts").
/// Fixes: #282
/// </summary>
public static partial class IncludingColonFixer
{
    // Matches "including" NOT already followed by a colon, on lines that contain
    // "with natural language prompts" (the intro paragraph signature).
    // Uses a negative lookahead to avoid double-colon.
    [GeneratedRegex(
        @"(?<=\bincluding)(?!:)(?=\s+\S.*with natural language prompts)",
        RegexOptions.None)]
    private static partial Regex MissingColonPattern();

    /// <summary>
    /// Inserts a colon after "including" in intro paragraphs that list capabilities
    /// and end with "with natural language prompts".
    /// Idempotent — paragraphs that already have the colon pass through unchanged.
    /// Does not modify non-intro uses of "including".
    /// </summary>
    public static string Fix(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
            return "";

        return MissingColonPattern().Replace(markdown, ":");
    }
}
