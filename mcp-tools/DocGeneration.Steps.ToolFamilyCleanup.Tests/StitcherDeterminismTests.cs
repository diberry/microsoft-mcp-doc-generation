// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ToolFamilyCleanup.Models;
using ToolFamilyCleanup.Services;
using Xunit;

namespace DocGeneration.Steps.ToolFamilyCleanup.Tests;

/// <summary>
/// Determinism tests - verifies that the same inputs produce identical output
/// across multiple runs, ensuring no non-deterministic side effects.
/// Fixes: PR #519 review (Statler) - determinism validation.
/// </summary>
public class StitcherDeterminismTests
{
    private static readonly DateTime FixedDate = new(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);
    private static Func<DateTime> FixedClock => () => FixedDate;

    [Fact]
    public void Stitch_SameInputs_ProducesIdenticalOutput_AcrossMultipleRuns()
    {
        var generator = new DeterministicFrontmatterGenerator(FixedClock);
        var header = generator.Generate("Azure Compute", 3, "1.0.0+abc", "Manage compute resources.");
        var metadata = generator.Assemble(header, "The Azure MCP Server manages compute resources.\n\nUse these tools to manage VMs.");

        var familyContent = new FamilyContent
        {
            FamilyName = "compute",
            Metadata = metadata,
            Tools =
            [
                new ToolContent
                {
                    ToolName = "Create virtual machine",
                    FileName = "azure-compute-vm-create.md",
                    FamilyName = "compute",
                    Content = "## Create virtual machine\n<!-- @mcpcli compute vm create -->\n\nCreates a new virtual machine.",
                    Command = "compute vm create",
                    Description = "Creates a new virtual machine."
                },
                new ToolContent
                {
                    ToolName = "List virtual machines",
                    FileName = "azure-compute-vm-list.md",
                    FamilyName = "compute",
                    Content = "## List virtual machines\n<!-- @mcpcli compute vm list -->\n\nLists all virtual machines.",
                    Command = "compute vm list",
                    Description = "Lists all virtual machines."
                },
                new ToolContent
                {
                    ToolName = "Delete virtual machine",
                    FileName = "azure-compute-vm-delete.md",
                    FamilyName = "compute",
                    Content = "## Delete virtual machine\n<!-- @mcpcli compute vm delete -->\n\nDeletes a virtual machine.",
                    Command = "compute vm delete",
                    Description = "Deletes a virtual machine."
                }
            ],
            RelatedContent = "## Related content\n\n- [Azure Compute overview](/azure/compute/overview)"
        };

        var stitcher = new FamilyFileStitcher();

        var results = Enumerable.Range(0, 5)
            .Select(_ => stitcher.Stitch(familyContent))
            .ToList();

        var baseline = results[0];
        Assert.NotEmpty(baseline);

        for (int i = 1; i < results.Count; i++)
        {
            Assert.Equal(baseline, results[i]);
        }
    }

    [Fact]
    public void Stitch_DeterministicFrontmatter_SameClockProducesSameDate()
    {
        var gen1 = new DeterministicFrontmatterGenerator(FixedClock);
        var gen2 = new DeterministicFrontmatterGenerator(FixedClock);

        var header1 = gen1.Generate("Azure Storage", 2, "2.0.0", "Storage tools.");
        var header2 = gen2.Generate("Azure Storage", 2, "2.0.0", "Storage tools.");

        Assert.Equal(header1, header2);
    }
}