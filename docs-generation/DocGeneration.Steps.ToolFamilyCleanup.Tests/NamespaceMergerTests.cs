// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Shared;
using ToolFamilyCleanup.Services;
using Xunit;

namespace DocGeneration.Steps.ToolFamilyCleanup.Tests;

/// <summary>
/// Tests for NamespaceMerger — merges tool-family articles from grouped
/// namespaces into a single article per AD-011.
/// </summary>
public class NamespaceMergerTests
{
    private static string BuildArticle(string title, string service, int toolCount,
        string overview, string[] tools, string relatedContent) =>
        string.Join("\n", new[]
        {
            "---",
            $"title: Azure MCP Server tools for {title}",
            $"description: Use Azure MCP Server tools for {title}.",
            "ms.service: azure-mcp-server",
            "ms.topic: concept-article",
            $"tool_count: {toolCount}",
            "---",
            "",
            $"# Azure MCP Server tools for {title}",
            "",
            overview,
            "",
            "[!INCLUDE [tip-about-params](../includes/tools/parameter-consideration.md)]",
            ""
        }.Concat(tools.SelectMany(t => new[] { t, "" }))
         .Concat(new[] { "## Related content", "", relatedContent }));

    // ── Core merge behavior ─────────────────────────────────────────

    [Fact]
    public void Merge_TwoNamespaces_CombinesToolSections()
    {
        var primary = BuildArticle("Azure Monitor", "monitor", 2,
            "Monitor overview.",
            new[] { "## Query metrics\n\nQuery metrics content.", "## List activity logs\n\nActivity log content." },
            "- [Monitor docs](/azure/azure-monitor/)");

        var secondary = BuildArticle("Azure Workbooks", "workbooks", 1,
            "Workbooks overview.",
            new[] { "## Create workbook\n\nCreate workbook content." },
            "- [Workbooks docs](/azure/azure-monitor/visualize/)");

        var config = new List<BrandMapping>
        {
            new() { McpServerName = "monitor", FileName = "azure-monitor",
                     MergeGroup = "azure-monitor", MergeOrder = 1, MergeRole = "primary" },
            new() { McpServerName = "workbooks", FileName = "azure-workbooks",
                     MergeGroup = "azure-monitor", MergeOrder = 2, MergeRole = "secondary" }
        };

        var articles = new Dictionary<string, string>
        {
            ["monitor"] = primary,
            ["workbooks"] = secondary
        };

        var result = NamespaceMerger.Merge(articles, config);

        // Should have one merged article
        Assert.Single(result);
        Assert.True(result.ContainsKey("azure-monitor"));

        var merged = result["azure-monitor"];

        // Primary's H1 and overview preserved
        Assert.Contains("# Azure MCP Server tools for Azure Monitor", merged);
        Assert.Contains("Monitor overview.", merged);

        // All 3 tool sections present
        Assert.Contains("## Query metrics", merged);
        Assert.Contains("## List activity logs", merged);
        Assert.Contains("## Create workbook", merged);

        // Secondary's overview NOT included
        Assert.DoesNotContain("Workbooks overview.", merged);

        // Related content from primary
        Assert.Contains("## Related content", merged);
        Assert.Contains("Monitor docs", merged);
    }

    [Fact]
    public void Merge_UpdatesToolCount()
    {
        var primary = BuildArticle("Azure Monitor", "monitor", 2,
            "Overview.",
            new[] { "## Tool A\n\nContent A.", "## Tool B\n\nContent B." },
            "- [Link](/azure/)");

        var secondary = BuildArticle("Azure Workbooks", "workbooks", 1,
            "Overview.",
            new[] { "## Tool C\n\nContent C." },
            "- [Link](/azure/)");

        var config = new List<BrandMapping>
        {
            new() { McpServerName = "monitor", MergeGroup = "azure-monitor", MergeOrder = 1, MergeRole = "primary", FileName = "azure-monitor" },
            new() { McpServerName = "workbooks", MergeGroup = "azure-monitor", MergeOrder = 2, MergeRole = "secondary", FileName = "azure-workbooks" }
        };

        var articles = new Dictionary<string, string> { ["monitor"] = primary, ["workbooks"] = secondary };
        var result = NamespaceMerger.Merge(articles, config);

        // tool_count should be 3 (2 + 1)
        Assert.Contains("tool_count: 3", result["azure-monitor"]);
    }

