// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.RegularExpressions;
using GenerativeAI;
using Shared;

namespace ToolGeneration_Improved.Services;

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

    /// <summary>
    /// Improves CLI prose fields (tool descriptions and switch descriptions) via AI.
    /// Returns a new dictionary with improved CliToolInfo records.
    /// Falls back to raw description on validation failure.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, CliToolInfo>> ImproveProseAsync(
        IReadOnlyDictionary<string, CliToolInfo> cliTools,
        TimeSpan? perToolTimeout = null,
        int maxTokens = 2000,
        CancellationToken cancellationToken = default)
    {
        var timeout = perToolTimeout ?? DefaultPerToolTimeout;
        var result = new Dictionary<string, CliToolInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, tool) in cliTools)
        {
            result[key] = await ImproveToolAsync(tool, timeout, maxTokens, cancellationToken);
        }

        return result;
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

    private static string BuildUserPrompt(CliToolInfo tool)
    {
        var switchDescs = new Dictionary<string, string>();
        foreach (var sw in tool.Switches)
            switchDescs[sw.Name] = sw.Description;

        var payload = new Dictionary<string, object>
        {
            ["tool_description"] = tool.Description,
            ["switch_descriptions"] = switchDescs
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

            // Validate and extract switch descriptions
            var improvedSwitches = new List<CliSwitch>(rawTool.Switches.Count);
            if (root.TryGetProperty("switch_descriptions", out var switchEl) &&
                switchEl.ValueKind == JsonValueKind.Object)
            {
                foreach (var sw in rawTool.Switches)
                {
                    if (switchEl.TryGetProperty(sw.Name, out var switchDescEl))
                    {
                        var candidate = switchDescEl.GetString() ?? "";
                        if (ValidateProseField(candidate, sw.Description, sw.Name, rawTool.Command))
                            improvedSwitches.Add(sw with { Description = candidate });
                        else
                            improvedSwitches.Add(sw);
                    }
                    else
                    {
                        // Switch name missing from AI response — keep raw
                        improvedSwitches.Add(sw);
                    }
                }
            }
            else
            {
                // No switch_descriptions in response — keep all raw
                improvedSwitches.AddRange(rawTool.Switches);
            }

            return rawTool with
            {
                Description = improvedDescription,
                Switches = improvedSwitches
            };
        }
    }

    /// <summary>
    /// Validates an AI-improved prose field against invariants.
    /// Returns true if valid, false if the raw value should be used instead.
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
            if (ratio < 0.5 || ratio > 2.0)
            {
                Console.WriteLine($"  ⚠ Length violation ({ratio:P0}) for {fieldName} in '{command}', using raw.");
                return false;
            }
        }

        return true;
    }
}
