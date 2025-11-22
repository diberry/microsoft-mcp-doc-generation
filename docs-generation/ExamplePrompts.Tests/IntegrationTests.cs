using System.Threading.Tasks;
using System;
using System.IO;
using System.Threading;
using Xunit;

namespace ExamplePrompts.Tests;

public class IntegrationTests
{
    [Fact(Skip = "Integration test - enable and set env vars to run")]
    public async Task GenerateAsync_ReturnsText_WhenConfigured()
    {
        var opts = ExamplePrompts.ExamplePromptsOptions.LoadFromEnvironmentOrDotEnv();
        if (string.IsNullOrEmpty(opts.ApiKey) || string.IsNullOrEmpty(opts.Endpoint) || string.IsNullOrEmpty(opts.Model))
        {
            // Skip test if not configured
            return;
        }

        var client = new ExamplePrompts.ExamplePromptsClient(opts);

        // Create temporary prompt files
        var systemFile = Path.GetTempFileName();
        var userFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(systemFile, "You are a helpful assistant.");
        await File.WriteAllTextAsync(userFile, "Say hello in one sentence.");

        var result = await client.GenerateAsync(systemFile, userFile, CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(result));

        File.Delete(systemFile);
        File.Delete(userFile);
    }
}
