// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;

namespace DocGeneration.E2E.Tests;

/// <summary>
/// Unit tests for OutputStructureValidator logic, using temp directories
/// with known good/bad inputs to verify validator correctness.
/// </summary>
[Trait("Category", "E2E")]
public class OutputStructureValidatorTests : IDisposable
{
    private readonly string _testRoot;

    public OutputStructureValidatorTests()
    {
        _testRoot = Path.Combine(
            Path.GetTempPath(),
            $"e2e-validator-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
            Directory.Delete(_testRoot, recursive: true);
    }

    private string CreateOutputDir(string name = "test-namespace")
    {
        var dir = Path.Combine(_testRoot, name);
        Directory.CreateDirectory(dir);
        return dir;
    }

    private void SeedFile(string basePath, string relativePath, string content)
    {
        var fullPath = Path.Combine(basePath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }

    #region ValidateDirectoryStructure

    [Fact]
    public void ValidateDirectoryStructure_AllRequiredDirsPresent_Succeeds()
    {
        var outputPath = CreateOutputDir();
        Directory.CreateDirectory(Path.Combine(outputPath, "annotations"));
        Directory.CreateDirectory(Path.Combine(outputPath, "parameters"));
        Directory.CreateDirectory(Path.Combine(outputPath, "tool-family"));
        Directory.CreateDirectory(Path.Combine(outputPath, "tools"));

        var result = OutputStructureValidator.ValidateDirectoryStructure(outputPath);

        Assert.True(result.Success);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void ValidateDirectoryStructure_MissingToolFamily_ReportsIssue()
    {
        var outputPath = CreateOutputDir();
        Directory.CreateDirectory(Path.Combine(outputPath, "annotations"));
        Directory.CreateDirectory(Path.Combine(outputPath, "parameters"));
        Directory.CreateDirectory(Path.Combine(outputPath, "tools"));
        // tool-family deliberately missing

        var result = OutputStructureValidator.ValidateDirectoryStructure(outputPath);

        Assert.False(result.Success);
        Assert.Single(result.Issues);
        Assert.Contains("tool-family", result.Issues[0]);
    }

    [Fact]
    public void ValidateDirectoryStructure_NonexistentDir_ReportsIssue()
    {
        var result = OutputStructureValidator.ValidateDirectoryStructure(
            Path.Combine(_testRoot, "nonexistent"));

        Assert.False(result.Success);
        Assert.Single(result.Issues);
        Assert.Contains("does not exist", result.Issues[0]);
    }

    [Fact]
    public void ValidateDirectoryStructure_ExtraDirs_DoesNotFail()
    {
        var outputPath = CreateOutputDir();
        Directory.CreateDirectory(Path.Combine(outputPath, "annotations"));
        Directory.CreateDirectory(Path.Combine(outputPath, "parameters"));
        Directory.CreateDirectory(Path.Combine(outputPath, "tool-family"));
        Directory.CreateDirectory(Path.Combine(outputPath, "tools"));
        Directory.CreateDirectory(Path.Combine(outputPath, "logs"));
        Directory.CreateDirectory(Path.Combine(outputPath, "reports"));

        var result = OutputStructureValidator.ValidateDirectoryStructure(outputPath);

        Assert.True(result.Success);
    }

    #endregion

    #region ValidateNoLeakedTokens

    [Fact]
    public void ValidateNoLeakedTokens_CleanFiles_Succeeds()
    {
        var outputPath = CreateOutputDir();
        SeedFile(outputPath, "tool-family/service.md",
            "---\ntitle: Clean article\n---\n\n# Clean content\n\nNo tokens here.");

        var result = OutputStructureValidator.ValidateNoLeakedTokens(outputPath);

        Assert.True(result.Success);
    }

    [Fact]
    public void ValidateNoLeakedTokens_HandlebarsPlaceholder_DetectsLeak()
    {
        var outputPath = CreateOutputDir();
        SeedFile(outputPath, "tool-family/service.md",
            "---\ntitle: Test\n---\n\n# Title\n\n{{EXAMPLE_PROMPTS_CONTENT}}");

        var result = OutputStructureValidator.ValidateNoLeakedTokens(outputPath);

        Assert.False(result.Success);
        Assert.Single(result.Issues);
        Assert.Contains("{{EXAMPLE_PROMPTS_CONTENT}}", result.Issues[0]);
    }

    [Fact]
    public void ValidateNoLeakedTokens_TripleHandlebars_DetectsLeak()
    {
        var outputPath = CreateOutputDir();
        SeedFile(outputPath, "annotations/ann.md",
            "---\nms.topic: include\n---\n\n{{{RAW_CONTENT}}}");

        var result = OutputStructureValidator.ValidateNoLeakedTokens(outputPath);

        Assert.False(result.Success);
        // Triple braces match both {{...}} and {{{...}}} patterns
        Assert.True(result.Issues.Count >= 1);
    }

    [Fact]
    public void ValidateNoLeakedTokens_PipelineTokenPatterns_DetectsLeaks()
    {
        var outputPath = CreateOutputDir();
        SeedFile(outputPath, "annotations/ann.md",
            "---\nms.topic: include\n---\n\n<<<TPL_DESCRIPTION>>> and __TPL_TITLE__");

        var result = OutputStructureValidator.ValidateNoLeakedTokens(outputPath);

        Assert.False(result.Success);
        Assert.Equal(2, result.Issues.Count);
    }

    [Fact]
    public void ValidateNoLeakedTokens_IgnoresIntermediateDirs()
    {
        var outputPath = CreateOutputDir();
        // tools-raw is intermediate, should NOT be scanned
        SeedFile(outputPath, "tools-raw/raw.md",
            "---\nms.topic: reference\n---\n\n{{UNRESOLVED}}");

        var result = OutputStructureValidator.ValidateNoLeakedTokens(outputPath);

        Assert.True(result.Success, "Should not scan intermediate dirs like tools-raw");
    }

    [Fact]
    public void ValidateNoLeakedTokens_LowercaseHandlebars_DoesNotFalsePositive()
    {
        var outputPath = CreateOutputDir();
        // Normal Handlebars helper syntax like {{> partial}} should not trigger
        SeedFile(outputPath, "tool-family/service.md",
            "---\ntitle: Test\n---\n\nUse {{lowercase}} in your template.");

        var result = OutputStructureValidator.ValidateNoLeakedTokens(outputPath);

        Assert.True(result.Success,
            "Lowercase handlebars like {{lowercase}} should not trigger leaked token detection");
    }

    #endregion

    #region ValidateMarkdownFrontmatter

    [Fact]
    public void ValidateMarkdownFrontmatter_ValidFrontmatter_Succeeds()
    {
        var outputPath = CreateOutputDir();
        SeedFile(outputPath, "tool-family/service.md",
            "---\ntitle: My Service\nms.topic: concept-article\n---\n\n# My Service");

        var result = OutputStructureValidator.ValidateMarkdownFrontmatter(outputPath);

        Assert.True(result.Success);
    }

    [Fact]
    public void ValidateMarkdownFrontmatter_MissingClosingDashes_ReportsIssue()
    {
        var outputPath = CreateOutputDir();
        SeedFile(outputPath, "tool-family/broken.md",
            "---\ntitle: Broken\nms.topic: concept-article\n\n# Broken");

        var result = OutputStructureValidator.ValidateMarkdownFrontmatter(outputPath);

        Assert.False(result.Success);
        Assert.Contains("frontmatter", result.Issues[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateMarkdownFrontmatter_NoFrontmatter_ReportsIssue()
    {
        var outputPath = CreateOutputDir();
        SeedFile(outputPath, "tool-family/tool.md", "# Just a heading\n\nSome content.");

        var result = OutputStructureValidator.ValidateMarkdownFrontmatter(outputPath);

        Assert.False(result.Success);
    }

    [Fact]
    public void ValidateMarkdownFrontmatter_EmptyDir_Succeeds()
    {
        var outputPath = CreateOutputDir();
        // No files at all — nothing to validate
        var result = OutputStructureValidator.ValidateMarkdownFrontmatter(outputPath);

        Assert.True(result.Success);
    }

    #endregion

    #region ValidateFileIntegrity

    [Fact]
    public void ValidateFileIntegrity_NormalFiles_Succeeds()
    {
        var outputPath = CreateOutputDir();
        SeedFile(outputPath, "tool-family/good.md",
            "---\ntitle: Good File\n---\n\n# Good File\n\nThis has enough content to pass.");

        var result = OutputStructureValidator.ValidateFileIntegrity(outputPath);

        Assert.True(result.Success);
    }

    [Fact]
    public void ValidateFileIntegrity_EmptyFile_ReportsIssue()
    {
        var outputPath = CreateOutputDir();
        SeedFile(outputPath, "annotations/empty.md", "");

        var result = OutputStructureValidator.ValidateFileIntegrity(outputPath);

        Assert.False(result.Success);
        Assert.Contains("Empty file", result.Issues[0]);
    }

    [Fact]
    public void ValidateFileIntegrity_TruncatedFile_ReportsIssue()
    {
        var outputPath = CreateOutputDir();
        SeedFile(outputPath, "annotations/truncated.md", "---\ntitle: X\n---");

        var result = OutputStructureValidator.ValidateFileIntegrity(outputPath);

        Assert.False(result.Success);
        Assert.Contains("Suspiciously short", result.Issues[0]);
    }

    #endregion

    #region ValidateToolCount

    [Fact]
    public void ValidateToolCount_MatchingCount_Succeeds()
    {
        var outputPath = CreateOutputDir();
        var content = @"---
title: Azure MCP Server tools for Test Service
tool_count: 3
ms.topic: concept-article
---

# Test Service

Overview text.

## Create resource

<!-- @mcpcli test resource create -->

Description.

## Delete resource

<!-- @mcpcli test resource delete -->

Description.

## Get resource

<!-- @mcpcli test resource get -->

Description.
";
        SeedFile(outputPath, "tool-family/test-service.md", content);

        var result = OutputStructureValidator.ValidateToolCount(outputPath);

        Assert.True(result.Success);
    }

    [Fact]
    public void ValidateToolCount_MismatchedCount_ReportsIssue()
    {
        var outputPath = CreateOutputDir();
        var content = @"---
title: Test Service
tool_count: 5
---

# Test Service

## Create resource

<!-- @mcpcli test resource create -->

## Delete resource

<!-- @mcpcli test resource delete -->
";
        SeedFile(outputPath, "tool-family/test-service.md", content);

        var result = OutputStructureValidator.ValidateToolCount(outputPath);

        Assert.False(result.Success);
        Assert.Contains("mismatch", result.Issues[0], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("5", result.Issues[0]);
        Assert.Contains("2", result.Issues[0]);
    }

    [Fact]
    public void ValidateToolCount_NoToolCountInFrontmatter_ReportsIssue()
    {
        var outputPath = CreateOutputDir();
        SeedFile(outputPath, "tool-family/missing.md",
            "---\ntitle: Missing Count\n---\n\n# Missing\n\n## Tool\n\n<!-- @mcpcli test tool -->");

        var result = OutputStructureValidator.ValidateToolCount(outputPath);

        Assert.False(result.Success);
        Assert.Contains("No tool_count", result.Issues[0]);
    }

    [Fact]
    public void ValidateToolCount_NoToolFamilyDir_ReportsIssue()
    {
        var outputPath = CreateOutputDir();
        // No tool-family directory

        var result = OutputStructureValidator.ValidateToolCount(outputPath);

        Assert.False(result.Success);
        Assert.Contains("tool-family directory not found", result.Issues[0]);
    }

    #endregion

    #region Internal Helper Methods

    [Theory]
    [InlineData("---\ntitle: Test\n---\n\nContent", true)]
    [InlineData("---\ntitle: Test\nms.topic: include\n---", true)]
    [InlineData("# No frontmatter", false)]
    [InlineData("", false)]
    [InlineData("---\nunclosed frontmatter", false)]
    [InlineData("  ---\ntitle: Test\n---", true)]  // Leading whitespace OK
    public void HasValidFrontmatter_VariousInputs(string content, bool expected)
    {
        Assert.Equal(expected, OutputStructureValidator.HasValidFrontmatter(content));
    }

    [Theory]
    [InlineData("---\ntool_count: 12\n---", 12)]
    [InlineData("---\ntitle: T\ntool_count: 0\n---", 0)]
    [InlineData("---\ntitle: T\n---", null)]
    [InlineData("tool_count: not_a_number", null)]
    public void ExtractToolCountFromFrontmatter_VariousInputs(string content, int? expected)
    {
        Assert.Equal(expected, OutputStructureValidator.ExtractToolCountFromFrontmatter(content));
    }

    [Theory]
    [InlineData("<!-- @mcpcli compute disk create -->", 1)]
    [InlineData("No markers here", 0)]
    [InlineData("<!-- @mcpcli a b -->\n<!-- @mcpcli c d -->", 2)]
    [InlineData("<!--  @mcpcli  spaced  out  -->", 1)]  // Extra whitespace
    public void CountMcpCliMarkers_VariousInputs(string content, int expected)
    {
        Assert.Equal(expected, OutputStructureValidator.CountMcpCliMarkers(content));
    }

    #endregion

    #region ValidationResult

    [Fact]
    public void ValidationResult_EmptyResult_IsSuccess()
    {
        var result = new ValidationResult();
        Assert.True(result.Success);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void ValidationResult_WithIssue_IsNotSuccess()
    {
        var result = new ValidationResult();
        result.AddIssue("test issue");
        Assert.False(result.Success);
        Assert.Single(result.Issues);
    }

    [Fact]
    public void ValidationResult_Merge_CombinesIssues()
    {
        var r1 = new ValidationResult();
        r1.AddIssue("issue 1");
        var r2 = new ValidationResult();
        r2.AddIssue("issue 2");
        r2.AddIssue("issue 3");

        r1.Merge(r2);

        Assert.Equal(3, r1.Issues.Count);
    }

    #endregion
}
