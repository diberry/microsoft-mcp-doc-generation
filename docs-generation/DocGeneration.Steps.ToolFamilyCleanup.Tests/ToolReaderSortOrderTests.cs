// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ToolFamilyCleanup.Models;
using ToolFamilyCleanup.Services;
using Xunit;

namespace DocGeneration.Steps.ToolFamilyCleanup.Tests;

/// <summary>
/// Tests for ToolReader.GetResourceSortKey and the resource-type-first ordering
/// of tools within each family (Issue #279).
/// </summary>
public class ToolReaderSortOrderTests
{
    // --- GetResourceSortKey unit tests ---

    [Theory]
    [InlineData("compute disk create", "disk\0create")]
    [InlineData("compute disk delete", "disk\0delete")]
    [InlineData("compute vm create", "vm\0create")]
    [InlineData("compute vmss update", "vmss\0update")]
    [InlineData("storage account create", "account\0create")]
    [InlineData("storage blob list", "blob\0list")]
    public void GetResourceSortKey_ThreeSegmentCommand_ReturnsResourceThenVerb(
        string command, string expected)
    {
        var result = ToolReader.GetResourceSortKey(command);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("foundry agent thread create", "agent thread\0create")]
    [InlineData("cosmos db container query", "db container\0query")]
    public void GetResourceSortKey_MultiResourceCommand_ReturnsResourcesThenVerb(
        string command, string expected)
    {
        var result = ToolReader.GetResourceSortKey(command);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("advisor list", "\0list")]
    [InlineData("storage list", "\0list")]
    public void GetResourceSortKey_TwoSegmentCommand_PutsVerbAfterSeparator(
        string command, string expected)
    {
        var result = ToolReader.GetResourceSortKey(command);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetResourceSortKey_SingleSegment_ReturnsFallback()
    {
        var result = ToolReader.GetResourceSortKey("advisor");
        Assert.Equal("advisor", result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetResourceSortKey_NullOrEmpty_ReturnsEmpty(string? command)
    {
        var result = ToolReader.GetResourceSortKey(command);
        Assert.Equal(string.Empty, result);
    }

    // --- Integration-level ordering test ---

    [Fact]
    public void Tools_SortedByResourceSortKey_GroupByResourceThenVerb()
    {
        // Arrange: tools in verb-first order (the current buggy behavior)
        var tools = new List<ToolContent>
        {
            MakeTool("Create compute disk",    "compute disk create"),
            MakeTool("Create virtual machine", "compute vm create"),
            MakeTool("Create VMSS",            "compute vmss create"),
            MakeTool("Delete compute disk",    "compute disk delete"),
            MakeTool("Delete virtual machine", "compute vm delete"),
            MakeTool("Delete VMSS",            "compute vmss delete"),
            MakeTool("Get compute disk",       "compute disk get"),
            MakeTool("Get virtual machine",    "compute vm get"),
            MakeTool("Get VMSS",               "compute vmss get"),
            MakeTool("Update compute disk",    "compute disk update"),
            MakeTool("Update virtual machine", "compute vm update"),
            MakeTool("Update VMSS",            "compute vmss update"),
        };

        // Act: sort with the resource-first key (must use StringComparer.Ordinal
        // to match production code — culture-sensitive comparison ignores \0)
        var sorted = tools
            .OrderBy(t => ToolReader.GetResourceSortKey(t.Command), StringComparer.Ordinal)
            .ThenBy(t => t.ToolName, StringComparer.Ordinal)
            .ToList();

        // Assert: expected resource-first ordering
        //   disk (create, delete, get, update) → vm (create, ...) → vmss (create, ...)
        var commands = sorted.Select(t => t.Command).ToList();

        Assert.Equal("compute disk create", commands[0]);
        Assert.Equal("compute disk delete", commands[1]);
        Assert.Equal("compute disk get", commands[2]);
        Assert.Equal("compute disk update", commands[3]);
        Assert.Equal("compute vm create", commands[4]);
        Assert.Equal("compute vm delete", commands[5]);
        Assert.Equal("compute vm get", commands[6]);
        Assert.Equal("compute vm update", commands[7]);
        Assert.Equal("compute vmss create", commands[8]);
        Assert.Equal("compute vmss delete", commands[9]);
        Assert.Equal("compute vmss get", commands[10]);
        Assert.Equal("compute vmss update", commands[11]);
    }

    [Fact]
    public void Tools_WithMixedCommandLengths_SortBareVerbsFirst()
    {
        // Bare namespace+verb commands should sort before resource-specific ones,
        // not interleave with resource groups.
        var tools = new List<ToolContent>
        {
            MakeTool("Get storage accounts",   "storage account list"),
            MakeTool("Create storage account",  "storage account create"),
            MakeTool("Get blob",               "storage blob get"),
            MakeTool("Get storage",            "storage list"),
        };

        var sorted = tools
            .OrderBy(t => ToolReader.GetResourceSortKey(t.Command), StringComparer.Ordinal)
            .ThenBy(t => t.ToolName, StringComparer.Ordinal)
            .ToList();

        var commands = sorted.Select(t => t.Command).ToList();

        // Bare verb ("storage list") has empty resource → sorts first
        Assert.Equal("storage list", commands[0]);
        // Then account group
        Assert.Equal("storage account create", commands[1]);
        Assert.Equal("storage account list", commands[2]);
        // Then blob group
        Assert.Equal("storage blob get", commands[3]);
    }

    [Fact]
    public void Tools_WithNullCommand_FallBackToToolNameSort()
    {
        var tools = new List<ToolContent>
        {
            MakeTool("Zebra tool", null),
            MakeTool("Alpha tool", null),
            MakeTool("Middle tool", null),
        };

        var sorted = tools
            .OrderBy(t => ToolReader.GetResourceSortKey(t.Command), StringComparer.Ordinal)
            .ThenBy(t => t.ToolName, StringComparer.Ordinal)
            .ToList();

        Assert.Equal("Alpha tool", sorted[0].ToolName);
        Assert.Equal("Middle tool", sorted[1].ToolName);
        Assert.Equal("Zebra tool", sorted[2].ToolName);
    }

    private static ToolContent MakeTool(string toolName, string? command)
    {
        return new ToolContent
        {
            ToolName = toolName,
            FileName = $"{(command ?? toolName).Replace(' ', '-')}.md",
            FamilyName = "test",
            Content = $"# {toolName}\n\nSome content.",
            Command = command,
        };
    }
}
