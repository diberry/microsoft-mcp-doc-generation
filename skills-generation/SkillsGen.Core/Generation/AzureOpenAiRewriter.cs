using System.Diagnostics;
using System.Text.Json;
using Azure.AI.OpenAI;
using Azure.Identity;
using DocGeneration.Core.Tracing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using SkillsGen.Core.Models;

namespace SkillsGen.Core.Generation;

public class AzureOpenAiRewriter : ILlmRewriter
{
    private readonly IChatClient _chatClient;
    private readonly ILogger<AzureOpenAiRewriter> _logger;
    private readonly IPipelineTracer _tracer;
    private readonly string _modelName;
    private readonly string _systemPromptIntro;
    private readonly string _userPromptIntroTemplate;
    private readonly string _systemPromptKnowledge;

    /// <summary>
    /// Primary constructor. Takes an <see cref="IChatClient"/> (Microsoft.Extensions.AI)
    /// so the rewriter is decoupled from the concrete SDK and credential type, and is unit-testable.
    /// </summary>
    public AzureOpenAiRewriter(
        IChatClient chatClient,
        string modelName,
        string systemPromptIntro,
        string userPromptIntroTemplate,
        string? acrolinxRules,
        ILogger<AzureOpenAiRewriter> logger,
        IPipelineTracer? tracer = null)
    {
        _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
        _logger = logger;
        _tracer = tracer ?? NullTracer.Instance;
        _modelName = modelName;

        // Replace {{ACROLINX_RULES}} placeholder with actual rules
        _systemPromptIntro = systemPromptIntro.Replace("{{ACROLINX_RULES}}", acrolinxRules ?? "");
        _userPromptIntroTemplate = userPromptIntroTemplate;
        _systemPromptKnowledge = _systemPromptIntro; // Same system prompt for both (knowledge reuse)
    }

