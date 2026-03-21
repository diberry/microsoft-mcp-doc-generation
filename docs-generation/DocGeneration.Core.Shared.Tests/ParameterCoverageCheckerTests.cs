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

    // ── CLI switch prefix stripping (behavioral) ────────────────────

    [Fact]
    public void ParameterWithoutPrefix_Name_WithConcreteValue_ReturnsCovered()
    {
        // Parameter "name" (no --prefix) with prompt containing a concrete value
        var prompts = new[] { "Delete file share named 'myshare'" };
        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "name", 1);
        Assert.True(result.Covered);
    }

    [Fact]
    public void ParameterWithoutPrefix_MessageArray_WithJsonArray_ReturnsCovered()
    {
        // Parameter "message-array" (no --prefix) with JSON array value
        var prompts = new[] { "Send messages [{'role':'user','content':'hello'}]" };
        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "message-array", 2);
        Assert.True(result.Covered);
    }

    [Fact]
    public void ParameterWithoutPrefix_Param_WithConcreteValue_ReturnsCovered()
    {
        // Parameter "param" (no --prefix) with concrete value
        var prompts = new[] { "Get the server parameter named 'max_connections'" };
        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "param", 1);
        Assert.True(result.Covered);
    }

    // ── JSON array placeholder rejection ────────────────────────────

    [Fact]
    public void JsonArrayPlaceholder_ReturnsCoveredFalse()
    {
        // Placeholder-like content in brackets should not count as concrete
        var prompts = new[] { "Process items [{config}]" };
        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "items", 2);
        Assert.False(result.Covered, "Placeholder-like JSON array should not be covered");
    }

    [Fact]
    public void RealJsonArray_ReturnsCoveredTrue()
    {
        // Real JSON object array with concrete values should count as covered
        var prompts = new[] { "Process items [{'id': 1, 'name': 'test'}]" };
        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "items", 2);
        Assert.True(result.Covered, "Real JSON array should be covered");
    }

    // ── Issue #161: Array parameter patterns ─────────────────────────
    // AI-generated prompts for array params don't always include literal
    // JSON array syntax. The checker must handle natural language references.

    [Fact]
    public void ArrayParam_NaturalLanguageReference_WithConcreteValue()
    {
        // AI prompt: describes the messages but uses natural language, not JSON array
        var prompts = new[] { "Create a chat completion with the user message 'What is the weather today?'" };
        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "message-array", 4);
        Assert.True(result.Covered,
            "Array param 'message-array' should match when base word 'message' appears with a concrete quoted value");
    }

    [Fact]
    public void ArrayParam_PluralBaseWord_WithConcreteValue()
    {
        // AI uses plural form "messages" instead of exact param name
        var prompts = new[] { "Send messages 'Hello, how can I help?' to the chat completion endpoint" };
        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "message-array", 4);
        Assert.True(result.Covered,
            "Array param should match when plural base word 'messages' appears with concrete value");
    }

    [Fact]
    public void ArrayParam_JsonArraySyntax_WithObjects()
    {
        // Best case: AI actually includes JSON array syntax
        var prompts = new[] { "Create a chat completion with message array [{'role': 'user', 'content': 'hello'}]" };
        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "message-array", 4);
        Assert.True(result.Covered,
            "Array param with literal JSON array syntax should definitely be covered");
    }

    [Fact]
    public void ArrayParam_NoMention_ReturnsFalse()
    {
        // Negative: prompt doesn't mention the array param at all
        var prompts = new[] { "Create a chat completion using deployment 'gpt-4' in resource group 'rg-prod'" };
        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "message-array", 4);
        Assert.False(result.Covered,
            "Array param should NOT be covered when neither 'message' nor 'array' appears");
    }

    // ── Issue #161: Single-word parameter patterns ───────────────────
    // Params like "name", "key", "app" are common English words that appear
    // in many contexts. The checker must handle them robustly.

    [Fact]
    public void SingleWordParam_Name_InUpdateContext()
    {
        // AI prompt describes updating a named resource — "name" is implicit
        var prompts = new[] { "Update the file share 'analytics-share' to increase quota to 200 GB" };
        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "name", 2);
        Assert.True(result.Covered,
            "Single-word param 'name' should be covered when a concrete quoted resource name is the first/primary arg");
    }

    [Fact]
    public void SingleWordParam_Name_WithExplicitNamed()
    {
        // AI prompt uses "named" keyword with concrete value
        var prompts = new[] { "Update the file share named 'data-share' in resource group 'rg-storage'" };
        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "name", 2);
        Assert.True(result.Covered,
            "Single-word param 'name' should match 'named' + concrete value");
    }

    [Fact]
    public void SingleWordParam_Name_WithCalledKeyword()
    {
        // AI prompt uses "called" instead of "named"
        var prompts = new[] { "Delete the file share called 'temp-share'" };
        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "name", 1);
        Assert.True(result.Covered,
            "Single-word param 'name' should match when any concrete quoted resource identifier exists");
    }

    [Fact]
    public void ArrayParam_JsonInsideQuotes_ReturnsCovered()
    {
        // The EXACT pattern foundryextensions generates: JSON array inside single quotes
        var prompts = new[] { "Create a chat completion with message-array '[{\"role\":\"user\",\"content\":\"Hello\"}]' for resource-group 'rg-foundry'" };
        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "message-array", 4);
        Assert.True(result.Covered,
            "Quoted value containing JSON (braces/brackets inside quotes) should be accepted as concrete");
    }

    [Fact]
    public void SingleWordParam_Name_NoConcreteValue_ReturnsFalse()
    {
        // Negative: prompt uses "name" generically without concrete value
        var prompts = new[] { "Update the file share quota" };
        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "name", 2);
        Assert.False(result.Covered,
            "Single-word param 'name' should NOT be covered without any concrete value");
    }

    [Fact]
    public void SingleWordParam_Key_WithConcreteValue()
    {
        // Another single-word param: "key"
        var prompts = new[] { "Get the secret key named 'api-key-prod' from the vault" };
        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "key", 2);
        Assert.True(result.Covered,
            "Single-word param 'key' should match with concrete value");
    }

    [Fact]
    public void SingleWordParam_Param_WithConcreteValue()
    {
        // "param" — the postgres server-param-get case
        var prompts = new[] { "Get the server parameter 'max_connections' from the PostgreSQL server 'prod-pg'" };
        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "param", 2);
        Assert.True(result.Covered,
            "Single-word param 'param' should match 'parameter' + concrete value");
    }

    [Fact]
    public void SingleWordParam_Param_PluralForm()
    {
        // AI uses "parameters" (plural) instead of "param"
        var prompts = new[] { "List all server parameters for PostgreSQL server 'prod-pg'" };
        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "param", 2);
        Assert.True(result.Covered,
            "Single-word param 'param' should match its expanded form 'parameters'");
    }

    // ── Issue #161: Combined edge cases ──────────────────────────────

    [Fact]
    public void SingleWordParam_AsOnlyRequiredParam_WithQuotedValue()
    {
        // When a tool has only 1 required param and a quoted value exists anywhere
        var prompts = new[] { "Delete 'my-resource'" };
        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "name", 1);
        Assert.True(result.Covered,
            "Single required param 'name' with ANY quoted value in a 1-param tool should be covered");
    }

    [Fact]
    public void MultiplePrompts_OneHasCoverage_ReturnsCovered()
    {
        // At least one prompt in the set has concrete coverage
        var prompts = new[] {
            "Update the file share quota",  // no concrete name
            "Update file share 'data-share' to 500 GB"  // has concrete name
        };
        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "name", 2);
        Assert.True(result.Covered,
            "Should be covered if ANY prompt in the set has the parameter with concrete value");
    }
}
