using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ExamplePrompts;

public class ExamplePromptsClient
{
    private readonly HttpClient _http;
    private readonly ExamplePromptsOptions _options;

    public ExamplePromptsClient(ExamplePromptsOptions? options = null, HttpClient? httpClient = null)
    {
        _options = options ?? ExamplePromptsOptions.LoadFromEnvironmentOrDotEnv();
        _http = httpClient ?? new HttpClient();
        if (!string.IsNullOrEmpty(_options.ApiKey))
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        }
    }

    public async Task<string> GenerateAsync(string systemPromptFile, string userPromptFile, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_options.Endpoint)) throw new InvalidOperationException("FOUNDRY_ENDPOINT is not configured.");
        if (string.IsNullOrEmpty(_options.Model)) throw new InvalidOperationException("FOUNDRY_MODEL is not configured.");

        var systemText = File.Exists(systemPromptFile) ? await File.ReadAllTextAsync(systemPromptFile, ct) : string.Empty;
        var userText = File.Exists(userPromptFile) ? await File.ReadAllTextAsync(userPromptFile, ct) : string.Empty;

        var payload = new Dictionary<string, object?>
        {
            ["instance"] = _options.Instance,
            ["model"] = _options.Model,
            ["modelApiVersion"] = _options.ModelApiVersion,
            ["input"] = new[]
            {
                new { role = "system", content = systemText },
                new { role = "user", content = userText }
            }
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        var resp = await _http.PostAsync(_options.Endpoint!, content, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Request failed ({(int)resp.StatusCode}): {body}");
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("output", out var outputEl) && outputEl.ValueKind == JsonValueKind.Array && outputEl.GetArrayLength() > 0)
            {
                var first = outputEl[0];
                if (first.TryGetProperty("content", out var contentEl) && contentEl.ValueKind == JsonValueKind.Array && contentEl.GetArrayLength() > 0)
                {
                    var c0 = contentEl[0];
                    if (c0.TryGetProperty("text", out var textEl) && textEl.ValueKind == JsonValueKind.String)
                    {
                        return textEl.GetString() ?? body;
                    }
                }
            }

            if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array && choices.GetArrayLength() > 0)
            {
                var firstChoice = choices[0];
                if (firstChoice.TryGetProperty("message", out var message))
                {
                    if (message.TryGetProperty("content", out var contentMessage) && contentMessage.ValueKind == JsonValueKind.String)
                    {
                        return contentMessage.GetString() ?? body;
                    }
                }
                if (firstChoice.TryGetProperty("text", out var text))
                {
                    return text.GetString() ?? body;
                }
            }
        }
        catch
        {
        }

        return body;
    }
}
