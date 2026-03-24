// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ToolFamilyCleanup.Services;
using Xunit;

namespace DocGeneration.Steps.ToolFamilyCleanup.Tests;

/// <summary>
/// Tests for LearnUrlRelativizer — deterministic post-processor that
/// converts full https://learn.microsoft.com URLs to site-root-relative
/// paths per AD-017 (Link Format Convention). Fixes: #220
/// </summary>
public class LearnUrlRelativizerTests
{
    // ── Basic URL conversion ────────────────────────────────────────

    [Theory]
    [InlineData(
        "https://learn.microsoft.com/azure/developer/azure-mcp-server/overview",
        "/azure/developer/azure-mcp-server/overview")]
    [InlineData(
        "https://learn.microsoft.com/azure/storage/",
        "/azure/storage/")]
    [InlineData(
        "https://learn.microsoft.com/azure/cosmos-db/",
        "/azure/cosmos-db/")]
    public void Relativize_FullLearnUrl_ReturnsRelativePath(string input, string expected)
    {
        var result = LearnUrlRelativizer.Relativize(input);
        Assert.Equal(expected, result);
    }

    // ── Locale stripping ────────────────────────────────────────────

    [Theory]
    [InlineData(
        "https://learn.microsoft.com/en-us/azure/cosmos-db/",
        "/azure/cosmos-db/")]
    [InlineData(
        "https://learn.microsoft.com/en-us/azure/storage/blobs/overview",
        "/azure/storage/blobs/overview")]
    public void Relativize_WithLocale_StripsLocaleAndPrefix(string input, string expected)
    {
        var result = LearnUrlRelativizer.Relativize(input);
        Assert.Equal(expected, result);
    }

    // ── Markdown link context ───────────────────────────────────────

    [Fact]
    public void Relativize_InsideMarkdownLink_ReplacesUrl()
    {
        var input = "[Azure Advisor documentation](https://learn.microsoft.com/azure/advisor/)";
        var expected = "[Azure Advisor documentation](/azure/advisor/)";
        var result = LearnUrlRelativizer.Relativize(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Relativize_InsideMarkdownLinkWithLocale_ReplacesUrl()
    {
        var input = "[Key Vault Best Practices](https://learn.microsoft.com/en-us/azure/key-vault/general/best-practices)";
        var expected = "[Key Vault Best Practices](/azure/key-vault/general/best-practices)";
        var result = LearnUrlRelativizer.Relativize(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Relativize_MultipleLinksInParagraph_ReplacesAll()
    {
        var input = "See [Advisor](https://learn.microsoft.com/azure/advisor/) and [Storage](https://learn.microsoft.com/en-us/azure/storage/).";
        var expected = "See [Advisor](/azure/advisor/) and [Storage](/azure/storage/).";
        var result = LearnUrlRelativizer.Relativize(input);
        Assert.Equal(expected, result);
    }

    // ── URLs inside backticks should NOT be modified ────────────────

    [Fact]
    public void Relativize_InsideBackticks_NotModified()
    {
        var input = "Use `https://learn.microsoft.com/azure/storage/` as the endpoint.";
        var result = LearnUrlRelativizer.Relativize(input);
        Assert.Equal(input, result);
    }

    [Fact]
    public void Relativize_InsideCodeBlock_NotModified()
    {
        var input = "```\nhttps://learn.microsoft.com/azure/storage/\n```";
        var result = LearnUrlRelativizer.Relativize(input);
        Assert.Equal(input, result);
    }

    // ── Non-learn URLs should NOT be modified ───────────────────────

    [Theory]
    [InlineData("https://github.com/microsoft/azure-mcp-server")]
    [InlineData("https://portal.azure.com")]
    [InlineData("https://docs.github.com/en/rest")]
    public void Relativize_NonLearnUrl_NotModified(string input)
    {
        var result = LearnUrlRelativizer.Relativize(input);
        Assert.Equal(input, result);
    }

    // ── Already-relative paths should pass through unchanged ────────

    [Theory]
    [InlineData("/azure/developer/azure-mcp-server/overview")]
    [InlineData("/azure/storage/")]
    [InlineData("../includes/azure-mcp-server-overview.md")]
    public void Relativize_AlreadyRelative_NoChange(string input)
    {
        var result = LearnUrlRelativizer.Relativize(input);
        Assert.Equal(input, result);
    }

    // ── Query params and anchors preserved ──────────────────────────

    [Theory]
    [InlineData(
        "https://learn.microsoft.com/azure/storage/blobs?view=azure-cli-latest",
        "/azure/storage/blobs?view=azure-cli-latest")]
    [InlineData(
        "https://learn.microsoft.com/en-us/azure/cosmos-db/introduction#key-benefits",
        "/azure/cosmos-db/introduction#key-benefits")]
    [InlineData(
        "https://learn.microsoft.com/azure/storage/?tabs=overview#section",
        "/azure/storage/?tabs=overview#section")]
    public void Relativize_WithQueryParamsOrAnchors_PreservesTrailing(string input, string expected)
    {
        var result = LearnUrlRelativizer.Relativize(input);
        Assert.Equal(expected, result);
    }

    // ── Null/empty edge cases ───────────────────────────────────────

    [Fact]
    public void Relativize_NullOrEmpty_ReturnsInput()
    {
        Assert.Equal("", LearnUrlRelativizer.Relativize(""));
        Assert.Equal("", LearnUrlRelativizer.Relativize(null!));
    }

    // ── Idempotent — running twice gives same result ────────────────

    [Fact]
    public void Relativize_RunTwice_Idempotent()
    {
        var input = "See [docs](https://learn.microsoft.com/azure/advisor/) for details.";
        var first = LearnUrlRelativizer.Relativize(input);
        var second = LearnUrlRelativizer.Relativize(first);
        Assert.Equal(first, second);
    }

    // ── Real-world generated content regression test ────────────────

    [Fact]
    public void Relativize_RealGeneratedIntro_ConvertsUrl()
    {
        var input = "Azure Advisor is a personalized cloud consultant; for more information, see [Azure Advisor documentation](https://learn.microsoft.com/azure/advisor/).";
        var expected = "Azure Advisor is a personalized cloud consultant; for more information, see [Azure Advisor documentation](/azure/advisor/).";
        var result = LearnUrlRelativizer.Relativize(input);
        Assert.Equal(expected, result);
    }

    // ── Non-azure learn paths should also be relativized ────────────

    [Theory]
    [InlineData(
        "https://learn.microsoft.com/cli/azure/acr",
        "/cli/azure/acr")]
    [InlineData(
        "https://learn.microsoft.com/en-us/dotnet/api/overview",
        "/dotnet/api/overview")]
    public void Relativize_NonAzureLearnPath_StillRelativized(string input, string expected)
    {
        var result = LearnUrlRelativizer.Relativize(input);
        Assert.Equal(expected, result);
    }
}
