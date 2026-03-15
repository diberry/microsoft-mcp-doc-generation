using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.AI;
using System.ClientModel;

namespace GenerativeAI;

public class GenerativeAIClient
{
    private const int MaxRetries = 5;
    private readonly IChatClient _chatClient;

    public GenerativeAIClient(GenerativeAIOptions? opts = null)
        : this(CreateChatClient(opts ?? GenerativeAIOptions.LoadFromEnvironmentOrDotEnv()))
    {
    }

    public GenerativeAIClient(IChatClient chatClient)
    {
        _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
    }

    public async Task<string> GetChatCompletionAsync(string systemPrompt, string userPrompt, int maxTokens = 8000, CancellationToken ct = default)
    {
        var messages = new[]
        {
            new ChatMessage(ChatRole.System, systemPrompt),
            new ChatMessage(ChatRole.User, userPrompt)
        };

        var options = new ChatOptions
        {
            MaxOutputTokens = maxTokens
        };

        var response = await _chatClient.GetResponseAsync(messages, options, ct);

        if (response.FinishReason == Microsoft.Extensions.AI.ChatFinishReason.Length)
        {
            throw new InvalidOperationException(
                $"LLM response was truncated due to token limit. " +
                $"Used tokens: {response.Usage?.TotalTokenCount ?? 0}, " +
                $"Max output tokens: {maxTokens}. " +
                $"Consider increasing maxTokens parameter.");
        }

        return response.Messages.FirstOrDefault()?.Text ?? string.Empty;
    }

    private static IChatClient CreateChatClient(GenerativeAIOptions opts)
    {
        ArgumentNullException.ThrowIfNull(opts);

        if (string.IsNullOrEmpty(opts.Endpoint) ||
            string.IsNullOrEmpty(opts.Deployment) ||
            (string.IsNullOrEmpty(opts.ApiKey) && !opts.UseDefaultCredential))
        {
            throw new InvalidOperationException("Azure OpenAI configuration incomplete");
        }

        var azureClient = string.IsNullOrWhiteSpace(opts.ApiKey)
            ? new AzureOpenAIClient(new Uri(opts.Endpoint!), new DefaultAzureCredential())
            : new AzureOpenAIClient(new Uri(opts.Endpoint!), new ApiKeyCredential(opts.ApiKey));

        var chatClient = azureClient.GetChatClient(opts.Deployment!).AsIChatClient();

        return new ChatClientBuilder(chatClient)
            .Use(
                static (messages, options, next, cancellationToken) =>
                    ExecuteWithRetryAsync(() => next.GetResponseAsync(messages, options, cancellationToken), cancellationToken),
                getStreamingResponseFunc: null)
            .Build();
    }

    private static async Task<ChatResponse> ExecuteWithRetryAsync(Func<Task<ChatResponse>> operation, CancellationToken ct)
    {
        int retryDelayMs = 1000;

        for (int attempt = 0; ; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex) when (IsRateLimitError(ex) && attempt < MaxRetries)
            {
                Console.WriteLine($"  ⏳ Rate limit hit, retrying in {retryDelayMs}ms (attempt {attempt + 1}/{MaxRetries})");
                await Task.Delay(retryDelayMs, ct);
                retryDelayMs *= 2;
            }
        }
    }

    private static bool IsRateLimitError(Exception ex)
    {
        return ex.Message.Contains("429") ||
               ex.Message.Contains("rate limit", StringComparison.OrdinalIgnoreCase) ||
               ex.Message.Contains("too many requests", StringComparison.OrdinalIgnoreCase) ||
               ex.Message.Contains("quota", StringComparison.OrdinalIgnoreCase);
    }
}
