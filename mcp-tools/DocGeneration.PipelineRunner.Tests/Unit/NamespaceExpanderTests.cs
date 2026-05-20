using PipelineRunner.Services;
using Xunit;

namespace PipelineRunner.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="NamespaceExpander"/> — the service that resolves a requested
/// namespace string to a list of concrete CLI namespace names.
/// </summary>
public class NamespaceExpanderTests
{
    // ── helpers ──────────────────────────────────────────────────────────────

    private static BrandMappingEntry Entry(string mcpServerName, string? composition = null)
        => new(mcpServerName, $"Azure {mcpServerName}", mcpServerName, mcpServerName, composition);

    // ── null / all ────────────────────────────────────────────────────────────

    [Fact]
    public void Expand_NullNamespace_ReturnsAllCliNamespaces()
    {
        var cli = new[] { "advisor", "compute" };
        var result = NamespaceExpander.Expand(null, [], cli);

        Assert.True(result.IsAll);
        Assert.True(result.IsResolved);
        Assert.Equal(cli, result.Namespaces);
    }

    [Theory]
    [InlineData("all")]
    [InlineData("ALL")]
    [InlineData("All")]
    public void Expand_AllKeyword_ReturnsAllCliNamespaces(string keyword)
    {
        var cli = new[] { "advisor", "compute" };
        var result = NamespaceExpander.Expand(keyword, [], cli);

        Assert.True(result.IsAll);
        Assert.True(result.IsResolved);
        Assert.Equal(cli, result.Namespaces);
    }

    // ── exact match ───────────────────────────────────────────────────────────

    [Fact]
    public void Expand_ExactBrandMatch_ReturnsSingleNamespace_NotExpanded()
    {
        var entries = new[] { Entry("functionapp", "standalone"), Entry("compute", "standalone") };
        var cli = new[] { "functionapp", "compute" };

        var result = NamespaceExpander.Expand("functionapp", entries, cli);

        Assert.True(result.IsResolved);
        Assert.False(result.IsExpanded);
        Assert.Equal(["functionapp"], result.Namespaces);
    }

    [Fact]
    public void Expand_ExactBrandMatch_CaseInsensitive()
    {
        var entries = new[] { Entry("advisor", "standalone") };
        var cli = new[] { "advisor" };

        var result = NamespaceExpander.Expand("ADVISOR", entries, cli);

        Assert.True(result.IsResolved);
        Assert.False(result.IsExpanded);
        Assert.Equal(["advisor"], result.Namespaces);
    }

    // ── prefix expansion ──────────────────────────────────────────────────────

    [Fact]
    public void Expand_PrefixMatch_ExpandsToAllSubNamespaces()
    {
        var entries = new[]
        {
            Entry("extension_azqr", "split"),
            Entry("extension_cli_generate", "split"),
            Entry("extension_cli_install", "split"),
            Entry("advisor", "standalone"),
        };
        var cli = new[] { "extension_azqr", "extension_cli_generate", "extension_cli_install", "advisor" };

        var result = NamespaceExpander.Expand("extension", entries, cli);

        Assert.True(result.IsResolved);
        Assert.True(result.IsExpanded);
        Assert.Equal(3, result.Namespaces.Count);
        Assert.Contains("extension_azqr", result.Namespaces);
        Assert.Contains("extension_cli_generate", result.Namespaces);
        Assert.Contains("extension_cli_install", result.Namespaces);
        Assert.DoesNotContain("advisor", result.Namespaces);
    }

    [Fact]
    public void Expand_PrefixMatch_FilteredToCliAvailable()
    {
        var entries = new[]
        {
            Entry("extension_azqr", "split"),
            Entry("extension_cli_generate", "split"),
            Entry("extension_cli_install", "split"),
        };
        // CLI only has 2 of the 3 sub-entries
        var cli = new[] { "extension_azqr", "extension_cli_generate" };

        var result = NamespaceExpander.Expand("extension", entries, cli);

        Assert.True(result.IsResolved);
        Assert.True(result.IsExpanded);
        Assert.Equal(2, result.Namespaces.Count);
        Assert.DoesNotContain("extension_cli_install", result.Namespaces);
    }