    /// <summary>
    /// Builds a rewriter using keyless authentication only.
    /// This repository NEVER uses API keys — auth is always managed identity /
    /// <see cref="DefaultAzureCredential"/>. The chat client is created via
    /// Microsoft.Extensions.AI (<c>AsIChatClient</c>), which also avoids the
    /// Azure.AI.OpenAI/OpenAI binary mismatch that breaks the raw ChatClient path.
    /// </summary>
    public static AzureOpenAiRewriter CreateKeyless(
        string endpoint,
        string modelName,
        string systemPromptIntro,
        string userPromptIntroTemplate,
        string? acrolinxRules,
        ILogger<AzureOpenAiRewriter> logger,
        IPipelineTracer? tracer = null)
    {
        var azureClient = new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential());
        IChatClient chatClient = azureClient.GetChatClient(modelName).AsIChatClient();
        return new AzureOpenAiRewriter(
            chatClient, modelName, systemPromptIntro, userPromptIntroTemplate, acrolinxRules, logger, tracer);
    }

    public async Task<string> RewriteIntroAsync(string skillName, string rawDescription, CancellationToken ct = default)
    {
        var userPrompt = _userPromptIntroTemplate
            .Replace("{{skillName}}", skillName)
            .Replace("{{description}}", rawDescription);

        return await CallLlmAsync(skillName, "rewrite-intro", _systemPromptIntro, userPrompt, ct);
    }

    public async Task<string> GenerateKnowledgeOverviewAsync(string skillName, string rawBody, CancellationToken ct = default)
    {
        var userPrompt = $"Write a 2-3 sentence knowledge overview for the Azure Skill \"{skillName}\".\n\nSource content:\n{rawBody}";
        return await CallLlmAsync(skillName, "generate-knowledge-overview", _systemPromptKnowledge, userPrompt, ct);
    }

    public async Task<string?> SynthesizeWhatItProvidesAsync(string skillName, SkillData skillData, CancellationToken ct = default)
    {
        var serviceNames = skillData.Services.Count > 0
            ? string.Join(", ", skillData.Services.Select(s => s.Name))
            : "N/A";

        // §7.9: Only customer-facing data — no MCP tool names or workflow steps (§4.2)
        var useCases = skillData.UseFor.Count > 0
            ? string.Join("\n", skillData.UseFor.Select(u => $"  - {u}"))
            : "N/A";

        var systemPrompt = "You are a technical writer for Microsoft Azure documentation. Write clear, customer-facing content.";
        var userPrompt = $"""
            Write a 2-3 sentence "What it provides" summary for the Azure skill "{skillName}".

            Context:
            - Description: {skillData.Description}
            - Services: {serviceNames}
            - Use cases:
            {useCases}

            Rules:
            - Write from the customer's perspective (what THEY get, not what the system does)
            - Avoid engineering jargon like "entry point", "discovery", "mcp"
            - Be specific about capabilities, not generic
            - 2-3 sentences max
            - Do NOT use markdown formatting
            """;

        try
        {
            return await CallLlmAsync(skillName, "synthesize-what-it-provides", systemPrompt, userPrompt, ct);
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
            var response = await CallLlmAsync(skillName, "translate-workflow-steps", systemPrompt, userPrompt, ct);
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

    private async Task<string> CallLlmAsync(string skillName, string operationName, string systemPrompt, string userPrompt, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, userPrompt)
        };

        var options = new ChatOptions
        {
            MaxOutputTokens = MaxOutputTokensFor(_modelName)
        };

        // gpt-5 / o-series reasoning models only support the default temperature (1)
        // and reject an explicit value (HTTP 400 unsupported_value). For all other
        // models we pin a low temperature for deterministic, consistent rewrites.
        if (SupportsCustomTemperature(_modelName))
            options.Temperature = 0.3f;

        for (int attempt = 0; attempt <= MaxRetries; attempt++)
        {
            try
            {
                _logger.LogDebug("Calling Azure OpenAI with {PromptLength} char prompt (attempt {Attempt})", userPrompt.Length, attempt + 1);
                var response = await _chatClient.GetResponseAsync(messages, options, ct);

                var result = response.Messages.FirstOrDefault()?.Text ?? string.Empty;
                if (string.IsNullOrEmpty(result))
                    throw new InvalidOperationException("Azure OpenAI returned empty content");

                _logger.LogDebug("Received {ResponseLength} char response", result.Length);

                _tracer.RecordAiCall(new AiInteractionRecord
                {
                    SkillOrToolName = skillName,
                    Operation = operationName,
                    SystemPrompt = systemPrompt,
                    UserPrompt = userPrompt,
                    ResponseContent = result,
                    Model = _modelName,
                    TotalTokens = response.Usage?.TotalTokenCount is long totalTokenCount ? (int?)totalTokenCount : null,
                    DurationMs = sw.ElapsedMilliseconds,
                    RetryCount = attempt
                });

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

    /// <summary>
    /// Returns false for model families that only support the default temperature (1)
    /// and reject an explicit value — currently the gpt-5 family and the o-series
    /// reasoning models (o1/o3/o4). Returns true for all other models.
    /// </summary>
    internal static bool SupportsCustomTemperature(string? modelName)
    {
        if (string.IsNullOrWhiteSpace(modelName))
            return true;

        var m = modelName.Trim().ToLowerInvariant();
        return !(m.StartsWith("gpt-5")
            || m.StartsWith("o1")
            || m.StartsWith("o3")
            || m.StartsWith("o4"));
    }

    /// <summary>
    /// Returns the output-token budget for a model. Reasoning models (gpt-5 family,
    /// o-series) spend tokens on internal reasoning before producing visible output,
    /// so a small cap leaves no room for the answer and yields empty content. Those
    /// families get a larger budget; all other models keep the lean default.
    /// </summary>
    internal static int MaxOutputTokensFor(string? modelName)
        => SupportsCustomTemperature(modelName) ? 500 : 4000;

    private static bool IsRateLimitError(Exception ex)
    {        if (ex is Azure.RequestFailedException rfe && rfe.Status == 429)
            return true;

        var message = ex.Message;
        return message.Contains("rate limit", StringComparison.OrdinalIgnoreCase)
            || message.Contains("too many requests", StringComparison.OrdinalIgnoreCase)
            || message.Contains("429", StringComparison.Ordinal)
            || message.Contains("quota", StringComparison.OrdinalIgnoreCase);
    }
}
