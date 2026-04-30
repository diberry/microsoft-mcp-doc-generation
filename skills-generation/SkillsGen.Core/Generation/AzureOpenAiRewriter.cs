using System.Net;
using System.Text;
using System.Text.Json;
using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using SkillsGen.Core.Models;

namespace SkillsGen.Core.Generation;

public class AzureOpenAiRewriter : ILlmRewriter
{
    private readonly ChatClient _chatClient;
    private readonly ILogger<AzureOpenAiRewriter> _logger;
    private readonly string _systemPromptIntro;
    private readonly string _userPromptIntroTemplate;
    private readonly string _systemPromptKnowledge;

    public AzureOpenAiRewriter(
        string endpoint,
        string apiKey,
        string modelName,
        string systemPromptIntro,
        string userPromptIntroTemplate,
        string? acrolinxRules,
        ILogger<AzureOpenAiRewriter> logger)
    {
        _logger = logger;

        var client = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
        _chatClient = client.GetChatClient(modelName);

        // Replace {{ACROLINX_RULES}} placeholder with actual rules
        _systemPromptIntro = systemPromptIntro.Replace("{{ACROLINX_RULES}}", acrolinxRules ?? "");
        _userPromptIntroTemplate = userPromptIntroTemplate;
        _systemPromptKnowledge = _systemPromptIntro; // Same system prompt for both (knowledge reuse)
    }

    public async Task<string> RewriteIntroAsync(string skillName, string rawDescription, CancellationToken ct = default)
    {
        var userPrompt = _userPromptIntroTemplate
            .Replace("{{skillName}}", skillName)
            .Replace("{{description}}", rawDescription);

        return await CallLlmAsync(_systemPromptIntro, userPrompt, ct);
    }

    public async Task<string> GenerateKnowledgeOverviewAsync(string skillName, string rawBody, CancellationToken ct = default)
    {
        var userPrompt = $"Write a 2-3 sentence knowledge overview for the Azure Skill \"{skillName}\".\n\nSource content:\n{rawBody}";
        return await CallLlmAsync(_systemPromptKnowledge, userPrompt, ct);
    }

    public async Task<string?> SynthesizeWhatItProvidesAsync(string skillName, SkillData skillData, CancellationToken ct = default)
    {
        var serviceNames = skillData.Services.Count > 0
            ? string.Join(", ", skillData.Services.Select(s => s.Name))
            : "N/A";

        var toolPurposes = skillData.McpTools.Count > 0
            ? string.Join("\n", skillData.McpTools
                .Where(t => !string.IsNullOrWhiteSpace(t.Purpose))
                .Select(t => $"  - {t.ToolName}: {t.Purpose}"))
            : "N/A";

        var useCases = skillData.UseFor.Count > 0
            ? string.Join("\n", skillData.UseFor.Select(u => $"  - {u}"))
            : "N/A";

        var workflowSteps = skillData.WorkflowSteps.Count > 0
            ? string.Join("\n", skillData.WorkflowSteps.Select((s, i) => $"  {i + 1}. {s}"))
            : "N/A";

        var systemPrompt = "You are a technical writer for Microsoft Azure documentation. Write clear, customer-facing content.";
        var userPrompt = $"""
            Write a 2-3 sentence "What it provides" summary for the Azure skill "{skillName}".

            Context:
            - Description: {skillData.Description}
            - Services: {serviceNames}
            - Tools:
            {toolPurposes}
            - Use cases:
            {useCases}
            - Workflow steps:
            {workflowSteps}

            Rules:
            - Write from the customer's perspective (what THEY get, not what the system does)
            - Avoid engineering jargon like "entry point", "discovery", "mcp"
            - Be specific about capabilities, not generic
            - 2-3 sentences max
            - Do NOT use markdown formatting
            """;

        try
        {
            return await CallLlmAsync(systemPrompt, userPrompt, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SynthesizeWhatItProvides failed for {SkillName}, falling back to mechanical", skillName);
            return null;
        }
    }

    public async Task<List<string>> TranslateWorkflowStepsAsync(string skillName, List<string> rawSteps, List<McpToolEntry> tools, CancellationToken ct = default)
    {
        if (rawSteps.Count == 0)
            return rawSteps;

        var numberedSteps = string.Join("\n", rawSteps.Select((s, i) => $"  {i + 1}. {s}"));

        var toolContext = tools.Count > 0
            ? string.Join("\n", tools.Select(t => $"  - {t.ToolName}: {t.Purpose}"))
            : "  (none)";

        var systemPrompt = "You are a technical writer translating internal agent workflow instructions into customer-facing guidance for Microsoft Azure documentation.";
        var userPrompt = $"""
            Translate these workflow steps for the "{skillName}" skill into clear customer guidance.

            Raw steps (agent instructions):
            {numberedSteps}

            Available MCP tools in this skill:
            {toolContext}

            Rules:
            - Rewrite each step from the customer's perspective ("You can..." not "The agent will...")
            - Replace engineering jargon: "mcp entry point" → "starting point", "discovery" → "explore available tools"
            - Keep the same number of steps in the same order
            - Each step should be 1-2 sentences, actionable
            - Do NOT add markdown formatting within steps
            - Return ONLY a JSON array of strings (one per step), no other text
            """;

        try
        {
            var response = await CallLlmAsync(systemPrompt, userPrompt, ct);
            return ParseWorkflowStepsResponse(response, rawSteps);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "TranslateWorkflowSteps failed for {SkillName}, falling back to raw steps", skillName);
            return rawSteps;
        }
    }

