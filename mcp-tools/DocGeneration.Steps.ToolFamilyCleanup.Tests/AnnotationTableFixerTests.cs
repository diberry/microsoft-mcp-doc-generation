// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;
using ToolFamilyCleanup.Services;
using Xunit;

namespace DocGeneration.Steps.ToolFamilyCleanup.Tests;

/// <summary>
/// Unit tests for AnnotationTableFixer.
/// Validates that inline "Key: emoji | ..." annotation value lines are
/// converted to the 3-row markdown table format, and that already-table
/// input is passed through unchanged (idempotency).
/// </summary>
public class AnnotationTableFixerTests
{
    private const string LinkLine =
        "[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):";

    private static string MakeInlineDoc(string inlineLine) =>
        $"{LinkLine}\n\n{inlineLine}\n";

    private static string MakeTableDoc(string headerRow, string sepRow, string valueRow) =>
        $"{LinkLine}\n\n{headerRow}\n{sepRow}\n{valueRow}\n";

    // ── Inline → Table conversion ─────────────────────────────────

    [Fact]
    public void Fix_InlineFormat_ConvertsToTableFormat()
    {
        var inline = "Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌";
        var input = MakeInlineDoc(inline);

        var result = AnnotationTableFixer.Fix(input);

        // Link line preserved
        Assert.Contains(LinkLine, result);
        // Table header row present
        Assert.Contains("| Destructive | Idempotent | Open World | Read Only | Secret | Local Required |", result);
        // Separator row present (centered alignment)
        Assert.Contains("|:-----------:|:----------:|:----------:|:---------:|:------:|:--------------:|", result);
        // Values row present
        Assert.Contains("| ❌ | ✅ | ❌ | ✅ | ❌ | ❌ |", result);
        // OLD inline format must NOT appear in result
        Assert.DoesNotMatch(@"(?m)^\s*Destructive:\s*(✅|❌)", result);
    }

    [Fact]
    public void Fix_InlineFormat_PreservesCorrectEmojiValues()
    {
        // All-true: every field is ✅
        var inline = "Destructive: ✅ | Idempotent: ✅ | Open World: ✅ | Read Only: ✅ | Secret: ✅ | Local Required: ✅";
        var result = AnnotationTableFixer.Fix(MakeInlineDoc(inline));
        Assert.Contains("| ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |", result);
    }

    [Fact]
    public void Fix_InlineFormat_MixedEmojis_CorrectOrder()
    {
        // Destructive=✅ Idempotent=❌ OpenWorld=❌ ReadOnly=❌ Secret=✅ LocalRequired=❌
        var inline = "Destructive: ✅ | Idempotent: ❌ | Open World: ❌ | Read Only: ❌ | Secret: ✅ | Local Required: ❌";
        var result = AnnotationTableFixer.Fix(MakeInlineDoc(inline));
        Assert.Contains("| ✅ | ❌ | ❌ | ❌ | ✅ | ❌ |", result);
    }

    [Fact]
    public void Fix_InlineWithTrailingPipe_ConvertsCorrectly()
    {
        // Trailing pipe sometimes added by AI (handled by AnnotationTrailingPipeFixer
        // but we must be robust before that step runs)
        var inline = "Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌ |";
        var result = AnnotationTableFixer.Fix(MakeInlineDoc(inline));
        Assert.Contains("| ❌ | ✅ | ❌ | ✅ | ❌ | ❌ |", result);
        Assert.DoesNotMatch(@"(?m)^\s*Destructive:\s*(✅|❌)", result);
    }

    [Fact]
    public void Fix_InlineFormat_PartialFields_LeftUnconverted()
    {
        // Only 2 fields — a partial inline annotation must NOT be silently defaulted to ❌.
        // The fixer must leave the line unconverted so the post-assembly validator blocks it.
        var partial = "Destructive: ❌ | Idempotent: ✅";
        var input = MakeInlineDoc(partial);

        var result = AnnotationTableFixer.Fix(input);

        // Table separator must NOT appear (no silent conversion from partial input)
        Assert.DoesNotContain("|:-----------:|", result);
        // Original partial inline text must be preserved in output
        Assert.Contains(partial, result);
    }

