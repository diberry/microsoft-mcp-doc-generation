// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using GenerativeAI;
using Xunit;

namespace DocGeneration.Steps.ToolFamilyCleanup.Tests;

/// <summary>
/// Regression for the missing-variable guidance emitted by the tool-family cleanup entry point.
/// The deployment/model name is loaded from the <c>FOUNDRY_MODEL_NAME</c> environment variable
/// (via <see cref="GenerativeAIOptions.LoadFromEnvironmentOrDotEnv"/>), so when it is missing the
/// error must tell the user to set <c>FOUNDRY_MODEL_NAME</c> — not the non-existent
/// <c>TOOL_FAMILY_CLEANUP_FOUNDRY_MODEL_NAME</c>, which the loader never reads.
/// </summary>
public sealed class ValidateAIOptionsModelNameTests
{
    [Fact]
    public void ValidateAIOptions_MissingDeployment_ReportsFoundryModelName()
    {
        var options = new GenerativeAIOptions
        {
            UseDefaultCredential = true,
            Endpoint = "https://oai-cosmos-cleanup.cognitiveservices.azure.com/",
            Deployment = null,
        };

        var missing = global::ToolFamilyCleanup.Program.ValidateAIOptions(options);

        Assert.Contains("FOUNDRY_MODEL_NAME", missing);
        Assert.DoesNotContain("TOOL_FAMILY_CLEANUP_FOUNDRY_MODEL_NAME", missing);
    }

    [Fact]
    public void ValidateAIOptions_DeploymentPresent_DoesNotReportModelName()
    {
        var options = new GenerativeAIOptions
        {
            UseDefaultCredential = true,
            Endpoint = "https://oai-keyvault-cleanup.cognitiveservices.azure.com/",
            Deployment = "gpt-4o",
        };

        var missing = global::ToolFamilyCleanup.Program.ValidateAIOptions(options);

        Assert.DoesNotContain("FOUNDRY_MODEL_NAME", missing);
    }
}
