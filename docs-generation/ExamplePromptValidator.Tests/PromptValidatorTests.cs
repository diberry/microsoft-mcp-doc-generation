using System.Collections.Generic;
using Xunit;
using ExamplePromptValidator;

namespace ExamplePromptValidator.Tests;

public class PromptValidatorTests
{
    [Fact]
    public void ValidatePrompt_WithAllRequiredParameters_ReturnsValid()
    {
        // Arrange
        var prompt = "Create a storage account named mystorageaccount in the westus location with resource group myresourcegroup";
        var requiredParameters = new List<string> { "account-name", "location", "resource-group" };

        // Act
        var result = PromptValidator.ValidatePrompt(prompt, requiredParameters);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.MissingParameters);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void ValidatePrompt_WithMissingRequiredParameter_ReturnsInvalid()
    {
        // Arrange
        var prompt = "Create a storage account named mystorageaccount in the westus location";
        var requiredParameters = new List<string> { "account-name", "location", "resource-group" };

        // Act
        var result = PromptValidator.ValidatePrompt(prompt, requiredParameters);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("resource-group", result.MissingParameters);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public void ValidatePrompt_ExcludesCommonParameters()
    {
        // Arrange - prompt missing subscription-id which should be excluded
        var prompt = "Create a storage account named mystorageaccount";
        var requiredParameters = new List<string> { "account-name", "subscription-id", "tenant-id" };

        // Act
        var result = PromptValidator.ValidatePrompt(prompt, requiredParameters);

        // Assert - should be valid because common parameters are excluded
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidatePrompt_WithSpacesInsteadOfDashes_FindsParameter()
    {
        // Arrange - prompt uses "resource group" instead of "resource-group"
        var prompt = "Create a storage account in resource group mygroup";
        var requiredParameters = new List<string> { "resource-group" };

        // Act
        var result = PromptValidator.ValidatePrompt(prompt, requiredParameters);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidatePrompts_WithMultiplePrompts_AggregatesResults()
    {
        // Arrange
        var prompts = new List<string>
        {
            "Create a storage account named test with location westus and resource group mygroup",
            "Create a storage account named test2 with location eastus", // missing resource-group
            "Create a storage account with location centralus and resource group anothergroup" // missing account-name
        };
        var requiredParameters = new List<string> { "account-name", "location", "resource-group" };

        // Act
        var result = PromptValidator.ValidatePrompts(prompts, requiredParameters);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(3, result.TotalPrompts);
        Assert.Equal(1, result.ValidPrompts);
        Assert.Equal(2, result.InvalidPrompts);
        Assert.Contains("resource-group", result.AllMissingParameters);
        Assert.Contains("account-name", result.AllMissingParameters);
    }

    [Fact]
    public void ValidatePrompt_WithEmptyPrompt_ReturnsInvalid()
    {
        // Arrange
        var prompt = "";
        var requiredParameters = new List<string> { "account-name" };

        // Act
        var result = PromptValidator.ValidatePrompt(prompt, requiredParameters);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("Prompt is empty or null", result.ErrorMessage);
    }

    [Fact]
    public void ValidatePrompt_WithNoRequiredParameters_ReturnsValid()
    {
        // Arrange
        var prompt = "Show all storage accounts";
        var requiredParameters = new List<string>();

        // Act
        var result = PromptValidator.ValidatePrompt(prompt, requiredParameters);

        // Assert
        Assert.True(result.IsValid);
    }
}
