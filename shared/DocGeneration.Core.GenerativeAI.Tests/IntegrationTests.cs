using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using Client = GenerativeAI.GenerativeAIClient;

namespace GenerativeAI.Tests;

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
        var opts = GenerativeAIOptions.LoadFromEnvironmentOrDotEnv();
        
        _output.WriteLine($"Debug - ApiKey: {(string.IsNullOrEmpty(opts.ApiKey) ? "MISSING" : "SET")}");
        _output.WriteLine($"Debug - Endpoint: {opts.Endpoint ?? "MISSING"}");
        _output.WriteLine($"Debug - Deployment: {opts.Deployment ?? "MISSING"}");
        
        if (string.IsNullOrEmpty(opts.ApiKey) || string.IsNullOrEmpty(opts.Endpoint) || string.IsNullOrEmpty(opts.Deployment))
        {
            _output.WriteLine("⚠️  Skipping test - credentials not configured");
            return; // skip if not configured
        }

        // Use inline prompts for integration testing (legacy prompt files removed in #295)
        var systemPrompt = "You are a helpful assistant that generates concise documentation.";
        var userPrompt = "Describe Azure Storage in one sentence.";

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
        var result = await client.GetChatCompletionAsync(systemPrompt, userPrompt, 8000, CancellationToken.None);

        _output.WriteLine("========================================");
        _output.WriteLine("AI RESPONSE:");
        _output.WriteLine("========================================");
        _output.WriteLine(result);
        _output.WriteLine("");

        Assert.False(string.IsNullOrWhiteSpace(result));
    }
}
