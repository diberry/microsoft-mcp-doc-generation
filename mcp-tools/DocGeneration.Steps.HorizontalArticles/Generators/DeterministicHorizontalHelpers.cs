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
        "create", "delete", "update", "add", "remove", "set", "import", "assign",
        "cancel", "deploy", "publish", "send", "move", "copy", "upload"
    };

    private static readonly HashSet<string> DataVerbs = new(StringComparer.OrdinalIgnoreCase)
    {
        "list", "get", "query", "search", "find", "read", "export", "download",
        "recognize", "synthesize", "diagnose", "check_status"
    };

    /// <summary>
    /// Classifies a tool as "management" or "data" plane based on metadata and command verb.
    /// Management: destructive operations, create/delete/update commands.
    /// Data: read-only operations, list/get/query commands.
    /// </summary>
    public static string ClassifyToolPlane(HorizontalToolSummary tool)
    {
        // Metadata takes priority (null-safe)
        if (tool.Metadata != null)
        {
            if (tool.Metadata.TryGetValue("destructive", out var destructive) && destructive.Value)
                return "management";
            if (tool.Metadata.TryGetValue("readOnly", out var readOnly) && readOnly.Value)
                return "data";
        }

        // Fall back to command verb classification
        var verb = ExtractVerb(tool.Command);
        if (verb != null)
        {
            // Handle compound verbs like "createorupdate"
            if (verb.Contains("create", StringComparison.OrdinalIgnoreCase) ||
                verb.Contains("delete", StringComparison.OrdinalIgnoreCase) ||
                verb.Contains("update", StringComparison.OrdinalIgnoreCase))
                return "management";
            if (ManagementVerbs.Contains(verb))
                return "management";
            if (DataVerbs.Contains(verb))
                return "data";
        }

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
    /// Pre-computes one capability string per tool, keyed by command.
    /// </summary>
    public static Dictionary<string, string> PreComputeCapabilities(List<HorizontalToolSummary> tools)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var tool in tools)
        {
            // Skip duplicates (first wins)
            if (!result.ContainsKey(tool.Command))
                result[tool.Command] = ExtractCapability(tool);
        }
        return result;
    }

    /// <summary>
    /// Pre-computes short descriptions (10-15 words) for each tool, keyed by command.
    /// </summary>
    public static Dictionary<string, string> PreComputeShortDescriptions(List<HorizontalToolSummary> tools)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var tool in tools)
        {
            if (!result.ContainsKey(tool.Command))
                result[tool.Command] = TruncateDescription(tool.Description?.Trim() ?? tool.Command, maxWords: 12);
        }
        return result;
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
