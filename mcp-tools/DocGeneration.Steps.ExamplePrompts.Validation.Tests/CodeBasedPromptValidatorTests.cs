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

    // --- Enum-aware coverage (required parameter whose description enumerates a closed option set) ---

    [Fact]
    public void EnumParam_PromptReferencesAllowedValue_IsValidTrue()
    {
        // Advisor 'recommendation apply --resource' is a required enum of resource TYPES.
        // The e2e prompt references the "Storage Account" resource type (allowed value
        // 'storage_storageaccounts') without ever using the word "resource".
        var prompts = new[]
        {
            "Apply the recommended configuration for my Storage Account",
        };
        var requiredParams = new[] { "resource" };
        var descriptions = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["resource"] = "The resource type. Available options: 'aad_domainservices', 'storage_storageaccounts', 'sql_servers'.",
        };

        var result = _validator.ValidatePrompts(prompts, requiredParams, descriptions);

        Assert.True(result.IsValid);
        var detail = Assert.Single(result.Details);
        Assert.True(detail.Covered);
    }

    [Fact]
    public void EnumParam_NoDescription_StaysUncovered()
    {
        // Without enum description threading, the same prompt cannot cover 'resource'.
        var prompts = new[]
        {
            "Apply the recommended configuration for my Storage Account",
        };
        var requiredParams = new[] { "resource" };

        var result = _validator.ValidatePrompts(prompts, requiredParams);

        Assert.False(result.IsValid);
        var detail = Assert.Single(result.Details);
        Assert.False(detail.Covered);
    }

    [Fact]
    public void EnumParam_PromptDoesNotReferenceAllowedValue_StaysUncovered()
    {
        var prompts = new[]
        {
            "Apply the recommended configuration for my web app",
        };
        var requiredParams = new[] { "resource" };
        var descriptions = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["resource"] = "The resource type. Available options: 'storage_storageaccounts', 'sql_servers', 'cosmosdb_databaseaccounts'.",
        };

        var result = _validator.ValidatePrompts(prompts, requiredParams, descriptions);

        Assert.False(result.IsValid);
        var detail = Assert.Single(result.Details);
        Assert.False(detail.Covered);
    }
}
