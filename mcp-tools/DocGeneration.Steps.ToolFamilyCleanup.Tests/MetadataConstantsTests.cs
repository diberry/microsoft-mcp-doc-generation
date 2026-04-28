// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ToolFamilyCleanup.Services;
using Xunit;

namespace DocGeneration.Steps.ToolFamilyCleanup.Tests;

/// <summary>
/// Tests for MetadataConstants to verify all constants are accessible
/// and have the expected values.
/// Part of Phase 0: metadata extraction PRD.
/// </summary>
public class MetadataConstantsTests
{
    [Fact]
    public void Author_IsCorrect()
    {
        Assert.Equal("diberry", MetadataConstants.Author);
    }

    [Fact]
    public void Reviewer_IsCorrect()
    {
        Assert.Equal("mbaldwin", MetadataConstants.Reviewer);
    }

    [Fact]
    public void AiUsage_IsCorrect()
    {
        Assert.Equal("ai-generated", MetadataConstants.AiUsage);
    }

    [Fact]
    public void ContentWellValue_IsCorrect()
    {
        Assert.Equal("AI-contribution", MetadataConstants.ContentWellValue);
    }

    [Fact]
    public void MsCustom_IsCorrect()
    {
        Assert.Equal("build-2025", MetadataConstants.MsCustom);
    }

    [Fact]
    public void MsService_IsCorrect()
    {
        Assert.Equal("azure-mcp-server", MetadataConstants.MsService);
    }

    [Fact]
    public void MsTopic_IsCorrect()
    {
        Assert.Equal("concept-article", MetadataConstants.MsTopic);
    }

    [Fact]
    public void ProductName_IsCorrect()
    {
        Assert.Equal("Azure MCP Server", MetadataConstants.ProductName);
    }

    [Fact]
    public void TitleTemplate_IsCorrect()
    {
        Assert.Equal("{0} tools for {1}", MetadataConstants.TitleTemplate);
    }

    [Fact]
    public void DescriptionTemplate_IsCorrect()
    {
        Assert.Equal("Use {0} tools to manage {1} resources with natural language prompts from your IDE.", MetadataConstants.DescriptionTemplate);
    }

    [Fact]
    public void IncludeParameterConsideration_IsCorrect()
    {
        Assert.Equal("[!INCLUDE [tip-about-params](../includes/tools/parameter-consideration.md)]", MetadataConstants.IncludeParameterConsideration);
    }

    [Fact]
    public void TitleTemplate_FormatsCorrectly()
    {
        var result = string.Format(MetadataConstants.TitleTemplate, MetadataConstants.ProductName, "Azure Storage");
        Assert.Equal("Azure MCP Server tools for Azure Storage", result);
    }

    [Fact]
    public void DescriptionTemplate_FormatsCorrectly()
    {
        var result = string.Format(MetadataConstants.DescriptionTemplate, MetadataConstants.ProductName, "Azure Storage");
        Assert.Equal("Use Azure MCP Server tools to manage Azure Storage resources with natural language prompts from your IDE.", result);
    }
}
