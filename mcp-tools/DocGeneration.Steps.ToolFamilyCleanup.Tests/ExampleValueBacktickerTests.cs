// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ToolFamilyCleanup.Services;
using Xunit;

namespace DocGeneration.Steps.ToolFamilyCleanup.Tests;

/// <summary>
/// Tests for ExampleValueBackticker — ensures example values in parameter
/// descriptions are wrapped in backticks for consistent formatting.
/// Fixes: #152 — "(for example, my-webapp)" should be "(for example, `my-webapp`)"
/// </summary>
public class ExampleValueBacktickerTests
{
    // ── Core fix: wrap bare values in backticks ─────────────────────

    [Theory]
    [InlineData(
        "(for example, my-webapp)",
        "(for example, `my-webapp`)")]
    [InlineData(
        "(for example, mydb)",
        "(for example, `mydb`)")]
    [InlineData(
        "(for example, myserver.database.windows.net)",
        "(for example, `myserver.database.windows.net`)")]
    public void Fix_SingleBareValue_WrapsInBackticks(string input, string expected)
    {
        var result = ExampleValueBackticker.Fix(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Fix_CommaSeparatedBareValues_WrapsEach()
    {
        var input = "(for example, SqlServer, MySQL, PostgreSQL)";
        var result = ExampleValueBackticker.Fix(input);
        Assert.Equal("(for example, `SqlServer`, `MySQL`, `PostgreSQL`)", result);
    }

    // ── Already backticked — idempotent ─────────────────────────────

    [Theory]
    [InlineData("(for example, `my-webapp`)")]
    [InlineData("(for example, `PT1H` for 1 hour, `PT5M` for 5 minutes)")]
    [InlineData("(for example, `2023-01-01T00:00:00Z`)")]
    [InlineData("(for example, `Availability`, `CpuAnalysis`, `MemoryAnalysis`)")]
    public void Fix_AlreadyBackticked_NoChange(string input)
    {
        var result = ExampleValueBackticker.Fix(input);
        Assert.Equal(input, result);
    }

    // ── In parameter table context ──────────────────────────────────

    [Fact]
    public void Fix_InParameterTableRow_FixesBareValue()
    {
        var input = "| **App** |  Required | The name of the Azure App Service (for example, my-webapp). |";
        var result = ExampleValueBackticker.Fix(input);
        Assert.Contains("(for example, `my-webapp`)", result);
    }

    [Fact]
    public void Fix_MultipleRowsInDocument_FixesAll()
    {
        var input = string.Join("\n", new[]
        {
            "| **App** | Required | The app name (for example, my-webapp). |",
            "| **Database** | Required | The database name (for example, mydb). |",
            "| **Detector** | Required | The detector (for example, `Availability`). |"
        });

        var result = ExampleValueBackticker.Fix(input);

        Assert.Contains("(for example, `my-webapp`)", result);
        Assert.Contains("(for example, `mydb`)", result);
        // Already backticked — unchanged
        Assert.Contains("(for example, `Availability`)", result);
    }

    // ── Edge cases ──────────────────────────────────────────────────

    [Fact]
    public void Fix_NullOrEmpty_ReturnsInput()
    {
        Assert.Equal("", ExampleValueBackticker.Fix(""));
        Assert.Equal("", ExampleValueBackticker.Fix(null!));
    }

    [Fact]
    public void Fix_NoExamplePattern_ReturnsUnchanged()
    {
        var input = "This is a normal description without examples.";
        var result = ExampleValueBackticker.Fix(input);
        Assert.Equal(input, result);
    }

    [Fact]
    public void Fix_ValueWithTrailingPeriodInsideParens_HandlesCorrectly()
    {
        // Period before closing paren should stay outside backtick
        var input = "(for example, my-webapp).";
        var result = ExampleValueBackticker.Fix(input);
        Assert.Equal("(for example, `my-webapp`).", result);
    }
}
