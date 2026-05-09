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
    /// Converts "This tool creates..." → "Creates..."
    /// Strips MCP preamble sentences.
    /// Replaces "MCP Server" with "Azure MCP CLI".
    /// </summary>
    public static string AdaptNlpToCliVoice(string nlpDescription)
    {
        var desc = nlpDescription.Trim();

        // Remove MCP preamble sentence if present
        var mcpMatch = McpToolPattern.Match(desc);
        if (mcpMatch.Success)
        {
            var periodIdx = desc.IndexOf('.', mcpMatch.Index);
            if (periodIdx >= 0 && periodIdx < desc.Length - 1)
            {
                desc = desc[(periodIdx + 1)..].TrimStart();
            }
        }

        // Convert "This tool creates..." → "Creates..."
        desc = ThisToolPattern.Replace(desc, "");
        if (desc.Length > 0 && char.IsLower(desc[0]))
        {
            desc = char.ToUpper(desc[0]) + desc[1..];
        }

        desc = desc.Replace("MCP Server", "Azure MCP CLI", StringComparison.OrdinalIgnoreCase);
        desc = desc.Replace("MCP server", "Azure MCP CLI", StringComparison.OrdinalIgnoreCase);

        return desc;
    }
}
