// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using NaturalLanguageGenerator;
using Xunit;

namespace DocGeneration.Steps.AnnotationsParametersRaw.Annotations.Tests;

/// <summary>
/// Tests for TextCleanup.WrapExampleValues — ensures "for example" values
/// in parameter descriptions are wrapped in backticks at the source level.
/// Fixes: #190
/// </summary>
public class WrapExampleValuesTests
{
    // ── Core fix: wrap bare values in backticks ─────────────────────

    [Theory]
    [InlineData(
        "The name of the app (for example, my-webapp).",
        "The name of the app (for example, `my-webapp`).")]
    [InlineData(
        "The database name (for example, mydb).",
        "The database name (for example, `mydb`).")]
    [InlineData(
        "The server FQDN (for example, myserver.database.windows.net).",
        "The server FQDN (for example, `myserver.database.windows.net`).")]
    public void WrapExampleValues_SingleBareValue_WrapsInBackticks(string input, string expected)
    {
        var result = TextCleanup.WrapExampleValues(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void WrapExampleValues_CommaSeparatedValues_WrapsEachValue()
    {
        var input = "The type (for example, SqlServer, MySQL, PostgreSQL).";
        var result = TextCleanup.WrapExampleValues(input);

        Assert.Equal("The type (for example, `SqlServer`, `MySQL`, `PostgreSQL`).", result);
    }

    // ── Idempotent — already-backticked values pass through ─────────

    [Theory]
    [InlineData("The name (for example, `my-webapp`).")]
    [InlineData("The interval (for example, `PT1H` for 1 hour, `PT5M` for 5 minutes).")]
    [InlineData("The timestamp (for example, `2023-01-01T00:00:00Z`).")]
    public void WrapExampleValues_AlreadyBackticked_NoChange(string input)
    {
        var result = TextCleanup.WrapExampleValues(input);
        Assert.Equal(input, result);
    }

    // ── Edge cases ──────────────────────────────────────────────────

    [Fact]
    public void WrapExampleValues_NullInput_ReturnsNull()
    {
        Assert.Null(TextCleanup.WrapExampleValues(null!));
    }

    [Fact]
    public void WrapExampleValues_EmptyInput_ReturnsEmpty()
    {
        Assert.Equal("", TextCleanup.WrapExampleValues(""));
    }

    [Fact]
    public void WrapExampleValues_NoExamplePattern_ReturnsUnchanged()
    {
        var input = "The name of the storage account.";
        Assert.Equal(input, TextCleanup.WrapExampleValues(input));
    }

    [Fact]
    public void WrapExampleValues_InParameterTable_WrapsCorrectly()
    {
        var input = "| **App** |  Required | The name of the Azure App Service (for example, my-webapp). |";
        var result = TextCleanup.WrapExampleValues(input);

        Assert.Contains("(for example, `my-webapp`)", result);
    }
}
