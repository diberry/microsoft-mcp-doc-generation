// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;
using Shared;

namespace GenerativeAI;

/// <summary>
/// Aligns CLI tool descriptions with NLP descriptions using deterministic voice transformation.
/// No AI involved — pure regex-based voice adaptation for reliable, consistent output.
/// </summary>
public class CliProseImprover
{
    // Deterministic voice patterns: convert "This tool..." to imperative voice
    private static readonly Regex ThisToolPattern = new(
        @"^This\s+tool\s+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex McpToolPattern = new(
        @"^Model\s+Context\s+Protocol\s+\(MCP\)\s+tools?\s+let[s]?\s+you\s+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex McpToolSetPreamble = new(
        @"This\s+tool\s+is\s+part\s+of\s+the\s+Model\s+Context\s+Protocol\s+\(MCP\)\s+tool\s+set\.\s*",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex McpServerReference = new(
        @"MCP\s+Server",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Aligns CLI tool descriptions with NLP descriptions using deterministic voice transformation.
    /// When an NLP description is available, it's adapted to CLI voice.
    /// When no NLP description exists, the raw CLI description is kept as-is.
    /// </summary>
    public Task<IReadOnlyDictionary<string, CliToolInfo>> ImproveProseAsync(
        IReadOnlyDictionary<string, CliToolInfo> cliTools,
        IReadOnlyDictionary<string, string>? nlpDescriptions = null,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, CliToolInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, tool) in cliTools)
        {
            string? nlpDescription = null;
            nlpDescriptions?.TryGetValue(key, out nlpDescription);

            if (!string.IsNullOrWhiteSpace(nlpDescription))
            {
                var cliDescription = AdaptNlpToCliVoice(nlpDescription);
                result[key] = tool with { Description = cliDescription };
            }
            else
            {
                result[key] = tool;
            }
        }

        return Task.FromResult<IReadOnlyDictionary<string, CliToolInfo>>(result);
    }

    /// <summary>
    /// Deterministic NLP→CLI voice adaptation.
    /// Strips MCP preamble sentences and "This tool is part of..." boilerplate.
    /// Converts "This tool creates..." → "Creates..."
    /// Replaces "MCP Server" with "Azure MCP CLI".
    /// Preserves ALL substantive content (return fields, behaviors, conditions).
    /// </summary>
    public static string AdaptNlpToCliVoice(string nlpDescription)
    {
        var desc = nlpDescription.Trim();

        // Remove "This tool is part of the Model Context Protocol (MCP) tool set." preamble
        desc = McpToolSetPreamble.Replace(desc, "").TrimStart();

        // Remove MCP preamble sentence if present
        var mcpMatch = McpToolPattern.Match(desc);
        if (mcpMatch.Success)
        {
            // Use ". " (period + space) to find sentence boundaries, avoiding version numbers like "v2.0"
            var periodIdx = desc.IndexOf(". ", mcpMatch.Index, StringComparison.Ordinal);
            if (periodIdx >= 0 && periodIdx < desc.Length - 2)
            {
                desc = desc[(periodIdx + 2)..].TrimStart();
            }
            else if (desc.Length > mcpMatch.Index && !desc[mcpMatch.Index..].Contains(". "))
            {
                // If no ". " found, the MCP preamble is the entire description
                desc = "";
            }
        }

        // Convert "This tool creates..." → "Creates..."
        desc = ThisToolPattern.Replace(desc, "");
        if (desc.Length > 0 && char.IsLower(desc[0]))
        {
            desc = char.ToUpper(desc[0]) + desc[1..];
        }

        // Replace "MCP Server" references with "Azure MCP CLI"
        desc = McpServerReference.Replace(desc, "Azure MCP CLI");

        return desc;
    }
}
