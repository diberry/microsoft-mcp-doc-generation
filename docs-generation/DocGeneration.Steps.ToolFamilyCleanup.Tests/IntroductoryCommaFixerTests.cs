// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ToolFamilyCleanup.Services;
using Xunit;

namespace DocGeneration.Steps.ToolFamilyCleanup.Tests;

/// <summary>
/// Tests for IntroductoryCommaFixer — deterministic post-processor that
/// inserts missing commas after introductory phrases per Microsoft style
/// guide. Fixes: #146 (Acrolinx GR-3: Comma after introductory phrase)
/// </summary>
public class IntroductoryCommaFixerTests
{
    // ── Core introductory phrase comma insertion ─────────────────────

    [Theory]
    [InlineData("For example you can list resources.", "For example, you can list resources.")]
    [InlineData("In addition you can filter results.", "In addition, you can filter results.")]
    [InlineData("By default the tool returns all results.", "By default, the tool returns all results.")]
    [InlineData("In this case the tool uses the default.", "In this case, the tool uses the default.")]
    [InlineData("If not the tool returns an error.", "If not, the tool returns an error.")]
    public void Fix_MissingCommaAfterIntroPhrase_CommaInserted(string input, string expected)
    {
        var result = IntroductoryCommaFixer.Fix(input);
        Assert.Equal(expected, result);
    }

    // ── Sentence-start only (mid-sentence should NOT be modified) ───

    [Fact]
    public void Fix_IntroPhrase_OnlyAtSentenceStart()
    {
        // "By default" mid-sentence should NOT get a comma
        var input = "The value is set by default to 10.";
        var result = IntroductoryCommaFixer.Fix(input);
        Assert.Equal(input, result);
    }

    [Fact]
    public void Fix_ForExample_AfterPeriod_CommaInserted()
    {
        // "For example" after a period = start of new sentence
        var input = "Use this tool. For example you can list storage accounts.";
        var result = IntroductoryCommaFixer.Fix(input);
        Assert.Contains("For example, you", result);
    }

    // ── Already correct — idempotent ────────────────────────────────

    [Theory]
    [InlineData("For example, you can list resources.")]
    [InlineData("In addition, you can filter results.")]
    [InlineData("By default, the tool returns all results.")]
    [InlineData("In this case, the tool uses the default.")]
    [InlineData("If not, the tool returns an error.")]
    public void Fix_AlreadyHasComma_NoChange(string input)
    {
        var result = IntroductoryCommaFixer.Fix(input);
        Assert.Equal(input, result);
    }

    // ── Content inside backticks should NOT be modified ──────────────

    [Fact]
    public void Fix_InsideBackticks_NotModified()
    {
        var input = "Use `for example` as the parameter value.";
        var result = IntroductoryCommaFixer.Fix(input);
        Assert.Contains("`for example`", result);
    }

    [Fact]
    public void Fix_InsideCodeBlock_NotModified()
    {
        var input = "```\nFor example you can list\n```";
        var result = IntroductoryCommaFixer.Fix(input);
        Assert.Equal(input, result);
    }

    // ── Multiple occurrences ────────────────────────────────────────

    [Fact]
    public void Fix_MultipleIntroPhrases_AllFixed()
    {
        var input = "For example you can list. In addition you can filter. By default it returns all.";
        var result = IntroductoryCommaFixer.Fix(input);
        Assert.Contains("For example, you", result);
        Assert.Contains("In addition, you", result);
        Assert.Contains("By default, it", result);
    }

    // ── Edge cases ──────────────────────────────────────────────────

    [Fact]
    public void Fix_NullOrEmpty_ReturnsInput()
    {
        Assert.Equal("", IntroductoryCommaFixer.Fix(""));
        Assert.Equal("", IntroductoryCommaFixer.Fix(null!));
    }

    [Fact]
    public void Fix_NoMatchingPhrases_ReturnsUnchanged()
    {
        var input = "This is a normal sentence about Azure resources.";
        var result = IntroductoryCommaFixer.Fix(input);
        Assert.Equal(input, result);
    }

    [Fact]
    public void Fix_PhraseAtVeryStartOfDocument_CommaInserted()
    {
        var input = "For example you can deploy to Azure.";
        var result = IntroductoryCommaFixer.Fix(input);
        Assert.Equal("For example, you can deploy to Azure.", result);
    }

    [Fact]
    public void Fix_PhraseAfterNewline_CommaInserted()
    {
        var input = "Use the tool.\nFor example you can list storage.";
        var result = IntroductoryCommaFixer.Fix(input);
        Assert.Contains("For example, you", result);
    }

    [Fact]
    public void Fix_PhraseAfterBullet_CommaInserted()
    {
        var input = "- For example you can list resources.";
        var result = IntroductoryCommaFixer.Fix(input);
        Assert.Contains("For example, you", result);
    }

    // ── Issue #280: "If not" must not corrupt longer phrases ────────

    [Theory]
    [InlineData(
        "If not specified the AZURE_SUBSCRIPTION_ID environment variable will be used instead.",
        "If not specified, the AZURE_SUBSCRIPTION_ID environment variable will be used instead.")]
    [InlineData(
        "If not provided a default is generated.",
        "If not provided, a default is generated.")]
    public void Fix_IfNotSpecified_CommaAfterFullPhrase(string input, string expected)
    {
        var result = IntroductoryCommaFixer.Fix(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("If not specified, the AZURE_SUBSCRIPTION_ID environment variable will be used.")]
    [InlineData("If not provided, a default value is used.")]
    public void Fix_IfNotSpecified_AlreadyCorrect_NoChange(string input)
    {
        var result = IntroductoryCommaFixer.Fix(input);
        Assert.Equal(input, result);
    }

    [Fact]
    public void Fix_IfNotSpecified_InTableRow_NoSpuriousComma()
    {
        // Real-world pattern from generated parameter tables (issue #280)
        var input = "| **Subscription** | Optional | Specifies the Azure subscription to use. If not specified the `AZURE_SUBSCRIPTION_ID` environment variable is used instead. |";
        var result = IntroductoryCommaFixer.Fix(input);
        Assert.Contains("If not specified, the", result);
        Assert.DoesNotContain("If not, specified", result);
    }

    [Fact]
    public void Fix_IfNot_StillWorksWhenNotPartOfLongerPhrase()
    {
        // Bare "If not" should still get a comma when it's the complete phrase
        var input = "If not the tool returns an error.";
        var result = IntroductoryCommaFixer.Fix(input);
        Assert.Equal("If not, the tool returns an error.", result);
    }
}
