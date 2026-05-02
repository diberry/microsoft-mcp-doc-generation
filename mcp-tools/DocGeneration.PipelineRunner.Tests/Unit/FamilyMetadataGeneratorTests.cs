// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using Xunit;

namespace PipelineRunner.Tests.Unit;

/// <summary>
/// Tests for FamilyMetadataGenerator after hybrid architecture refactor (issue #511).
/// Tests now focus on deterministic template output and verb formatting.
/// The EnsureCorrectToolCount hack has been removed as tool_count is now template-based.
/// </summary>
public class FamilyMetadataGeneratorTests
{
    [Theory]
    [InlineData("create, delete, get", "create, delete, and get")]
    [InlineData("list, update", "list and update")]
    [InlineData("get", "get")]
    [InlineData("create, delete, get, list, update", "create, delete, get, list, and update")]
    [InlineData("", "operations")] // Empty fallback
    public void FormatVerbsAsPhrase_ReturnsCorrectNaturalLanguage(string verbList, string expected)
    {
        // Act
        var result = InvokeFormatVerbsAsPhrase(verbList);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void FormatVerbsAsPhrase_HandlesWhitespace()
    {
        // Arrange
        var verbList = "  create  ,  delete  ,  get  ";

        // Act
        var result = InvokeFormatVerbsAsPhrase(verbList);

        // Assert
        Assert.Equal("create, delete, and get", result);
    }

    [Fact]
    public void BuildDeterministicMetadata_GeneratesCorrectStructure()
    {
        // Arrange
        var displayName = "Azure Storage";
        var toolCount = 5;
        var cliVersion = "1.0.0";
        var tools = new List<ToolFamilyCleanup.Models.ToolContent>
        {
            new() { Command = "azure-storage account create", ToolName = "create", Content = "", FileName = "test-create.md", FamilyName = "storage" },
            new() { Command = "azure-storage account delete", ToolName = "delete", Content = "", FileName = "test-delete.md", FamilyName = "storage" },
            new() { Command = "azure-storage account get", ToolName = "get", Content = "", FileName = "test-get.md", FamilyName = "storage" }
        };

        // Act
        var result = InvokeBuildDeterministicMetadata(displayName, toolCount, cliVersion, tools);

        // Assert
        Assert.Contains("---", result);
        Assert.Contains("title: Azure MCP Server tools for Azure Storage", result);
        Assert.Contains("description: Use Azure MCP Server tools to manage Azure Storage resources", result);
        Assert.Contains("ms.service: azure-mcp-server", result);
        Assert.Contains("ms.topic: concept-article", result);
        Assert.Contains("tool_count: 5", result);
        Assert.Contains("mcp-cli.version: 1.0.0", result);
        Assert.Contains("# Azure MCP Server tools for Azure Storage", result);
        Assert.Contains("create, delete, and get", result); // Verb phrase
    }

    [Fact]
    public void BuildDeterministicMetadata_ToolCountAlwaysMatchesInput()
    {
        // Arrange
        var displayName = "Test Service";
        var toolCount = 16; // Azure Backup case that caused token limit issues
        var cliVersion = "2.0.0";
        var tools = Enumerable.Range(1, 16).Select(i => new ToolFamilyCleanup.Models.ToolContent
        {
            Command = $"test tool{i}",
            ToolName = $"tool{i}",
            Content = "",
            FileName = $"test-tool{i}.md",
            FamilyName = "test"
        }).ToList();

        // Act
        var result = InvokeBuildDeterministicMetadata(displayName, toolCount, cliVersion, tools);

        // Assert
        Assert.Contains("tool_count: 16", result);
        Assert.DoesNotContain("tool_count: 15", result);
        Assert.DoesNotContain("tool_count: 17", result);
    }

    [Fact]
    public void BuildFallbackServiceDescription_GeneratesGenericDescription()
    {
        // Arrange
        var displayName = "Azure Key Vault";
        var familyName = "keyvault";

        // Act
        var result = InvokeBuildFallbackServiceDescription(displayName, familyName);

        // Assert
        Assert.Contains(displayName, result);
        Assert.Contains("Azure service", result);
        Assert.Contains("For more information, see", result);
        Assert.Contains("[Azure Key Vault documentation](/azure/keyvault/)", result);
    }

    /// <summary>
    /// Uses reflection to invoke the private static FormatVerbsAsPhrase method.
    /// </summary>
    private static string InvokeFormatVerbsAsPhrase(string verbList)
    {
        var generatorType = Type.GetType("ToolFamilyCleanup.Services.FamilyMetadataGenerator, DocGeneration.Steps.ToolFamilyCleanup");
        if (generatorType == null)
        {
            throw new InvalidOperationException("Could not load FamilyMetadataGenerator type");
        }

        var method = generatorType.GetMethod(
            "FormatVerbsAsPhrase",
            BindingFlags.NonPublic | BindingFlags.Static,
            null,
            new[] { typeof(string) },
            null);

        if (method == null)
        {
            throw new InvalidOperationException("Could not find FormatVerbsAsPhrase method");
        }

        var result = method.Invoke(null, new object[] { verbList });
        return result?.ToString() ?? string.Empty;
    }

    /// <summary>
    /// Uses reflection to invoke the private static BuildDeterministicMetadata method.
    /// </summary>
    private static string InvokeBuildDeterministicMetadata(
        string displayName,
        int toolCount,
        string cliVersion,
        List<ToolFamilyCleanup.Models.ToolContent> tools)
    {
        var generatorType = Type.GetType("ToolFamilyCleanup.Services.FamilyMetadataGenerator, DocGeneration.Steps.ToolFamilyCleanup");
        if (generatorType == null)
        {
            throw new InvalidOperationException("Could not load FamilyMetadataGenerator type");
        }

        var method = generatorType.GetMethod(
            "BuildDeterministicMetadata",
            BindingFlags.NonPublic | BindingFlags.Static);

        if (method == null)
        {
            throw new InvalidOperationException("Could not find BuildDeterministicMetadata method");
        }

        var result = method.Invoke(null, new object[] { displayName, toolCount, cliVersion, tools });
        return result?.ToString() ?? string.Empty;
    }

    /// <summary>
    /// Uses reflection to invoke the private static BuildFallbackServiceDescription method.
    /// </summary>
    private static string InvokeBuildFallbackServiceDescription(string displayName, string familyName)
    {
        var generatorType = Type.GetType("ToolFamilyCleanup.Services.FamilyMetadataGenerator, DocGeneration.Steps.ToolFamilyCleanup");
        if (generatorType == null)
        {
            throw new InvalidOperationException("Could not load FamilyMetadataGenerator type");
        }

        var method = generatorType.GetMethod(
            "BuildFallbackServiceDescription",
            BindingFlags.NonPublic | BindingFlags.Static,
            null,
            new[] { typeof(string), typeof(string) },
            null);

        if (method == null)
        {
            throw new InvalidOperationException("Could not find BuildFallbackServiceDescription method");
        }

        var result = method.Invoke(null, new object[] { displayName, familyName });
        return result?.ToString() ?? string.Empty;
    }
}
