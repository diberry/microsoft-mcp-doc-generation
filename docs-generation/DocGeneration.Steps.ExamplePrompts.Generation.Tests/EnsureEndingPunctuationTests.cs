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
}
