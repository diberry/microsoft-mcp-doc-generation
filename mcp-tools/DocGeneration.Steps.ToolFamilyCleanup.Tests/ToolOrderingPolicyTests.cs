// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ToolFamilyCleanup.Models;
using ToolFamilyCleanup.Services;
using Xunit;

namespace DocGeneration.Steps.ToolFamilyCleanup.Tests;

/// <summary>
/// Tests for ToolOrderingPolicy — centralized ordering logic for single-resource
/// and multi-resource families (#503, #504).
/// </summary>
public class ToolOrderingPolicyTests
{
    #region Single-Resource Ordering

    [Fact]
    public void OrderForSingleResource_SortsAlphabeticallyByToolName()
    {
        var tools = new List<ToolContent>
        {
            MakeTool("vm delete", "compute vm delete", "vm-delete.md"),
            MakeTool("disk list", "compute disk list", "disk-list.md"),
            MakeTool("vm create", "compute vm create", "vm-create.md"),
        };

        var ordered = ToolOrderingPolicy.OrderForSingleResource(tools).ToList();

        Assert.Equal("disk list", ordered[0].ToolName);
        Assert.Equal("vm create", ordered[1].ToolName);
        Assert.Equal("vm delete", ordered[2].ToolName);
    }

    [Fact]
    public void OrderForSingleResource_CaseInsensitive()
    {
        var tools = new List<ToolContent>
        {
            MakeTool("Zebra list", "ns zebra list", "zebra.md"),
            MakeTool("apple create", "ns apple create", "apple.md"),
            MakeTool("Banana get", "ns banana get", "banana.md"),
        };

        var ordered = ToolOrderingPolicy.OrderForSingleResource(tools).ToList();

        Assert.Equal("apple create", ordered[0].ToolName);
        Assert.Equal("Banana get", ordered[1].ToolName);
        Assert.Equal("Zebra list", ordered[2].ToolName);
    }

    [Fact]
    public void OrderForSingleResource_TieBreakByFileName()
    {
        var tools = new List<ToolContent>
        {
            MakeTool("list", "ns list", "b-list.md"),
            MakeTool("list", "ns list", "a-list.md"),
        };

        var ordered = ToolOrderingPolicy.OrderForSingleResource(tools).ToList();

        Assert.Equal("a-list.md", ordered[0].FileName);
        Assert.Equal("b-list.md", ordered[1].FileName);
    }

    [Fact]
    public void OrderForSingleResource_EmptyList_ReturnsEmpty()
    {
        var ordered = ToolOrderingPolicy.OrderForSingleResource(Array.Empty<ToolContent>()).ToList();
        Assert.Empty(ordered);
    }

    [Fact]
    public void OrderForSingleResource_SingleTool_ReturnsThatTool()
    {
        var tools = new List<ToolContent> { MakeTool("only tool", "ns only", "only.md") };

        var ordered = ToolOrderingPolicy.OrderForSingleResource(tools).ToList();

        Assert.Single(ordered);
        Assert.Equal("only tool", ordered[0].ToolName);
    }

    #endregion

    #region Multi-Resource Ordering

    [Fact]
    public void OrderForMultiResource_SortsByActionVerb()
    {
        var tools = new List<ToolContent>
        {
            MakeTool("VM delete", "compute vm delete", "vm-delete.md"),
            MakeTool("VM create", "compute vm create", "vm-create.md"),
            MakeTool("VM list", "compute vm list", "vm-list.md"),
        };

        var ordered = ToolOrderingPolicy.OrderForMultiResource(tools).ToList();

        // Sorted by action verb: create, delete, list
        Assert.Equal("compute vm create", ordered[0].Command);
        Assert.Equal("compute vm delete", ordered[1].Command);
        Assert.Equal("compute vm list", ordered[2].Command);
    }

    [Fact]
    public void OrderForMultiResource_ActionVerbNotToolName()
    {
        // ToolName may differ from the action verb (e.g., after heading rewrite)
        // Sort must use action verb from command, not ToolName
        var tools = new List<ToolContent>
        {
            MakeTool("Managed disk: list", "compute disk list", "disk-list.md"),
            MakeTool("Managed disk: create", "compute disk create", "disk-create.md"),
            MakeTool("Managed disk: delete", "compute disk delete", "disk-delete.md"),
        };

        var ordered = ToolOrderingPolicy.OrderForMultiResource(tools).ToList();

        // Sorted by action verb (create, delete, list) NOT by ToolName
        Assert.Equal("compute disk create", ordered[0].Command);
        Assert.Equal("compute disk delete", ordered[1].Command);
        Assert.Equal("compute disk list", ordered[2].Command);
    }

    [Fact]
    public void OrderForMultiResource_SameActionVerb_TieBreakByFileName()
    {
        var tools = new List<ToolContent>
        {
            MakeTool("disk b list", "compute disk-b list", "disk-b-list.md"),
            MakeTool("disk a list", "compute disk-a list", "disk-a-list.md"),
        };

        var ordered = ToolOrderingPolicy.OrderForMultiResource(tools).ToList();

        Assert.Equal("disk-a-list.md", ordered[0].FileName);
        Assert.Equal("disk-b-list.md", ordered[1].FileName);
    }

    [Fact]
    public void OrderForMultiResource_EmptyList_ReturnsEmpty()
    {
        var ordered = ToolOrderingPolicy.OrderForMultiResource(Array.Empty<ToolContent>()).ToList();
        Assert.Empty(ordered);
    }

