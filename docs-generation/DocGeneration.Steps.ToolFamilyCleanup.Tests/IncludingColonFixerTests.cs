// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ToolFamilyCleanup.Services;
using Xunit;

namespace DocGeneration.Steps.ToolFamilyCleanup.Tests;

/// <summary>
/// Tests for IncludingColonFixer — ensures a colon follows "including"
/// in intro paragraphs that list capabilities.
/// Fixes: #282 — generated intro paragraphs are missing colon after "including".
/// </summary>
public class IncludingColonFixerTests
{
    // ── Core fix: insert colon after "including" ────────────────────

    [Fact]
    public void Fix_MissingColon_InsertsColon()
    {
        var input =
            "The Azure MCP Server lets you manage Azure App Service web apps, " +
            "including add, diagnose, get, and list operations, " +
            "with natural language prompts.";

        var result = IncludingColonFixer.Fix(input);

        Assert.Contains("including:", result);
        Assert.DoesNotContain("including add", result);
    }

    [Fact]
    public void Fix_ColonAlreadyPresent_Unchanged()
    {
        var input =
            "The Azure MCP Server lets you manage storage accounts, " +
            "including: create, list, and delete operations, " +
            "with natural language prompts.";

        var result = IncludingColonFixer.Fix(input);

        Assert.Equal(input, result);
    }

    // ── Varied Azure services (universal design principle) ──────────

    [Fact]
    public void Fix_CosmosDb_InsertsColon()
    {
        var input =
            "The Azure MCP Server lets you manage Azure Cosmos DB resources, " +
            "including list and query operations for Cosmos DB accounts and container items, " +
            "with natural language prompts.";

        var result = IncludingColonFixer.Fix(input);

        Assert.Contains("including:", result);
    }

    [Fact]
    public void Fix_Monitor_InsertsColon()
    {
        var input =
            "The Azure MCP Server lets you manage Azure Monitor telemetry, " +
            "including metrics and logs querying, workspace and workspace resource management, " +
            "with natural language prompts.";

        var result = IncludingColonFixer.Fix(input);

        Assert.Contains("including:", result);
    }

    [Fact]
    public void Fix_AppLens_SingleVerb_InsertsColon()
    {
        var input =
            "The Azure MCP Server lets you manage Azure AppLens diagnostics, " +
            "including diagnose, " +
            "with natural language prompts.";

        var result = IncludingColonFixer.Fix(input);

        Assert.Contains("including: diagnose", result);
    }

    // ── Full document context ───────────────────────────────────────

    [Fact]
    public void Fix_InFullDocument_OnlyFixesIntroPattern()
    {
        var input = string.Join("\n", new[]
        {
            "---",
            "title: Azure MCP Server tools for Azure Storage",
            "---",
            "",
            "# Azure MCP Server tools for Azure Storage",
            "",
            "The Azure MCP Server lets you manage Azure Storage resources, including create, list, and delete operations, with natural language prompts.",
            "",
            "Azure Storage is a cloud storage solution. For more information, see [Azure Storage documentation](/azure/storage/).",
            "",
            "## Create storage account",
            "",
            "This tool creates a storage account, including all required properties.",
            ""
        });

        var result = IncludingColonFixer.Fix(input);

        // Intro paragraph should have colon added
        Assert.Contains("including: create, list, and delete operations, with natural language prompts.", result);
        // Non-intro uses of "including" should NOT be modified
        Assert.Contains("including all required properties.", result);
    }

    // ── Must not modify non-intro uses of "including" ───────────────

    [Fact]
    public void Fix_IncludingInToolDescription_NotAffected()
    {
        var input = "This tool retrieves diagnostic results, including a status summary, insights, and supporting metrics.";

        var result = IncludingColonFixer.Fix(input);

        // Tool descriptions should NOT be modified — only intro paragraphs
        Assert.Equal(input, result);
    }

    [Fact]
    public void Fix_IncludingInParameterDescription_NotAffected()
    {
        var input = "| **Source** | Optional | Source to create the disk from, including a resource ID of a snapshot. |";

        var result = IncludingColonFixer.Fix(input);

        Assert.Equal(input, result);
    }

    // ── Edge cases ──────────────────────────────────────────────────

    [Fact]
    public void Fix_NullOrEmpty_ReturnsEmpty()
    {
        Assert.Equal("", IncludingColonFixer.Fix(""));
        Assert.Equal("", IncludingColonFixer.Fix(null!));
    }

    [Fact]
    public void Fix_NoIncluding_ReturnsUnchanged()
    {
        var input = "The Azure MCP Server lets you manage virtual machines with natural language prompts.";

        var result = IncludingColonFixer.Fix(input);

        Assert.Equal(input, result);
    }

    [Fact]
    public void Fix_DoubleColon_DoesNotOccur()
    {
        var input =
            "The Azure MCP Server lets you manage resources, " +
            "including: list and get, with natural language prompts.";

        var result = IncludingColonFixer.Fix(input);

        Assert.DoesNotContain("including::", result);
    }
}