    [Fact]
    public void Expand_PrefixMatch_ResultIsSortedAlphabetically()
    {
        var entries = new[]
        {
            Entry("extension_cli_install", "split"),
            Entry("extension_azqr", "split"),
            Entry("extension_cli_generate", "split"),
        };
        var cli = new[] { "extension_azqr", "extension_cli_generate", "extension_cli_install" };

        var result = NamespaceExpander.Expand("extension", entries, cli);

        Assert.Equal(["extension_azqr", "extension_cli_generate", "extension_cli_install"], result.Namespaces);
    }

    [Fact]
    public void Expand_PrefixMatch_CaseInsensitive()
    {
        var entries = new[] { Entry("monitor_logs", "split"), Entry("monitor_metrics", "split") };
        var cli = new[] { "monitor_logs", "monitor_metrics" };

        var result = NamespaceExpander.Expand("MONITOR", entries, cli);

        Assert.True(result.IsResolved);
        Assert.True(result.IsExpanded);
        Assert.Equal(2, result.Namespaces.Count);
    }

    // ── sub-entries not in CLI ────────────────────────────────────────────────

    [Fact]
    public void Expand_SubEntriesInBrandMappingButNotInCli_ReturnsSubEntriesNotInCli()
    {
        var entries = new[]
        {
            Entry("extension_azqr", "split"),
            Entry("extension_cli_generate", "split"),
        };
        // CLI does not expose any of the sub-namespaces yet
        var cli = new[] { "advisor" };

        var result = NamespaceExpander.Expand("extension", entries, cli);

        Assert.False(result.IsResolved);
        Assert.True(result.IsSubEntriesNotInCli);
        Assert.Equal("extension", result.RequestedNamespace);
        Assert.Contains("extension_azqr", result.SubEntriesFound);
        Assert.Contains("extension_cli_generate", result.SubEntriesFound);
    }

    // ── not in brand mapping ──────────────────────────────────────────────────

    [Fact]
    public void Expand_NotInBrandMapping_ReturnsNotInBrandMapping()
    {
        var entries = new[] { Entry("advisor", "standalone") };
        var cli = new[] { "advisor" };

        var result = NamespaceExpander.Expand("compute", entries, cli);

        Assert.False(result.IsResolved);
        Assert.True(result.IsNotInBrandMapping);
        Assert.Equal("compute", result.RequestedNamespace);
    }

    [Fact]
    public void Expand_EmptyBrandMapping_ReturnsNotInBrandMapping()
    {
        var result = NamespaceExpander.Expand("compute", [], ["compute"]);

        Assert.True(result.IsNotInBrandMapping);
    }

    // ── edge cases ────────────────────────────────────────────────────────────

    [Fact]
    public void Expand_WhitespaceIsTrimmedFromNamespace()
    {
        var entries = new[] { Entry("advisor", "standalone") };
        var cli = new[] { "advisor" };

        var result = NamespaceExpander.Expand("  advisor  ", entries, cli);

        Assert.True(result.IsResolved);
        Assert.Equal(["advisor"], result.Namespaces);
    }

    [Fact]
    public void Expand_NoPrefixPartialMatch_DoesNotExpandUnintended()
    {
        // "ext" should NOT match "extension_azqr" because the separator "_" is required
        var entries = new[] { Entry("extension_azqr", "split") };
        var cli = new[] { "extension_azqr" };

        var result = NamespaceExpander.Expand("ext", entries, cli);

        // Not an exact match, not a prefix match (extension_azqr doesn't start with "ext_")
        Assert.True(result.IsNotInBrandMapping);
    }
}