    [Fact]
    public void OrderForMultiResource_SingleTool_ReturnsThatTool()
    {
        var tools = new List<ToolContent> { MakeTool("vm create", "compute vm create", "vm-create.md") };

        var ordered = ToolOrderingPolicy.OrderForMultiResource(tools).ToList();

        Assert.Single(ordered);
        Assert.Equal("vm create", ordered[0].ToolName);
    }

    [Fact]
    public void OrderForMultiResource_NullCommand_SortsToFront()
    {
        var tools = new List<ToolContent>
        {
            MakeTool("vm list", "compute vm list", "vm-list.md"),
            MakeTool("unknown", null, "unknown.md"),
        };

        var ordered = ToolOrderingPolicy.OrderForMultiResource(tools).ToList();

        // Null command extracts empty verb which sorts before "list"
        Assert.Equal("unknown.md", ordered[0].FileName);
        Assert.Equal("vm-list.md", ordered[1].FileName);
    }

    [Fact]
    public void OrderForMultiResource_ConsistentResultsWhenToolNameDiffersFromDisplayHeading()
    {
        // Simulates the real bug: ToolName="VM create" but display heading becomes "VM: create"
        // after ReformatToolHeadingForMultiResource. The sort must produce consistent
        // ordering regardless of ToolName value.
        var tools = new List<ToolContent>
        {
            MakeTool("Create virtual machine", "compute vm create", "vm-create.md"),
            MakeTool("Delete virtual machine", "compute vm delete", "vm-delete.md"),
            MakeTool("List virtual machines", "compute vm list", "vm-list.md"),
        };

        var orderedRun1 = ToolOrderingPolicy.OrderForMultiResource(tools).ToList();
        var orderedRun2 = ToolOrderingPolicy.OrderForMultiResource(tools).ToList();

        // Verify order by action verb (create < delete < list) even though
        // ToolNames would sort differently (Create < Delete < List happens to match,
        // but the mechanism is verb-based)
        Assert.Equal("compute vm create", orderedRun1[0].Command);
        Assert.Equal("compute vm delete", orderedRun1[1].Command);
        Assert.Equal("compute vm list", orderedRun1[2].Command);

        // Deterministic across runs
        for (int i = 0; i < orderedRun1.Count; i++)
        {
            Assert.Equal(orderedRun1[i].Command, orderedRun2[i].Command);
        }
    }

    [Fact]
    public void OrderForMultiResource_RealWorldToolNames_CorrectOrder()
    {
        // Real-world scenario: compute family with mixed resources
        var tools = new List<ToolContent>
        {
            MakeTool("VMSS update", "compute vmss update", "vmss-update.md"),
            MakeTool("disk create", "compute disk create", "disk-create.md"),
            MakeTool("vm delete", "compute vm delete", "vm-delete.md"),
            MakeTool("disk list", "compute disk list", "disk-list.md"),
            MakeTool("vm create", "compute vm create", "vm-create.md"),
        };

        var ordered = ToolOrderingPolicy.OrderForMultiResource(tools).ToList();

        // All sorted by action verb: create, create, delete, list, update
        Assert.Equal("compute disk create", ordered[0].Command);
        Assert.Equal("compute vm create", ordered[1].Command);
        Assert.Equal("compute vm delete", ordered[2].Command);
        Assert.Equal("compute disk list", ordered[3].Command);
        Assert.Equal("compute vmss update", ordered[4].Command);
    }

    #endregion

    #region ExtractActionVerb

    [Theory]
    [InlineData("compute disk create", "create")]
    [InlineData("compute vm delete", "delete")]
    [InlineData("storage blob list", "list")]
    [InlineData("foundry agent thread create", "create")]
    [InlineData("advisor list", "list")]
    public void ExtractActionVerb_ReturnsLastSegment(string command, string expected)
    {
        var result = ToolOrderingPolicy.ExtractActionVerb(command);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ExtractActionVerb_NullOrEmpty_ReturnsEmpty(string? command)
    {
        var result = ToolOrderingPolicy.ExtractActionVerb(command);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void ExtractActionVerb_SingleSegment_ReturnsThatSegment()
    {
        var result = ToolOrderingPolicy.ExtractActionVerb("list");
        Assert.Equal("list", result);
    }

    #endregion

    [Theory]
    [InlineData("compute vm create ", "create")]
    [InlineData("  storage blob list  ", "list")]
    public void ExtractActionVerb_TrailingOrLeadingWhitespace_ReturnsVerb(string command, string expected)
    {
        var result = ToolOrderingPolicy.ExtractActionVerb(command);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("az compute vm create --sku Standard", "create")]
    [InlineData("compute disk list --output json", "list")]
    [InlineData("storage blob upload --file test.txt --container my-container", "upload")]
    public void ExtractActionVerb_CommandWithParameters_ReturnsVerbBeforeFlags(string command, string expected)
    {
        var result = ToolOrderingPolicy.ExtractActionVerb(command);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("storage-account-create", "storage-account-create")]
    [InlineData("vm-deallocate", "vm-deallocate")]
    public void ExtractActionVerb_HyphenatedSingleSegment_ReturnsFullSegment(string command, string expected)
    {
        // Hyphenated commands without spaces are treated as a single verb segment
        var result = ToolOrderingPolicy.ExtractActionVerb(command);
        Assert.Equal(expected, result);
    }

    #region Helpers

    private static ToolContent MakeTool(string toolName, string? command, string fileName)
    {
        return new ToolContent
        {
            ToolName = toolName,
            FileName = fileName,
            FamilyName = "test",
            Content = $"## {toolName}\n\nSome content.",
            Command = command,
            ResourceType = command != null ? ToolReader.GetResourceType(command) : string.Empty
        };
    }

    #endregion
}