    // ── Ungrouped namespaces pass through ───────────────────────────

    [Fact]
    public void Merge_UngroupedNamespaces_PassThrough()
    {
        var storage = BuildArticle("Azure Storage", "storage", 3,
            "Storage overview.",
            new[] { "## List accounts\n\nContent.", "## Get blob\n\nContent.", "## Upload file\n\nContent." },
            "- [Storage docs](/azure/storage/)");

        var config = new List<BrandMapping>
        {
            new() { McpServerName = "storage", FileName = "azure-storage" }
        };

        var articles = new Dictionary<string, string> { ["storage"] = storage };
        var result = NamespaceMerger.Merge(articles, config);

        Assert.Single(result);
        Assert.True(result.ContainsKey("storage"));
        Assert.Equal(storage, result["storage"]);
    }

    // ── Edge cases ──────────────────────────────────────────────────

    [Fact]
    public void Merge_EmptyArticleMap_ReturnsEmpty()
    {
        var result = NamespaceMerger.Merge(
            new Dictionary<string, string>(),
            new List<BrandMapping>());

        Assert.Empty(result);
    }

    [Fact]
    public void Merge_MissingSecondaryArticle_UsesOnlyPrimary()
    {
        var primary = BuildArticle("Azure Monitor", "monitor", 2,
            "Overview.",
            new[] { "## Tool A\n\nContent.", "## Tool B\n\nContent." },
            "- [Link](/azure/)");

        var config = new List<BrandMapping>
        {
            new() { McpServerName = "monitor", MergeGroup = "azure-monitor", MergeOrder = 1, MergeRole = "primary", FileName = "azure-monitor" },
            new() { McpServerName = "workbooks", MergeGroup = "azure-monitor", MergeOrder = 2, MergeRole = "secondary", FileName = "azure-workbooks" }
        };

        // Only primary provided, secondary missing
        var articles = new Dictionary<string, string> { ["monitor"] = primary };
        var result = NamespaceMerger.Merge(articles, config);

        Assert.Single(result);
        Assert.Contains("## Tool A", result["azure-monitor"]);
        Assert.Contains("tool_count: 2", result["azure-monitor"]);
    }

    [Fact]
    public void Merge_OrderRespected_SecondaryToolsAfterPrimary()
    {
        var primary = BuildArticle("Azure Monitor", "monitor", 1,
            "Overview.",
            new[] { "## AAA Primary Tool\n\nFirst." },
            "- [Link](/azure/)");

        var secondary = BuildArticle("Azure Workbooks", "workbooks", 1,
            "Overview.",
            new[] { "## ZZZ Secondary Tool\n\nSecond." },
            "- [Link](/azure/)");

        var config = new List<BrandMapping>
        {
            new() { McpServerName = "monitor", MergeGroup = "azure-monitor", MergeOrder = 1, MergeRole = "primary", FileName = "azure-monitor" },
            new() { McpServerName = "workbooks", MergeGroup = "azure-monitor", MergeOrder = 2, MergeRole = "secondary", FileName = "azure-workbooks" }
        };

        var articles = new Dictionary<string, string> { ["monitor"] = primary, ["workbooks"] = secondary };
        var result = NamespaceMerger.Merge(articles, config);

        var merged = result["azure-monitor"];
        var primaryIdx = merged.IndexOf("## AAA Primary Tool");
        var secondaryIdx = merged.IndexOf("## ZZZ Secondary Tool");
        var relatedIdx = merged.IndexOf("## Related content");

        Assert.True(primaryIdx < secondaryIdx, "Primary tools should come before secondary");
        Assert.True(secondaryIdx < relatedIdx, "Secondary tools should come before Related content");
    }
}
