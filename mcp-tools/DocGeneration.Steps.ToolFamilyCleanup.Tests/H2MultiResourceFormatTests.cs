// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ToolFamilyCleanup.Models;
using ToolFamilyCleanup.Services;
using Xunit;

namespace DocGeneration.Steps.ToolFamilyCleanup.Tests;

/// <summary>
/// TDD tests for Issue #416, Item 1: H2 format for multi-resource pages.
/// When a namespace has multiple resource types (e.g., compute has disk/VM/VMSS),
/// generated H2 tool headings should use "Resource type: action" format
/// (e.g., "Managed disk: create") instead of "Action resource" format.
/// Single-resource namespaces keep the current "Action resource" format unchanged.
///
/// Note: Basic multi-resource detection, grouping, and heading demotion are covered
/// in ResourceGroupingTests.cs. These tests focus specifically on the H2 TEXT FORMAT.
/// </summary>
public class H2MultiResourceFormatTests
{
    // ── Multi-resource H2 tool heading: "Resource type: action" format ──

    [Fact]
    public void FormatMultiResourceToolH2_UsesResourceTypeColonAction()
    {
        // #416: Multi-resource pages should format tool H2 as "Resource type: action"
        // e.g., "Managed disk: create" instead of "Create a managed disk"
        var resourceType = "disk";
        var action = "create";

        var result = MultiResourceH2Formatter.FormatToolHeading(resourceType, action);

        Assert.Equal("Managed disk: create", result);
    }

    [Fact]
    public void FormatMultiResourceToolH2_VMResource_UsesAcronymDisplayName()
    {
        var result = MultiResourceH2Formatter.FormatToolHeading("vm", "list");

        Assert.Equal("VM: list", result);
    }

    [Fact]
    public void FormatMultiResourceToolH2_VMSSResource_UsesAcronymDisplayName()
    {
        var result = MultiResourceH2Formatter.FormatToolHeading("vmss", "create");

        Assert.Equal("VMSS: create", result);
    }

    [Theory]
    [InlineData("disk", "create", "Managed disk: create")]
    [InlineData("disk", "list", "Managed disk: list")]
    [InlineData("disk", "get", "Managed disk: get")]
    [InlineData("vm", "create", "VM: create")]
    [InlineData("vmss", "scale", "VMSS: scale")]
    [InlineData("account", "create", "Account: create")]
    [InlineData("blob", "upload", "Blob: upload")]
    public void FormatMultiResourceToolH2_VariousActions_FormatsCorrectly(
        string resourceType, string action, string expected)
    {
        var result = MultiResourceH2Formatter.FormatToolHeading(resourceType, action);

        Assert.Equal(expected, result);
    }

    // ── Single-resource retains "Action resource" format ────────────

    [Fact]
    public void FormatSingleResourceToolH2_KeepsActionFirstFormat()
    {
        // Single-resource namespaces keep the existing "Action resource" format
        var toolName = "Create key vault";

        var result = SingleResourceH2Formatter.FormatToolHeading(toolName);

        Assert.Equal("Create key vault", result);
    }

    [Theory]
    [InlineData("Create key vault")]
    [InlineData("List key vaults")]
    [InlineData("Delete secret")]
    public void FormatSingleResourceToolH2_PreservesExistingHeading(string heading)
    {
        var result = SingleResourceH2Formatter.FormatToolHeading(heading);

        Assert.Equal(heading, result);
    }

    // ── Detection of multi-resource namespaces from tool metadata ──

    [Fact]
    public void DetectMultiResource_SameNamespace_DifferentResources_ReturnsFalse()
    {
        // Compute has disk, vm, vmss — but it's ONE namespace, NOT multi-resource
        var tools = new List<ToolContent>
        {
            MakeTool("disk", "compute disk create"),
            MakeTool("vm", "compute vm list"),
            MakeTool("vmss", "compute vmss scale"),
        };

        Assert.False(FamilyFileStitcher.IsMultiResourceFamily(tools));
    }

    [Fact]
    public void DetectMultiResource_FromToolMetadata_SingleNamespaceIsNot()
    {
        var tools = new List<ToolContent>
        {
            MakeTool("secret", "keyvault secret create"),
            MakeTool("secret", "keyvault secret get"),
        };

        Assert.False(FamilyFileStitcher.IsMultiResourceFamily(tools));
    }

    [Fact]
    public void DetectMultiResource_DifferentNamespaces_ReturnsTrue()
    {
        // Multi-resource = multiple namespaces combined in one file
        var tools = new List<ToolContent>
        {
            new ToolContent
            {
                ToolName = "disk create",
                FileName = "azure-compute-disk-create.complete.md",
                FamilyName = "compute",
                Content = "## Create disk\n\nDescription.",
                Command = "compute disk create",
                Description = "Create a disk.",
                ResourceType = "disk",
            },
            new ToolContent
            {
                ToolName = "account create",
                FileName = "azure-storage-account-create.complete.md",
                FamilyName = "storage",
                Content = "## Create account\n\nDescription.",
                Command = "storage account create",
                Description = "Create an account.",
                ResourceType = "account",
            },
        };

        Assert.True(FamilyFileStitcher.IsMultiResourceFamily(tools));
    }

    // ── Integration: Stitch uses correct format per family type ────

    [Fact]
    public void Stitch_MultiResource_ToolH3sUseResourceTypeColonActionFormat()
    {
        // This test validates the END-TO-END integration:
        // Multi-resource = tools from different namespaces in one file.
        // When stitching, the generated H3 tool headings use "Resource type: action" format.
        var familyContent = new FamilyContent
        {
            FamilyName = "mixed",
            Metadata = "---\ntitle: Mixed\n---\n# Mixed\n\nIntro paragraph.",
            Tools = new List<ToolContent>
            {
                new ToolContent
                {
                    ToolName = "disk create",
                    FileName = "azure-compute-disk-create.complete.md",
                    FamilyName = "compute",
                    Content = "## Create disk\n\nDescription of create disk.",
                    Command = "compute disk create",
                    Description = "Create a disk.",
                    ResourceType = "disk",
                },
                new ToolContent
                {
                    ToolName = "vm list",
                    FileName = "azure-compute-vm-list.complete.md",
                    FamilyName = "network",
                    Content = "## List VM\n\nDescription of list vm.",
                    Command = "network vm list",
                    Description = "List VMs.",
                    ResourceType = "vm",
                },
            },
            RelatedContent = "## Related content\n\n- [Link](/path)"
        };

        var stitcher = new FamilyFileStitcher();
        var result = stitcher.Stitch(familyContent);

        // Multi-resource: tool headings should be demoted and use resource type format
        Assert.Contains("### Managed disk: create", result);
        Assert.Contains("### VM: list", result);
    }

    // ── Helpers ───────────────────────────────────────────────────

    private static ToolContent MakeTool(string resourceType, string command)
    {
        var parts = command.Split(' ');
        var action = parts.Last();
        return new ToolContent
        {
            ToolName = $"{resourceType} {action}",
            FileName = $"azure-{command.Replace(' ', '-')}.complete.md",
            FamilyName = parts.First(),
            Content = $"## {char.ToUpper(action[0])}{action[1..]} {resourceType}\n\nDescription of {action} {resourceType}.",
            Command = command,
            Description = $"{char.ToUpper(action[0])}{action[1..]} a {resourceType}.",
            ResourceType = resourceType,
        };
    }
}
