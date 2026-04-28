// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ToolFamilyCleanup.Services;
using Xunit;

namespace DocGeneration.Steps.ToolFamilyCleanup.Tests;

/// <summary>
/// Tests for FrontmatterEnricher to ensure required Microsoft Learn frontmatter
/// fields are injected into tool-family articles.
/// Fixes: #155 — generated articles missing required frontmatter fields.
/// Phase 0: Updated to test instance-based API with clock injection.
/// </summary>
public class FrontmatterEnricherTests
{
    private static readonly DateTime FixedDate = new DateTime(2025, 3, 15, 10, 30, 0, DateTimeKind.Utc);
    private static Func<DateTime> FixedClock => () => FixedDate;

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

        var enricher = new FrontmatterEnricher(FixedClock);
        var result = enricher.Enrich(markdown);

        Assert.Contains("author: diberry", result);
        Assert.Contains("ms.author: diberry", result);
        Assert.Contains("ms.date:", result);
        Assert.Contains("ai-usage: ai-generated", result);
        Assert.Contains("content_well_notification:", result);
        Assert.Contains("  - AI-contribution", result);
        Assert.Contains("ms.custom: build-2025", result);
        Assert.Contains("ms.reviewer: mbaldwin", result);
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

        var enricher = new FrontmatterEnricher(FixedClock);
        var result = enricher.Enrich(markdown);

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

        var enricher = new FrontmatterEnricher(FixedClock);
        var result = enricher.Enrich(markdown);

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

        var enricher = new FrontmatterEnricher(FixedClock);
        var result = enricher.Enrich(markdown);

        // ms.date should be in MM/DD/YYYY format
        Assert.Matches(@"ms\.date: \d{2}/\d{2}/\d{4}", result);
        // With fixed clock, should be exactly the fixed date
        Assert.Contains("ms.date: 03/15/2025", result);
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

        var enricher = new FrontmatterEnricher(FixedClock);
        var result = enricher.Enrich(markdown);

