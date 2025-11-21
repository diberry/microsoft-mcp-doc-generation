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

        var credential = new AzureKeyCredential(_options.ApiKey);
        _client = new OpenAIClient(new Uri(_options.Endpoint!), credential);
    }

    public async Task<string> GenerateAsync(string systemPromptFile, string userPromptFile, CancellationToken ct = default)
    {
        var systemText = File.Exists(systemPromptFile) ? await File.ReadAllTextAsync(systemPromptFile, ct) : string.Empty;
        var userText = File.Exists(userPromptFile) ? await File.ReadAllTextAsync(userPromptFile, ct) : string.Empty;

        var messages = new List<ChatMessage>
        {
            new ChatMessage(ChatRole.System, systemText),
            new ChatMessage(ChatRole.User, userText)
        };

        var options = new ChatCompletionsOptions();
        foreach (var m in messages) options.Messages.Add(m);
        options.MaxTokens = 1024;

        var deploymentOrModel = _options.Instance ?? _options.Model ?? throw new InvalidOperationException("No model/deployment configured");

        Response<ChatCompletions> response = await _client.GetChatCompletionsAsync(deploymentOrModel, options, ct: ct);
        var first = response.Value.Choices.FirstOrDefault();
        if (first == null) return string.Empty;
        return first.Message.Content.ToString() ?? string.Empty;
    }
}
