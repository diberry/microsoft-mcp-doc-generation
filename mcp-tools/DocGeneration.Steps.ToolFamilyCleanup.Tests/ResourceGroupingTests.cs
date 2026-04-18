// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ToolFamilyCleanup.Models;
using ToolFamilyCleanup.Services;
using Xunit;

namespace DocGeneration.Steps.ToolFamilyCleanup.Tests;

/// <summary>
/// Tests for resource sub-type grouping in FamilyFileStitcher and ToolReader (#412).
/// </summary>
public class ResourceGroupingTests
{
    // --- ToolReader.GetResourceType tests ---

    [Theory]
    [InlineData("compute disk create", "disk")]
    [InlineData("compute vm delete", "vm")]
    [InlineData("storage account create", "account")]
    [InlineData("storage blob list", "blob")]
    [InlineData("foundry agent thread create", "agent thread")]
    [InlineData("cosmos db container query", "db container")]
    public void GetResourceType_MultiSegmentCommand_ReturnsResourcePortion(
        string command, string expected)
    {
        var result = ToolReader.GetResourceType(command);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("advisor list", "")]
    [InlineData("storage list", "")]
    public void GetResourceType_TwoSegmentCommand_ReturnsEmpty(string command, string expected)
    {
        var result = ToolReader.GetResourceType(command);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("advisor")]
    public void GetResourceType_NullEmptyOrSingleSegment_ReturnsEmpty(string? command)
    {
        var result = ToolReader.GetResourceType(command);
        Assert.Equal(string.Empty, result);
    }

    // --- IsMultiResourceFamily tests ---

    [Fact]
    public void IsMultiResourceFamily_SingleResourceType_ReturnsFalse()
    {
        var tools = new List<ToolContent>
        {
            MakeTool("Get advisor", "advisor list", ""),
            MakeTool("Get recommendation", "advisor recommendation list", "recommendation"),
        };
        // Only one non-empty resource type "recommendation" -> false
        Assert.False(FamilyFileStitcher.IsMultiResourceFamily(tools));
    }

    [Fact]
    public void IsMultiResourceFamily_AllBareVerbs_ReturnsFalse()
    {
        var tools = new List<ToolContent>
        {
            MakeTool("Get advisor", "advisor get", ""),
            MakeTool("List advisor", "advisor list", ""),
        };
        Assert.False(FamilyFileStitcher.IsMultiResourceFamily(tools));
    }

    [Fact]
    public void IsMultiResourceFamily_MultipleResourceTypes_ReturnsTrue()
    {
        var tools = new List<ToolContent>
        {
            MakeTool("Create disk", "compute disk create", "disk"),
            MakeTool("Delete disk", "compute disk delete", "disk"),
            MakeTool("Create VM", "compute vm create", "vm"),
            MakeTool("Delete VM", "compute vm delete", "vm"),
        };
        Assert.True(FamilyFileStitcher.IsMultiResourceFamily(tools));
    }

    [Fact]
    public void IsMultiResourceFamily_TwoResourceTypes_ReturnsTrue()
    {
        var tools = new List<ToolContent>
        {
            MakeTool("Create account", "storage account create", "account"),
            MakeTool("Get blob", "storage blob get", "blob"),
        };
        Assert.True(FamilyFileStitcher.IsMultiResourceFamily(tools));
    }

    // --- FormatResourceTypeDisplayName tests ---

    [Theory]
    [InlineData("disk", "Disk")]
    [InlineData("vm", "VM")]
    [InlineData("vmss", "VMSS")]
    [InlineData("db", "DB")]
    [InlineData("account", "Account")]
    [InlineData("blob", "Blob")]
    [InlineData("db container", "DB container")]
    [InlineData("agent thread", "Agent thread")]
    [InlineData("", "General")]
    [InlineData("   ", "General")]
    public void FormatResourceTypeDisplayName_ProducesHumanReadableName(
        string resourceType, string expected)
    {
        var result = FamilyFileStitcher.FormatResourceTypeDisplayName(resourceType);
        Assert.Equal(expected, result);
    }

    // --- DemoteHeadings tests ---

    [Fact]
    public void DemoteHeadings_H2BecomesH3()
    {
        var input = "## Create a disk\n\nSome content.";
        var result = FamilyFileStitcher.DemoteHeadings(input);
        Assert.StartsWith("### Create a disk", result);
    }

    [Fact]
    public void DemoteHeadings_H3BecomesH4()
    {
        var input = "### Parameters\n\nSome content.";
        var result = FamilyFileStitcher.DemoteHeadings(input);
        Assert.StartsWith("#### Parameters", result);
    }

    [Fact]
    public void DemoteHeadings_H6StaysH6()
    {
        var input = "###### Deep heading\n\nSome content.";
        var result = FamilyFileStitcher.DemoteHeadings(input);
        Assert.StartsWith("###### Deep heading", result);
    }

    [Fact]
    public void DemoteHeadings_MixedHeadingLevels_AllDemoted()
    {
        var input = "## Top\n\nContent\n\n### Sub\n\nMore content\n\n#### Deep\n\nDeepest";
        var result = FamilyFileStitcher.DemoteHeadings(input);
        Assert.Contains("### Top", result);
        Assert.Contains("#### Sub", result);
        Assert.Contains("##### Deep", result);
    }

    [Fact]
    public void DemoteHeadings_NoHeadings_Unchanged()
    {
        var input = "Just some text with no headings.";
        var result = FamilyFileStitcher.DemoteHeadings(input);
        Assert.Equal(input, result);
    }

