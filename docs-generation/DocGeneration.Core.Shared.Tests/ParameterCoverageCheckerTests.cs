using Shared;
using Xunit;

namespace Shared.Tests;

public class ParameterCoverageCheckerTests
{
    // ── ConvertToSlug ───────────────────────────────────────────────

    [Theory]
    [InlineData("account", "account")]
    [InlineData("resource-group", "resource-group")]
    [InlineData("ResourceGroup", "resourcegroup")]
    [InlineData("", "")]
    [InlineData("  ", "")]
    public void ConvertToSlug_ReturnsExpected(string input, string expected)
    {
        var result = ParameterCoverageChecker.ConvertToSlug(input);
        Assert.Equal(expected, result);
    }

    // ── RemoveMarkup ────────────────────────────────────────────────

    [Theory]
    [InlineData("**bold**", "bold")]
    [InlineData("`code`", "code")]
    [InlineData("<em>html</em>", "html")]
    [InlineData("  extra   spaces  ", "extra spaces")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void RemoveMarkup_ReturnsExpected(string? input, string expected)
    {
        var result = ParameterCoverageChecker.RemoveMarkup(input!);
        Assert.Equal(expected, result);
    }

    // ── GetConcretePromptCoverage ───────────────────────────────────

    [Fact]
    public void ExactSlugMatch_WithConcreteValue_ReturnsCovered()
    {
        var prompts = new[] { "List resources for account named 'myaccount123'" };
        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "account", 1);

        Assert.True(result.Covered);
    }

    [Fact]
    public void MultiWordParameter_MatchesVariants()
    {
        // "resource-group" should match "resource group" and "resource_group"
        var prompts = new[] { "Deploy to resource group named 'my-rg'" };
        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "resource-group", 2);

        Assert.True(result.Covered);
    }

    [Fact]
    public void TypeSuffixStripping_SubscriptionName_MatchesBySubscription()
    {
        // "subscription-name" should strip "-name" suffix and match "subscription"
        var prompts = new[] { "Use subscription named 'my-sub-001'" };
        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "subscription-name", 1);

        Assert.True(result.Covered);
    }

    [Theory]
    [InlineData("<account>")]
    [InlineData("{account}")]
    [InlineData("[account]")]
    [InlineData("`account`")]
    public void PlaceholderDetection_VariousFormats(string placeholder)
    {
        var prompts = new[] { $"List resources for {placeholder}" };
        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "account", 1);

        Assert.False(result.Covered, "Placeholder should not count as concrete coverage");
        Assert.True(result.PlaceholderDetected, $"Should detect placeholder in: {placeholder}");
    }

    [Fact]
    public void DoubleWrappedPlaceholder_DetectsPlaceholder()
    {
        var prompts = new[] { "List resources for `<account>`" };
        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "account", 1);

        Assert.False(result.Covered);
        Assert.True(result.PlaceholderDetected);
    }

    [Fact]
    public void SemanticFallback_KeyNameMatchesKey()
    {
        // <key_name> should match parameter "key" via semantic word-level fallback
        var prompts = new[] { "Get secret with <key_name>" };
        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "key", 1);

        Assert.False(result.Covered);
        Assert.True(result.PlaceholderDetected);
    }

    [Fact]
    public void NoMatch_ReturnsCoveredFalse()
    {
        var prompts = new[] { "List all virtual machines in the region" };
        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "account", 2);

        Assert.False(result.Covered);
        Assert.False(result.PlaceholderDetected);
    }

    [Fact]
    public void EmptyPromptList_ReturnsCoveredFalse()
    {
        var prompts = Array.Empty<string>();
        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "account", 1);

        Assert.False(result.Covered);
        Assert.False(result.PlaceholderDetected);
    }

    // ── Defect regression tests ─────────────────────────────────────

    [Fact]
    public void SingleWordParameter_SubstringInLongerWord_WithConcreteValue_ReturnsCovered()
    {
        // Defect 1: "Name" should not match inside "named" incorrectly
        var prompts = new[] { "Delete the file share named 'myshare'" };
        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "Name", 1);
        Assert.True(result.Covered);
    }

    [Fact]
    public void ArrayParameter_JsonObjectArrayValue_ReturnsCovered()
    {
        // Defect 2: JSON objects in arrays should be valid concrete values
        var prompts = new[] { "Create chat completion with messages [{'role': 'user', 'content': 'hello'}]" };
        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "Message array", 2);
        Assert.True(result.Covered);
    }

    [Fact]
    public void MultiWordParameter_AtSentenceEnd_ReturnsCovered()
    {
        // Defect 3: Multi-word structural parameters at sentence end
        var prompts = new[] { "Generate an architecture diagram from the raw mcp tool input" };
        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "Raw mcp tool input", 2);
        Assert.True(result.Covered);
    }

    [Fact]
    public void AbbreviationInParentheses_FoundInPrompt_ReturnsCovered()
    {
        // Defect 4: Parenthetical abbreviation should match
        var prompts = new[] { "Create a new virtual machine scale set named 'my-vmss' in resource group 'rg-prod'" };
        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "Virtual machine scale set (VMSS) name", 2);
        Assert.True(result.Covered);
    }

    [Fact]
    public void VaguePrompt_NoConcreteValue_ReturnsFalse()
    {
        // Negative test: genuinely missing concrete value should still fail
        var prompts = new[] { "Get the app settings" };
        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "App", 2);
        Assert.False(result.Covered);
    }
}
