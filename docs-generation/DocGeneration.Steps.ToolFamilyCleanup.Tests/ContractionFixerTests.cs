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

    // ── Positive contractions ───────────────────────────────────────

    [Theory]
    [InlineData("it is available", "it's available")]
    [InlineData("you are welcome", "you're welcome")]
    [InlineData("we have finished", "we've finished")]
    [InlineData("that is correct", "that's correct")]
    [InlineData("there is a way", "there's a way")]
    [InlineData("here is the file", "here's the file")]
    [InlineData("what is this", "what's this")]
    [InlineData("who is responsible", "who's responsible")]
    public void Fix_PositiveContraction_ReplacedWithContraction(string input, string expected)
    {
        var result = ContractionFixer.Fix(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("It is available", "It's available")]
    [InlineData("You are welcome", "You're welcome")]
    [InlineData("We have finished", "We've finished")]
    [InlineData("That is correct", "That's correct")]
    [InlineData("There is a way", "There's a way")]
    [InlineData("Here is the file", "Here's the file")]
    [InlineData("What is this", "What's this")]
    [InlineData("Who is responsible", "Who's responsible")]
    public void Fix_PositiveContraction_PreservesCapitalization(string input, string expected)
    {
        var result = ContractionFixer.Fix(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Fix_PossessiveIts_NotChanged()
    {
        var input = "Check its value before proceeding.";
        var result = ContractionFixer.Fix(input);
        Assert.Equal(input, result);
    }

    [Fact]
    public void Fix_WordBoundary_ExitIs_NotChanged()
    {
        var input = "The exit is on the left.";
        var result = ContractionFixer.Fix(input);
        Assert.Equal(input, result);
    }

    [Fact]
    public void Fix_PositiveContraction_InsideBackticks_NotReplaced()
    {
        var input = "Use `it is` as a parameter.";
        var result = ContractionFixer.Fix(input);
        Assert.Contains("`it is`", result);
    }

    [Theory]
    [InlineData("it's available")]
    [InlineData("you're welcome")]
    [InlineData("we've finished")]
    [InlineData("that's correct")]
    [InlineData("there's a way")]
    [InlineData("here's the file")]
    [InlineData("what's this")]
    [InlineData("who's responsible")]
    public void Fix_PositiveAlreadyContracted_NoChange(string input)
    {
        var result = ContractionFixer.Fix(input);
        Assert.Equal(input, result);
    }

    [Fact]
    public void Fix_MixedPositiveAndNegative_AllReplaced()
    {
        var input = "It is ready but it does not work and you are invited.";
        var result = ContractionFixer.Fix(input);
        Assert.Contains("It's", result);
        Assert.Contains("doesn't", result);
        Assert.Contains("you're", result);
    }

    [Fact]
    public void Fix_NullOrEmpty_StillWorks_WithPositiveRules()
    {
        Assert.Equal("", ContractionFixer.Fix(""));
        Assert.Equal("", ContractionFixer.Fix(null!));
    }
}
