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
        // #416: Multi-resource pages should format tool H2 as "Resource type: Action"
        // e.g., "Managed disk: Create" instead of "Create a managed disk"
        var resourceType = "disk";
        var action = "create";

        var result = MultiResourceH2Formatter.FormatToolHeading(resourceType, action);

        Assert.Equal("Managed disk: Create", result);
    }

    [Fact]
    public void FormatMultiResourceToolH2_VMResource_UsesTitleCasedDisplayName()
    {
        // "vm" is no longer treated as an acronym — overrides handle the full display name.
        // Fallback title-cases: "Vm: List"
        var result = MultiResourceH2Formatter.FormatToolHeading("vm", "list");

        Assert.Equal("Vm: List", result);
    }

    [Fact]
    public void FormatMultiResourceToolH2_VMSSResource_UsesTitleCasedDisplayName()
    {
        // "vmss" is no longer treated as an acronym — overrides handle the full display name.
        // Fallback title-cases: "Vmss: Create"
        var result = MultiResourceH2Formatter.FormatToolHeading("vmss", "create");

        Assert.Equal("Vmss: Create", result);
    }

    [Theory]
    [InlineData("disk", "create", "Managed disk: Create")]
    [InlineData("disk", "list", "Managed disk: List")]
    [InlineData("disk", "get", "Managed disk: Get")]
    [InlineData("vm", "create", "Vm: Create")]
    [InlineData("vmss", "scale", "Vmss: Scale")]
    [InlineData("account", "create", "Account: Create")]
    [InlineData("blob", "upload", "Blob: Upload")]
    [InlineData("disasterrecovery", "enable-crr", "Disasterrecovery: Enable crr")]
    [InlineData("governance", "soft-delete", "Governance: Soft delete")]
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
    public void DetectMultiResource_FromToolMetadata_ComputeIsMultiResource()
    {
        var tools = new List<ToolContent>
        {
            MakeTool("disk", "compute disk create"),
            MakeTool("vm", "compute vm list"),
            MakeTool("vmss", "compute vmss scale"),
        };

        Assert.True(FamilyFileStitcher.IsMultiResourceFamily(tools));
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

    // ── Integration: Stitch uses correct format per family type ────

    [Fact]
    public void Stitch_MultiResource_ToolH2sUseResourceTypeColonActionFormat()
    {
        // This test validates the END-TO-END integration:
        // When stitching a multi-resource family, the generated H2 tool headings
        // should use "Resource type: action" format, not "Action resource" format.
        // "compute disk create" is in the override file → "Managed disk: Create"
        // "compute vm get" is in the override file → "Virtual machine: List or get"
        var familyContent = new FamilyContent
        {
            FamilyName = "compute",
            Metadata = "---\ntitle: Compute\n---\n# Compute\n\nIntro paragraph.",
            Tools = new List<ToolContent>
            {
                MakeTool("disk", "compute disk create"),
                MakeTool("vm", "compute vm get"),
            },
            RelatedContent = "## Related content\n\n- [Link](/path)"
        };

        var stitcher = new FamilyFileStitcher();
        var result = stitcher.Stitch(familyContent);

        // Multi-resource: tool headings are H2, with overridden display names
        Assert.Contains("## Managed disk: Create", result);
        Assert.Contains("## Virtual machine: List or get", result);
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
