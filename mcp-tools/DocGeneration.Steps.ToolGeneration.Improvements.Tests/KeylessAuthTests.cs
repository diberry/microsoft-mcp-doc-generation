// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using GenerativeAI;
using ToolGeneration_Improved.Services;
using Xunit;

namespace DocGeneration.Steps.ToolGeneration.Improvements.Tests;

[Trait("Category", "Keyless")]
public sealed class KeylessAuthTests
{
    [Fact]
    public void ValidateAIOptions_DefaultCredentialWithoutApiKey_DoesNotRequireApiKey()
    {
        var options = CreateKeylessOptions();

        var missing = ToolGeneration_Improved.Program.ValidateAIOptions(options);

        Assert.DoesNotContain("FOUNDRY_API_KEY", missing);
        Assert.Empty(missing);
    }

    [Fact]
    public void ImprovedToolGeneratorService_DefaultCredentialWithoutApiKey_Constructs()
    {
        var options = CreateKeylessOptions();
        var client = new GenerativeAIClient(options);

        var service = new ImprovedToolGeneratorService(client, "system", "user");

        Assert.NotNull(service);
    }

    [Fact]
    public void ValidateAIOptions_NoDefaultCredentialAndNoApiKey_RequiresApiKey()
    {
        var options = CreateKeylessOptions();
        options.UseDefaultCredential = false;

        var missing = ToolGeneration_Improved.Program.ValidateAIOptions(options);

        Assert.Contains("FOUNDRY_API_KEY", missing);
    }

    private static GenerativeAIOptions CreateKeylessOptions()
        => new()
        {
            UseDefaultCredential = true,
            ApiKey = "",
            Endpoint = "https://oai-tool-generation-keyless.cognitiveservices.azure.com/",
            Deployment = "gpt-5-mini",
            ApiVersion = "2024-10-01-preview",
        };
}
