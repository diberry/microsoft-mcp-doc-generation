using FluentAssertions;
using SkillsGen.Core.Cataloging;
using Xunit;

namespace SkillsGen.Core.Tests.Unit.Cataloging;

public class SourceOutlineCatalogerTests
{
    private readonly SourceOutlineCataloger _cataloger = new();

    [Fact]
    public void Catalog_HappyPath_ExtractsH2AndH3Headings()
    {
        var content = """
            # Top level heading

            ## Prerequisites

            Some text.

            ### Required Roles

            More text.

            ## Use cases

            Content.
            """;

        var result = _cataloger.Catalog("azure-compute", content);

        result.Headings.Should().HaveCount(3);
        result.Headings[0].Level.Should().Be(2);
        result.Headings[0].Text.Should().Be("Prerequisites");
        result.Headings[0].MappedTo.Should().Be("Prerequisites");

        result.Headings[1].Level.Should().Be(3);
        result.Headings[1].Text.Should().Be("Required Roles");
        result.Headings[1].MappedTo.Should().Be("Prerequisites (RBAC sub-section)");

        result.Headings[2].Level.Should().Be(2);
        result.Headings[2].Text.Should().Be("Use cases");
        result.Headings[2].MappedTo.Should().Be("When to use this skill");
    }

    [Fact]
    public void Catalog_UnmappedHeadings_SetsNullMappedToAndCountsCorrectly()
    {
        var content = """
            ## New Section

            ## Another Unknown

            ## Prerequisites
            """;

        var result = _cataloger.Catalog("azure-storage", content);

        result.UnmappedCount.Should().Be(2);
        result.Headings.Should().HaveCount(3);
        result.Headings[0].MappedTo.Should().BeNull();
        result.Headings[1].MappedTo.Should().BeNull();
        result.Headings[2].MappedTo.Should().Be("Prerequisites");
    }

    [Fact]
    public void Catalog_EmptyContent_ReturnsEmptyOutline()
    {
        var result = _cataloger.Catalog("azure-empty", "");

        result.Headings.Should().BeEmpty();
        result.UnmappedCount.Should().Be(0);
    }

    [Fact]
    public void Catalog_NullContent_ReturnsEmptyOutline()
    {
        var result = _cataloger.Catalog("azure-null", null!);

        result.Headings.Should().BeEmpty();
        result.UnmappedCount.Should().Be(0);
    }

    [Fact]
    public void Catalog_MalformedHeadings_SkipsMalformedRetainsValid()
    {
        var content = """
            ## 

            ### 

            ## Prerequisites
            """;

        var result = _cataloger.Catalog("azure-test", content);

        // Only the valid heading should be returned
        result.Headings.Should().HaveCount(1);
        result.Headings[0].Text.Should().Be("Prerequisites");
    }

    [Fact]
    public void Catalog_HeadingsInsideCodeBlocks_AreSkipped()
    {
        var content = """
            ## Real Heading

            ```
            ## This is inside a code block
            ### Also inside
            ```

            ## Prerequisites
            """;

        var result = _cataloger.Catalog("azure-code", content);

        result.Headings.Should().HaveCount(2);
        result.Headings[0].Text.Should().Be("Real Heading");
        result.Headings[1].Text.Should().Be("Prerequisites");
    }

    [Fact]
    public void Catalog_DoesNotExtractH1Headings()
    {
        var content = """
            # Top Level

            ## Prerequisites
            """;

        var result = _cataloger.Catalog("azure-test", content);

        result.Headings.Should().HaveCount(1);
        result.Headings[0].Level.Should().Be(2);
    }

    [Fact]
    public void Catalog_SetsH2Level2AndH3Level3()
    {
        var content = """
            ## Section A

            ### Sub-section B
            """;

        var result = _cataloger.Catalog("azure-levels", content);

        result.Headings[0].Level.Should().Be(2);
        result.Headings[1].Level.Should().Be(3);
    }

    [Fact]
    public void Catalog_ExcludedHeadings_MappedToNullButCountedAsKnown()
    {
        var content = """
            ## Workflow

            ## Prerequisites
            """;

        var result = _cataloger.Catalog("azure-workflow", content);

        // "Workflow" is known (excluded), "Prerequisites" is known (mapped)
        // Neither is "unmapped" — unmapped = not in Rules at all
        result.UnmappedCount.Should().Be(0);
        result.Headings[0].MappedTo.Should().BeNull(); // excluded
        result.Headings[1].MappedTo.Should().Be("Prerequisites");
    }

    [Fact]
    public void Catalog_SetsTimestamp()
    {
        var before = DateTime.UtcNow;
        var result = _cataloger.Catalog("azure-test", "## Prerequisites");
        var after = DateTime.UtcNow;

        result.CatalogedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }
}
