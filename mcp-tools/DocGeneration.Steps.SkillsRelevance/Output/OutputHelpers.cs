// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;

namespace SkillsRelevance.Output;

/// <summary>
/// Shared helpers for skill relevance output writers.
/// </summary>
internal static class OutputHelpers
{
    /// <summary>
    /// Sanitizes a service name into a safe filename component.
    /// </summary>
    internal static string SanitizeFileName(string name) =>
        Regex.Replace(name.ToLowerInvariant().Replace(' ', '-'), @"[^a-z0-9\-]", "");

    /// <summary>
    /// Maps a relevance score to a human-readable level string.
    /// </summary>
    internal static string GetRelevanceLevel(double score) => score switch
    {
        >= 0.8 => "high",
        >= 0.5 => "medium",
        >= 0.2 => "low",
        _ => "minimal"
    };
}
