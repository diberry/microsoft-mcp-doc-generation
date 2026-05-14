// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Shared;
using Xunit;

namespace DocGeneration.Steps.ToolFamilyCleanup.Tests;

public class DescriptionAlignmentValidatorTests
{
    [Fact]
    public void Validate_IdenticalDescriptions_ReturnsValid()
    {
        var mcp = new Dictionary<string, string> { ["storage account list"] = "Lists all storage accounts in a subscription." };
        var cli = new Dictionary<string, string> { ["storage account list"] = "Lists all storage accounts in a subscription." };

        var result = DescriptionAlignmentValidator.Validate(mcp, cli);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Validate_HighSimilarity_NoWarnings()
    {
        var mcp = new Dictionary<string, string>
        {
            ["storage account list"] = "This tool retrieves all storage accounts in the current Azure subscription, including account name and location."
        };
        var cli = new Dictionary<string, string>
        {
            ["storage account list"] = "Retrieves all storage accounts in the current Azure subscription, including account name and location."
        };

        var result = DescriptionAlignmentValidator.Validate(mcp, cli);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Validate_ModerateDivergence_EmitsWarning()
    {
        var mcp = new Dictionary<string, string>
        {
            ["keyvault secret show"] = "Retrieves secret value, version history, and expiration metadata from Azure Key Vault."
        };
        var cli = new Dictionary<string, string>
        {
            ["keyvault secret show"] = "Gets a secret from the vault."
        };

        var result = DescriptionAlignmentValidator.Validate(mcp, cli);

        // The CLI description lost most of the content
        Assert.True(result.Warnings.Count > 0 || result.Errors.Count > 0);
    }

    [Fact]
    public void Validate_SevereDivergence_EmitsError()
    {
        var mcp = new Dictionary<string, string>
        {
            ["compute vm create"] = "Creates a virtual machine with specified configuration including OS disk, network interface, and availability set."
        };
        var cli = new Dictionary<string, string>
        {
            ["compute vm create"] = "Deploys cloud resources."
        };

        var result = DescriptionAlignmentValidator.Validate(mcp, cli);

        Assert.False(result.IsValid);
        Assert.True(result.Errors.Count > 0);
    }

    [Fact]
    public void Validate_EmptyDescriptions_Skipped()
    {
        var mcp = new Dictionary<string, string> { ["tool1"] = "" };
        var cli = new Dictionary<string, string> { ["tool1"] = "Some CLI content." };

        var result = DescriptionAlignmentValidator.Validate(mcp, cli);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Validate_MissingCliKey_Skipped()
    {
        var mcp = new Dictionary<string, string> { ["tool1"] = "MCP description." };
        var cli = new Dictionary<string, string>();

        var result = DescriptionAlignmentValidator.Validate(mcp, cli);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ComputeWordOverlapSimilarity_IdenticalTexts_Returns1()
    {
        var score = DescriptionAlignmentValidator.ComputeWordOverlapSimilarity(
            "Creates a storage account in the specified region.",
            "Creates a storage account in the specified region.");

        Assert.Equal(1.0, score);
    }

    [Fact]
    public void ComputeWordOverlapSimilarity_CompletelyDifferent_ReturnsLow()
    {
        var score = DescriptionAlignmentValidator.ComputeWordOverlapSimilarity(
            "Creates virtual machine configurations with network interfaces.",
            "Deploys cloud database backup schedules automatically.");

        Assert.True(score < 0.3);
    }

    [Fact]
    public void ComputeWordOverlapSimilarity_IgnoresStopWords()
    {
        // These differ only in stop words — significant words are the same
        var score = DescriptionAlignmentValidator.ComputeWordOverlapSimilarity(
            "Lists the storage accounts",
            "Lists storage accounts in the subscription");

        // "subscription" is extra in second, so not perfect, but still high
        Assert.True(score > 0.5);
    }

    [Fact]
    public void Validate_MultipleTools_ReportsPerTool()
    {
        var mcp = new Dictionary<string, string>
        {
            ["tool1"] = "Creates a resource group with specified tags and location.",
            ["tool2"] = "Deletes a storage blob permanently from the container."
        };
        var cli = new Dictionary<string, string>
        {
            ["tool1"] = "Creates a resource group with specified tags and location.",
            ["tool2"] = "Removes data."
        };

        var result = DescriptionAlignmentValidator.Validate(mcp, cli);

        // tool1 should be fine, tool2 should have a warning or error
        Assert.True(result.Warnings.Count > 0 || result.Errors.Count > 0);
        Assert.True(
            result.Warnings.Any(w => w.Contains("tool2")) ||
            result.Errors.Any(e => e.Contains("tool2")));
    }
}
