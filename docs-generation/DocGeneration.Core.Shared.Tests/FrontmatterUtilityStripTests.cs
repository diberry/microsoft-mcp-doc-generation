// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Shared;
using Xunit;

namespace DocGeneration.Core.Shared.Tests;

/// <summary>
/// Tests for FrontmatterUtility.StripFrontmatter — ensures YAML frontmatter
/// is removed from markdown content before inline rendering.
/// Consolidates tests from PageGeneratorStripFrontmatterTests (issue #216).
/// </summary>
public class FrontmatterUtilityStripTests
{
    // === Ported from PageGeneratorStripFrontmatterTests ===

    [Fact]
    public void StripFrontmatter_RemovesFrontmatter_ReturnsContentAfter()
    {
        var input = "---\nms.topic: include\nms.date: 2026-03-20\n---\nDestructive: ❌ | Idempotent: ✅\n";
        var result = FrontmatterUtility.StripFrontmatter(input);

        Assert.Contains("Destructive: ❌ | Idempotent: ✅", result);
        Assert.DoesNotContain("ms.topic", result);
        Assert.DoesNotContain("---", result.Trim());
    }

    [Fact]
    public void StripFrontmatter_NoFrontmatter_ReturnsOriginal()
    {
        var input = "Destructive: ❌ | Idempotent: ✅\n";
        var result = FrontmatterUtility.StripFrontmatter(input);

        Assert.Equal(input, result);
    }

    [Fact]
    public void StripFrontmatter_EmptyString_ReturnsEmpty()
    {
        Assert.Equal("", FrontmatterUtility.StripFrontmatter(""));
    }

    [Fact]
    public void StripFrontmatter_NullString_ReturnsNull()
    {
        Assert.Null(FrontmatterUtility.StripFrontmatter(null!));
    }

    [Fact]
    public void StripFrontmatter_OnlyFrontmatter_ReturnsEmptyContent()
    {
        var input = "---\nms.topic: include\n---\n";
        var result = FrontmatterUtility.StripFrontmatter(input);

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

        var result = FrontmatterUtility.StripFrontmatter(input).Trim();

        Assert.Equal(
            "Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌",
            result);
    }

    // === New edge case tests ===

    [Fact]
    public void StripFrontmatter_OnlyOpeningDelimiter_ReturnsOriginal()
    {
        var input = "---\nms.topic: include\nSome content without closing delimiter\n";
        var result = FrontmatterUtility.StripFrontmatter(input);

        // With only one ---, there's no valid frontmatter block to strip
        Assert.Equal(input, result);
    }

    [Fact]
    public void StripFrontmatter_TripleDashInsideBody_DoesNotStripBody()
    {
        var input = "---\nms.topic: include\n---\nSome content\n---\nMore content after separator\n";
        var result = FrontmatterUtility.StripFrontmatter(input);

        // Should strip only the first frontmatter block
        Assert.Contains("Some content", result);
        Assert.Contains("More content after separator", result);
        Assert.DoesNotContain("ms.topic", result);
    }

    [Fact]
    public void StripFrontmatter_WindowsLineEndings_StripsCorrectly()
    {
        var input = "---\r\nms.topic: include\r\nms.date: 2026-03-20\r\n---\r\nDestructive: ❌ | Idempotent: ✅\r\n";
        var result = FrontmatterUtility.StripFrontmatter(input);

        Assert.Contains("Destructive: ❌ | Idempotent: ✅", result);
        Assert.DoesNotContain("ms.topic", result);
    }

    [Fact]
    public void StripFrontmatter_EmptyFrontmatter_StripsDelimiters()
    {
        var input = "---\n---\nContent after empty frontmatter\n";
        var result = FrontmatterUtility.StripFrontmatter(input);

        Assert.Equal("Content after empty frontmatter\n", result);
        Assert.DoesNotContain("---", result);
    }
}