        // Body content should be completely untouched
        Assert.Contains("The Azure MCP Server lets you manage Cosmos DB accounts.", result);
        Assert.Contains("## List accounts", result);
        Assert.Contains("List all Cosmos DB accounts.", result);
    }

    // ── ms.reviewer injection (#284) ────────────────────────────────

    [Fact]
    public void Enrich_InjectsMsReviewer()
    {
        var markdown = @"---
title: Azure MCP Server tools for Azure Storage
description: Use Azure MCP Server tools to manage storage.
ms.service: azure-mcp-server
ms.topic: concept-article
tool_count: 5
---

# Azure MCP Server tools for Azure Storage";

        var enricher = new FrontmatterEnricher(FixedClock);
        var result = enricher.Enrich(markdown);

        Assert.Contains("ms.reviewer: mbaldwin", result);
    }

    [Fact]
    public void Enrich_DoesNotDuplicateExistingMsReviewer()
    {
        var markdown = @"---
title: Azure MCP Server tools for Key Vault
ms.reviewer: mbaldwin
ms.service: azure-mcp-server
---

# Azure MCP Server tools for Key Vault";

        var enricher = new FrontmatterEnricher(FixedClock);
        var result = enricher.Enrich(markdown);

        var count = CountOccurrences(result, "ms.reviewer: mbaldwin");
        Assert.Equal(1, count);
    }

    [Fact]
    public void Enrich_MsReviewerIsInsideFrontmatter()
    {
        var markdown = @"---
title: Azure MCP Server tools for Cosmos DB
ms.service: azure-mcp-server
---

# Azure MCP Server tools for Cosmos DB

Some body content.";

        var enricher = new FrontmatterEnricher(FixedClock);
        var result = enricher.Enrich(markdown);
        var normalized = result.Replace("\r\n", "\n");

        // Extract frontmatter block
        var fmStart = normalized.IndexOf("---");
        var fmEnd = normalized.IndexOf("\n---", fmStart + 3);
        var frontmatter = normalized.Substring(fmStart, fmEnd + 4 - fmStart);

        Assert.Contains("ms.reviewer:", frontmatter);
    }

    // ── Edge cases ──────────────────────────────────────────────────

    [Fact]
    public void Enrich_NoFrontmatter_ReturnsUnchanged()
    {
        var markdown = "# Just a heading\n\nSome content.";

        var enricher = new FrontmatterEnricher(FixedClock);
        var result = enricher.Enrich(markdown);

        Assert.Equal(markdown, result);
    }

    [Fact]
    public void Enrich_NullOrEmpty_ReturnsInput()
    {
        var enricher = new FrontmatterEnricher(FixedClock);
        Assert.Equal("", enricher.Enrich(""));
        Assert.Null(enricher.Enrich(null!));
    }

    [Fact]
    public void Enrich_ContentWellNotification_IsMultiLine()
    {
        var markdown = @"---
title: Azure MCP Server tools for Storage
ms.service: azure-mcp-server
---

# Azure MCP Server tools for Storage";

        var enricher = new FrontmatterEnricher(FixedClock);
        var result = enricher.Enrich(markdown);

        // content_well_notification must be YAML array format
        Assert.Contains("content_well_notification:", result);
        Assert.Contains("  - AI-contribution", result);
        // Verify the array item follows the key (check lines)
        var lines = result.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
        int keyLine = Array.FindIndex(lines, l => l.StartsWith("content_well_notification:"));
        Assert.True(keyLine >= 0, "content_well_notification key not found");
        Assert.Equal("  - AI-contribution", lines[keyLine + 1]);
    }

    // ── Phase 0: Clock injection tests ──────────────────────────────

    [Fact]
    public void Enrich_CustomClock_ProducesExpectedDate()
    {
        var customDate = new DateTime(2026, 12, 25, 14, 30, 0, DateTimeKind.Utc);
        var customClock = () => customDate;

        var markdown = @"---
title: Azure MCP Server tools for Storage
ms.service: azure-mcp-server
---

# Azure MCP Server tools for Storage";

        var enricher = new FrontmatterEnricher(customClock);
        var result = enricher.Enrich(markdown);

        Assert.Contains("ms.date: 12/25/2026", result);
    }

    [Fact]
    public void Enrich_DefaultClock_UsesTodaysDate()
    {
        var markdown = @"---
title: Azure MCP Server tools for Storage
ms.service: azure-mcp-server
---

# Azure MCP Server tools for Storage";

        var enricher = new FrontmatterEnricher(); // No clock provided, should use default
        var result = enricher.Enrich(markdown);

        var expectedDate = DateTime.UtcNow.ToString("MM/dd/yyyy");
        Assert.Contains($"ms.date: {expectedDate}", result);
    }

    [Fact]
    public void Enrich_UsesMetadataConstants()
    {
        var markdown = @"---
title: Azure MCP Server tools for Storage
ms.service: azure-mcp-server
---

# Azure MCP Server tools for Storage";

        var enricher = new FrontmatterEnricher(FixedClock);
        var result = enricher.Enrich(markdown);

        // Verify all constants from MetadataConstants are used
        Assert.Contains($"author: {MetadataConstants.Author}", result);
        Assert.Contains($"ms.author: {MetadataConstants.Author}", result);
        Assert.Contains($"ms.reviewer: {MetadataConstants.Reviewer}", result);
        Assert.Contains($"ai-usage: {MetadataConstants.AiUsage}", result);
        Assert.Contains(MetadataConstants.ContentWellValue, result);
        Assert.Contains($"ms.custom: {MetadataConstants.MsCustom}", result);
    }

    [Fact]
    public void EnrichWithDefaults_WorksWithoutInstantiation()
    {
        var markdown = @"---
title: Azure MCP Server tools for Storage
ms.service: azure-mcp-server
---

# Azure MCP Server tools for Storage";

        var result = FrontmatterEnricher.EnrichWithDefaults(markdown);

        Assert.Contains("author: diberry", result);
        Assert.Contains("ms.date:", result);
        Assert.Matches(@"ms\.date: \d{2}/\d{2}/\d{4}", result);
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
