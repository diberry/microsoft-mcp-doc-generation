// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ToolFamilyCleanup.Services;
using Xunit;

namespace DocGeneration.Steps.ToolFamilyCleanup.Tests;

/// <summary>
/// Tests for AnnotationSpaceFixer — ensures a blank line separates
/// the "[Tool annotation hints]" link from the annotation values.
/// Fixes: #151 — missing blank line causes annotation values to render
/// on the same paragraph as the link text in Markdown.
/// </summary>
public class AnnotationSpaceFixerTests
{
    // ── Core fix: insert blank line when missing ────────────────────

    [Fact]
    public void Fix_MissingBlankLine_InsertsBlankLine()
    {
        // Arrange — annotation link immediately followed by values (no blank line)
        var input = "[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):\nDestructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌\n";

        // Act
        var result = AnnotationSpaceFixer.Fix(input);

        // Assert — blank line must exist between link and values
        Assert.Contains(
            "[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):\n\nDestructive:",
            result);
    }

    [Fact]
    public void Fix_BlankLineAlreadyPresent_NoChange()
    {
        // Arrange — already correct
        var input = "[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):\n\nDestructive: ❌ | Idempotent: ✅\n";

        // Act
        var result = AnnotationSpaceFixer.Fix(input);

        // Assert — unchanged
        Assert.Equal(input, result);
    }

    // ── Multiple tools in one document ──────────────────────────────

    [Fact]
    public void Fix_MultipleToolSections_FixesAll()
    {
        // Arrange — two tools, both missing blank line
        var input = string.Join("\n", new[]
        {
            "## Create storage account",
            "",
            "[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):",
            "Destructive: ❌ | Idempotent: ✅",
            "",
            "## Delete storage account",
            "",
            "[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):",
            "Destructive: ✅ | Idempotent: ❌",
            ""
        });

        // Act
        var result = AnnotationSpaceFixer.Fix(input);

        // Assert — both should have blank line inserted
        var parts = result.Split("[Tool annotation hints]");
        Assert.Equal(3, parts.Length); // 2 occurrences = 3 parts
        foreach (var part in parts.Skip(1))
        {
            Assert.StartsWith("(index.md#tool-annotations-for-azure-mcp-server):\n\n", part);
        }
    }

    // ── Edge cases ──────────────────────────────────────────────────

    [Fact]
    public void Fix_NullOrEmpty_ReturnsInput()
    {
        Assert.Equal("", AnnotationSpaceFixer.Fix(""));
        Assert.Equal("", AnnotationSpaceFixer.Fix(null!));
    }

    [Fact]
    public void Fix_NoAnnotationLink_ReturnsUnchanged()
    {
        var input = "## Create VM\n\nCreates a virtual machine.\n";

        var result = AnnotationSpaceFixer.Fix(input);

        Assert.Equal(input, result);
    }

    [Fact]
    public void Fix_AnnotationLinkWithInclude_NotAffected()
    {
        // When annotation is an [!INCLUDE] directive, there should already be a blank line
        // and we should not add an extra one
        var input = "[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):\n\n[!INCLUDE [tool](../annotations/tool.md)]\n";

        var result = AnnotationSpaceFixer.Fix(input);

        Assert.Equal(input, result);
    }

    // ── Realistic full-document test ────────────────────────────────

    [Fact]
    public void Fix_RealisticDocument_FixesMissingBlankLines()
    {
        var input = string.Join("\n", new[]
        {
            "---",
            "title: Azure MCP Server tools for Azure App Service",
            "---",
            "",
            "# Azure MCP Server tools for Azure App Service",
            "",
            "## Add database",
            "",
            "<!-- appservice database add -->",
            "",
            "Adds a database connection to your app.",
            "",
            "| Parameter | Required or optional | Description |",
            "|-----------|---------------------|-------------|",
            "| **App** | Required | The app name. |",
            "",
            "[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):",
            "Destructive: ❌ | Idempotent: ❌ | Open World: ✅ | Read Only: ❌ | Secret: ❌ | Local Required: ❌",
            "",
            "## Get webapp",
            "",
            "<!-- appservice webapp get -->",
            "",
            "Gets webapp details.",
            "",
            "[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):",
            "Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌",
            "",
            "## Related content",
            ""
        });

        var result = AnnotationSpaceFixer.Fix(input);

        // Both annotation links should now have blank line before values
        Assert.DoesNotContain("server):\nDestructive:", result);
        Assert.Contains("server):\n\nDestructive:", result);
        // Structure preserved
        Assert.Contains("## Add database", result);
        Assert.Contains("## Get webapp", result);
        Assert.Contains("## Related content", result);
    }
}
