// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using GenerativeAI;
using Xunit;

namespace BrandMapperValidator.Tests;

/// <summary>
/// Keyless auth regression for the brand-mapping validator subprocess entry point. This tool
/// invokes AI generation (GenerativeAIClient) to suggest brand mappings, so it must follow the
/// same auth contract as the other AI entry points: keyless (DefaultAzureCredential) is the
/// intended, supported path, so <c>FOUNDRY_API_KEY</c> must NOT be required when
/// <c>FOUNDRY_USE_DEFAULT_CREDENTIAL</c> is enabled — only the endpoint and deployment are
/// required. When default credential is disabled, the API key stays required.
/// </summary>
public sealed class ValidateAIOptionsKeylessTests
{
    [Fact]
    public void ValidateAIOptions_DefaultCredentialWithoutApiKey_DoesNotRequireApiKey()
    {
        var options = new GenerativeAIOptions
        {
            UseDefaultCredential = true,
            Endpoint = "https://oai-brandmap-keyless.cognitiveservices.azure.com/",
            Deployment = "gpt-5-mini",
        };

        var missing = global::BrandMapperValidator.Program.ValidateAIOptions(options);

        Assert.DoesNotContain("FOUNDRY_API_KEY", missing);
        Assert.Empty(missing);
    }

    [Fact]
    public void ValidateAIOptions_NoDefaultCredentialAndNoApiKey_RequiresApiKey()
    {
        var options = new GenerativeAIOptions
        {
            UseDefaultCredential = false,
            Endpoint = "https://oai-brandmap-keyed.cognitiveservices.azure.com/",
            Deployment = "gpt-5-mini",
        };

        var missing = global::BrandMapperValidator.Program.ValidateAIOptions(options);

        Assert.Contains("FOUNDRY_API_KEY", missing);
    }
}
