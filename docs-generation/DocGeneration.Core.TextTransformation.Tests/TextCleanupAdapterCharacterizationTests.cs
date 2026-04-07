using Xunit;
using Azure.Mcp.TextTransformation.Services;

namespace DocGeneration.Core.TextTransformation.Tests;

/// <summary>
/// Characterization tests that load real NL-format test data files
/// and verify TextCleanupAdapter produces identical results to known
/// TextCleanup outputs for a broad set of real-world inputs.
/// </summary>
public class TextCleanupAdapterCharacterizationTests : IDisposable
{
    private static readonly string TestDataDir = Path.Combine(
        AppContext.BaseDirectory, "TestData");

    private static readonly string NlParamsFile = Path.Combine(TestDataDir, "test-nl-parameters.json");
    private static readonly string StaticTextFile = Path.Combine(TestDataDir, "test-static-text-replacement.json");

    private readonly TextCleanupAdapter _adapter;

    public TextCleanupAdapterCharacterizationTests()
    {
        _adapter = new TextCleanupAdapter();

        // Require test data files to exist
        if (File.Exists(NlParamsFile) && File.Exists(StaticTextFile))
        {
            var loaded = _adapter.LoadFiles(new List<string> { NlParamsFile, StaticTextFile });
            Assert.True(loaded, "TextCleanupAdapter.LoadFiles should return true with valid test data");
        }
    }

    public void Dispose() { }

    // ─────────────────────────────────────────────
    // LoadFiles with real data
    // ─────────────────────────────────────────────

    [Fact]
    public void LoadFiles_WithTestData_SetsInitialized()
    {
        Assert.True(_adapter.IsInitialized);
    }

    [Fact]
    public void LoadFiles_IsIdempotent_SecondCallSucceeds()
    {
        var adapter = new TextCleanupAdapter();
        var files = new List<string> { NlParamsFile, StaticTextFile };
        Assert.True(adapter.LoadFiles(files));
        Assert.True(adapter.LoadFiles(files));
        Assert.True(adapter.IsInitialized);
    }

    // ─────────────────────────────────────────────
    // ReplaceStaticText — brand term replacements from test data
    // ─────────────────────────────────────────────

    [Fact]
    public void ReplaceStaticText_AzureAD_ReplacedWithEntraID()
    {
        var result = _adapter.ReplaceStaticText("Configure Azure Active Directory for your app");
        Assert.Contains("Microsoft Entra ID", result);
        Assert.DoesNotContain("Azure Active Directory", result);
    }

    [Fact]
    public void ReplaceStaticText_AzureAD_ShortForm()
    {
        var result = _adapter.ReplaceStaticText("Use Azure AD for authentication");
        Assert.Contains("Microsoft Entra ID", result);
    }

    [Fact]
    public void ReplaceStaticText_CosmosDB_Replaced()
    {
        var result = _adapter.ReplaceStaticText("Store data in CosmosDB");
        Assert.Contains("Azure Cosmos DB", result);
    }

    [Fact]
    public void ReplaceStaticText_InclusiveLanguage_Whitelist()
    {
        var result = _adapter.ReplaceStaticText("Add to the whitelist");
        Assert.Contains("allowlist", result);
        Assert.DoesNotContain("whitelist", result);
    }

    [Fact]
    public void ReplaceStaticText_InclusiveLanguage_Blacklist()
    {
        var result = _adapter.ReplaceStaticText("Remove from the blacklist");
        Assert.Contains("blocklist", result);
    }

    [Fact]
    public void ReplaceStaticText_Leverage_ReplacedWithUse()
    {
        var result = _adapter.ReplaceStaticText("We leverage this feature");
        Assert.Contains("use", result);
        Assert.DoesNotContain("leverage", result);
    }

    [Fact]
    public void ReplaceStaticText_Utilize_ReplacedWithUse()
    {
        var result = _adapter.ReplaceStaticText("We utilize Azure VMs");
        Assert.Contains("use", result);
        Assert.DoesNotContain("utilize", result);
    }

    [Fact]
    public void ReplaceStaticText_MasterBranch_Replaced()
    {
        var result = _adapter.ReplaceStaticText("Push to the master branch");
        Assert.Contains("main branch", result);
    }

    [Fact]
    public void ReplaceStaticText_MultipleReplacements_InSameText()
    {
        var result = _adapter.ReplaceStaticText("Use Azure AD to whitelist users in CosmosDB");
        Assert.Contains("Microsoft Entra ID", result);
        Assert.Contains("allowlist", result);
        Assert.Contains("Azure Cosmos DB", result);
    }

    [Fact]
    public void ReplaceStaticText_NoMatch_ReturnsUnchanged()
    {
        var input = "This text has no replaceable terms";
        var result = _adapter.ReplaceStaticText(input);
        Assert.Equal(input, result);
    }

    [Fact]
    public void ReplaceStaticText_CaseInsensitive()
    {
        var result = _adapter.ReplaceStaticText("Store data in cosmosdb");
        Assert.Contains("Azure Cosmos DB", result);
    }

    // ─────────────────────────────────────────────
    // NormalizeParameter — comprehensive characterization
    // ─────────────────────────────────────────────

    [Theory]
    [InlineData("subscription", "Subscription")]
    [InlineData("resource-group", "Resource group")]
    [InlineData("resource-group-name", "Resource group name")]
    [InlineData("vm-id", "VM ID")]
    [InlineData("api-version", "API version")]
    [InlineData("dns-name", "DNS name")]
    [InlineData("sku", "SKU")]
    [InlineData("cpu-count", "CPU count")]
    [InlineData("ssl-enabled", "SSL enabled")]
    [InlineData("tls-version", "TLS version")]
    [InlineData("http-port", "HTTP port")]
    [InlineData("json-output", "JSON output")]
    [InlineData("oauth-token", "OAuth token")]
    [InlineData("cdn-endpoint", "CDN endpoint")]
    public void NormalizeParameter_AcronymHandling(string input, string expected)
    {
        Assert.Equal(expected, TextCleanupAdapter.NormalizeParameter(input));
    }

    [Theory]
    [InlineData("--subscription", "Subscription")]
    [InlineData("--resource-group", "Resource group")]
    [InlineData("--vm-id", "VM ID")]
    public void NormalizeParameter_StripsDashDashPrefix(string input, string expected)
    {
        Assert.Equal(expected, TextCleanupAdapter.NormalizeParameter(input));
    }

    // ─────────────────────────────────────────────
    // Composed pipeline: ReplaceStaticText + EnsureEndsPeriod
    // (mirrors how callers typically chain these)
    // ─────────────────────────────────────────────

    [Fact]
    public void ComposedPipeline_ReplaceAndEnsurePeriod()
    {
        var input = "This uses Azure AD for authentication";
        var replaced = _adapter.ReplaceStaticText(input);
        var result = TextCleanupAdapter.EnsureEndsPeriod(replaced);

        Assert.Contains("Microsoft Entra ID", result);
        Assert.EndsWith(".", result);
    }

    [Fact]
    public void ComposedPipeline_WrapExamplesAndEnsurePeriod()
    {
        var input = "Choose a format (for example, json) for the output";
        var wrapped = TextCleanupAdapter.WrapExampleValues(input);
        var result = TextCleanupAdapter.EnsureEndsPeriod(wrapped);

        Assert.Contains("`json`", result);
        Assert.EndsWith(".", result);
    }
}
