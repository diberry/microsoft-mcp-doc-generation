// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ToolFamilyCleanup.Services;
using Xunit;

namespace DocGeneration.Steps.ToolFamilyCleanup.Tests;

/// <summary>
/// TDD tests for Issue #416, Item 6: Redirection entries for file renames.
/// When a tool file is renamed, a redirect entry is generated in
/// .openpublishing.redirection.json format. Redirect chains must validate
/// (301 → 200). Duplicate entries must not be created.
/// </summary>
public class RedirectEntryGeneratorTests
{
    // ── Redirect entry generation ────────────────────────────────────

    [Fact]
    public void GenerateRedirect_ProducesCorrectFormat()
    {
        var oldPath = "/azure/developer/azure-mcp-server/azure-mcp-server-storage";
        var newPath = "/azure/developer/azure-mcp-server/azure-mcp-server-azure-storage";

        var entry = RedirectEntryGenerator.GenerateEntry(oldPath, newPath);

        Assert.Equal(oldPath, entry.SourcePath);
        Assert.Equal(newPath, entry.RedirectUrl);
        Assert.False(entry.RedirectDocumentId, "Redirect should not carry document ID by default");
    }

    [Fact]
    public void GenerateRedirect_FromFileRename_MapsPathsCorrectly()
    {
        var oldFileName = "azure-mcp-server-storage.md";
        var newFileName = "azure-mcp-server-azure-storage.md";
        var docsetBasePath = "/azure/developer/azure-mcp-server";

        var entry = RedirectEntryGenerator.FromFileRename(oldFileName, newFileName, docsetBasePath);

        Assert.Equal("/azure/developer/azure-mcp-server/azure-mcp-server-storage", entry.SourcePath);
        Assert.Equal("/azure/developer/azure-mcp-server/azure-mcp-server-azure-storage", entry.RedirectUrl);
    }

    // ── Redirect chain validation (301 → 200) ───────────────────────

    [Fact]
    public void ValidateChain_DirectRedirect_IsValid()
    {
        var redirects = new[]
        {
            new RedirectEntry("/old-page", "/new-page", false),
        };

        // The new-page exists (simulate via a known-pages set)
        var knownPages = new HashSet<string> { "/new-page" };

        var errors = RedirectEntryGenerator.ValidateChains(redirects, knownPages);

        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateChain_ChainedRedirect_DetectsChain()
    {
        var redirects = new[]
        {
            new RedirectEntry("/page-a", "/page-b", false),
            new RedirectEntry("/page-b", "/page-c", false),
        };

        var knownPages = new HashSet<string> { "/page-c" };

        var errors = RedirectEntryGenerator.ValidateChains(redirects, knownPages);

        // Should flag the chain: /page-a → /page-b → /page-c
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("/page-b", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateChain_BrokenRedirect_DetectsDeadEnd()
    {
        var redirects = new[]
        {
            new RedirectEntry("/old-page", "/nonexistent", false),
        };

        var knownPages = new HashSet<string>(); // target doesn't exist

        var errors = RedirectEntryGenerator.ValidateChains(redirects, knownPages);

        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("/nonexistent", StringComparison.OrdinalIgnoreCase));
    }

    // ── Duplicate prevention ────────────────────────────────────────

    [Fact]
    public void AddEntry_NoDuplicate_AddsSuccessfully()
    {
        var existing = new List<RedirectEntry>();
        var newEntry = new RedirectEntry("/old", "/new", false);

        var result = RedirectEntryGenerator.AddIfNotDuplicate(existing, newEntry);

        Assert.True(result, "Should add non-duplicate entry");
        Assert.Single(existing);
    }

    [Fact]
    public void AddEntry_DuplicateSourcePath_DoesNotAdd()
    {
        var existing = new List<RedirectEntry>
        {
            new RedirectEntry("/old", "/new", false),
        };
        var duplicate = new RedirectEntry("/old", "/another-new", false);

        var result = RedirectEntryGenerator.AddIfNotDuplicate(existing, duplicate);

        Assert.False(result, "Should not add duplicate source path");
        Assert.Single(existing);
    }

    [Fact]
    public void AddEntry_DifferentSourcePath_Adds()
    {
        var existing = new List<RedirectEntry>
        {
            new RedirectEntry("/old-a", "/new-a", false),
        };
        var different = new RedirectEntry("/old-b", "/new-b", false);

        var result = RedirectEntryGenerator.AddIfNotDuplicate(existing, different);

        Assert.True(result, "Should add entry with different source path");
        Assert.Equal(2, existing.Count);
    }

    // ── JSON serialization ──────────────────────────────────────────

    [Fact]
    public void SerializeToJson_ProducesOpenPublishingFormat()
    {
        var entries = new[]
        {
            new RedirectEntry("/azure/old-page", "/azure/new-page", false),
        };

        var json = RedirectEntryGenerator.SerializeToOpenPublishingJson(entries);

        Assert.Contains("\"source_path\"", json);
        Assert.Contains("\"redirect_url\"", json);
        Assert.Contains("\"redirect_document_id\"", json);
        Assert.Contains("/azure/old-page", json);
        Assert.Contains("/azure/new-page", json);
    }

    // ── Edge cases ──────────────────────────────────────────────────

    [Fact]
    public void GenerateEntry_EmptyPaths_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            RedirectEntryGenerator.GenerateEntry("", "/new"));
    }

    [Fact]
    public void GenerateEntry_SameSourceAndTarget_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            RedirectEntryGenerator.GenerateEntry("/same", "/same"));
    }
}


