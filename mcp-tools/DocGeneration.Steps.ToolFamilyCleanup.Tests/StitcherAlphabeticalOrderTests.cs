// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ToolFamilyCleanup.Models;
using ToolFamilyCleanup.Services;
using Xunit;

namespace DocGeneration.Steps.ToolFamilyCleanup.Tests;

/// <summary>
/// Tests verifying that the stitcher outputs tools in alphabetical order (#501).
/// </summary>
public class StitcherAlphabeticalOrderTests
{
    private static FamilyContent CreateFamilyWithTools(params (string name, string content)[] tools)
    {
        return new FamilyContent
        {
            FamilyName = "test",
            Metadata = "---\ntitle: Test\n---\n\n# Test\n\nIntro paragraph.",
            Tools = tools.Select(t => new ToolContent
            {
                ToolName = t.name,
                FileName = $"{t.name.Replace(" ", "-")}.md",
                FamilyName = "test",
                Content = t.content
            }).ToList(),
            RelatedContent = "## Related content"
        };
    }

    [Fact]
    public void Stitch_SingleResource_ToolsAppearInAlphabeticalOrder()
    {
        // Arrange: tools in reverse alphabetical order
        var content = CreateFamilyWithTools(
            ("zoo list", "## Zoo list\n\nLists zoo items."),
            ("alpha create", "## Alpha create\n\nCreates alpha items."),
            ("middle get", "## Middle get\n\nGets middle items."));

        var stitcher = new FamilyFileStitcher();

        // Act
        var result = stitcher.Stitch(content);

        // Assert: tools should appear alphabetically
        var alphaPos = result.IndexOf("## Alpha create");
        var middlePos = result.IndexOf("## Middle get");
        var zooPos = result.IndexOf("## Zoo list");

        Assert.True(alphaPos < middlePos, "Alpha should appear before Middle");
        Assert.True(middlePos < zooPos, "Middle should appear before Zoo");
    }

    [Fact]
    public void Stitch_SingleResource_CaseInsensitiveOrdering()
    {
        // Arrange: mixed case tool names
        var content = CreateFamilyWithTools(
            ("Zebra list", "## Zebra list\n\nDesc."),
            ("apple create", "## apple create\n\nDesc."),
            ("Banana get", "## Banana get\n\nDesc."));

        var stitcher = new FamilyFileStitcher();

        // Act
        var result = stitcher.Stitch(content);

        // Assert: case-insensitive alphabetical order
        var applePos = result.IndexOf("## apple create");
        var bananaPos = result.IndexOf("## Banana get");
        var zebraPos = result.IndexOf("## Zebra list");

        Assert.True(applePos < bananaPos, "apple should appear before Banana (case-insensitive)");
        Assert.True(bananaPos < zebraPos, "Banana should appear before Zebra");
    }

    [Fact]
    public void Stitch_SingleResource_IdenticalOutputOnRepeatedRuns()
    {
        // Arrange
        var content = CreateFamilyWithTools(
            ("zoo list", "## Zoo list\n\nDesc."),
            ("alpha create", "## Alpha create\n\nDesc."),
            ("middle get", "## Middle get\n\nDesc."));

        var stitcher = new FamilyFileStitcher();

        // Act: run multiple times
        var result1 = stitcher.Stitch(content);
        var result2 = stitcher.Stitch(content);
        var result3 = stitcher.Stitch(content);

        // Assert: byte-for-byte identical
        Assert.Equal(result1, result2);
        Assert.Equal(result2, result3);
    }

    [Fact]
    public void Stitch_SingleResource_DifferentInputOrderSameOutput()
    {
        // Arrange: same tools in different input orders
        var tools = new[]
        {
            ("alpha create", "## Alpha create\n\nDesc."),
            ("middle get", "## Middle get\n\nDesc."),
            ("zoo list", "## Zoo list\n\nDesc.")
        };

        var contentForward = CreateFamilyWithTools(tools);
        var contentReversed = CreateFamilyWithTools(tools.Reverse().ToArray());

        var stitcher = new FamilyFileStitcher();

        // Act
        var resultForward = stitcher.Stitch(contentForward);
        var resultReversed = stitcher.Stitch(contentReversed);

        // Assert: same output regardless of input order
        Assert.Equal(resultForward, resultReversed);
    }