    internal static List<string> ParseWorkflowStepsResponse(string response, List<string> fallback)
    {
        // Try to extract JSON array from the response
        var text = response.Trim();

        // Handle ```json code blocks
        if (text.StartsWith("```"))
        {
            var startIdx = text.IndexOf('\n');
            var endIdx = text.LastIndexOf("```");
            if (startIdx >= 0 && endIdx > startIdx)
                text = text[(startIdx + 1)..endIdx].Trim();
        }

        // Find first [ and last ] for raw JSON extraction
        var bracketStart = text.IndexOf('[');
        var bracketEnd = text.LastIndexOf(']');
        if (bracketStart >= 0 && bracketEnd > bracketStart)
            text = text[bracketStart..(bracketEnd + 1)];

        try
        {
            var parsed = JsonSerializer.Deserialize<List<string>>(text);
            if (parsed != null && parsed.Count > 0)
                return parsed;
        }
        catch (JsonException)
        {
            // Fall through to fallback
        }

        return fallback;
    }

    private const int MaxRetries = 5;
    private static readonly int[] RetryDelaysMs = [1000, 2000, 4000, 8000, 16000];

    private async Task<string> CallLlmAsync(string systemPrompt, string userPrompt, CancellationToken ct)
    {
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(userPrompt)
        };

        var options = new ChatCompletionOptions();

        for (int attempt = 0; attempt <= MaxRetries; attempt++)
        {
            try
            {
                _logger.LogDebug("Calling Azure OpenAI with {PromptLength} char prompt (attempt {Attempt})", userPrompt.Length, attempt + 1);
                var response = await _chatClient.CompleteChatAsync(messages, options, ct);

                if (response.Value.Content.Count == 0)
                    throw new InvalidOperationException("Azure OpenAI returned empty content");

                var result = response.Value.Content[0].Text;
                _logger.LogDebug("Received {ResponseLength} char response", result.Length);

                return result;
            }
            catch (Exception ex) when (attempt < MaxRetries && IsRateLimitError(ex))
            {
                var delay = RetryDelaysMs[attempt];
                _logger.LogWarning("Rate limit hit on attempt {Attempt}/{MaxRetries}, retrying in {Delay}ms: {Message}",
                    attempt + 1, MaxRetries + 1, delay, ex.Message);
                await Task.Delay(delay, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LLM call failed (attempt {Attempt})", attempt + 1);
                throw;
            }
        }

        // Unreachable — final attempt throws in the catch above
        throw new InvalidOperationException("Exhausted all retry attempts");
    }

    private static bool IsRateLimitError(Exception ex)
    {
        if (ex is Azure.RequestFailedException rfe && rfe.Status == 429)
            return true;

        var message = ex.Message;
        return message.Contains("rate limit", StringComparison.OrdinalIgnoreCase)
            || message.Contains("too many requests", StringComparison.OrdinalIgnoreCase)
            || message.Contains("429", StringComparison.Ordinal)
            || message.Contains("quota", StringComparison.OrdinalIgnoreCase);
    }
}
