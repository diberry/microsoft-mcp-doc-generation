using System.Net;
using System.Text;
using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

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

    private const int MaxRetries = 5;
    private static readonly int[] RetryDelaysMs = [1000, 2000, 4000, 8000, 16000];

    private async Task<string> CallLlmAsync(string systemPrompt, string userPrompt, CancellationToken ct)
    {
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(userPrompt)
        };

        var options = new ChatCompletionOptions
        {
            Temperature = 0.3f,
            MaxOutputTokenCount = 500
        };

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
