// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ToolFamilyCleanup.Services;
using Xunit;

namespace DocGeneration.Steps.ToolFamilyCleanup.Tests;

/// <summary>
/// Tests for FrontmatterEnricher to ensure required Microsoft Learn frontmatter
/// fields are injected into tool-family articles.
/// Fixes: #155 — generated articles missing required frontmatter fields.
/// </summary>
public class FrontmatterEnricherTests
{
    // ── Core injection behavior ─────────────────────────────────────

    [Fact]
    public void Enrich_InjectsAllMissingFields()
    {
        var markdown = @"---
title: Azure MCP Server tools for Azure Storage
description: Use Azure MCP Server tools to manage storage.
ms.service: azure-mcp-server
ms.topic: concept-article
tool_count: 5
---

# Azure MCP Server tools for Azure Storage";

        var result = FrontmatterEnricher.Enrich(markdown);

        Assert.Contains("author: diberry", result);
        Assert.Contains("ms.author: diberry", result);
        Assert.Contains("ms.date:", result);
        Assert.Contains("ai-usage: ai-generated", result);
        Assert.Contains("content_well_notification:", result);
        Assert.Contains("  - AI-contribution", result);
        Assert.Contains("ms.custom: build-2025", result);
    }

    [Fact]
    public void Enrich_PreservesExistingFields()
    {
        var markdown = @"---
title: Azure MCP Server tools for Key Vault
description: Use Azure MCP Server tools to manage secrets.
ms.service: azure-mcp-server
ms.topic: concept-article
tool_count: 3
mcp-cli.version: 2.0.0-beta.31
---

# Azure MCP Server tools for Key Vault";

        var result = FrontmatterEnricher.Enrich(markdown);

        // All original fields should be preserved
        Assert.Contains("title: Azure MCP Server tools for Key Vault", result);
        Assert.Contains("description: Use Azure MCP Server tools to manage secrets.", result);
        Assert.Contains("ms.service: azure-mcp-server", result);
        Assert.Contains("ms.topic: concept-article", result);
        Assert.Contains("tool_count: 3", result);
        Assert.Contains("mcp-cli.version: 2.0.0-beta.31", result);
    }

    [Fact]
    public void Enrich_DoesNotDuplicateExistingFields()
    {
        var markdown = @"---
title: Azure MCP Server tools for Monitor
author: diberry
ms.author: diberry
ms.date: 03/21/2026
ms.service: azure-mcp-server
ms.topic: concept-article
ai-usage: ai-generated
content_well_notification:
  - AI-contribution
ms.custom: build-2025
---

# Azure MCP Server tools for Monitor";

        var result = FrontmatterEnricher.Enrich(markdown);

        // Count author fields — should be exactly 1
        var authorCount = CountOccurrences(result, "author: diberry");
        // ms.author also contains "author:" so exclude it
        var msAuthorCount = CountOccurrences(result, "ms.author: diberry");
        Assert.Equal(1, msAuthorCount);
        // "author: diberry" appears in both "author:" and "ms.author:" lines
        Assert.Equal(2, authorCount);
    }

    [Fact]
    public void Enrich_MsDateFormatIsCorrect()
    {
        var markdown = @"---
title: Azure MCP Server tools for SQL
ms.service: azure-mcp-server
---

# Azure MCP Server tools for SQL";

        var result = FrontmatterEnricher.Enrich(markdown);

        // ms.date should be in MM/DD/YYYY format
        Assert.Matches(@"ms\.date: \d{2}/\d{2}/\d{4}", result);
    }

    [Fact]
    public void Enrich_PreservesBodyContent()
    {
        var markdown = @"---
title: Azure MCP Server tools for Cosmos DB
ms.service: azure-mcp-server
---

# Azure MCP Server tools for Cosmos DB

The Azure MCP Server lets you manage Cosmos DB accounts.

## List accounts

List all Cosmos DB accounts.";

        var result = FrontmatterEnricher.Enrich(markdown);

        // Body content should be completely untouched
        Assert.Contains("The Azure MCP Server lets you manage Cosmos DB accounts.", result);
        Assert.Contains("## List accounts", result);
        Assert.Contains("List all Cosmos DB accounts.", result);
    }

    // ── Edge cases ──────────────────────────────────────────────────

    [Fact]
    public void Enrich_NoFrontmatter_ReturnsUnchanged()
    {
        var markdown = "# Just a heading\n\nSome content.";

        var result = FrontmatterEnricher.Enrich(markdown);

        Assert.Equal(markdown, result);
    }

    [Fact]
    public void Enrich_NullOrEmpty_ReturnsInput()
    {
        Assert.Equal("", FrontmatterEnricher.Enrich(""));
        Assert.Null(FrontmatterEnricher.Enrich(null!));
    }

    [Fact]
    public void Enrich_ContentWellNotification_IsMultiLine()
    {
        var markdown = @"---
title: Azure MCP Server tools for Storage
ms.service: azure-mcp-server
---

# Azure MCP Server tools for Storage";

        var result = FrontmatterEnricher.Enrich(markdown);

        // content_well_notification must be YAML array format
        Assert.Contains("content_well_notification:", result);
        Assert.Contains("  - AI-contribution", result);
        // Verify the array item follows the key (check lines)
        var lines = result.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
        int keyLine = Array.FindIndex(lines, l => l.StartsWith("content_well_notification:"));
        Assert.True(keyLine >= 0, "content_well_notification key not found");
        Assert.Equal("  - AI-contribution", lines[keyLine + 1]);
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
