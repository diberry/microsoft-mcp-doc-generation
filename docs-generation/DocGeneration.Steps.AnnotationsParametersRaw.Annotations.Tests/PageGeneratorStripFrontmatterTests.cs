// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using CSharpGenerator.Generators;
using Xunit;

namespace DocGeneration.Steps.AnnotationsParametersRaw.Annotations.Tests;

/// <summary>
/// Tests for PageGenerator.StripFrontmatter — ensures YAML frontmatter
/// is removed from annotation file content before inline rendering.
/// Fixes: #193
/// </summary>
public class PageGeneratorStripFrontmatterTests
{
    [Fact]
    public void StripFrontmatter_RemovesFrontmatter_ReturnsContentAfter()
    {
        var input = "---\nms.topic: include\nms.date: 2026-03-20\n---\nDestructive: ❌ | Idempotent: ✅\n";
        var result = PageGenerator.StripFrontmatter(input);

        Assert.Contains("Destructive: ❌ | Idempotent: ✅", result);
        Assert.DoesNotContain("ms.topic", result);
        Assert.DoesNotContain("---", result.Trim());
    }

    [Fact]
    public void StripFrontmatter_NoFrontmatter_ReturnsOriginal()
    {
        var input = "Destructive: ❌ | Idempotent: ✅\n";
        var result = PageGenerator.StripFrontmatter(input);

        Assert.Equal(input, result);
    }

    [Fact]
    public void StripFrontmatter_EmptyString_ReturnsEmpty()
    {
        Assert.Equal("", PageGenerator.StripFrontmatter(""));
    }

    [Fact]
    public void StripFrontmatter_NullString_ReturnsNull()
    {
        Assert.Null(PageGenerator.StripFrontmatter(null!));
    }

    [Fact]
    public void StripFrontmatter_OnlyFrontmatter_ReturnsEmptyContent()
    {
        var input = "---\nms.topic: include\n---\n";
        var result = PageGenerator.StripFrontmatter(input);

        Assert.DoesNotContain("ms.topic", result.Trim());
    }

    [Fact]
    public void StripFrontmatter_FullAnnotationFile_ReturnsAnnotationLine()
    {
        var input = string.Join("\n", new[]
        {
            "---",
            "ms.topic: include",
            "ms.date: 2026-03-20",
            "mcp-cli.version: 1.0.0",
            "---",
            "Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌",
            ""
        });

        var result = PageGenerator.StripFrontmatter(input).Trim();

        Assert.Equal(
            "Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌",
            result);
    }
}