    [Fact]
    public void Fix_InlineFormat_AllSixFields_StillConverts()
    {
        // Guard against over-correction: a complete 6-field inline line must still convert
        // after the ST1 fail-safe change. Ensures the new null-return path is never triggered
        // for valid full-field input.
        var inline = "Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ✅";
        var input = MakeInlineDoc(inline);

        var result = AnnotationTableFixer.Fix(input);

        Assert.Contains(AnnotationTableFixer.SeparatorRow, result);
        Assert.DoesNotMatch(@"(?m)^\s*Destructive:\s*(✅|❌)", result);
    }

    [Fact]
    public void Fix_InlineFormat_ProducesExactly3TableRows()
    {
        var inline = "Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌";
        var result = AnnotationTableFixer.Fix(MakeInlineDoc(inline));

        // Exactly one header, one separator, one values row
        int headerCount = Regex.Matches(result, @"^\| Destructive \|", RegexOptions.Multiline).Count;
        int separatorCount = Regex.Matches(result, @"^\|:-----------:\|", RegexOptions.Multiline).Count;
        Assert.True(headerCount == 1, $"Expected 1 header row, got {headerCount}");
        Assert.True(separatorCount == 1, $"Expected 1 separator row, got {separatorCount}");
    }

    // ── Idempotency: already-table input unchanged ────────────────

    [Fact]
    public void Fix_TableFormatAlreadyPresent_Idempotent()
    {
        var headerRow = "| Destructive | Idempotent | Open World | Read Only | Secret | Local Required |";
        var sepRow = "|:-----------:|:----------:|:----------:|:---------:|:------:|:--------------:|";
        var valueRow = "| ❌ | ✅ | ❌ | ✅ | ❌ | ❌ |";
        var input = MakeTableDoc(headerRow, sepRow, valueRow);

        var result = AnnotationTableFixer.Fix(input);

        Assert.Equal(input, result);
    }

    [Fact]
    public void Fix_TableFormat_DoesNotDuplicateTableRows()
    {
        var headerRow = "| Destructive | Idempotent | Open World | Read Only | Secret | Local Required |";
        var sepRow = "|:-----------:|:----------:|:----------:|:---------:|:------:|:--------------:|";
        var valueRow = "| ✅ | ❌ | ❌ | ❌ | ❌ | ❌ |";
        var input = MakeTableDoc(headerRow, sepRow, valueRow);

        var result = AnnotationTableFixer.Fix(input);

        // Header appears exactly once
        int headerCount = Regex.Matches(result, @"^\| Destructive \|", RegexOptions.Multiline).Count;
        Assert.True(headerCount == 1, $"Expected 1 header row, got {headerCount}");
    }

    // ── Multiple annotation blocks in one document ────────────────

    [Fact]
    public void Fix_MultipleAnnotationBlocks_ConvertsAll()
    {
        var doc = string.Join("\n", new[]
        {
            "## Create vault",
            "",
            LinkLine,
            "",
            "Destructive: ✅ | Idempotent: ❌ | Open World: ❌ | Read Only: ❌ | Secret: ❌ | Local Required: ❌",
            "",
            "## List policies",
            "",
            LinkLine,
            "",
            "Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌",
            ""
        });

        var result = AnnotationTableFixer.Fix(doc);

        // No inline format remains
        Assert.DoesNotMatch(@"(?m)^\s*Destructive:\s*(✅|❌)", result);
        // Both annotation blocks contain the table separator
        int separatorCount = Regex.Matches(result, @"^\|:-----------:\|", RegexOptions.Multiline).Count;
        Assert.True(separatorCount == 2, $"Expected 2 separator rows, got {separatorCount}");
        // Both value rows are present
        Assert.Contains("| ✅ | ❌ | ❌ | ❌ | ❌ | ❌ |", result);
        Assert.Contains("| ❌ | ✅ | ❌ | ✅ | ❌ | ❌ |", result);
    }

