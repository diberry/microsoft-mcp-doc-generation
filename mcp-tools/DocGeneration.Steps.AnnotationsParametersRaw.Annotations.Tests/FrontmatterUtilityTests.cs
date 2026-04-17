// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using CSharpGenerator.Generators;
using Xunit;

namespace CSharpGenerator.Tests;

/// <summary>
/// Tests FrontmatterUtility.GenerateAnnotationFrontmatter and GenerateParameterFrontmatter.
/// Priority: P1 — frontmatter is prepended to every generated include file.
/// </summary>
public class FrontmatterUtilityTests
{
    // ── Annotation Frontmatter ─────────────────────────────────

    [Fact]
    public void GenerateAnnotationFrontmatter_ContainsYamlMarkers()
    {
        var result = FrontmatterUtility.GenerateAnnotationFrontmatter(
            "storage account list", "1.0.0", "storage-account-list-annotations.md");

        Assert.StartsWith("---", result);
        Assert.Contains("---", result.Substring(3)); // closing marker
    }

    [Fact]
    public void GenerateAnnotationFrontmatter_ContainsMsTopic()
    {
        var result = FrontmatterUtility.GenerateAnnotationFrontmatter(
            "storage account list", "1.0.0", "storage-account-list-annotations.md");

        Assert.Contains("ms.topic: include", result);
    }

    [Fact]
    public void GenerateAnnotationFrontmatter_ContainsVersion()
    {
        var result = FrontmatterUtility.GenerateAnnotationFrontmatter(
            "storage account list", "2.5.0", "test.md");

        Assert.Contains("mcp-cli.version: 2.5.0", result);
    }

    [Fact]
    public void GenerateAnnotationFrontmatter_NullVersion_ShowsUnknown()
    {
        var result = FrontmatterUtility.GenerateAnnotationFrontmatter(
            "storage account list", null, "test.md");

        Assert.Contains("mcp-cli.version: unknown", result);
    }

    [Fact]
    public void GenerateAnnotationFrontmatter_ContainsIncludeComment()
    {
        var result = FrontmatterUtility.GenerateAnnotationFrontmatter(
            "storage account list", "1.0.0", "storage-list-annotations.md");

        Assert.Contains("# [!INCLUDE [storage account list](../includes/tools/annotations/storage-list-annotations.md)]", result);
    }

    [Fact]
    public void GenerateAnnotationFrontmatter_ContainsAzmcpComment()
    {
        var result = FrontmatterUtility.GenerateAnnotationFrontmatter(
            "storage account list", "1.0.0", "test.md");

        Assert.Contains("# azmcp storage account list", result);
    }

    [Fact]
    public void GenerateAnnotationFrontmatter_ContainsDateInIsoFormat()
    {
        var result = FrontmatterUtility.GenerateAnnotationFrontmatter(
            "test", "1.0.0", "test.md");

        // Date format: yyyy-MM-dd
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        Assert.Contains($"ms.date: {today}", result);
    }

    [Fact]
    public void GenerateAnnotationFrontmatter_ContainsGeneratedTimestamp()
    {
        var result = FrontmatterUtility.GenerateAnnotationFrontmatter(
            "test", "1.0.0", "test.md");

        Assert.Contains("generated:", result);
        Assert.Contains("UTC", result);
    }

    // ── Parameter Frontmatter ──────────────────────────────────

    [Fact]
    public void GenerateParameterFrontmatter_ContainsParametersPath()
    {
        var result = FrontmatterUtility.GenerateParameterFrontmatter(
            "storage account list", "1.0.0", "storage-account-list-parameters.md");

        Assert.Contains("../includes/tools/parameters/storage-account-list-parameters.md", result);
    }

    [Fact]
    public void GenerateParameterFrontmatter_ContainsYamlMarkers()
    {
        var result = FrontmatterUtility.GenerateParameterFrontmatter(
            "test", "1.0.0", "test.md");

        Assert.StartsWith("---", result);
    }

    [Fact]
    public void GenerateParameterFrontmatter_NullVersion_ShowsUnknown()
    {
        var result = FrontmatterUtility.GenerateParameterFrontmatter(
            "test", null, "test.md");

        Assert.Contains("mcp-cli.version: unknown", result);
    }

    [Fact]
    public void GenerateParameterFrontmatter_ContainsAzmcpComment()
    {
        var result = FrontmatterUtility.GenerateParameterFrontmatter(
            "keyvault secret get", "1.0.0", "test.md");

        Assert.Contains("# azmcp keyvault secret get", result);
    }

    // ── Annotation vs Parameter paths differ ───────────────────

    [Fact]
    public void Frontmatter_AnnotationAndParameter_UseDifferentPaths()
    {
        var annotation = FrontmatterUtility.GenerateAnnotationFrontmatter(
            "test", "1.0.0", "file.md");
        var parameter = FrontmatterUtility.GenerateParameterFrontmatter(
            "test", "1.0.0", "file.md");

        Assert.Contains("/annotations/", annotation);
        Assert.Contains("/parameters/", parameter);
        Assert.DoesNotContain("/parameters/", annotation);
        Assert.DoesNotContain("/annotations/", parameter);
    }
}
