using Azure;
using Azure.AI.OpenAI;

namespace ExamplePrompts;

public class ExamplePromptsClient
{
    private readonly OpenAIClient _client;
    private readonly ExamplePromptsOptions _options;

    public ExamplePromptsClient(ExamplePromptsOptions? options = null)
    {
        _options = options ?? ExamplePromptsOptions.LoadFromEnvironmentOrDotEnv();
        if (string.IsNullOrEmpty(_options.ApiKey) || string.IsNullOrEmpty(_options.Endpoint))
        {
            throw new InvalidOperationException("FOUNDRY_API_KEY and FOUNDRY_ENDPOINT must be configured");
        }

        _client = new OpenAIClient(new Uri(_options.Endpoint!), new AzureKeyCredential(_options.ApiKey));
    }

    public async Task<string> GenerateAsync(string systemPromptFile, string userPromptFile, CancellationToken ct = default)
    {
        var systemText = File.Exists(systemPromptFile) ? await File.ReadAllTextAsync(systemPromptFile, ct) : string.Empty;
        var userText = File.Exists(userPromptFile) ? await File.ReadAllTextAsync(userPromptFile, ct) : string.Empty;

        var chatOptions = new ChatCompletionsOptions();
        chatOptions.MaxTokens = 1024;
        chatOptions.Messages.Add(new ChatMessage(ChatRole.System, systemText));
        chatOptions.Messages.Add(new ChatMessage(ChatRole.User, userText));

        var deployment = _options.Instance ?? _options.Model ?? throw new InvalidOperationException("No model/deployment configured");

        var response = await _client.GetChatCompletionsAsync(deployment, chatOptions, ct: ct);
        var first = response.Value.Choices.FirstOrDefault();
        if (first == null) return string.Empty;
        return first.Message?.Content?.ToString() ?? string.Empty;
    }
}