    // --- Stitch integration tests ---

    [Fact]
    public void Stitch_SingleResourceNamespace_NoGroupingHeaders()
    {
        var familyContent = new FamilyContent
        {
            FamilyName = "advisor",
            Metadata = "---\ntitle: Advisor\n---\n# Advisor\n\nIntro paragraph.",
            Tools = new List<ToolContent>
            {
                MakeTool("Get recommendations", "advisor get", ""),
            },
            RelatedContent = "## Related content\n\n- [Link](/path)"
        };

        var stitcher = new FamilyFileStitcher();
        var result = stitcher.Stitch(familyContent);

        Assert.Contains("## Get recommendations", result);
        Assert.DoesNotContain("## General", result);
    }

    [Fact]
    public void Stitch_MultiResourceNamespace_GroupsToolsByResourceType()
    {
        var familyContent = new FamilyContent
        {
            FamilyName = "compute",
            Metadata = "---\ntitle: Compute\n---\n# Compute\n\nIntro paragraph.",
            Tools = new List<ToolContent>
            {
                MakeTool("Create a disk", "compute disk create", "disk"),
                MakeTool("Delete a disk", "compute disk delete", "disk"),
                MakeTool("Create a VM", "compute vm create", "vm"),
                MakeTool("Delete a VM", "compute vm delete", "vm"),
            },
            RelatedContent = "## Related content\n\n- [Link](/path)"
        };

        var stitcher = new FamilyFileStitcher();
        var result = stitcher.Stitch(familyContent);

        Assert.Contains("## Disk", result);
        Assert.Contains("## VM", result);
        Assert.Contains("### Create a disk", result);
        Assert.Contains("### Delete a disk", result);
        Assert.Contains("### Create a VM", result);
        Assert.Contains("### Delete a VM", result);

        var diskIndex = result.IndexOf("## Disk");
        var vmIndex = result.IndexOf("## VM");
        Assert.True(diskIndex < vmIndex, "Disk group should appear before VM group");
    }

    [Fact]
    public void Stitch_MultiResourceWithThreeGroups_AllGroupsPresent()
    {
        var familyContent = new FamilyContent
        {
            FamilyName = "compute",
            Metadata = "---\ntitle: Compute\n---\n# Compute\n\nIntro.",
            Tools = new List<ToolContent>
            {
                MakeTool("Create disk", "compute disk create", "disk"),
                MakeTool("Create VM", "compute vm create", "vm"),
                MakeTool("Create VMSS", "compute vmss create", "vmss"),
            },
            RelatedContent = "## Related content"
        };

        var stitcher = new FamilyFileStitcher();
        var result = stitcher.Stitch(familyContent);

        Assert.Contains("## Disk", result);
        Assert.Contains("## VM", result);
        Assert.Contains("## VMSS", result);
        Assert.Contains("### Create disk", result);
        Assert.Contains("### Create VM", result);
        Assert.Contains("### Create VMSS", result);
    }

    [Fact]
    public void Stitch_MultiResourceWithSubHeadings_SubHeadingsDemoted()
    {
        var familyContent = new FamilyContent
        {
            FamilyName = "storage",
            Metadata = "---\ntitle: Storage\n---\n# Storage\n\nIntro.",
            Tools = new List<ToolContent>
            {
                MakeToolWithSubHeadings("Create account", "storage account create", "account"),
                MakeToolWithSubHeadings("Get blob", "storage blob get", "blob"),
            },
            RelatedContent = "## Related content"
        };

        var stitcher = new FamilyFileStitcher();
        var result = stitcher.Stitch(familyContent);

        Assert.Contains("### Create account", result);
        Assert.Contains("#### Parameters", result);
    }

    [Fact]
    public void Stitch_SingleResourceWithMultipleTools_NoGrouping()
    {
        var familyContent = new FamilyContent
        {
            FamilyName = "keyvault",
            Metadata = "---\ntitle: Key Vault\n---\n# Key Vault\n\nIntro.",
            Tools = new List<ToolContent>
            {
                MakeTool("Create secret", "keyvault secret create", "secret"),
                MakeTool("Get secret", "keyvault secret get", "secret"),
                MakeTool("Delete secret", "keyvault secret delete", "secret"),
            },
            RelatedContent = "## Related content"
        };

        var stitcher = new FamilyFileStitcher();
        var result = stitcher.Stitch(familyContent);

        Assert.Contains("## Create secret", result);
        Assert.Contains("## Get secret", result);
        Assert.Contains("## Delete secret", result);
        Assert.DoesNotContain("## Secret", result);
    }

    // --- Helpers ---

    private static ToolContent MakeTool(string toolName, string? command, string resourceType)
    {
        return new ToolContent
        {
            ToolName = toolName,
            FileName = $"{(command ?? toolName).Replace(' ', '-')}.md",
            FamilyName = "test",
            Content = $"## {toolName}\n\nDescription of {toolName}.",
            Command = command,
            ResourceType = resourceType
        };
    }

    private static ToolContent MakeToolWithSubHeadings(string toolName, string? command, string resourceType)
    {
        return new ToolContent
        {
            ToolName = toolName,
            FileName = $"{(command ?? toolName).Replace(' ', '-')}.md",
            FamilyName = "test",
            Content = $"## {toolName}\n\nDescription.\n\n### Parameters\n\n| Name | Type |\n|---|---|\n| foo | string |",
            Command = command,
            ResourceType = resourceType
        };
    }
}
