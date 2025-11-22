using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using Client = AzureOpenAIClient.AzureOpenAIClient;

namespace AzureOpenAIClient.Tests;

public class IntegrationTests
{
    private readonly ITestOutputHelper _output;

    public IntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task GetChatCompletion_WithPromptFiles_ReturnsText()
    {
        var opts = AzureOpenAIOptions.LoadFromEnvironmentOrDotEnv();
        
        _output.WriteLine($"Debug - ApiKey: {(string.IsNullOrEmpty(opts.ApiKey) ? "MISSING" : "SET")}");
        _output.WriteLine($"Debug - Endpoint: {opts.Endpoint ?? "MISSING"}");
        _output.WriteLine($"Debug - Deployment: {opts.Deployment ?? "MISSING"}");
        
        if (string.IsNullOrEmpty(opts.ApiKey) || string.IsNullOrEmpty(opts.Endpoint) || string.IsNullOrEmpty(opts.Deployment))
        {
            _output.WriteLine("⚠️  Skipping test - credentials not configured");
            return; // skip if not configured
        }

        // Read prompts from files (navigate to docs-generation/prompts)
        var promptsDir = Path.Combine("..", "..", "..", "..", "prompts");
        var systemPromptPath = Path.Combine(promptsDir, "system-prompt.txt");
        var userPromptPath = Path.Combine(promptsDir, "user-prompt.txt");

        Assert.True(File.Exists(systemPromptPath), $"System prompt file not found: {systemPromptPath}");
        Assert.True(File.Exists(userPromptPath), $"User prompt file not found: {userPromptPath}");

        var systemPrompt = File.ReadAllText(systemPromptPath).Trim();
        var userPrompt = File.ReadAllText(userPromptPath).Trim();

        _output.WriteLine("========================================");
        _output.WriteLine("SYSTEM PROMPT:");
        _output.WriteLine("========================================");
        _output.WriteLine(systemPrompt);
        _output.WriteLine("");
        _output.WriteLine("========================================");
        _output.WriteLine("USER PROMPT:");
        _output.WriteLine("========================================");
        _output.WriteLine(userPrompt);
        _output.WriteLine("");

        // Call Azure OpenAI
        var client = new Client(opts);
        var result = await client.GetChatCompletionAsync(systemPrompt, userPrompt, CancellationToken.None);

        _output.WriteLine("========================================");
        _output.WriteLine("AI RESPONSE:");
        _output.WriteLine("========================================");
        _output.WriteLine(result);
        _output.WriteLine("");

        Assert.False(string.IsNullOrWhiteSpace(result));
    }
}
