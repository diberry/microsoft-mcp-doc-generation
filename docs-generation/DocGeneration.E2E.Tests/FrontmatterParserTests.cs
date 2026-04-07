// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using DocGeneration.E2E.Tests.Helpers;

namespace DocGeneration.E2E.Tests;

/// <summary>
/// Unit tests for the FrontmatterParser helper.
/// These run independently of the pipeline (no E2E fixture needed).
/// </summary>
[Trait("Category", "E2E")]
public sealed class FrontmatterParserTests
{
    [Fact]
    public void Parse_ValidFrontmatter_ReturnsFields()
    {
        var content = "---\nms.topic: include\nms.date: 2025-07-01\nmcp-cli.version: 1.0.0\n---\n\n# Content";
        var result = FrontmatterParser.Parse(content);

        Assert.NotNull(result);
        Assert.Equal("include", result["ms.topic"]);
        Assert.Equal("2025-07-01", result["ms.date"]);
        Assert.Equal("1.0.0", result["mcp-cli.version"]);
    }

    [Fact]
    public void Parse_FrontmatterWithComments_SkipsComments()
    {
        var content = "---\nms.topic: include\n# This is a comment\nms.date: 2025-07-01\n---\n";
        var result = FrontmatterParser.Parse(content);

        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Equal("include", result["ms.topic"]);
    }

    [Fact]
    public void Parse_NoFrontmatter_ReturnsNull()
    {
        var content = "# Just a markdown file\nWith some content.";
        var result = FrontmatterParser.Parse(content);

        Assert.Null(result);
    }

    [Fact]
    public void Parse_EmptyContent_ReturnsNull()
    {
        Assert.Null(FrontmatterParser.Parse(""));
        Assert.Null(FrontmatterParser.Parse(null!));
    }

    [Fact]
    public void Parse_MissingClosingDelimiter_ReturnsNull()
    {
        var content = "---\nms.topic: include\nms.date: 2025-07-01\n";
        var result = FrontmatterParser.Parse(content);

        Assert.Null(result);
    }

    [Fact]
    public void HasFrontmatter_ValidBlock_ReturnsTrue()
    {
        var content = "---\nms.topic: include\n---\n# Content";
        Assert.True(FrontmatterParser.HasFrontmatter(content));
    }

    [Fact]
    public void HasFrontmatter_NoBlock_ReturnsFalse()
    {
        var content = "# Just markdown\nNo frontmatter here.";
        Assert.False(FrontmatterParser.HasFrontmatter(content));
    }

    [Fact]
    public void Parse_FrontmatterWithGeneratedTimestamp_ParsesCorrectly()
    {
        var content = "---\nms.topic: include\nms.date: 2025-07-01\nmcp-cli.version: 1.2.3\ngenerated: 2025-07-01 12:00:00 UTC\n---\n";
        var result = FrontmatterParser.Parse(content);

        Assert.NotNull(result);
        Assert.Equal("2025-07-01 12:00:00 UTC", result["generated"]);
    }
}
