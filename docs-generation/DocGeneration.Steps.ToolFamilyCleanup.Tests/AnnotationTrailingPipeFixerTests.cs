// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ToolFamilyCleanup.Services;
using Xunit;

namespace DocGeneration.Steps.ToolFamilyCleanup.Tests;

/// <summary>
/// Tests for AnnotationTrailingPipeFixer — strips trailing pipe characters
/// from annotation value lines.
/// Fixes: #281 — AI sometimes adds a trailing | to annotation lines.
/// </summary>
public class AnnotationTrailingPipeFixerTests
{
    // ── Core fix: strip trailing pipe ───────────────────────────────

    [Fact]
    public void Fix_TrailingPipe_StripsIt()
    {
        var input = "Destructive: ✅ | Idempotent: ❌ | Open World: ❌ | Read Only: ❌ | Secret: ❌ | Local Required: ❌ |";

        var result = AnnotationTrailingPipeFixer.Fix(input);

        Assert.Equal(
            "Destructive: ✅ | Idempotent: ❌ | Open World: ❌ | Read Only: ❌ | Secret: ❌ | Local Required: ❌",
            result);
    }

    [Fact]
    public void Fix_TrailingPipeWithSpaces_StripsIt()
    {
        var input = "Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌ |  ";

        var result = AnnotationTrailingPipeFixer.Fix(input);

        Assert.DoesNotContain("❌ |  ", result);
        Assert.EndsWith("Local Required: ❌", result.TrimEnd());
    }

    [Fact]
    public void Fix_NoTrailingPipe_Unchanged()
    {
        var input = "Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌";

        var result = AnnotationTrailingPipeFixer.Fix(input);

        Assert.Equal(input, result);
    }

    // ── Full document context ───────────────────────────────────────

    [Fact]
    public void Fix_InDocument_StripsTrailingPipeFromAnnotationLine()
    {
        var input = string.Join("\n", new[]
        {
            "## update-appsettings",
            "",
            "Updates app settings for a web app.",
            "",
            "[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):",
            "",
            "Destructive: ✅ | Idempotent: ❌ | Open World: ❌ | Read Only: ❌ | Secret: ❌ | Local Required: ❌ |",
            "",
            "## get-webapp",
            "",
            "Gets webapp details.",
            "",
            "[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):",
            "",
            "Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌",
            ""
        });

        var result = AnnotationTrailingPipeFixer.Fix(input);

        // The trailing pipe on the first annotation line should be removed
        Assert.DoesNotContain("Local Required: ❌ |", result);
        // The correctly formatted line should remain unchanged
        Assert.Contains("Local Required: ❌\n", result);
        // Both tool sections should still exist
        Assert.Contains("## update-appsettings", result);
        Assert.Contains("## get-webapp", result);
    }

    [Fact]
    public void Fix_MultipleAnnotationLinesWithTrailingPipes_FixesAll()
    {
        var input = string.Join("\n", new[]
        {
            "Destructive: ✅ | Idempotent: ❌ | Open World: ❌ | Read Only: ❌ | Secret: ❌ | Local Required: ❌ |",
            "",
            "Some content between",
            "",
            "Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ✅ | Local Required: ❌ |",
            ""
        });

        var result = AnnotationTrailingPipeFixer.Fix(input);

        // Count occurrences of trailing pipe pattern — should be zero
        var lines = result.Split('\n');
        foreach (var line in lines)
        {
            if (line.Contains("Destructive:"))
            {
                Assert.DoesNotMatch(@"\|\s*$", line);
            }
        }
    }

    // ── Must not affect markdown tables ─────────────────────────────

    [Fact]
    public void Fix_MarkdownTableRow_NotAffected()
    {
        var input = string.Join("\n", new[]
        {
            "| Parameter | Required or optional | Description |",
            "|-----------|---------------------|-------------|",
            "| **App** | Required | The app name. |",
            "",
            "Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌ |",
            ""
        });

        var result = AnnotationTrailingPipeFixer.Fix(input);

        // Table rows must keep their trailing pipe
        Assert.Contains("| **App** | Required | The app name. |", result);
        // Annotation line should have trailing pipe stripped
        Assert.DoesNotContain("Local Required: ❌ |", result);
        Assert.Contains("Local Required: ❌", result);
    }

    // ── Edge cases ──────────────────────────────────────────────────

    [Fact]
    public void Fix_NullOrEmpty_ReturnsEmpty()
    {
        Assert.Equal("", AnnotationTrailingPipeFixer.Fix(""));
        Assert.Equal("", AnnotationTrailingPipeFixer.Fix(null!));
    }

    [Fact]
    public void Fix_NoAnnotationLines_ReturnsUnchanged()
    {
        var input = "## Create VM\n\nCreates a virtual machine.\n";

        var result = AnnotationTrailingPipeFixer.Fix(input);

        Assert.Equal(input, result);
    }
}
