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
        Assert.Contains("| Parameter | Required |", result);
        Assert.DoesNotContain("List storage accounts.", result);
        Assert.Contains(cliContent.TrimEnd(), result);
        AssertCliTabBeforeMcpTab(result);
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
        const string cliContent = "   ";

        var result = CliTabWrapper.WrapWithTabs(mcpContent, cliContent);

        Assert.Equal(mcpContent, result);
    }

    [Fact]
    public void WrapWithTabs_TabIds_AreCorrect()
    {
        var result = CliTabWrapper.WrapWithTabs("mcp stuff", "cli stuff");

        Assert.Contains("#tab/mcp-server", result);
        Assert.Contains("#tab/azure-mcp-cli", result);
        AssertCliTabBeforeMcpTab(result);
    }

    [Fact]
    public void WrapWithTabs_NoHorizontalRule_InsideContent()
    {
        var mcpContent = "MCP content line 1\nMCP content line 2";
        var cliContent = "CLI content line 1\nCLI content line 2";

        var result = CliTabWrapper.WrapWithTabs(mcpContent, cliContent);

        var lines = result.Split('\n');

        int dashCount = 0;
        foreach (var line in lines)
        {
            if (line.Trim() == "---")
                dashCount++;
        }
        Assert.Equal(1, dashCount);
    }

    [Fact]
    public void WrapWithTabs_DescriptionExtracted_NotInEitherTab()
    {
        const string description = "List storage accounts in the current subscription.";
        var mcpContent = $"{description}\n\n| Parameter | Required |\n|---|---|\n| Subscription | Yes |";
        var cliContent = $"{description}\n\n```bash\naz storage account list\n```";

        var (tabBlock, extractedDescription) = CliTabWrapper.WrapWithTabsAndExtractDescription(mcpContent, cliContent);

        Assert.Equal(description, extractedDescription);
        Assert.Equal(0, CountOccurrences(tabBlock, description));
        AssertCliTabBeforeMcpTab(tabBlock);
    }

    [Fact]
    public void WrapWithTabs_DescriptionPlaced_AboveTabBlock()
    {
        const string article = """
            ## List accounts
            <!-- @mcpcli storage account list -->

            List storage accounts in a subscription.

            | Parameter | Required |
            |---|---|
            | Subscription | Yes |
            """;

        var cliContent = new Dictionary<string, string>
        {
            ["storage account list"] = """
                List storage accounts in a subscription.

                ```bash
                az storage account list
                ```
                """
        };

        var result = CliTabWrapper.ApplyTabsToFamilyArticle(article, cliContent).ReplaceLineEndings("\n");

        Assert.Contains("## List accounts\n\nList storage accounts in a subscription.\n\n#### [Azure MCP CLI](#tab/azure-mcp-cli)", result);
    }

    [Fact]
    public void WrapWithTabs_NullDescription_NoEmptyParagraph()
    {
        const string article = """
            ## List accounts
            <!-- @mcpcli storage account list -->

            | Parameter | Required |
            |---|---|
            | Subscription | Yes |
            """;

        var cliContent = new Dictionary<string, string>
        {
            ["storage account list"] = "```bash\naz storage account list\n```"
        };

        var result = CliTabWrapper.ApplyTabsToFamilyArticle(article, cliContent).ReplaceLineEndings("\n");

        Assert.DoesNotContain("## List accounts\n\n\n#### [Azure MCP CLI](#tab/azure-mcp-cli)", result);
        Assert.Contains("## List accounts\n#### [Azure MCP CLI](#tab/azure-mcp-cli)", result);
    }

    [Fact]
    public void WrapWithTabs_EmptyMcpDescription_TabsStillRender()
    {
        var mcpContent = "| Parameter | Required |\n|---|---|\n| Subscription | Yes |";
        var cliContent = "```bash\naz storage account list\n```";

        var (tabBlock, description) = CliTabWrapper.WrapWithTabsAndExtractDescription(mcpContent, cliContent);

        Assert.Null(description);
        Assert.Contains("#### [Azure MCP CLI](#tab/azure-mcp-cli)", tabBlock);
        Assert.Contains("#### [MCP Server](#tab/mcp-server)", tabBlock);
        AssertCliTabBeforeMcpTab(tabBlock);
    }

    [Fact]
    public void WrapWithTabs_LongDescription_PlacedVerbatim()
    {
        var description = string.Join(" ", Enumerable.Repeat("This tool lists storage accounts across filtered scopes for reporting and inventory scenarios.", 7));
        var mcpContent = $"{description}\n\n| Parameter | Required |\n|---|---|\n| Subscription | Yes |";
        var cliContent = $"{description}\n\n```bash\naz storage account list\n```";

        var article = $"""
            ## List accounts
            <!-- @mcpcli storage account list -->

            {description}

            | Parameter | Required |
            |---|---|
            | Subscription | Yes |
            """;

        var cliContentByCommand = new Dictionary<string, string>
        {
            ["storage account list"] = cliContent
        };

        var result = CliTabWrapper.ApplyTabsToFamilyArticle(article, cliContentByCommand);
        var normalized = result.ReplaceLineEndings("\n");

        Assert.Contains(description, normalized);
        Assert.Contains($"\n\n{description}\n\n#### [Azure MCP CLI](#tab/azure-mcp-cli)", normalized);
        Assert.DoesNotContain($"{description}\n\n```bash", CliTabWrapper.WrapWithTabsAndExtractDescription(mcpContent, cliContent).TabBlock);
    }

    [Fact]
    public void WrapWithTabs_Output_DoesNotContainMcpBranding()
    {
        const string description = "Use Model Context Protocol (MCP) wording here.";
        var mcpContent = $"{description}\n\n| Parameter | Required |\n|---|---|\n| Subscription | Yes |";
        var cliContent = $"{description}\n\n```bash\naz storage account list\n```";

        var result = CliTabWrapper.WrapWithTabsAndExtractDescription(mcpContent, cliContent).TabBlock;

        Assert.DoesNotContain("(MCP)", result);
    }

    [Fact]
    public void StripMatchingDescriptionFromCli_DifferentDescription_LeavesCliUnchanged()
    {
        var cliContent = "Different CLI description.\n\n```bash\naz storage list\n```";

        var result = Shared.CliTabWrapper.StripMatchingDescriptionFromCli(cliContent, "MCP description text.");

        Assert.Contains("Different CLI description.", result);
    }

    [Fact]
    public void ExtractDescription_MultiLineDescription_JoinsAsOneParagraph()
    {
        var content = "First line of description\nsecond line continues here.\n\n| Parameter | Required |\n|---|---|";

        var (remaining, description) = Shared.CliTabWrapper.ExtractDescription(content);

        Assert.Equal("First line of description second line continues here.", description);
        Assert.Contains("| Parameter | Required |", remaining);
    }

    [Fact]
    public void ExtractDescription_PipeInDescription_DoesNotTruncate()
    {
        // Pipe at start of line IS a boundary; pipe mid-line is NOT
        var content = "Lists items and their status.\n\n| Parameter | Required |\n|---|---|";

        var (_, description) = Shared.CliTabWrapper.ExtractDescription(content);

        Assert.Equal("Lists items and their status.", description);
    }

    [Fact]
    public void ExtractDescription_DescriptionOnly_NoTable_ExtractsCorrectly()
    {
        var content = "Creates a new storage account in the specified resource group.";

        var (remaining, description) = Shared.CliTabWrapper.ExtractDescription(content);

        Assert.Equal("Creates a new storage account in the specified resource group.", description);
        Assert.Equal("", remaining.Trim());
    }

    [Fact]
    public void StripProseFromMcpContent_RemovesExtraParagraphsBeforeExamplePrompts()
    {
        var content = """
            <!-- @mcpcli foundryextensions knowledge index list -->

            List the knowledge indexes in a Microsoft Foundry project.

            Requires the project endpoint URL and authentication context before you run the tool.

            Notes:
            - The list shows indexes that are available to your project.

            Example prompts include:

            - "List all knowledge indexes in my project."

            | Parameter | Required or optional | Description |
            |-----------|----------------------|-------------|
            | **Project endpoint** | Required | The project endpoint URL. |
            """;

        var result = Shared.CliTabWrapper.StripProseFromMcpContent(content).ReplaceLineEndings("\n");

        Assert.StartsWith("<!-- @mcpcli foundryextensions knowledge index list -->\n\nExample prompts include:", result);
        Assert.DoesNotContain("Requires the project endpoint URL", result);
        Assert.DoesNotContain("Notes:", result);
        Assert.Contains("| Parameter | Required or optional | Description |", result);
    }

    [Fact]
    public void StripProseFromMcpContent_WithoutExamplePrompts_KeepsParameterTable()
    {
        var content = """
            <!-- @mcpcli storage account list -->

            List storage accounts in a subscription.

            Use this to inspect account inventory before you take action.

            | Parameter | Required |
            |---|---|
            | Subscription | Yes |
            """;

        var result = Shared.CliTabWrapper.StripProseFromMcpContent(content).ReplaceLineEndings("\n");

        Assert.StartsWith("<!-- @mcpcli storage account list -->\n\n| Parameter | Required |", result);
        Assert.DoesNotContain("Use this to inspect account inventory", result);
    }

    [Fact]
    public void WrapWithTabsAndExtractDescription_StripsMcpProseButPreservesAnnotationBlock()
    {
        var mcpContent = """
            <!-- @mcpcli storage account list -->

            List storage accounts in a subscription.

            Use this to inspect inventory before you take action.

            Example prompts include:

            - "List storage accounts in subscription 'sub1'."

            | Parameter | Required |
            |---|---|
            | Subscription | Yes |

            [Tool annotation hints](index.md#tool-annotations):

            Destructive: ❌ | Idempotent: ✅ | Read-only: ✅
            """;
        var cliContent = """
            ```bash
            az storage account list
            ```
            """;

        var (tabBlock, description) = CliTabWrapper.WrapWithTabsAndExtractDescription(mcpContent, cliContent);

        Assert.Equal("List storage accounts in a subscription.", description);
        Assert.DoesNotContain("Use this to inspect inventory before you take action.", tabBlock);
        Assert.Contains("Example prompts include:", tabBlock);
        Assert.Contains("[Tool annotation hints](index.md#tool-annotations):", tabBlock);
        Assert.Contains("Destructive: ❌ | Idempotent: ✅ | Read-only: ✅", tabBlock);
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
            ["storage account list"] = "List storage accounts in a subscription.\n\n```bash\naz storage account list\n```"
        };

        var result = CliTabWrapper.ApplyTabsToFamilyArticle(FamilyArticle, cliContent);

        Assert.Contains("#### [MCP Server](#tab/mcp-server)", result);
        Assert.Contains("#### [Azure MCP CLI](#tab/azure-mcp-cli)", result);
        Assert.Contains("az storage account list", result);
        AssertCliTabBeforeMcpTab(result);
    }

    [Fact]
    public void ApplyTabsToFamilyArticle_MultipleTools_AllWrapped()
    {
        var cliContent = new Dictionary<string, string>
        {
            ["storage account list"] = "List storage accounts in a subscription.\n\n```bash\naz storage account list\n```",
            ["storage account create"] = "Create a new storage account.\n\n```bash\naz storage account create\n```"
        };

        var result = CliTabWrapper.ApplyTabsToFamilyArticle(FamilyArticle, cliContent);

        var mcpTabCount = CountOccurrences(result, "#### [MCP Server](#tab/mcp-server)");
        Assert.Equal(2, mcpTabCount);

        var cliTabCount = CountOccurrences(result, "#### [Azure MCP CLI](#tab/azure-mcp-cli)");
        Assert.Equal(2, cliTabCount);
        AssertCliTabBeforeMcpTab(result);
    }

    [Fact]
    public void ApplyTabsToFamilyArticle_ToolWithoutCliContent_LeftUnchanged()
    {
        var cliContent = new Dictionary<string, string>
        {
            ["storage account list"] = "List storage accounts in a subscription.\n\n```bash\naz storage account list\n```"
        };

        var result = CliTabWrapper.ApplyTabsToFamilyArticle(FamilyArticle, cliContent);

        var mcpTabCount = CountOccurrences(result, "#### [MCP Server](#tab/mcp-server)");
        Assert.Equal(1, mcpTabCount);

        Assert.Contains("Create a new storage account.", result);
    }

    [Fact]
    public void ApplyTabsToFamilyArticle_NoCliContent_ReturnsOriginal()
    {
        var cliContent = new Dictionary<string, string>();

        var result = CliTabWrapper.ApplyTabsToFamilyArticle(FamilyArticle, cliContent);

        Assert.Equal(FamilyArticle.ReplaceLineEndings(), result.ReplaceLineEndings());
    }

    [Fact]
    public void ApplyTabsToFamilyArticle_PreservesNonToolSections()
    {
        var cliContent = new Dictionary<string, string>
        {
            ["storage account list"] = "cli content"
        };

        var result = CliTabWrapper.ApplyTabsToFamilyArticle(FamilyArticle, cliContent);

        Assert.Contains("ms.topic: include", result);
        Assert.Contains("# Azure Storage tools", result);
        Assert.Contains("## Quick Navigation", result);
    }

    [Fact]
    public void ApplyTabsToFamilyArticle_CommandNormalized_CaseInsensitive()
    {
        var cliContent = new Dictionary<string, string>
        {
            ["storage account list"] = "cli content for list"
        };

        var result = CliTabWrapper.ApplyTabsToFamilyArticle(FamilyArticle, cliContent);

        Assert.Contains("#### [MCP Server](#tab/mcp-server)", result);
        Assert.Contains("cli content for list", result);
        AssertCliTabBeforeMcpTab(result);
    }

    [Fact]
    public void ApplyTabsToFamilyArticle_NoMcpCliMarkers_NoTabsInjected()
    {
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

        Assert.DoesNotContain("#### [MCP Server](#tab/mcp-server)", result);
        Assert.DoesNotContain("#### [Azure MCP CLI](#tab/azure-mcp-cli)", result);
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

        var exception = Record.Exception(() =>
            CliTabWrapper.ApplyTabsToFamilyArticle(mcpOnlyArticle, cliContent));

        Assert.Null(exception);
    }

    // ── Annotation placement — after --- separator ─────────────

    [Fact]
    public void WrapWithTabs_AnnotationInMcpContent_PlacedOnceAfterSeparator()
    {
        var mcpContent = "List storage accounts.\n\n" +
            "| Parameter | Required |\n|---|---|\n| Sub | Yes |\n\n" +
            "[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):\n\n" +
            "| Destructive | Idempotent | Open World | Read Only | Secret | Local Required |\n" +
            "|:-----------:|:----------:|:----------:|:---------:|:------:|:--------------:|  \n" +
            "| ❌ | ✅ | ❌ | ✅ | ❌ | ❌ |";
        var cliContent = "List storage accounts.\n\n```bash\naz storage account list\n```\n\n| Parameter | Required |\n|---|---|\n| --sub | Yes |";

        var result = CliTabWrapper.WrapWithTabs(mcpContent, cliContent);

        var annotationCount = CountOccurrences(result, "[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):");
        Assert.Equal(1, annotationCount);

        var separatorPos = result.IndexOf("\n---", StringComparison.Ordinal);
        var annotationPos = result.IndexOf("[Tool annotation hints]", StringComparison.Ordinal);
        Assert.True(annotationPos > separatorPos, "Annotation should be after --- separator");
        Assert.Contains("| Destructive | Idempotent | Open World | Read Only | Secret | Local Required |", result);
        Assert.Contains("|:-----------:|:----------:|:----------:|:---------:|:------:|:--------------:|", result);
        Assert.Contains("| ❌ | ✅ | ❌ | ✅ | ❌ | ❌ |", result);
        Assert.DoesNotContain("|:--------------:|  ", result);
    }

    [Fact]
    public void WrapWithTabs_NoAnnotationInMcpContent_CliTabUnchanged()
    {
        var mcpContent = "List storage accounts.\n\n| Parameter | Required |\n|---|---|\n| Sub | Yes |";
        var cliContent = "```bash\naz storage account list\n```";

        var result = CliTabWrapper.WrapWithTabs(mcpContent, cliContent);

        Assert.DoesNotContain("[Tool annotation hints]", result);
        Assert.Contains("az storage account list", result);
    }

    [Fact]
    public void WrapWithTabs_AnnotationBlock_NotInsideMcpOrCliTab()
    {
        var mcpContent = "Description.\n\n" +
            "[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):\n\n" +
            "Destructive: ✅ | Idempotent: ❌ | Open World: ✅ | Read Only: ❌ | Secret: ❌ | Local Required: ❌";
        var cliContent = "Description.\n\n```bash\naz tool run\n```";

        var result = CliTabWrapper.WrapWithTabs(mcpContent, cliContent);

        var terminatorPos = result.IndexOf("\n---", StringComparison.Ordinal);
        var annotationPos = result.IndexOf("Destructive: ✅", StringComparison.Ordinal);

        Assert.True(annotationPos > terminatorPos, "Annotation should be after --- terminator");

        var mcpTabStart = result.IndexOf("#### [MCP Server](#tab/mcp-server)", StringComparison.Ordinal);
        var cliTabStart = result.IndexOf("#### [Azure MCP CLI](#tab/azure-mcp-cli)", StringComparison.Ordinal);
        var cliTabContent = result.Substring(cliTabStart, mcpTabStart - cliTabStart);
        var mcpTabContent = result.Substring(mcpTabStart, terminatorPos - mcpTabStart);

        Assert.DoesNotContain("[Tool annotation hints]", cliTabContent);
        Assert.DoesNotContain("[Tool annotation hints]", mcpTabContent);
    }

    [Fact]
    public void ApplyTabsToFamilyArticle_AnnotationsInTool_PlacedOnceAfterSeparator()
    {
        const string familyWithAnnotations = """
            ---
            ms.topic: include
            ---

            # Azure Storage tools

            ## List accounts
            <!-- @mcpcli storage account list -->

            List storage accounts in a subscription.

            [Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

            Destructive: ❌ | Idempotent: ✅ | Read Only: ✅

            ---
            """;

        var cliContent = new Dictionary<string, string>
        {
            ["storage account list"] = "List storage accounts in a subscription.\n\n```bash\naz storage account list\n```"
        };

        var result = CliTabWrapper.ApplyTabsToFamilyArticle(familyWithAnnotations, cliContent);

        var annotationCount = CountOccurrences(result, "[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):");
        Assert.Equal(1, annotationCount);

        var separatorPos = result.IndexOf("\n---", StringComparison.Ordinal);
        var annotationPos = result.IndexOf("[Tool annotation hints]", StringComparison.Ordinal);
        Assert.True(annotationPos > separatorPos, "Annotation should be after --- separator");
    }

    [Fact]
    public void WrapWithTabs_CliTabAppearsBeforeMcpServerTab()
    {
        var mcpContent = "| Parameter | Required |\n|---|---|\n| Subscription | Yes |";
        var cliContent = "```bash\naz storage account create --name myaccount\n```";

        var result = CliTabWrapper.WrapWithTabs(mcpContent, cliContent);

        var mcpIdx = result.IndexOf("#### [MCP Server](#tab/mcp-server)", StringComparison.Ordinal);
        var cliIdx = result.IndexOf("#### [Azure MCP CLI](#tab/azure-mcp-cli)", StringComparison.Ordinal);

        Assert.True(mcpIdx >= 0, "MCP Server tab must be present");
        Assert.True(cliIdx >= 0, "CLI tab must be present");
        Assert.True(cliIdx < mcpIdx, "CLI tab must appear before MCP Server tab");
    }

    private static void AssertCliTabBeforeMcpTab(string text)
    {
        Assert.True(
            text.IndexOf("#tab/azure-mcp-cli", StringComparison.Ordinal) <
            text.IndexOf("#tab/mcp-server", StringComparison.Ordinal));
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