    [Fact]
    public void Fix_MixedBlocks_InlineAndTable_ConvertsOnlyInline()
    {
        // First block is already table, second is inline
        var tableValueRow = "| ❌ | ✅ | ❌ | ✅ | ❌ | ❌ |";
        var doc = string.Join("\n", new[]
        {
            "## Tool A",
            "",
            LinkLine,
            "",
            "| Destructive | Idempotent | Open World | Read Only | Secret | Local Required |",
            "|:-----------:|:----------:|:----------:|:---------:|:------:|:--------------:|",
            tableValueRow,
            "",
            "## Tool B",
            "",
            LinkLine,
            "",
            "Destructive: ✅ | Idempotent: ❌ | Open World: ❌ | Read Only: ❌ | Secret: ❌ | Local Required: ❌",
            ""
        });

        var result = AnnotationTableFixer.Fix(doc);

        // No inline format remains
        Assert.DoesNotMatch(@"(?m)^\s*Destructive:\s*(✅|❌)", result);
        // Table A's value row still present
        Assert.Contains(tableValueRow, result);
        // Table B converted
        Assert.Contains("| ✅ | ❌ | ❌ | ❌ | ❌ | ❌ |", result);
        // Separator appears exactly twice
        int sepCount = Regex.Matches(result, @"^\|:-----------:\|", RegexOptions.Multiline).Count;
        Assert.True(sepCount == 2, $"Expected 2 separator rows, got {sepCount}");
    }

    // ── No-annotation passthrough ─────────────────────────────────

    [Fact]
    public void Fix_NoAnnotationLink_ReturnsUnchanged()
    {
        var input = "## Create VM\n\nCreates a virtual machine.\n\nSome description.\n";

        var result = AnnotationTableFixer.Fix(input);

        Assert.Equal(input, result);
    }

    [Fact]
    public void Fix_NullOrEmpty_ReturnsEmpty()
    {
        Assert.Equal("", AnnotationTableFixer.Fix(""));
        Assert.Equal("", AnnotationTableFixer.Fix(null!));
    }

    [Fact]
    public void Fix_AnnotationLinkWithInclude_NotAffected()
    {
        // [!INCLUDE] directives don't start with "Destructive:" or "| Destructive |"
        // and should pass through unchanged
        var input = $"{LinkLine}\n\n[!INCLUDE [tool](../annotations/tool.md)]\n";

        var result = AnnotationTableFixer.Fix(input);

        Assert.Equal(input, result);
    }

    // ── ConvertInlineToTable helper (internal) ────────────────────

    [Theory]
    [InlineData(
        "Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌",
        "| ❌ | ✅ | ❌ | ✅ | ❌ | ❌ |")]
    [InlineData(
        "Destructive: ✅ | Idempotent: ✅ | Open World: ✅ | Read Only: ✅ | Secret: ✅ | Local Required: ✅",
        "| ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |")]
    [InlineData(
        "Destructive: ✅ | Idempotent: ❌ | Open World: ❌ | Read Only: ❌ | Secret: ✅ | Local Required: ❌",
        "| ✅ | ❌ | ❌ | ❌ | ✅ | ❌ |")]
    public void ConvertInlineToTable_ProducesCorrectValueRow(string inlineLine, string expectedValueRow)
    {
        var rows = AnnotationTableFixer.ConvertInlineToTable(inlineLine);

        Assert.NotNull(rows); // full 6-field input must always produce a table
        Assert.Equal(3, rows.Length);
        Assert.Equal(AnnotationTableFixer.HeaderRow, rows[0]);
        Assert.Equal(AnnotationTableFixer.SeparatorRow, rows[1]);
        Assert.Equal(expectedValueRow, rows[2]);
    }
}
