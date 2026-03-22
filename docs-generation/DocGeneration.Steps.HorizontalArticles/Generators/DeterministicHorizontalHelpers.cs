// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using HorizontalArticleGenerator.Models;

namespace HorizontalArticleGenerator.Generators;

/// <summary>
/// Pre-computes deterministic structural data for horizontal articles:
/// tool ordering (management/data plane), capabilities, short descriptions.
/// Reduces AI workload by providing pre-computed fields as defaults.
/// Fixes: #163 Tier 2b
/// </summary>
public static class DeterministicHorizontalHelpers
{
    private static readonly HashSet<string> ManagementVerbs = new(StringComparer.OrdinalIgnoreCase)
    {
        "create", "delete", "update", "add", "remove", "set", "import", "assign"
    };

    private static readonly HashSet<string> DataVerbs = new(StringComparer.OrdinalIgnoreCase)
    {
        "list", "get", "query", "search", "find", "read", "export", "download"
    };

    /// <summary>
    /// Classifies a tool as "management" or "data" plane based on metadata and command verb.
    /// Management: destructive operations, create/delete/update commands.
    /// Data: read-only operations, list/get/query commands.
    /// </summary>
    public static string ClassifyToolPlane(HorizontalToolSummary tool)
    {
        // Metadata takes priority
        if (tool.Metadata.TryGetValue("destructive", out var destructive) && destructive.Value)
            return "management";
        if (tool.Metadata.TryGetValue("readOnly", out var readOnly) && readOnly.Value)
            return "data";

        // Fall back to command verb classification
        var verb = ExtractVerb(tool.Command);
        if (verb != null && ManagementVerbs.Contains(verb))
            return "management";
        if (verb != null && DataVerbs.Contains(verb))
            return "data";

        // Default to data plane (read-only assumption)
        return "data";
    }

    /// <summary>
    /// Orders tools: management plane first, then data plane.
    /// Alphabetical by command within each group.
    /// </summary>
    public static List<HorizontalToolSummary> OrderToolsByPlane(List<HorizontalToolSummary> tools)
    {
        return tools
            .OrderBy(t => ClassifyToolPlane(t) == "management" ? 0 : 1)
            .ThenBy(t => t.Command, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Extracts a capability string from a tool's description.
    /// Strips trailing periods and truncates to 15 words max.
    /// </summary>
    public static string ExtractCapability(HorizontalToolSummary tool)
    {
        var desc = tool.Description?.Trim() ?? tool.Command;
        return TruncateDescription(desc.TrimEnd('.'), maxWords: 15);
    }

    /// <summary>
    /// Pre-computes one capability string per tool.
    /// </summary>
    public static List<string> PreComputeCapabilities(List<HorizontalToolSummary> tools)
    {
        return tools.Select(ExtractCapability).ToList();
    }

    /// <summary>
    /// Pre-computes short descriptions (10-15 words) for each tool, keyed by command.
    /// </summary>
    public static Dictionary<string, string> PreComputeShortDescriptions(List<HorizontalToolSummary> tools)
    {
        return tools.ToDictionary(
            t => t.Command,
            t => TruncateDescription(t.Description?.Trim() ?? t.Command, maxWords: 12));
    }

    /// <summary>
    /// Truncates a description to the specified maximum word count.
    /// </summary>
    public static string TruncateDescription(string description, int maxWords)
    {
        var words = description.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length <= maxWords)
            return description.TrimEnd('.');

        return string.Join(' ', words.Take(maxWords)).TrimEnd('.', ',', ';', ':');
    }

    private static string? ExtractVerb(string command)
    {
        var segments = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length > 0 ? segments[^1] : null;
    }
}
