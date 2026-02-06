using Azure.AI.OpenAI;
using OpenAI.Chat;
using System.ClientModel;

namespace GenerativeAI;

public class GenerativeAIClient
{
    private readonly Azure.AI.OpenAI.AzureOpenAIClient _azureClient;
    private readonly ChatClient _chatClient;
    private readonly GenerativeAIOptions _opts;

    public GenerativeAIClient(GenerativeAIOptions? opts = null)
    {
        _opts = opts ?? GenerativeAIOptions.LoadFromEnvironmentOrDotEnv();
        if (string.IsNullOrEmpty(_opts.ApiKey) || string.IsNullOrEmpty(_opts.Endpoint) || string.IsNullOrEmpty(_opts.Deployment))
            throw new InvalidOperationException("Azure OpenAI configuration incomplete");

        _azureClient = new Azure.AI.OpenAI.AzureOpenAIClient(new Uri(_opts.Endpoint!), new ApiKeyCredential(_opts.ApiKey));
        _chatClient = _azureClient.GetChatClient(_opts.Deployment!);
    }

    public async Task<string> GetChatCompletionAsync(string systemPrompt, string userPrompt, int maxTokens = 8000, CancellationToken ct = default)
    {
        const int maxRetries = 5;
        int retryDelayMs = 1000; // Start with 1 second

        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                var messages = new ChatMessage[]
                {
                    new SystemChatMessage(systemPrompt),
                    new UserChatMessage(userPrompt)
                };

                var options = new ChatCompletionOptions
                {
                    MaxOutputTokenCount = maxTokens
                };

                var response = await _chatClient.CompleteChatAsync(messages, options, ct);

                // Check if completion was truncated due to token limit
                var finishReason = response.Value.FinishReason;
                if (finishReason == ChatFinishReason.Length)
                {
                    throw new InvalidOperationException(
                        $"LLM response was truncated due to token limit. " +
                        $"Used tokens: {response.Value.Usage?.TotalTokenCount ?? 0}, " +
                        $"Max output tokens: {maxTokens}. " +
                        $"Consider increasing maxTokens parameter.");
                }

                return response.Value.Content[0].Text ?? string.Empty;
            }
            catch (Exception ex) when (IsRateLimitError(ex) && attempt < maxRetries)
            {
                // Exponential backoff: 1s, 2s, 4s, 8s, 16s
                Console.WriteLine($"  ⏳ Rate limit hit, retrying in {retryDelayMs}ms (attempt {attempt + 1}/{maxRetries})");
                await Task.Delay(retryDelayMs, ct);
                retryDelayMs *= 2;
            }
        }

        // This should never be reached due to the retry logic, but satisfies compiler
        throw new InvalidOperationException("Failed to complete chat after all retry attempts");
    }

    private static bool IsRateLimitError(Exception ex)
    {
        // Check for 429 status code or rate limit messages
        return ex.Message.Contains("429") ||
               ex.Message.Contains("rate limit", StringComparison.OrdinalIgnoreCase) ||
               ex.Message.Contains("too many requests", StringComparison.OrdinalIgnoreCase) ||
               ex.Message.Contains("quota", StringComparison.OrdinalIgnoreCase);
    }
}
