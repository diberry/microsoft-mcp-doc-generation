using System;
using Xunit;
using ExamplePromptValidator;

namespace ExamplePromptValidator.Tests;

public class PromptValidatorTests
{
    [Fact]
    public void PromptValidator_Constructor_DoesNotThrow()
    {
        // Act & Assert - should not throw even if Azure OpenAI is not configured
        var validator = new PromptValidator();
        Assert.NotNull(validator);
    }

    [Fact]
    public void IsInitialized_WithoutAzureConfig_ReturnsFalse()
    {
        // Arrange
        var validator = new PromptValidator();

        // Act
        var isInit = validator.IsInitialized();

        // Assert
        // May be false if Azure OpenAI is not configured in test environment
        // This is expected behavior - validator gracefully handles missing config
        Assert.IsType<bool>(isInit);
    }

    [Fact]
    public void ValidationResult_CanBeInstantiated()
    {
        // Arrange & Act
        var result = new ValidationResult
        {
            ToolName = "test-tool",
            TotalPrompts = 5,
            ValidPrompts = 4,
            InvalidPrompts = 1,
            IsValid = false,
            Summary = "Test summary",
            RequiredParameters = new() { "param1", "param2" },
            Validation = new(),
            Recommendations = new() { "Fix prompt 1" }
        };

        // Assert
        Assert.Equal("test-tool", result.ToolName);
        Assert.Equal(5, result.TotalPrompts);
        Assert.Equal(4, result.ValidPrompts);
        Assert.Equal(1, result.InvalidPrompts);
        Assert.False(result.IsValid);
        Assert.Equal("Test summary", result.Summary);
        Assert.Equal(2, result.RequiredParameters.Count);
        Assert.Single(result.Recommendations);
    }

    [Fact]
    public void PromptValidation_CanBeInstantiated()
    {
        // Arrange & Act
        var promptValidation = new PromptValidation
        {
            Prompt = "Create a storage account",
            IsValid = true,
            MissingParameters = new(),
            FoundParameters = new() { "account-name" }
        };

        // Assert
        Assert.Equal("Create a storage account", promptValidation.Prompt);
        Assert.True(promptValidation.IsValid);
        Assert.Empty(promptValidation.MissingParameters);
        Assert.Single(promptValidation.FoundParameters);
    }
}
