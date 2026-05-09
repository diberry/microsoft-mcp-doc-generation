// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.RegularExpressions;
using Shared;

namespace GenerativeAI;

/// <summary>
/// Improves CLI prose fields (tool descriptions and switch descriptions) via AI.
/// AI only sees plain-text prose — never markdown, tables, commands, or code blocks.
/// Falls back to raw description on any validation failure.
/// </summary>
public class CliProseImprover
{
    private readonly GenerativeAIClient _aiClient;
    private readonly string _systemPrompt;

    public static readonly TimeSpan DefaultPerToolTimeout = TimeSpan.FromMinutes(2);

    private static readonly Regex MarkdownPattern = new(
        @"[#`\[\]*>]",
        RegexOptions.Compiled);

    public CliProseImprover(GenerativeAIClient aiClient, string systemPrompt)
    {
        _aiClient = aiClient ?? throw new ArgumentNullException(nameof(aiClient));
        _systemPrompt = systemPrompt ?? throw new ArgumentNullException(nameof(systemPrompt));
    }

    // Deterministic voice patterns: convert "This tool..." to imperative voice
    private static readonly Regex ThisToolPattern = new(
        @"^This\s+tool\s+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex McpToolPattern = new(
        @"^Model\s+Context\s+Protocol\s+\(MCP\)\s+tools?\s+let[s]?\s+you\s+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Aligns CLI tool descriptions with NLP descriptions using deterministic voice transformation.
    /// Takes the NLP description and converts to imperative voice without AI.
    /// Only tool_description is sent to the LLM for minimal prose cleanup.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, CliToolInfo>> ImproveProseAsync(
        IReadOnlyDictionary<string, CliToolInfo> cliTools,
        IReadOnlyDictionary<string, string>? nlpDescriptions = null,
        TimeSpan? perToolTimeout = null,
        int maxTokens = 2000,
        CancellationToken cancellationToken = default)
    {
        var timeout = perToolTimeout ?? DefaultPerToolTimeout;
        var result = new Dictionary<string, CliToolInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, tool) in cliTools)
        {
            // If NLP description available, use it as CLI description with deterministic voice change
            string? nlpDescription = null;
            nlpDescriptions?.TryGetValue(key, out nlpDescription);

            var toolToImprove = tool;
            if (!string.IsNullOrWhiteSpace(nlpDescription))
            {
                var cliDescription = AdaptNlpToCliVoice(nlpDescription);
                toolToImprove = tool with { Description = cliDescription };
            }

            // Send only tool_description to LLM for minimal prose cleanup (contract: tool desc only)
            result[key] = await ImproveToolAsync(toolToImprove, timeout, maxTokens, cancellationToken);
        }

        return result;
    }

    /// <summary>
    /// Deterministic NLP→CLI voice adaptation. No AI needed.
    /// Converts "This tool creates..." → "Creates..."
    /// Strips MCP preamble sentences.
    /// </summary>
    public static string AdaptNlpToCliVoice(string nlpDescription)
    {
        var desc = nlpDescription.Trim();

        // Remove MCP preamble sentence if present (e.g., "Model Context Protocol (MCP) tools let you run tasks...")
        var mcpMatch = McpToolPattern.Match(desc);
        if (mcpMatch.Success)
        {
            // Find the end of the MCP preamble sentence
            var periodIdx = desc.IndexOf('.', mcpMatch.Index);
            if (periodIdx >= 0 && periodIdx < desc.Length - 1)
            {
                desc = desc[(periodIdx + 1)..].TrimStart();
            }
        }

        // Convert "This tool creates..." → "Creates..."
        desc = ThisToolPattern.Replace(desc, "");
        // Capitalize first letter after stripping
        if (desc.Length > 0 && char.IsLower(desc[0]))
        {
            desc = char.ToUpper(desc[0]) + desc[1..];
        }

        // Replace "MCP" references with neutral phrasing
        desc = desc.Replace("MCP Server", "Azure MCP CLI", StringComparison.OrdinalIgnoreCase);
        desc = desc.Replace("MCP server", "Azure MCP CLI", StringComparison.OrdinalIgnoreCase);

        return desc;
    }

    private async Task<CliToolInfo> ImproveToolAsync(
        CliToolInfo tool,
        TimeSpan timeout,
        int maxTokens,
        CancellationToken cancellationToken)
    {
        try
        {
            using var timeoutCts = new CancellationTokenSource(timeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, timeoutCts.Token);

            // Contract: only send tool_description to LLM — minimal context
            var userPrompt = BuildUserPrompt(tool);
            var aiResponse = await _aiClient.GetChatCompletionAsync(
                _systemPrompt, userPrompt, maxTokens, linkedCts.Token);

            return ParseAndValidate(tool, aiResponse);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Console.WriteLine($"  ⚠ CLI prose improvement timed out for '{tool.Command}', using raw descriptions.");
            return tool;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw; // Caller cancelled — propagate
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ⚠ CLI prose improvement failed for '{tool.Command}': {ex.Message}. Using raw descriptions.");
            return tool;
        }
    }

    /// <summary>
    /// Builds minimal user prompt with only tool_description (per contract).
    /// </summary>
    private static string BuildUserPrompt(CliToolInfo tool)
    {
        var payload = new Dictionary<string, object>
        {
            ["tool_description"] = tool.Description
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = false });
    }

    private static CliToolInfo ParseAndValidate(CliToolInfo rawTool, string aiResponse)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(aiResponse);
        }
        catch (JsonException)
        {
            Console.WriteLine($"  ⚠ Malformed JSON from AI for '{rawTool.Command}', using raw descriptions.");
            return rawTool;
        }

        using (doc)
        {
            var root = doc.RootElement;

            // Validate and extract tool description
            var improvedDescription = rawTool.Description;
            if (root.TryGetProperty("tool_description", out var descEl))
            {
                var candidate = descEl.GetString() ?? "";
                if (ValidateProseField(candidate, rawTool.Description, "tool_description", rawTool.Command))
                    improvedDescription = candidate;
            }

            // Keep switch descriptions as-is (not sent to LLM per contract)
            return rawTool with { Description = improvedDescription };
        }
    }

    /// <summary>
    /// Validates an AI-improved prose field against invariants.
    /// </summary>
    private static bool ValidateProseField(string candidate, string original, string fieldName, string command)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            Console.WriteLine($"  ⚠ Empty AI response for {fieldName} in '{command}', using raw.");
            return false;
        }

        if (MarkdownPattern.IsMatch(candidate))
        {
            Console.WriteLine($"  ⚠ Markdown detected in AI response for {fieldName} in '{command}', using raw.");
            return false;
        }

        var originalLen = original.Length;
        if (originalLen > 0)
        {
            var ratio = (double)candidate.Length / originalLen;
            if (ratio < 0.3 || ratio > 3.0)
            {
                Console.WriteLine($"  ⚠ Length violation ({ratio:P0}) for {fieldName} in '{command}', using raw.");
                return false;
            }
        }

        return true;
    }
}
