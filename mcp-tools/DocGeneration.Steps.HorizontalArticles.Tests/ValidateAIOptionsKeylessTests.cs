// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using GenerativeAI;
using HorizontalArticleGenerator;
using HorizontalArticleGeneratorClass = HorizontalArticleGenerator.Generators.HorizontalArticleGenerator;
using Xunit;

namespace DocGeneration.Steps.HorizontalArticles.Tests;

/// <summary>
/// Keyless auth regression for the horizontal article subprocess entry point. Keyless
/// (DefaultAzureCredential) is the intended, supported auth path, so <c>FOUNDRY_API_KEY</c>
/// must NOT be required when <c>FOUNDRY_USE_DEFAULT_CREDENTIAL</c> is enabled — only the endpoint,
/// deployment, and API version are required. When default credential is disabled, the API key
/// stays required.
/// </summary>
[Trait("Category", "Keyless")]
public sealed class ValidateAIOptionsKeylessTests
{
    [Fact]
    public void Keyless_DefaultCredential_DoesNotRequireApiKey()
    {
        var options = new GenerativeAIOptions
        {
            UseDefaultCredential = true,
            Endpoint = "https://oai-ha-keyless.cognitiveservices.azure.com/",
            Deployment = "gpt-5-mini",
            ApiVersion = "2024-10-01-preview",
        };

        var missing = HorizontalArticleProgram.ValidateAIOptions(options);

        Assert.DoesNotContain("FOUNDRY_API_KEY", missing);
        Assert.Empty(missing);
    }

    [Fact]
    public void Keyless_DefaultCredential_ConstructsGeneratorWithoutApiKey()
    {
        var options = new GenerativeAIOptions
        {
            UseDefaultCredential = true,
            ApiKey = "",
            Endpoint = "https://oai-ha-keyless.cognitiveservices.azure.com/",
            Deployment = "gpt-5-mini",
            ApiVersion = "2024-10-01-preview",
        };

        var generator = new HorizontalArticleGeneratorClass(options);

        Assert.NotNull(generator);
    }

    [Fact]
    public void NonKeyless_MissingApiKey_ConstructorThrows()
    {
        var options = new GenerativeAIOptions
        {
            UseDefaultCredential = false,
            ApiKey = "",
            Endpoint = "https://oai-ha-keyed.cognitiveservices.azure.com/",
            Deployment = "gpt-5-mini",
            ApiVersion = "2024-10-01-preview",
        };

        var exception = Assert.Throws<InvalidOperationException>(() => new HorizontalArticleGeneratorClass(options));
        Assert.Contains("FOUNDRY_API_KEY", exception.Message);
    }

    [Fact]
    public void NonKeyless_MissingApiKey_RequiresApiKey()
    {
        var options = new GenerativeAIOptions
        {
            UseDefaultCredential = false,
            Endpoint = "https://oai-ha-keyed.cognitiveservices.azure.com/",
            Deployment = "gpt-5-mini",
            ApiVersion = "2024-10-01-preview",
        };

        var missing = HorizontalArticleProgram.ValidateAIOptions(options);

        Assert.Contains("FOUNDRY_API_KEY", missing);
    }
}
