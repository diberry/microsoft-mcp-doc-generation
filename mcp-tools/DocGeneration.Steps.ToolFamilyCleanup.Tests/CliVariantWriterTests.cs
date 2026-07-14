// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using DocGeneration.Steps.ToolFamilyCleanup.Services;
using Xunit;

namespace DocGeneration.Steps.ToolFamilyCleanup.Tests;

public class CliVariantWriterTests
{
    // A realistic canonical (plain) family article with @mcpcli markers that
    // ApplyTabsToFamilyArticle keys off. Uses varied Azure services across tests.
    private const string PlainArticle = """
        ---
        ms.topic: include
        ---

        # Azure Key Vault tools

        ## Get secret
        <!-- @mcpcli keyvault secret get -->

        Get a secret from a key vault.

        | Parameter | Required or optional | Description |
        |-----------|---------------------|-------------|
        | **Vault** | Required | Key vault name |

        ---
        """;

    private const string CliTabHeader = "#### [Azure MCP CLI](#tab/azure-mcp-cli)";
    private const string McpTabHeader = "#### [MCP Server](#tab/mcp-server)";

    // ── ResolveVariantPath ────────────────────────────────────────

    [Fact]
    public void ResolveVariantPath_AppendsCliSuffixBeforeExtension()
    {
        var result = CliVariantWriter.ResolveVariantPath(Path.Combine("tool-family", "storage.md"));
        Assert.Equal(Path.Combine("tool-family", "storage-cli.md"), result);
    }

    [Fact]
    public void ResolveVariantPath_PreservesFullDirectory()
    {
        var canonical = Path.Combine("out", "generated-cosmos", "tool-family", "cosmos.md");
        var expected = Path.Combine("out", "generated-cosmos", "tool-family", "cosmos-cli.md");
        Assert.Equal(expected, CliVariantWriter.ResolveVariantPath(canonical));
    }

    [Fact]
    public void ResolveVariantPath_BareFileName_NoDirectory()
    {
        Assert.Equal("monitor-cli.md", CliVariantWriter.ResolveVariantPath("monitor.md"));
    }

    [Fact]
    public void ResolveVariantPath_HyphenatedNamespace_Preserved()
    {
        var result = CliVariantWriter.ResolveVariantPath(Path.Combine("tool-family", "app-service.md"));
        Assert.Equal(Path.Combine("tool-family", "app-service-cli.md"), result);
    }

    // ── BuildVariantContent ───────────────────────────────────────

    [Fact]
    public void BuildVariantContent_AllowedWithCliData_InjectsTabs()
    {
        var assembled = new Dictionary<string, string>
        {
            ["keyvault secret get"] = "Get a secret from a key vault.\n\n```bash\naz keyvault secret show\n```"
        };

        var result = CliVariantWriter.BuildVariantContent(PlainArticle, assembled, namespaceAllowed: true);

        Assert.Contains(McpTabHeader, result);
        Assert.Contains(CliTabHeader, result);
        Assert.Contains("az keyvault secret show", result);
    }

    [Fact]
    public void BuildVariantContent_DisabledNamespace_ReturnsExactCopy()
    {
        var assembled = new Dictionary<string, string>
        {
            ["speech recognize start"] = "Recognize speech.\n\n```bash\naz cognitiveservices\n```"
        };

        var result = CliVariantWriter.BuildVariantContent(PlainArticle, assembled, namespaceAllowed: false);

        Assert.Equal(PlainArticle, result);
        Assert.DoesNotContain(CliTabHeader, result);
    }

    [Fact]
    public void BuildVariantContent_NoCliData_ReturnsExactCopy()
    {
        var result = CliVariantWriter.BuildVariantContent(
            PlainArticle, new Dictionary<string, string>(), namespaceAllowed: true);

        Assert.Equal(PlainArticle, result);
        Assert.DoesNotContain(CliTabHeader, result);
    }

    [Fact]
    public void BuildVariantContent_NullCliData_ReturnsExactCopy()
    {
        var result = CliVariantWriter.BuildVariantContent(PlainArticle, null, namespaceAllowed: true);

        Assert.Equal(PlainArticle, result);
        Assert.DoesNotContain(CliTabHeader, result);
    }

    // ── WriteVariantsAsync (two-file guarantee) ───────────────────

    [Fact]
    public async Task WriteVariantsAsync_AllowedWithData_WritesTabbedCanonicalAndVariant()
    {
        var dir = CreateTempDir();
        try
        {
            var canonical = Path.Combine(dir, "aks.md");
            await File.WriteAllTextAsync(canonical, PlainArticle);
            var assembled = new Dictionary<string, string>
            {
                ["keyvault secret get"] = "Get a secret from a key vault.\n\n```bash\naz keyvault secret show\n```"
            };

            var variantPath = await CliVariantWriter.WriteVariantsAsync(canonical, assembled, namespaceAllowed: true);

            Assert.Equal(Path.Combine(dir, "aks-cli.md"), variantPath);
            // Canonical is the publishable article and must contain CLI tabs.
            var canonicalAfter = await File.ReadAllTextAsync(canonical);
            Assert.Contains(CliTabHeader, canonicalAfter);
            Assert.Contains(McpTabHeader, canonicalAfter);
            // Variant mirrors the tabbed canonical article.
            var variant = await File.ReadAllTextAsync(variantPath!);
            Assert.Contains(CliTabHeader, variant);
            Assert.Equal(canonicalAfter, variant);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task WriteVariantsAsync_NoCliData_StillWritesVariantAsCopy()
    {
        var dir = CreateTempDir();
        try
        {
            var canonical = Path.Combine(dir, "sql.md");
            await File.WriteAllTextAsync(canonical, PlainArticle);

            var variantPath = await CliVariantWriter.WriteVariantsAsync(canonical, null, namespaceAllowed: true);

            // Always-two guarantee: variant exists even with no CLI data.
            Assert.NotNull(variantPath);
            Assert.True(File.Exists(variantPath));
            var variant = await File.ReadAllTextAsync(variantPath!);
            Assert.Equal(PlainArticle, variant);
            Assert.DoesNotContain(CliTabHeader, variant);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task WriteVariantsAsync_DisabledNamespace_StillWritesVariantAsCopy()
    {
        var dir = CreateTempDir();
        try
        {
            var canonical = Path.Combine(dir, "monitor.md");
            await File.WriteAllTextAsync(canonical, PlainArticle);
            var assembled = new Dictionary<string, string>
            {
                ["monitor metrics query"] = "Query metrics.\n\n```bash\naz monitor metrics list\n```"
            };

            var variantPath = await CliVariantWriter.WriteVariantsAsync(canonical, assembled, namespaceAllowed: false);

            Assert.NotNull(variantPath);
            var variant = await File.ReadAllTextAsync(variantPath!);
            Assert.Equal(PlainArticle, variant);
            Assert.DoesNotContain(CliTabHeader, variant);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task WriteVariantsAsync_MissingCanonical_ReturnsNull_WritesNothing()
    {
        var dir = CreateTempDir();
        try
        {
            var canonical = Path.Combine(dir, "does-not-exist.md");

            var variantPath = await CliVariantWriter.WriteVariantsAsync(canonical, null, namespaceAllowed: true);

            Assert.Null(variantPath);
            Assert.False(File.Exists(Path.Combine(dir, "does-not-exist-cli.md")));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"cli-variant-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }
}
