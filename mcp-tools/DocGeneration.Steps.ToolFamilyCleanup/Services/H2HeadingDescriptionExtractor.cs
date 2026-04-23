// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;

namespace ToolFamilyCleanup.Services;

/// <summary>
/// Extracts action verbs from tool descriptions for H2 heading generation.
/// Handles compound patterns like "List or get" that should be preserved
/// instead of being simplified to "get details".
/// Stub for TDD — implement to make H2ActionMatchingTests pass.
/// Fixes: #416 Item 2
/// </summary>
public static class H2HeadingDescriptionExtractor
{
    // Common verb patterns we recognize
    private static readonly HashSet<string> CommonVerbs = new(StringComparer.OrdinalIgnoreCase)
    {
        "list", "get", "create", "update", "delete", "remove", "add", "show",
        "describe", "query", "search", "retrieve", "check", "set", "modify"
    };

    // Regex to match compound verb patterns like "list or get", "create or update"
    private static readonly Regex CompoundVerbPattern = new(
        @"^\s*(\w+)\s+or\s+(\w+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Extracts the action portion from a tool description for use in H2 headings.
    /// Preserves compound verbs like "list or get", "create or update".
    /// </summary>
    /// <param name="description">Tool description (e.g., "List or get Azure SQL databases")</param>
    /// <returns>Action text (e.g., "list or get")</returns>
    public static string ExtractAction(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return "get";

        // Check for compound verb patterns first (e.g., "List or get", "Create or update")
        var compoundMatch = CompoundVerbPattern.Match(description);
        if (compoundMatch.Success)
        {
            var verb1 = compoundMatch.Groups[1].Value.ToLowerInvariant();
            var verb2 = compoundMatch.Groups[2].Value.ToLowerInvariant();
            return $"{verb1} or {verb2}";
        }

        // Extract first word as verb
        var words = description.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > 0)
        {
            var firstWord = words[0].ToLowerInvariant();
            // If it's a recognized verb, return it
            if (CommonVerbs.Contains(firstWord))
            {
                return firstWord;
            }
        }

        // Fallback
        return "get";
    }
}
