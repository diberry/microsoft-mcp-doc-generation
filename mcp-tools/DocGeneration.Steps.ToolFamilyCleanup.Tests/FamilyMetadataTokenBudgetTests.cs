// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ToolFamilyCleanup.Services;
using Xunit;

namespace DocGeneration.Steps.ToolFamilyCleanup.Tests;

/// <summary>
/// Tests for FamilyMetadataGenerator.CalculateMetadataMaxTokens to verify
/// the dynamic token budget scales correctly by tool count.
/// </summary>
public class FamilyMetadataTokenBudgetTests
{
    [Fact]
    public void ZeroTools_ReturnsBaseTokens()
    {
        Assert.Equal(2000, FamilyMetadataGenerator.CalculateMetadataMaxTokens(0));
    }

    [Fact]
    public void SingleTool_ReturnsBaseTokensPlusOneToolAllocation()
    {
        Assert.Equal(2150, FamilyMetadataGenerator.CalculateMetadataMaxTokens(1));
    }

    [Fact]
    public void SixteenTools_ExceedsAzureBackupRequirement()
    {
        // Azure Backup has 16 tools and needed ~3051 tokens
        var result = FamilyMetadataGenerator.CalculateMetadataMaxTokens(16);
        Assert.Equal(4400, result);
        Assert.True(result > 3051, "Must exceed the 3051 tokens Azure Backup required");
    }

    [Fact]
    public void FortyTools_HitsCap()
    {
        Assert.Equal(8000, FamilyMetadataGenerator.CalculateMetadataMaxTokens(40));
    }

    [Fact]
    public void HundredTools_StaysCapped()
    {
        Assert.Equal(8000, FamilyMetadataGenerator.CalculateMetadataMaxTokens(100));
    }

    [Fact]
    public void NegativeToolCount_TreatedAsZero()
    {
        Assert.Equal(2000, FamilyMetadataGenerator.CalculateMetadataMaxTokens(-5));
    }

    [Theory]
    [InlineData(5, 2750)]
    [InlineData(10, 3500)]
    [InlineData(20, 5000)]
    [InlineData(30, 6500)]
    public void ScalesLinearlyForTypicalFamilySizes(int toolCount, int expectedTokens)
    {
        Assert.Equal(expectedTokens, FamilyMetadataGenerator.CalculateMetadataMaxTokens(toolCount));
    }
}
