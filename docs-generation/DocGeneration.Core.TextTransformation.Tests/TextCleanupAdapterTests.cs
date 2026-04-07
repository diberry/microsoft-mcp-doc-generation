using Xunit;
using Azure.Mcp.TextTransformation.Services;

namespace DocGeneration.Core.TextTransformation.Tests;

/// <summary>
/// TDD tests for TextCleanupAdapter — the legacy-compatibility façade
/// that wraps NaturalLanguage's TextCleanup API and delegates to
/// TransformationEngine where proven byte-for-byte equivalent.
///
/// Phase 1 goal: zero behavior drift from original TextCleanup.
/// </summary>
public class TextCleanupAdapterTests
{
    // ─────────────────────────────────────────────
    // EnsureEndsPeriod — must match NL behavior exactly
    // ─────────────────────────────────────────────

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("The Azure subscription ID to use", "The Azure subscription ID to use.")]
    [InlineData("Specifies the Azure resource group.", "Specifies the Azure resource group.")]
    [InlineData("Is the resource currently available?", "Is the resource currently available?")]
    [InlineData("Resource provisioning failed!", "Resource provisioning failed!")]
    [InlineData("  The Cosmos DB account name  ", "The Cosmos DB account name.")]
    public void EnsureEndsPeriod_MatchesLegacyBehavior(string? input, string? expected)
    {
        var result = TextCleanupAdapter.EnsureEndsPeriod(input!);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Ask 'Why are requests timing out?'", "Ask 'Why are requests timing out?'")]
    [InlineData("Ask \"What is the status?\"", "Ask \"What is the status?\"")]
    [InlineData("Alert said \"Critical failure!\"", "Alert said \"Critical failure!\"")]
    [InlineData("Run command 'az show.'", "Run command 'az show.'")]
    [InlineData("Use resource 'my-resource'", "Use resource 'my-resource'.")]
    [InlineData("Show vault \"my-vault\"", "Show vault \"my-vault\".")]
    public void EnsureEndsPeriod_HandlesTrailingQuotes(string input, string expected)
    {
        var result = TextCleanupAdapter.EnsureEndsPeriod(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void EnsureEndsPeriod_IsIdempotent()
    {
        var input = "The Azure subscription ID to use";
        var first = TextCleanupAdapter.EnsureEndsPeriod(input);
        var second = TextCleanupAdapter.EnsureEndsPeriod(first);
        Assert.Equal("The Azure subscription ID to use.", second);
    }

    // ─────────────────────────────────────────────
    // CleanAIGeneratedText — unique to NL, must be preserved
    // ─────────────────────────────────────────────

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("Hello world", "Hello world")]
    public void CleanAIGeneratedText_HandlesNullAndEmpty(string? input, string? expected)
    {
        var result = TextCleanupAdapter.CleanAIGeneratedText(input!);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void CleanAIGeneratedText_ReplacesSmartQuotes()
    {
        // U+2018, U+2019 → '  and  U+201C, U+201D → "
        var input = "\u2018hello\u2019 \u201Cworld\u201D";
        var expected = "'hello' \"world\"";
        Assert.Equal(expected, TextCleanupAdapter.CleanAIGeneratedText(input));
    }

    [Fact]
    public void CleanAIGeneratedText_ReplacesHtmlEntities()
    {
        var input = "&quot;test&quot; &apos;value&apos; &amp; &lt;tag&gt;";
        var expected = "\"test\" 'value' & <tag>";
        Assert.Equal(expected, TextCleanupAdapter.CleanAIGeneratedText(input));
    }

    [Fact]
    public void CleanAIGeneratedText_ReplacesNumericEntities()
    {
        var input = "&#34;double&#34; &#39;single&#39;";
        var expected = "\"double\" 'single'";
        Assert.Equal(expected, TextCleanupAdapter.CleanAIGeneratedText(input));
    }

    // ─────────────────────────────────────────────
    // WrapExampleValues — unique to NL, must be preserved
    // ─────────────────────────────────────────────

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("No examples here", "No examples here")]
    public void WrapExampleValues_HandlesNullEmptyNoMatch(string? input, string? expected)
    {
        var result = TextCleanupAdapter.WrapExampleValues(input!);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void WrapExampleValues_WrapsSingleValue()
    {
        var input = "The format (for example, json)";
        var expected = "The format (for example, `json`)";
        Assert.Equal(expected, TextCleanupAdapter.WrapExampleValues(input));
    }

    [Fact]
    public void WrapExampleValues_WrapsMultipleValues()
    {
        var input = "Choose a format (for example, json, xml, yaml)";
        var expected = "Choose a format (for example, `json`, `xml`, `yaml`)";
        Assert.Equal(expected, TextCleanupAdapter.WrapExampleValues(input));
    }

    [Fact]
    public void WrapExampleValues_HandlesValueWithExplanation()
    {
        var input = "Set the period (for example, PT1H for 1 hour)";
        var expected = "Set the period (for example, `PT1H` for 1 hour)";
        Assert.Equal(expected, TextCleanupAdapter.WrapExampleValues(input));
    }

    // ─────────────────────────────────────────────
    // NormalizeParameter — hyphen-split legacy behavior
    // ─────────────────────────────────────────────

    [Fact]
    public void NormalizeParameter_ReturnsUnknown_ForNull()
    {
        Assert.Equal("Unknown", TextCleanupAdapter.NormalizeParameter(null!));
    }

    [Fact]
    public void NormalizeParameter_ReturnsUnknown_ForEmpty()
    {
        Assert.Equal("Unknown", TextCleanupAdapter.NormalizeParameter(""));
    }

    [Fact]
    public void NormalizeParameter_StripsLeadingDashes()
    {
        // "--resource-group" should strip the "--" prefix
        var result = TextCleanupAdapter.NormalizeParameter("--resource-group");
        Assert.DoesNotContain("--", result);
    }

    [Fact]
    public void NormalizeParameter_TransformsAcronyms()
    {
        // "id" → "ID", "vm" → "VM"
        Assert.Equal("ID", TextCleanupAdapter.NormalizeParameter("id"));
    }

    [Fact]
    public void NormalizeParameter_SplitsHyphensAndCapitalizes()
    {
        // "resource-group" → "Resource group"
        var result = TextCleanupAdapter.NormalizeParameter("resource-group");
        Assert.Equal("Resource group", result);
    }

    [Fact]
    public void NormalizeParameter_PreservesAllWords()
    {
        // "resource-group-name" → "Resource group name" (preserves "name")
        var result = TextCleanupAdapter.NormalizeParameter("resource-group-name");
        Assert.Equal("Resource group name", result);
    }

    [Fact]
    public void NormalizeParameter_HandlesVmId()
    {
        // "vm-id" → "VM ID"
        var result = TextCleanupAdapter.NormalizeParameter("vm-id");
        Assert.Equal("VM ID", result);
    }

    // ─────────────────────────────────────────────
    // ReplaceStaticText — brand term replacement
    // ─────────────────────────────────────────────

    [Fact]
    public void ReplaceStaticText_ReturnsInput_WhenNotInitialized()
    {
        // Without LoadFiles, should return text unchanged
        var adapter = new TextCleanupAdapter();
        Assert.Equal("hello world", adapter.ReplaceStaticText("hello world"));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    public void ReplaceStaticText_HandlesNullAndEmpty(string? input, string? expected)
    {
        var adapter = new TextCleanupAdapter();
        var result = adapter.ReplaceStaticText(input!);
        Assert.Equal(expected, result);
    }

    // ─────────────────────────────────────────────
    // LoadFiles — initialization contract
    // ─────────────────────────────────────────────

    [Fact]
    public void LoadFiles_NullList_ReturnsFalse()
    {
        var adapter = new TextCleanupAdapter();
        Assert.False(adapter.LoadFiles(null!));
    }

    [Fact]
    public void LoadFiles_EmptyList_ReturnsFalse()
    {
        var adapter = new TextCleanupAdapter();
        Assert.False(adapter.LoadFiles(new List<string>()));
    }

    // ─────────────────────────────────────────────
    // Adapter exposes full legacy API surface
    // ─────────────────────────────────────────────

    [Fact]
    public void Adapter_ExposesSamePublicMethods_AsTextCleanup()
    {
        // Verify all expected methods exist via reflection
        var adapterType = typeof(TextCleanupAdapter);

        Assert.NotNull(adapterType.GetMethod("EnsureEndsPeriod"));
        Assert.NotNull(adapterType.GetMethod("CleanAIGeneratedText"));
        Assert.NotNull(adapterType.GetMethod("WrapExampleValues"));
        Assert.NotNull(adapterType.GetMethod("NormalizeParameter"));
        Assert.NotNull(adapterType.GetMethod("ReplaceStaticText",
            new[] { typeof(string) }));
        Assert.NotNull(adapterType.GetMethod("LoadFiles"));
    }
}