    [Fact]
    public void Stitch_MultiResource_ToolsSortedWithinEachGroup()
    {
        // Arrange: multi-resource family with unsorted tools within groups
        // Use body text markers to detect order since headings get reformatted
        var content = new FamilyContent
        {
            FamilyName = "compute",
            Metadata = "---\ntitle: Test\n---\n\n# Test\n\nIntro paragraph.",
            Tools =
            [
                new ToolContent { ToolName = "vm delete", FileName = "vm-delete.md", FamilyName = "compute", Content = "## VM delete\n\nDELETE_MARKER content.", Command = "compute vm delete", ResourceType = "vm" },
                new ToolContent { ToolName = "vm create", FileName = "vm-create.md", FamilyName = "compute", Content = "## VM create\n\nCREATE_MARKER content.", Command = "compute vm create", ResourceType = "vm" },
                new ToolContent { ToolName = "disk list", FileName = "disk-list.md", FamilyName = "compute", Content = "## Disk list\n\nLIST_MARKER content.", Command = "compute disk list", ResourceType = "disk" },
                new ToolContent { ToolName = "disk create", FileName = "disk-create.md", FamilyName = "compute", Content = "## Disk create\n\nDISK_CREATE_MARKER content.", Command = "compute disk create", ResourceType = "disk" }
            ],
            RelatedContent = "## Related content"
        };

        var stitcher = new FamilyFileStitcher();

        // Act
        var result = stitcher.Stitch(content);

        // Assert: within the VM group, "vm create" before "vm delete" (sorted alphabetically)
        var createPos = result.IndexOf("CREATE_MARKER");
        var deletePos = result.IndexOf("DELETE_MARKER");
        Assert.True(createPos < deletePos, "vm create should appear before vm delete within the VM group");

        // Assert: within the Disk group, "disk create" before "disk list" (sorted alphabetically)
        var diskCreatePos = result.IndexOf("DISK_CREATE_MARKER");
        var diskListPos = result.IndexOf("LIST_MARKER");
        Assert.True(diskCreatePos < diskListPos, "disk create should appear before disk list within the Disk group");
    }

    [Fact]
    public void Stitch_SameNamespace_FlatH2sAlphabetical()
    {
        // Arrange: same namespace with multiple resource types → treated as single-resource (flat H2s)
        // Alphabetical order by ToolName applies
        var content = new FamilyContent
        {
            FamilyName = "compute",
            Metadata = "---\ntitle: Test\n---\n\n# Test\n\nIntro paragraph.",
            Tools =
            [
                new ToolContent { ToolName = "vm list", FileName = "vm-list.md", FamilyName = "compute", Content = "## VM list\n\nLists VMs.", Command = "compute vm list", ResourceType = "vm" },
                new ToolContent { ToolName = "disk list", FileName = "disk-list.md", FamilyName = "compute", Content = "## Disk list\n\nLists disks.", Command = "compute disk list", ResourceType = "disk" }
            ],
            RelatedContent = "## Related content"
        };

        var stitcher = new FamilyFileStitcher();

        // Act
        var result = stitcher.Stitch(content);

        // Assert: flat H2s for each tool, alphabetical (Disk before VM)
        var diskPos = result.IndexOf("## Disk list");
        var vmPos = result.IndexOf("## VM list");
        Assert.True(diskPos >= 0, "Disk list should appear as flat H2");
        Assert.True(vmPos >= 0, "VM list should appear as flat H2");
        Assert.True(diskPos < vmPos, "Alphabetical order: Disk before VM");
        // No resource group headers
        Assert.DoesNotContain("\n## VM\n", result);
        Assert.DoesNotContain("\n## Disk\n", result);
    }

    [Fact]
    public void Stitch_SingleResource_TieBreakOnFileName()
    {
        // Arrange: tools with same name (case-insensitive) but different filenames
        var content = new FamilyContent
        {
            FamilyName = "test",
            Metadata = "---\ntitle: Test\n---\n\n# Test\n\nIntro paragraph.",
            Tools =
            [
                new ToolContent { ToolName = "list", FileName = "b-list.md", FamilyName = "test", Content = "## List B\n\nFrom B." },
                new ToolContent { ToolName = "list", FileName = "a-list.md", FamilyName = "test", Content = "## List A\n\nFrom A." }
            ],
            RelatedContent = "## Related content"
        };

        var stitcher = new FamilyFileStitcher();

        // Act
        var result = stitcher.Stitch(content);

        // Assert: tie-break by filename — a-list.md before b-list.md
        var posA = result.IndexOf("## List A");
        var posB = result.IndexOf("## List B");
        Assert.True(posA < posB, "When ToolNames match, should tie-break by FileName");
    }
}
