using System;
using System.Collections.Generic;
using DocGeneration.Steps.ExamplePrompts.Validation;
using Xunit;

namespace DocGeneration.Steps.ExamplePrompts.Validation.Tests;

public class CodeBasedPromptValidatorTests
{
    private readonly CodeBasedPromptValidator _validator = new();

    [Fact]
    public void AllParamsCovered_ReturnsIsValidTrue()
    {
        var prompts = new[]
        {
            "List secrets in vault named 'my-vault' and show key named 'my-key'",
        };
        var requiredParams = new[] { "vault", "key" };

        var result = _validator.ValidatePrompts(prompts, requiredParams);

        Assert.True(result.IsValid);
        Assert.Equal(1, result.TotalPrompts);
        Assert.Equal(2, result.TotalRequiredParameters);
        Assert.All(result.Details, detail => Assert.True(
            detail.Covered || detail.PlaceholderDetected,
            $"Parameter '{detail.ParameterName}' should be covered or have placeholder"));
    }

    [Fact]
    public void MissingRequiredParam_ReturnsIsValidFalse()
    {
        var prompts = new[]
        {
            "List all virtual machines",
            "Show VM details",
        };
        var requiredParams = new[] { "resource-group" };

        var result = _validator.ValidatePrompts(prompts, requiredParams);

        Assert.False(result.IsValid);
        var detail = Assert.Single(result.Details);
        Assert.Equal("resource-group", detail.ParameterName);
        Assert.False(detail.Covered);
        Assert.False(detail.PlaceholderDetected);
    }

    [Fact]
    public void PlaceholderOnly_CoveredFalse_PlaceholderTrue()
    {
        var prompts = new[] { "Get secret from vault <vault-name>" };
        var requiredParams = new[] { "vault" };

        var result = _validator.ValidatePrompts(prompts, requiredParams);

        // Placeholder counts as "effectively covered" for IsValid
        Assert.True(result.IsValid);
        var detail = Assert.Single(result.Details);
        Assert.False(detail.Covered, "Placeholder is not concrete coverage");
        Assert.True(detail.PlaceholderDetected);
    }

    [Fact]
    public void EmptyRequiredParams_ReturnsIsValidTrue()
    {
        var prompts = new[] { "List all resources" };
        var requiredParams = Array.Empty<string>();

        var result = _validator.ValidatePrompts(prompts, requiredParams);

        Assert.True(result.IsValid);
        Assert.Equal(0, result.TotalRequiredParameters);
        Assert.Empty(result.Details);
    }
}
