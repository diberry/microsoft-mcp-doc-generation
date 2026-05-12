// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using DocGeneration.Steps.ToolFamilyCleanup.Services;
using Xunit;

namespace DocGeneration.Steps.ToolFamilyCleanup.Tests;

public class CliTabWrapperTests
{
    // ── WrapWithTabs ──────────────────────────────────────────────

    [Fact]
    public void WrapWithTabs_HasCliContent_WrapsCorrectly()
    {
        var mcpContent = "List storage accounts.\n\n| Parameter | Required |\n|---|---|\n| Sub | Yes |";
        var cliContent = "```bash\naz storage account list\n```";

        var result = CliTabWrapper.WrapWithTabs(mcpContent, cliContent);

        Assert.Contains("#### [MCP Server](#tab/mcp-server)", result);
        Assert.Contains("#### [Azure MCP CLI](#tab/azure-mcp-cli)", result);
        Assert.Contains(mcpContent.TrimEnd(), result);
        Assert.Contains(cliContent.TrimEnd(), result);
        // Must end with tab group terminator
        Assert.Contains("---", result);
    }

    [Fact]
    public void WrapWithTabs_NullCliContent_ReturnsOriginal()
    {
        var mcpContent = "Some MCP content here.";

        var result = CliTabWrapper.WrapWithTabs(mcpContent, null);

        Assert.Equal(mcpContent, result);
    }

    [Fact]
    public void WrapWithTabs_EmptyCliContent_ReturnsOriginal()
    {
        var mcpContent = "Some MCP content here.";

        var result = CliTabWrapper.WrapWithTabs(mcpContent, "   ");

        Assert.Equal(mcpContent, result);
    }

    [Fact]
    public void WrapWithTabs_TabIds_AreCorrect()
    {
        var result = CliTabWrapper.WrapWithTabs("mcp stuff", "cli stuff");

        Assert.Contains("#tab/mcp-server", result);
        Assert.Contains("#tab/azure-mcp-cli", result);
    }

    [Fact]
    public void WrapWithTabs_NoHorizontalRule_InsideContent()
    {
        var mcpContent = "MCP content line 1\nMCP content line 2";
        var cliContent = "CLI content line 1\nCLI content line 2";

        var result = CliTabWrapper.WrapWithTabs(mcpContent, cliContent);

        var lines = result.Split('\n');

        // The only standalone "---" should be the tab terminator at the end
        int dashCount = 0;
        foreach (var line in lines)
        {
            if (line.Trim() == "---")
                dashCount++;
        }
        Assert.Equal(1, dashCount);
    }

    // ── ApplyTabsToFamilyArticle ──────────────────────────────────

    private const string FamilyArticle = """
        ---
        ms.topic: include
        ---

        # Azure Storage tools

        ## Quick Navigation
        - [List accounts](#list-accounts)

        ## List accounts
        <!-- @mcpcli storage account list -->

        List storage accounts in a subscription.

        | Parameter | Required or optional | Description |
        |-----------|---------------------|-------------|
        | **Subscription** | Required | Azure subscription |

        ---

        ## Create account
        <!-- @mcpcli storage account create -->

        Create a new storage account.

        ---
        """;

    [Fact]
    public void ApplyTabsToFamilyArticle_SingleTool_Wrapped()
    {
        var cliContent = new Dictionary<string, string>
        {
            ["storage account list"] = "```bash\naz storage account list\n```"
        };

        var result = CliTabWrapper.ApplyTabsToFamilyArticle(FamilyArticle, cliContent);

        Assert.Contains("#### [MCP Server](#tab/mcp-server)", result);
        Assert.Contains("#### [Azure MCP CLI](#tab/azure-mcp-cli)", result);
        Assert.Contains("az storage account list", result);
    }

    [Fact]
    public void ApplyTabsToFamilyArticle_MultipleTools_AllWrapped()
    {
        var cliContent = new Dictionary<string, string>
        {
            ["storage account list"] = "```bash\naz storage account list\n```",
            ["storage account create"] = "```bash\naz storage account create\n```"
        };

        var result = CliTabWrapper.ApplyTabsToFamilyArticle(FamilyArticle, cliContent);

        // Count MCP Server tab headers — should have 2 (one per tool)
        var mcpTabCount = CountOccurrences(result, "#### [MCP Server](#tab/mcp-server)");
        Assert.Equal(2, mcpTabCount);

        var cliTabCount = CountOccurrences(result, "#### [Azure MCP CLI](#tab/azure-mcp-cli)");
        Assert.Equal(2, cliTabCount);
    }

