// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ToolFamilyCleanup.Services;
using Xunit;

namespace DocGeneration.Steps.ToolFamilyCleanup.Tests;

/// <summary>
/// Tests for ContractionFixer — deterministic post-processor that
/// converts formal non-contracted forms to contractions per Microsoft
/// style guide. Fixes: #145 (Acrolinx "Could you use contractions?")
/// </summary>
public class ContractionFixerTests
{
    // ── Core contractions ───────────────────────────────────────────

    [Theory]
    [InlineData("It does not support", "It doesn't support")]
    [InlineData("This does not apply", "This doesn't apply")]
    [InlineData("It do not require", "It don't require")]
    [InlineData("You do not need", "You don't need")]
    [InlineData("It is not available", "It isn't available")]
    [InlineData("This is not required", "This isn't required")]
    [InlineData("It will not work", "It won't work")]
    [InlineData("It can not be used", "It can't be used")]
    [InlineData("It cannot be used", "It can't be used")]
    public void Fix_NonContracted_ReplacedWithContraction(string input, string expected)
    {
        var result = ContractionFixer.Fix(input);
        Assert.Equal(expected, result);
    }

    // ── Already contracted — idempotent ─────────────────────────────

    [Theory]
    [InlineData("It doesn't support")]
    [InlineData("You don't need")]
    [InlineData("It isn't available")]
    [InlineData("It won't work")]
    [InlineData("It can't be used")]
    public void Fix_AlreadyContracted_NoChange(string input)
    {
        var result = ContractionFixer.Fix(input);
        Assert.Equal(input, result);
    }

    // ── Should NOT replace inside code/backticks ────────────────────

    [Fact]
    public void Fix_InsideBackticks_NotReplaced()
    {
        var input = "Use `does not` as a parameter value.";
        var result = ContractionFixer.Fix(input);
        Assert.Contains("`does not`", result);
    }

    // ── Multiple replacements in one string ─────────────────────────

    [Fact]
    public void Fix_MultipleOccurrences_AllReplaced()
    {
        var input = "It does not support X and it is not available in Y.";
        var result = ContractionFixer.Fix(input);
        Assert.Contains("doesn't", result);
        Assert.Contains("isn't", result);
    }

    // ── Edge cases ──────────────────────────────────────────────────

    [Fact]
    public void Fix_NullOrEmpty_ReturnsInput()
    {
        Assert.Equal("", ContractionFixer.Fix(""));
        Assert.Equal("", ContractionFixer.Fix(null!));
    }

    [Fact]
    public void Fix_NoMatchingPatterns_ReturnsUnchanged()
    {
        var input = "This is a normal sentence about Azure resources.";
        var result = ContractionFixer.Fix(input);
        Assert.Equal(input, result);
    }
}
