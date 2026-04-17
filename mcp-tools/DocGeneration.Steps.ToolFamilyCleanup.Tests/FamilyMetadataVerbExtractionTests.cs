// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ToolFamilyCleanup.Models;
using ToolFamilyCleanup.Services;
using Xunit;

namespace DocGeneration.Steps.ToolFamilyCleanup.Tests;

/// <summary>
/// Tests for verb extraction logic used by FamilyMetadataGenerator.
/// Verbs are extracted from ToolContent.Command (last segment) to provide
/// an accurate verb list to the AI prompt, preventing missing CRUD verbs.
/// Fixes #229.
/// </summary>
public class FamilyMetadataVerbExtractionTests
{
    [Fact]
    public void GetVerbSummary_ExtractsLastSegmentOfCommands()
    {
        var tools = new List<ToolContent>
        {
            MakeTool("vm list", "compute vm list"),
            MakeTool("vm get", "compute vm get"),
            MakeTool("vm create", "compute vm create"),
        };

        var result = FamilyMetadataGenerator.GetVerbSummary(tools);

        Assert.Equal("create, get, list", result);
    }

    [Fact]
    public void GetVerbSummary_DeduplicatesVerbs()
    {
        var tools = new List<ToolContent>
        {
            MakeTool("vm list", "compute vm list"),
            MakeTool("vmss list", "compute vmss list"),
            MakeTool("disk list", "compute disk list"),
        };

        var result = FamilyMetadataGenerator.GetVerbSummary(tools);

        Assert.Equal("list", result);
    }

    [Fact]
    public void GetVerbSummary_SortsAlphabetically()
    {
        var tools = new List<ToolContent>
        {
            MakeTool("vm update", "compute vm update"),
            MakeTool("vm delete", "compute vm delete"),
            MakeTool("vm create", "compute vm create"),
            MakeTool("vm get", "compute vm get"),
            MakeTool("vm list", "compute vm list"),
        };

        var result = FamilyMetadataGenerator.GetVerbSummary(tools);

        Assert.Equal("create, delete, get, list, update", result);
    }

    [Fact]
    public void GetVerbSummary_NormalizesToLowerCase()
    {
        var tools = new List<ToolContent>
        {
            MakeTool("vm List", "compute vm List"),
            MakeTool("vm GET", "compute vm GET"),
        };

        var result = FamilyMetadataGenerator.GetVerbSummary(tools);

        Assert.Equal("get, list", result);
    }

    [Fact]
    public void GetVerbSummary_SkipsNullCommands()
    {
        var tools = new List<ToolContent>
        {
            MakeTool("vm list", "compute vm list"),
            MakeToolNoCommand("vm orphan"),
            MakeTool("vm get", "compute vm get"),
        };

        var result = FamilyMetadataGenerator.GetVerbSummary(tools);

        Assert.Equal("get, list", result);
    }

    [Fact]
    public void GetVerbSummary_SkipsEmptyCommands()
    {
        var tools = new List<ToolContent>
        {
            MakeTool("vm list", "compute vm list"),
            MakeTool("vm empty", ""),
            MakeTool("vm whitespace", "   "),
        };

        var result = FamilyMetadataGenerator.GetVerbSummary(tools);

        Assert.Equal("list", result);
    }

    [Fact]
    public void GetVerbSummary_HandlesSingleWordCommand()
    {
        var tools = new List<ToolContent>
        {
            MakeTool("diagnose", "diagnose"),
        };

        var result = FamilyMetadataGenerator.GetVerbSummary(tools);

        Assert.Equal("diagnose", result);
    }

    [Fact]
    public void GetVerbSummary_ReturnsEmptyForNoTools()
    {
        var tools = new List<ToolContent>();

        var result = FamilyMetadataGenerator.GetVerbSummary(tools);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void GetVerbSummary_ReturnsEmptyWhenAllCommandsNull()
    {
        var tools = new List<ToolContent>
        {
            MakeToolNoCommand("tool1"),
            MakeToolNoCommand("tool2"),
        };

        var result = FamilyMetadataGenerator.GetVerbSummary(tools);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void GetVerbSummary_RealisticComputeNamespace()
    {
        // Realistic compute tools — the original bug scenario
        var tools = new List<ToolContent>
        {
            MakeTool("vm create", "compute vm create"),
            MakeTool("vm get", "compute vm get"),
            MakeTool("vm list", "compute vm list"),
            MakeTool("vm update", "compute vm update"),
            MakeTool("vm delete", "compute vm delete"),
            MakeTool("vmss list", "compute vmss list"),
            MakeTool("disk list", "compute disk list"),
        };

        var result = FamilyMetadataGenerator.GetVerbSummary(tools);

        // Must include "delete" — the verb that was missing in bug #229
        Assert.Contains("delete", result);
        Assert.Equal("create, delete, get, list, update", result);
    }

    private static ToolContent MakeTool(string name, string command) => new()
    {
        ToolName = name,
        FileName = $"{name.Replace(' ', '-')}.complete.md",
        FamilyName = "test",
        Content = $"## {name}\nSome content.",
        Command = command,
    };

    private static ToolContent MakeToolNoCommand(string name) => new()
    {
        ToolName = name,
        FileName = $"{name.Replace(' ', '-')}.complete.md",
        FamilyName = "test",
        Content = $"## {name}\nSome content.",
        Command = null,
    };
}
