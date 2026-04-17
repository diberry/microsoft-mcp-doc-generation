// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ToolFamilyCleanup.Services;
using Xunit;

namespace DocGeneration.Steps.ToolFamilyCleanup.Tests;

/// <summary>
/// Tests for PresentTenseFixer — deterministic post-processor that
/// converts future tense ("will ...") to present tense per Microsoft
/// style guide. Fixes: #145 (Acrolinx GR-1: Use present tense)
/// </summary>
public class PresentTenseFixerTests
{
    // ── "will be <past-participle>" → "is <past-participle>" ────────

    [Theory]
    [InlineData("The value will be returned.", "The value is returned.")]
    [InlineData("A new resource will be created.", "A new resource is created.")]
    [InlineData("The file will be deleted.", "The file is deleted.")]
    [InlineData("The output will be displayed.", "The output is displayed.")]
    [InlineData("Results will be filtered.", "Results are filtered.")]
    [InlineData("Resources will be listed.", "Resources are listed.")]
    public void Fix_WillBePastParticiple_ConvertedToPresentPassive(string input, string expected)
    {
        var result = PresentTenseFixer.Fix(input);
        Assert.Equal(expected, result);
    }

    // ── "will <verb>" → "<verb>s" ───────────────────────────────────

    [Theory]
    [InlineData("The tool will return the results.", "The tool returns the results.")]
    [InlineData("The command will create a new resource.", "The command creates a new resource.")]
    [InlineData("The tool will list all clusters.", "The tool lists all clusters.")]
    [InlineData("The tool will display the output.", "The tool displays the output.")]
    [InlineData("The command will delete the resource.", "The command deletes the resource.")]
    [InlineData("This tool will provide monitoring data.", "This tool provides monitoring data.")]
    public void Fix_WillVerb_ConvertedToPresentTense(string input, string expected)
    {
        var result = PresentTenseFixer.Fix(input);
        Assert.Equal(expected, result);
    }

    // ── "will not be" → "is not" (ContractionFixer handles "isn't" later) ──

    [Theory]
    [InlineData("The value will not be returned.", "The value is not returned.")]
    [InlineData("The resource will not be created.", "The resource is not created.")]
    public void Fix_WillNotBe_ConvertedToIsNot(string input, string expected)
    {
        var result = PresentTenseFixer.Fix(input);
        Assert.Equal(expected, result);
    }

    // ── Already present tense — idempotent ──────────────────────────

    [Theory]
    [InlineData("The value is returned.")]
    [InlineData("The tool returns the results.")]
    [InlineData("Results are filtered.")]
    [InlineData("The command creates a new resource.")]
    public void Fix_AlreadyPresentTense_NoChange(string input)
    {
        var result = PresentTenseFixer.Fix(input);
        Assert.Equal(input, result);
    }

    // ── Content inside backticks should NOT be modified ──────────────

    [Fact]
    public void Fix_InsideBackticks_NotModified()
    {
        var input = "Use the `will return` parameter.";
        var result = PresentTenseFixer.Fix(input);
        Assert.Contains("`will return`", result);
    }

    [Fact]
    public void Fix_InsideCodeBlock_NotModified()
    {
        var input = "```\nThis will return a value.\n```";
        var result = PresentTenseFixer.Fix(input);
        Assert.Equal(input, result);
    }

    // ── Multiple occurrences ────────────────────────────────────────

    [Fact]
    public void Fix_MultipleOccurrences_AllFixed()
    {
        var input = "The tool will return results. The output will be displayed.";
        var result = PresentTenseFixer.Fix(input);
        Assert.Contains("returns results", result);
        Assert.Contains("is displayed", result);
        Assert.DoesNotContain("will", result);
    }

    // ── Edge cases ──────────────────────────────────────────────────

    [Fact]
    public void Fix_NullOrEmpty_ReturnsInput()
    {
        Assert.Equal("", PresentTenseFixer.Fix(""));
        Assert.Equal("", PresentTenseFixer.Fix(null!));
    }

    [Fact]
    public void Fix_NoFutureTense_ReturnsUnchanged()
    {
        var input = "This is a normal sentence about Azure resources.";
        var result = PresentTenseFixer.Fix(input);
        Assert.Equal(input, result);
    }

    [Fact]
    public void Fix_WillInProperNoun_NotModified()
    {
        // "Will" as a proper noun should not be modified
        var input = "Will manages the deployment pipeline.";
        var result = PresentTenseFixer.Fix(input);
        Assert.Equal(input, result);
    }
}
