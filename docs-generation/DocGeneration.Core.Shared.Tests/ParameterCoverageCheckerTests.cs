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
}
