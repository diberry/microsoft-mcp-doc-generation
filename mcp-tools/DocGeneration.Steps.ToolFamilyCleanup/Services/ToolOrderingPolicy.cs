// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ToolFamilyCleanup.Models;

namespace ToolFamilyCleanup.Services;

/// <summary>
/// Centralizes tool ordering logic for both single-resource and multi-resource families.
/// This is the single authority for tool presentation order at stitch time.
/// ToolReader provides metadata for grouping; ordering is applied here.
/// </summary>
public static class ToolOrderingPolicy
{
    /// <summary>
    /// Orders tools for a single-resource family page.
    /// Sort contract: case-insensitive alphabetical by ToolName, then case-sensitive
    /// ToolName tie-break, then FileName for absolute determinism.
    /// Tools with null or whitespace ToolName are placed at the end of the sorted list
    /// to ensure they remain visible rather than being silently dropped.
    /// </summary>
    /// <param name="tools">Unordered tools from a single-resource family.</param>
    /// <returns>Tools in deterministic presentation order.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="tools"/> is null.</exception>
    public static IEnumerable<ToolContent> OrderForSingleResource(IEnumerable<ToolContent> tools)
    {
        ArgumentNullException.ThrowIfNull(tools);

        var toolList = tools.ToList();
        var valid = toolList.Where(t => !string.IsNullOrWhiteSpace(t.ToolName)).ToList();
        var invalid = toolList.Where(t => string.IsNullOrWhiteSpace(t.ToolName)).ToList();

        var sorted = valid
            .OrderBy(t => t.ToolName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(t => t.ToolName, StringComparer.Ordinal)
            .ThenBy(t => t.FileName, StringComparer.Ordinal);

        return sorted.Concat(invalid);
    }

    /// <summary>
    /// Orders tools within a multi-resource group by action verb (last segment of the command),
    /// then by FileName for determinism.
    /// Sort contract: alphabetical by action verb extracted from the command
    /// (the part after the resource type), NOT by ToolName — because ToolName may diverge
    /// from the displayed heading after ReformatToolHeadingForMultiResource rewrites it.
    /// Tools with null or whitespace Command are placed at the end of the sorted list
    /// to ensure they remain visible rather than being silently dropped.
    /// </summary>
    /// <param name="tools">Unordered tools within a single resource group.</param>
    /// <returns>Tools ordered by action verb then FileName.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="tools"/> is null.</exception>
    public static IEnumerable<ToolContent> OrderForMultiResource(IEnumerable<ToolContent> tools)
    {
        ArgumentNullException.ThrowIfNull(tools);

        var toolList = tools.ToList();
        var valid = toolList.Where(t => !string.IsNullOrWhiteSpace(t.Command)).ToList();
        var invalid = toolList.Where(t => string.IsNullOrWhiteSpace(t.Command)).ToList();

        var sorted = valid
            .OrderBy(t => ExtractActionVerb(t.Command), StringComparer.OrdinalIgnoreCase)
            .ThenBy(t => ExtractActionVerb(t.Command), StringComparer.Ordinal)
            .ThenBy(t => t.FileName, StringComparer.Ordinal);

        return sorted.Concat(invalid);
    }

    /// <summary>
    /// Validates a collection of ToolContent objects meet ordering prerequisites.
    /// Returns a ValidationResult with any issues found.
    /// </summary>
    /// <remarks>
    /// This is a public API for callers to optionally validate inputs before ordering.
    /// Callers may choose to call this for early diagnostics, or skip it and let
    /// <see cref="OrderForSingleResource"/> and <see cref="OrderForMultiResource"/> handle invalid inputs gracefully.
    /// </remarks>
    public static ToolOrderingValidationResult Validate(IEnumerable<ToolContent> tools)
    {
        ArgumentNullException.ThrowIfNull(tools);

        var toolList = tools.ToList();
        var warnings = new List<string>();
        var invalidTools = new List<ToolContent>();

        foreach (var tool in toolList)
        {
            bool hasIssue = false;
            if (string.IsNullOrWhiteSpace(tool.ToolName))
            {
                warnings.Add($"Tool '{tool.FileName}' has null or whitespace ToolName.");
                hasIssue = true;
            }
            if (string.IsNullOrWhiteSpace(tool.Command))
            {
                warnings.Add($"Tool '{tool.FileName}' has null or whitespace Command.");
                hasIssue = true;
            }
            if (hasIssue)
                invalidTools.Add(tool);
        }

        return new ToolOrderingValidationResult
        {
            IsValid = invalidTools.Count == 0,
            Warnings = warnings,
            InvalidTools = invalidTools
        };
    }

    /// <summary>
    /// Extracts the action verb (last segment) from a command string.
    /// Command format: "namespace resource1 [resource2...] verb"
    /// Returns the verb portion (e.g., "create" from "compute disk create").
    /// Returns empty string for null/empty commands.
    /// </summary>
    internal static string ExtractActionVerb(string? command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return string.Empty;

        var segments = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // Stop before the first parameter flag (e.g., "--sku")
        var verbSegments = segments.TakeWhile(s => !s.StartsWith('-')).ToArray();

        return verbSegments.Length > 0 ? verbSegments[^1] : string.Empty;
    }
}
