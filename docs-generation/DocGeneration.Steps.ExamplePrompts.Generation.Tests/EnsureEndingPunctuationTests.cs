using Xunit;
using ExamplePromptGeneratorStandalone.Generators;

namespace ExamplePromptGeneratorStandalone.Tests;

public class EnsureEndingPunctuationTests
{
    // ─────────────────────────────────────────────────
    // Already-punctuated prompts are unchanged
    // ─────────────────────────────────────────────────

    [Fact]
    public void LeavesPromptEndingWithPeriod()
    {
        var result = ExamplePromptGenerator.EnsureEndingPunctuation("List all storage accounts.");
        Assert.Equal("List all storage accounts.", result);
    }

    [Fact]
    public void LeavesPromptEndingWithQuestionMark()
    {
        var result = ExamplePromptGenerator.EnsureEndingPunctuation("What secrets are in my vault?");
        Assert.Equal("What secrets are in my vault?", result);
    }

    [Fact]
    public void LeavesPromptEndingWithExclamationMark()
    {
        var result = ExamplePromptGenerator.EnsureEndingPunctuation("Delete all expired keys now!");
        Assert.Equal("Delete all expired keys now!", result);
    }

    // ─────────────────────────────────────────────────
    // Missing punctuation gets a period appended
    // ─────────────────────────────────────────────────

    [Fact]
    public void AppendsPeriodWhenNoPunctuation()
    {
        var result = ExamplePromptGenerator.EnsureEndingPunctuation("List all storage accounts");
        Assert.Equal("List all storage accounts.", result);
    }

    [Fact]
    public void AppendsPeriodToPromptEndingWithClosingQuote()
    {
        var result = ExamplePromptGenerator.EnsureEndingPunctuation("Show details for vault \"my-vault\"");
        Assert.Equal("Show details for vault \"my-vault\".", result);
    }

    // ─────────────────────────────────────────────────
    // Trailing whitespace is trimmed before checking
    // ─────────────────────────────────────────────────

    [Fact]
    public void TrimsTrailingWhitespaceBeforeAppending()
    {
        var result = ExamplePromptGenerator.EnsureEndingPunctuation("List all resources   ");
        Assert.Equal("List all resources.", result);
    }

    [Fact]
    public void TrimsTrailingWhitespaceWhenAlreadyPunctuated()
    {
        var result = ExamplePromptGenerator.EnsureEndingPunctuation("List all resources.   ");
        Assert.Equal("List all resources.", result);
    }

    // ─────────────────────────────────────────────────
    // Edge cases
    // ─────────────────────────────────────────────────

    [Fact]
    public void ReturnsEmptyForEmptyInput()
    {
        var result = ExamplePromptGenerator.EnsureEndingPunctuation("");
        Assert.Equal("", result);
    }

    [Fact]
    public void ReturnsEmptyForWhitespaceOnlyInput()
    {
        var result = ExamplePromptGenerator.EnsureEndingPunctuation("   ");
        Assert.Equal("", result);
    }

    // Punctuation before closing quotes - no double punctuation

    [Fact]
    public void NoPeriodWhenQuestionMarkBeforeSingleQuote()
    {
        var result = ExamplePromptGenerator.EnsureEndingPunctuation(
            "Ask the question 'Why are requests timing out?'");
        Assert.Equal("Ask the question 'Why are requests timing out?'", result);
    }

    [Fact]
    public void NoPeriodWhenQuestionMarkBeforeDoubleQuote()
    {
        var result = ExamplePromptGenerator.EnsureEndingPunctuation(
            "Ask \"What is the status?\"");
        Assert.Equal("Ask \"What is the status?\"", result);
    }

    [Fact]
    public void NoPeriodWhenPeriodBeforeSingleQuote()
    {
        var result = ExamplePromptGenerator.EnsureEndingPunctuation(
            "Run the command 'az account show.'");
        Assert.Equal("Run the command 'az account show.'", result);
    }

    [Fact]
    public void NoPeriodWhenExclamationBeforeDoubleQuote()
    {
        var result = ExamplePromptGenerator.EnsureEndingPunctuation(
            "Alert said \"Critical failure!\"");
        Assert.Equal("Alert said \"Critical failure!\"", result);
    }

    [Fact]
    public void AppendsPeriodWhenNoEndPunctuationBeforeSingleQuote()
    {
        var result = ExamplePromptGenerator.EnsureEndingPunctuation(
            "Use resource 'my-resource'");
        Assert.Equal("Use resource 'my-resource'.", result);
    }

    [Fact]
    public void AppendsPeriodWhenNoEndPunctuationBeforeDoubleQuote()
    {
        var result = ExamplePromptGenerator.EnsureEndingPunctuation(
            "Show details for vault \"my-vault\"");
        Assert.Equal("Show details for vault \"my-vault\".", result);
    }

    [Fact]
    public void NoPeriodWhenQuestionMarkBeforeBacktick()
    {
        var result = ExamplePromptGenerator.EnsureEndingPunctuation(
            "Ask `Why is latency high?`");
        Assert.Equal("Ask `Why is latency high?`", result);
    }

    [Fact]
    public void AppendsPeriodWhenNoEndPunctuationBeforeBacktick()
    {
        var result = ExamplePromptGenerator.EnsureEndingPunctuation(
            "Run `az account show`");
        Assert.Equal("Run `az account show`.", result);
    }
}
