// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ToolFamilyCleanup.Services;
using Xunit;

namespace DocGeneration.Steps.ToolFamilyCleanup.Tests;

public class ChunkedGenerationValidatorTests
{
    // ════════════════════════════════════════════════════════════════════════
    //  Validation — correct output
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public void ValidateBatch_AllToolsPresent_ReturnsValid()
    {
        var expectedTools = new[] { "List vaults", "Get vault", "Create vault" };
        var content = string.Join("\n", new[]
        {
            "## Vault",
            "",
            "### List vaults",
            "Lists all vaults.",
            "",
            "### Get vault",
            "Gets a vault.",
            "",
            "### Create vault",
            "Creates a vault."
        });

        var result = ChunkedGenerationValidator.ValidateBatch(content, expectedTools);

        Assert.True(result.IsValid);
        Assert.Equal(3, result.ExpectedToolCount);
        Assert.Equal(3, result.ActualToolCount);
        Assert.Empty(result.MissingTools);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Validation — 16 expected, 8 actual → reports missing 8
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public void ValidateBatch_SixteenExpectedEightActual_ReportsMissingEight()
    {
        var expectedTools = Enumerable.Range(1, 16).Select(i => $"backup-tool-{i:D2}").ToList();
        var content = string.Join("\n",
            Enumerable.Range(1, 8).SelectMany(i => new[] { $"## backup-tool-{i:D2}", $"Content for backup-tool-{i:D2}.", "" }));

        var result = ChunkedGenerationValidator.ValidateBatch(content, expectedTools);

        Assert.False(result.IsValid);
        Assert.Equal(16, result.ExpectedToolCount);
        Assert.Equal(8, result.ActualToolCount);
        Assert.Equal(8, result.MissingTools.Count);
        Assert.Contains("backup-tool-09", result.MissingTools);
        Assert.Contains("backup-tool-16", result.MissingTools);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Validation — empty content
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public void ValidateBatch_EmptyContent_ReportsAllMissing()
    {
        var expectedTools = new[] { "List", "Get", "Delete" };

        var result = ChunkedGenerationValidator.ValidateBatch("", expectedTools);

        Assert.False(result.IsValid);
        Assert.Equal(3, result.MissingTools.Count);
        Assert.Contains("Generated content is empty", result.FailureReason!);
    }

    [Fact]
    public void ValidateBatch_NullContent_ReportsAllMissing()
    {
        var expectedTools = new[] { "List", "Get" };

        var result = ChunkedGenerationValidator.ValidateBatch(null!, expectedTools);

        Assert.False(result.IsValid);
        Assert.Equal(2, result.MissingTools.Count);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Truncation Detection
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public void IsTruncated_UnclosedCodeFence_ReturnsTrue()
    {
        var content = "## Tool\n\n```json\n{\"key\": \"value\"";

        Assert.True(ChunkedGenerationValidator.IsTruncated(content));
    }

    [Fact]
    public void IsTruncated_EndsWithHeading_ReturnsTrue()
    {
        var content = "## First tool\n\nContent here.\n\n## Second tool";

        Assert.True(ChunkedGenerationValidator.IsTruncated(content));
    }

    [Fact]
    public void IsTruncated_EndsWithIncompleteTableRow_ReturnsTrue()
    {
        var content = "## Tool\n\n| Name | Type |\n| --- | --- |\n| param1 |";

        Assert.True(ChunkedGenerationValidator.IsTruncated(content));
    }

    [Fact]
    public void IsTruncated_CompleteContent_ReturnsFalse()
    {
        var content = "## Tool\n\nComplete content with proper ending.\n";

        Assert.False(ChunkedGenerationValidator.IsTruncated(content));
    }

    [Fact]
    public void IsTruncated_EmptyContent_ReturnsFalse()
    {
        Assert.False(ChunkedGenerationValidator.IsTruncated(""));
        Assert.False(ChunkedGenerationValidator.IsTruncated(null!));
    }

    // ════════════════════════════════════════════════════════════════════════
    //  ValidateFinalOutput
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public void ValidateFinalOutput_AllToolsPresent_ReturnsValid()
    {
        var expectedTools = Enumerable.Range(1, 5).Select(i => $"tool-{i}").ToList();
        var content = string.Join("\n",
            Enumerable.Range(1, 5).SelectMany(i => new[] { $"## tool-{i}", $"Content.", "" }));

        var result = ChunkedGenerationValidator.ValidateFinalOutput(content, expectedTools);

        Assert.True(result.IsValid);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  CountToolSections
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public void CountToolSections_MixedH2AndH3_CountsH3WhenPresent()
    {
        var content = string.Join("\n", new[]
        {
            "## Resource group",
            "",
            "### Tool A",
            "Content.",
            "",
            "### Tool B",
            "Content.",
            "",
            "## Another group",
            "",
            "### Tool C",
            "Content."
        });

        var count = ChunkedGenerationValidator.CountToolSections(content);

        Assert.Equal(3, count);
    }

    [Fact]
    public void CountToolSections_OnlyH2_CountsH2ExcludingRelatedContent()
    {
        var content = string.Join("\n", new[]
        {
            "## List VMs",
            "Content.",
            "",
            "## Create VM",
            "Content.",
            "",
            "## Related content",
            "- Link"
        });

        var count = ChunkedGenerationValidator.CountToolSections(content);

        Assert.Equal(2, count);
    }
}
