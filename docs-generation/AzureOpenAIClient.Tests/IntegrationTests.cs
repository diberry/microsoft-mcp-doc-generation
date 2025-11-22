using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Client = AzureOpenAIClient.AzureOpenAIClient;

namespace AzureOpenAIClient.Tests;

public class IntegrationTests
{
    [Fact(Skip = "Integration test - enable and set env vars to run")]
    public async Task GetChatCompletion_ReturnsText_WhenConfigured()
    {
        var opts = AzureOpenAIOptions.LoadFromEnvironmentOrDotEnv();
        if (string.IsNullOrEmpty(opts.ApiKey) || string.IsNullOrEmpty(opts.Endpoint) || string.IsNullOrEmpty(opts.Deployment))
            return; // skip if not configured

        var client = new Client(opts);
        var result = await client.GetChatCompletionAsync("You are a helpful assistant.", "Say hello in one sentence.", CancellationToken.None);
        Assert.False(string.IsNullOrWhiteSpace(result));
    }
}
