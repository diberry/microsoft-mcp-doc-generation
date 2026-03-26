// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ToolFamilyCleanup.Services;
using Xunit;

namespace DocGeneration.Steps.ToolFamilyCleanup.Tests;

public class MissingToolDetectorTests
{
    // ════════════════════════════════════════════════════════════════════════
    //  DetectMissingTools — all tools present
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public void DetectMissingTools_AllToolsPresent_ReturnsEmptyList()
    {
        var expectedTools = new[] { "List storage accounts", "Get storage account" };
        var article = string.Join("\n", new[]
        {
            "---",
            "title: Storage tools",
            "tool_count: 2",
            "---",
            "# Storage tools",
            "",
            "## List storage accounts",
            "<!-- @mcpcli storage account list -->",
            "Lists all storage accounts.",
            "",
            "## Get storage account",
            "<!-- @mcpcli storage account get -->",
            "Gets a storage account.",
            "",
            "## Related content",
            "- [Link](https://example.com)"
        });

        var missing = MissingToolDetector.DetectMissingTools(expectedTools, article);

        Assert.Empty(missing);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  DetectMissingTools — one tool missing
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public void DetectMissingTools_OneMissing_ReturnsMissingTool()
    {
        var expectedTools = new[] { "List storage accounts", "Get storage account", "Create storage account" };
        var article = string.Join("\n", new[]
        {
            "---",
            "title: Storage tools",
            "tool_count: 3",
            "---",
            "# Storage tools",
            "",
            "## List storage accounts",
            "<!-- @mcpcli storage account list -->",
            "Lists all storage accounts.",
            "",
            "## Get storage account",
            "<!-- @mcpcli storage account get -->",
            "Gets a storage account.",
            "",
            "## Related content",
            "- [Link](https://example.com)"
        });

        var missing = MissingToolDetector.DetectMissingTools(expectedTools, article);

        Assert.Single(missing);
        Assert.Equal("Create storage account", missing[0]);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  DetectMissingTools — multiple tools missing
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public void DetectMissingTools_MultipleMissing_ReturnsAllMissingSorted()
    {
        var expectedTools = new[]
        {
            "List Key Vault secrets",
            "Get Key Vault secret",
            "Delete Key Vault secret",
            "Set Key Vault secret"
        };
        var article = string.Join("\n", new[]
        {
            "# Key Vault tools",
            "",
            "## List Key Vault secrets",
            "Lists secrets.",
            "",
            "## Related content",
            "- Link"
        });

        var missing = MissingToolDetector.DetectMissingTools(expectedTools, article);

        Assert.Equal(3, missing.Count);
        Assert.Equal("Delete Key Vault secret", missing[0]);
        Assert.Equal("Get Key Vault secret", missing[1]);
        Assert.Equal("Set Key Vault secret", missing[2]);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  DetectMissingTools — empty article
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public void DetectMissingTools_EmptyArticle_ReturnsAllToolsAsMissing()
    {
        var expectedTools = new[] { "List Cosmos DB containers", "Query Cosmos DB container" };

        var missing = MissingToolDetector.DetectMissingTools(expectedTools, "");

        Assert.Equal(2, missing.Count);
        Assert.Contains("List Cosmos DB containers", missing);
        Assert.Contains("Query Cosmos DB container", missing);
    }

    [Fact]
    public void DetectMissingTools_NullArticle_ReturnsAllToolsAsMissing()
    {
        var expectedTools = new[] { "List VMs", "Create VM" };

        var missing = MissingToolDetector.DetectMissingTools(expectedTools, null!);

        Assert.Equal(2, missing.Count);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  DetectMissingTools — empty tool list
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public void DetectMissingTools_EmptyToolList_ReturnsEmptyList()
    {
        var article = "## Some heading\nContent";

        var missing = MissingToolDetector.DetectMissingTools(Array.Empty<string>(), article);

        Assert.Empty(missing);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  DetectMissingTools — case insensitive matching
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public void DetectMissingTools_CaseInsensitiveMatch_ToolFound()
    {
        var expectedTools = new[] { "list storage accounts" };
        var article = "## List Storage Accounts\nContent";

        var missing = MissingToolDetector.DetectMissingTools(expectedTools, article);

        Assert.Empty(missing);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  DetectMissingTools — Related content excluded from matching
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public void DetectMissingTools_RelatedContentNotCountedAsToolSection()
    {
        var expectedTools = new[] { "Related content" };
        var article = string.Join("\n", new[]
        {
            "## Related content",
            "- Link"
        });

        var missing = MissingToolDetector.DetectMissingTools(expectedTools, article);

        // "Related content" H2 is excluded from tool section matching
        Assert.Single(missing);
        Assert.Equal("Related content", missing[0]);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  DetectMissingTools — tool names with special characters
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public void DetectMissingTools_ToolNamesWithSpecialCharacters_HandledCorrectly()
    {
        var expectedTools = new[] { "Get AI Foundry agent's status", "List (preview) resources" };
        var article = string.Join("\n", new[]
        {
            "## Get AI Foundry agent's status",
            "Gets the status.",
            "",
            "## List (preview) resources",
            "Lists preview resources."
        });

        var missing = MissingToolDetector.DetectMissingTools(expectedTools, article);

        Assert.Empty(missing);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  DetectMissingTools — whitespace in tool names
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public void DetectMissingTools_WhitespaceInToolNames_TrimmedBeforeMatching()
    {
        var expectedTools = new[] { "  List VMs  " };
        var article = "## List VMs\nContent";

        var missing = MissingToolDetector.DetectMissingTools(expectedTools, article);

        Assert.Empty(missing);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  FormatMissingToolsWarning
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public void FormatWarning_NoMissingTools_ReturnsNull()
    {
        var result = MissingToolDetector.FormatMissingToolsWarning(
            Array.Empty<string>(), "compute");

        Assert.Null(result);
    }

    [Fact]
    public void FormatWarning_SingleMissingTool_SingularGrammar()
    {
        var result = MissingToolDetector.FormatMissingToolsWarning(
            new[] { "Create VM" }, "compute");

        Assert.NotNull(result);
        Assert.Contains("1 tool has no H2 section", result);
        Assert.Contains("'Create VM'", result);
        Assert.Contains("Regenerate the 'compute' namespace", result);
    }

    [Fact]
    public void FormatWarning_MultipleMissingTools_PluralGrammarAndAllListed()
    {
        var result = MissingToolDetector.FormatMissingToolsWarning(
            new[] { "Create VM", "Delete VM", "Restart VM" }, "compute");

        Assert.NotNull(result);
        Assert.Contains("3 tools have no H2 section", result);
        Assert.Contains("'Create VM'", result);
        Assert.Contains("'Delete VM'", result);
        Assert.Contains("'Restart VM'", result);
        Assert.Contains("Regenerate the 'compute' namespace", result);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  ExtractH2Headings
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public void ExtractH2Headings_EmptyContent_ReturnsEmptyList()
    {
        var headings = MissingToolDetector.ExtractH2Headings("");

        Assert.Empty(headings);
    }

    [Fact]
    public void ExtractH2Headings_ExcludesRelatedContent()
    {
        var article = string.Join("\n", new[]
        {
            "## List resources",
            "Content",
            "## Related content",
            "- Link"
        });

        var headings = MissingToolDetector.ExtractH2Headings(article);

        Assert.Single(headings);
        Assert.Equal("List resources", headings[0]);
    }

    [Fact]
    public void ExtractH2Headings_MultipleH2s_ReturnsAll()
    {
        var article = string.Join("\n", new[]
        {
            "## List VMs",
            "Content",
            "## Create VM",
            "Content",
            "## Delete VM",
            "Content"
        });

        var headings = MissingToolDetector.ExtractH2Headings(article);

        Assert.Equal(3, headings.Count);
        Assert.Equal("List VMs", headings[0]);
        Assert.Equal("Create VM", headings[1]);
        Assert.Equal("Delete VM", headings[2]);
    }
}
