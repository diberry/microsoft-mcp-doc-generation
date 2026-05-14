// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ToolFamilyCleanup.Models;
using ToolFamilyCleanup.Services;
using Xunit;

namespace DocGeneration.Steps.ToolFamilyCleanup.Tests;

/// <summary>
/// Tests for ToolOrderingPolicy defensive validation — null checks, invalid tool handling,
/// and the Validate method (#522).
/// </summary>
public class ToolOrderingPolicyValidationTests
{
    #region OrderForSingleResource Validation

    [Fact]
    public void OrderForSingleResource_NullInput_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ToolOrderingPolicy.OrderForSingleResource(null!).ToList());
    }

    [Fact]
    public void OrderForSingleResource_EmptyCollection_ReturnsEmpty()
    {
        var result = ToolOrderingPolicy.OrderForSingleResource([]).ToList();
        Assert.Empty(result);
    }

    [Fact]
    public void OrderForSingleResource_NullToolName_PlacedAtEnd()
    {
        var tools = new List<ToolContent>
        {
            MakeTool(null!, "ns create", "create.md"),
            MakeTool("alpha", "ns alpha", "alpha.md"),
        };

        var ordered = ToolOrderingPolicy.OrderForSingleResource(tools).ToList();

        Assert.Equal("alpha", ordered[0].ToolName);
        Assert.Null(ordered[1].ToolName);
    }

    [Fact]
    public void OrderForSingleResource_WhitespaceToolName_PlacedAtEnd()
    {
        var tools = new List<ToolContent>
        {
            MakeTool("   ", "ns blank", "blank.md"),
            MakeTool("beta", "ns beta", "beta.md"),
        };

        var ordered = ToolOrderingPolicy.OrderForSingleResource(tools).ToList();

        Assert.Equal("beta", ordered[0].ToolName);
        Assert.Equal("   ", ordered[1].ToolName);
    }

    [Fact]
    public void OrderForSingleResource_MixedValidAndInvalid_ValidSortedFirst()
    {
        var tools = new List<ToolContent>
        {
            MakeTool("", "ns empty", "empty.md"),
            MakeTool("zebra", "ns zebra", "zebra.md"),
            MakeTool(null!, "ns null", "null.md"),
            MakeTool("alpha", "ns alpha", "alpha.md"),
            MakeTool("  ", "ns ws", "ws.md"),
        };

        var ordered = ToolOrderingPolicy.OrderForSingleResource(tools).ToList();

        // Valid tools sorted alphabetically first
        Assert.Equal("alpha", ordered[0].ToolName);
        Assert.Equal("zebra", ordered[1].ToolName);
        // Invalid tools at the end (order among invalids is preserved)
        Assert.Equal("empty.md", ordered[2].FileName);
        Assert.Equal("null.md", ordered[3].FileName);
        Assert.Equal("ws.md", ordered[4].FileName);
    }

    #endregion

    #region OrderForMultiResource Validation

    [Fact]
    public void OrderForMultiResource_NullInput_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ToolOrderingPolicy.OrderForMultiResource(null!).ToList());
    }

    [Fact]
    public void OrderForMultiResource_EmptyCollection_ReturnsEmpty()
    {
        var result = ToolOrderingPolicy.OrderForMultiResource([]).ToList();
        Assert.Empty(result);
    }

    [Fact]
    public void OrderForMultiResource_NullCommand_PlacedAtEnd()
    {
        var tools = new List<ToolContent>
        {
            MakeTool("unknown", null, "unknown.md"),
            MakeTool("vm list", "compute vm list", "vm-list.md"),
        };

        var ordered = ToolOrderingPolicy.OrderForMultiResource(tools).ToList();

        Assert.Equal("vm-list.md", ordered[0].FileName);
        Assert.Equal("unknown.md", ordered[1].FileName);
    }

    [Fact]
    public void OrderForMultiResource_WhitespaceCommand_PlacedAtEnd()
    {
        var tools = new List<ToolContent>
        {
            MakeTool("blank", "   ", "blank.md"),
            MakeTool("vm create", "compute vm create", "vm-create.md"),
        };

        var ordered = ToolOrderingPolicy.OrderForMultiResource(tools).ToList();

        Assert.Equal("vm-create.md", ordered[0].FileName);
        Assert.Equal("blank.md", ordered[1].FileName);
    }

    #endregion

    #region Validate Method

    [Fact]
    public void Validate_AllValid_ReturnsIsValidTrue()
    {
        var tools = new List<ToolContent>
        {
            MakeTool("vm create", "compute vm create", "vm-create.md"),
            MakeTool("vm list", "compute vm list", "vm-list.md"),
        };

        var result = ToolOrderingPolicy.Validate(tools);

        Assert.True(result.IsValid);
        Assert.Empty(result.Warnings);
        Assert.Empty(result.InvalidTools);
    }

    [Fact]
    public void Validate_NullToolNames_ReportsWarnings()
    {
        var tools = new List<ToolContent>
        {
            MakeTool(null!, "compute vm create", "vm-create.md"),
            MakeTool("vm list", "compute vm list", "vm-list.md"),
        };

        var result = ToolOrderingPolicy.Validate(tools);

        Assert.False(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Contains("ToolName"));
        Assert.Single(result.InvalidTools);
    }

    [Fact]
    public void Validate_NullCommands_ReportsWarnings()
    {
        var tools = new List<ToolContent>
        {
            MakeTool("vm create", null, "vm-create.md"),
            MakeTool("vm list", "compute vm list", "vm-list.md"),
        };

        var result = ToolOrderingPolicy.Validate(tools);

        Assert.False(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Contains("Command"));
        Assert.Single(result.InvalidTools);
    }

    [Fact]
    public void Validate_MixedIssues_ReportsAll()
    {
        var tools = new List<ToolContent>
        {
            MakeTool(null!, null, "both-bad.md"),
            MakeTool("  ", "compute vm list", "bad-name.md"),
            MakeTool("vm create", "  ", "bad-cmd.md"),
            MakeTool("vm list", "compute vm list", "valid.md"),
        };

        var result = ToolOrderingPolicy.Validate(tools);

        Assert.False(result.IsValid);
        // both-bad has two warnings (ToolName + Command), bad-name has 1, bad-cmd has 1
        Assert.True(result.Warnings.Count >= 4);
        Assert.Equal(3, result.InvalidTools.Count);
    }

    #endregion

    #region Helpers

    private static ToolContent MakeTool(string? toolName, string? command, string fileName)
    {
        return new ToolContent
        {
            ToolName = toolName!,
            FileName = fileName,
            FamilyName = "test",
            Content = $"## {toolName}\n\nSome content.",
            Command = command,
            ResourceType = string.Empty
        };
    }

    #endregion
}
