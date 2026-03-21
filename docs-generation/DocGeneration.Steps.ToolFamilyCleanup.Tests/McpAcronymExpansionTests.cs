// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ToolFamilyCleanup.Services;
using Xunit;

namespace DocGeneration.Steps.ToolFamilyCleanup.Tests;

/// <summary>
/// Tests for PostProcessor.ExpandMcpAcronym to ensure the first body mention
/// of "Azure MCP Server" is expanded to "Azure Model Context Protocol (MCP) Server".
/// Fixes: #142 — MCP acronym not defined on first use.
/// </summary>
public class McpAcronymExpansionTests
{
    // ── Core expansion behavior ─────────────────────────────────────

    [Fact]
    public void ExpandMcpAcronym_ExpandsFirstBodyMention()
    {
        var markdown = @"---
title: Azure MCP Server tools for Azure Storage
description: Use Azure MCP Server tools to manage storage.
ms.topic: concept-article
---

# Azure MCP Server tools for Azure Storage

The Azure MCP Server lets you manage Azure Storage resources.

## List storage accounts

Use Azure MCP Server to list accounts.";

        var result = PostProcessor.ExpandMcpAcronym(markdown);

        // First body mention (intro paragraph) should be expanded
        Assert.Contains("Azure Model Context Protocol (MCP) Server lets you manage", result);
        // Title and H1 should NOT be expanded
        Assert.Contains("title: Azure MCP Server tools", result);
        Assert.Contains("# Azure MCP Server tools for Azure Storage", result);
        // Second body mention should remain unchanged
        Assert.Contains("Use Azure MCP Server to list", result);
    }

    [Fact]
    public void ExpandMcpAcronym_OnlyExpandsFirstBodyOccurrence()
    {
        var markdown = @"---
title: Azure MCP Server tools for Azure Cosmos DB
---

# Azure MCP Server tools for Azure Cosmos DB

The Azure MCP Server lets you manage databases.

You must be authenticated to the Azure MCP Server.

The Azure MCP Server supports multiple regions.";

        var result = PostProcessor.ExpandMcpAcronym(markdown);

        // Count occurrences of expanded form — should be exactly 1
        var expandedCount = CountOccurrences(result, "Model Context Protocol (MCP)");
        Assert.Equal(1, expandedCount);

        // Count remaining "Azure MCP Server" (unexpanded) — should be multiple
        var unexpandedCount = CountOccurrences(result, "Azure MCP Server");
        Assert.True(unexpandedCount >= 2, $"Expected at least 2 unexpanded mentions, got {unexpandedCount}");
    }

    [Fact]
    public void ExpandMcpAcronym_PreservesFrontmatter()
    {
        var markdown = @"---
title: Azure MCP Server tools for Key Vault
description: Use Azure MCP Server tools to manage secrets.
ms.service: azure-mcp-server
---

# Azure MCP Server tools for Key Vault

The Azure MCP Server lets you manage Key Vault secrets.";

        var result = PostProcessor.ExpandMcpAcronym(markdown);

        // Frontmatter should be completely untouched
        Assert.Contains("title: Azure MCP Server tools for Key Vault", result);
        Assert.Contains("description: Use Azure MCP Server tools to manage secrets.", result);
    }

    [Fact]
    public void ExpandMcpAcronym_PreservesH1Heading()
    {
        var markdown = @"---
title: Azure MCP Server tools for Monitor
---

# Azure MCP Server tools for Monitor

The Azure MCP Server provides monitoring tools.";

        var result = PostProcessor.ExpandMcpAcronym(markdown);

        // H1 should not be modified
        Assert.Contains("# Azure MCP Server tools for Monitor", result);
        // Body should be expanded
        Assert.Contains("Azure Model Context Protocol (MCP) Server provides monitoring", result);
    }

    // ── Edge cases ──────────────────────────────────────────────────

    [Fact]
    public void ExpandMcpAcronym_NoFrontmatter_ExpandsFirstAfterH1()
    {
        var markdown = @"# Azure MCP Server tools for Storage

The Azure MCP Server lets you manage storage.";

        var result = PostProcessor.ExpandMcpAcronym(markdown);

        // H1 preserved
        Assert.Contains("# Azure MCP Server tools for Storage", result);
        // Body expanded
        Assert.Contains("Azure Model Context Protocol (MCP) Server lets you manage", result);
    }

    [Fact]
    public void ExpandMcpAcronym_NoMcpMention_ReturnsUnchanged()
    {
        var markdown = @"---
title: Some other article
---

# Some article

This article has no MCP mentions.";

        var result = PostProcessor.ExpandMcpAcronym(markdown);

        Assert.Equal(markdown, result);
    }

    [Fact]
    public void ExpandMcpAcronym_NullOrEmpty_ReturnsInput()
    {
        Assert.Equal("", PostProcessor.ExpandMcpAcronym(""));
        Assert.Null(PostProcessor.ExpandMcpAcronym(null!));
    }

    [Fact]
    public void ExpandMcpAcronym_AlreadyExpanded_NoDoubleExpansion()
    {
        var markdown = @"---
title: Azure MCP Server tools for SQL
---

# Azure MCP Server tools for SQL

The Azure Model Context Protocol (MCP) Server lets you manage databases.";

        var result = PostProcessor.ExpandMcpAcronym(markdown);

        // Should not create "Azure Model Context Protocol (MCP) Model Context Protocol (MCP) Server"
        var expandedCount = CountOccurrences(result, "Model Context Protocol (MCP)");
        Assert.Equal(1, expandedCount);
    }

    // ── Helpers ──────────────────────────────────────────────────────

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