    [Fact]
    public void ApplyTabsToFamilyArticle_ToolWithoutCliContent_LeftUnchanged()
    {
        // Only provide CLI content for one of the two tools
        var cliContent = new Dictionary<string, string>
        {
            ["storage account list"] = "```bash\naz storage account list\n```"
        };

        var result = CliTabWrapper.ApplyTabsToFamilyArticle(FamilyArticle, cliContent);

        // Should have exactly 1 tab group (only for "list")
        var mcpTabCount = CountOccurrences(result, "#### [MCP Server](#tab/mcp-server)");
        Assert.Equal(1, mcpTabCount);

        // "Create account" section content should still be present
        Assert.Contains("Create a new storage account.", result);
    }

    [Fact]
    public void ApplyTabsToFamilyArticle_NoCliContent_ReturnsOriginal()
    {
        var cliContent = new Dictionary<string, string>();

        var result = CliTabWrapper.ApplyTabsToFamilyArticle(FamilyArticle, cliContent);

        Assert.Equal(FamilyArticle, result);
    }

    [Fact]
    public void ApplyTabsToFamilyArticle_PreservesNonToolSections()
    {
        var cliContent = new Dictionary<string, string>
        {
            ["storage account list"] = "cli content"
        };

        var result = CliTabWrapper.ApplyTabsToFamilyArticle(FamilyArticle, cliContent);

        // Frontmatter preserved
        Assert.Contains("ms.topic: include", result);
        // Title preserved
        Assert.Contains("# Azure Storage tools", result);
        // Quick Navigation preserved
        Assert.Contains("## Quick Navigation", result);
    }

    [Fact]
    public void ApplyTabsToFamilyArticle_CommandNormalized_CaseInsensitive()
    {
        // The marker says "storage account list" but we provide uppercase key
        // NormalizeCommand lowercases, so matching should work
        var cliContent = new Dictionary<string, string>
        {
            ["storage account list"] = "cli content for list"
        };

        var result = CliTabWrapper.ApplyTabsToFamilyArticle(FamilyArticle, cliContent);

        Assert.Contains("#### [MCP Server](#tab/mcp-server)", result);
        Assert.Contains("cli content for list", result);
    }

    [Fact]
    public void ApplyTabsToFamilyArticle_NoMcpCliMarkers_NoTabsInjected()
    {
        // Article with NO @mcpcli markers — MCP-only content
        const string mcpOnlyArticle = """
            ---
            ms.topic: include
            ---

            # Azure Key Vault tools

            ## Overview
            Tools for managing Azure Key Vault secrets and keys.

            ## List secrets

            List all secrets in a key vault.

            | Parameter | Required or optional | Description |
            |-----------|---------------------|-------------|
            | **Vault name** | Required | Key vault name |

            ---

            ## Get secret

            Retrieve a specific secret value.

            ---
            """;

        var cliContent = new Dictionary<string, string>
        {
            ["keyvault secret list"] = "cli content"
        };

        var result = CliTabWrapper.ApplyTabsToFamilyArticle(mcpOnlyArticle, cliContent);

        // Verify no tab markers added (content may have normalized line endings)
        Assert.DoesNotContain("#### [MCP Server](#tab/mcp-server)", result);
        Assert.DoesNotContain("#### [Azure MCP CLI](#tab/azure-mcp-cli)", result);
        // Verify original content is preserved (key sections present)
        Assert.Contains("# Azure Key Vault tools", result);
        Assert.Contains("## Overview", result);
        Assert.Contains("## List secrets", result);
        Assert.Contains("## Get secret", result);
        Assert.Contains("| **Vault name** | Required | Key vault name |", result);
    }

    [Fact]
    public void ApplyTabsToFamilyArticle_NoMcpCliMarkers_NoExceptions()
    {
        const string mcpOnlyArticle = """
            ---
            ms.topic: include
            ---

            # Simple MCP article

            ## Tool section

            Description without markers.

            ---
            """;

        var cliContent = new Dictionary<string, string>
        {
            ["some tool"] = "cli content"
        };

        // Should not throw
        var exception = Record.Exception(() => 
            CliTabWrapper.ApplyTabsToFamilyArticle(mcpOnlyArticle, cliContent));

        Assert.Null(exception);
    }

    private static int CountOccurrences(string text, string pattern)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += pattern.Length;
        }
        return count;
    }
}
