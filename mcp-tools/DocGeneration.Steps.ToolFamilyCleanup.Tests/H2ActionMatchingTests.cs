// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ToolFamilyCleanup.Services;
using Xunit;

namespace DocGeneration.Steps.ToolFamilyCleanup.Tests;

/// <summary>
/// TDD tests for Issue #416, Item 2: H2 "get" action matching.
/// When a tool description says "List or get", the H2 should say "list or get"
/// not "get details". This tests the deterministic H2 description extraction.
/// </summary>
public class H2ActionMatchingTests
{
    // ── "List or get" pattern detection ──────────────────────────────

    [Fact]
    public void ExtractActionFromDescription_ListOrGet_PreservesCompoundAction()
    {
        var description = "List or get Azure SQL databases, servers, and elastic pools.";

        // The H2 heading generator should detect "List or get" and use it verbatim
        // instead of defaulting to "get details" or just "list"
        var heading = H2HeadingDescriptionExtractor.ExtractAction(description);

        Assert.Contains("list or get", heading, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("get details", heading, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExtractActionFromDescription_ListOrGetVariant_CaseInsensitive()
    {
        var description = "List Or Get resources in a subscription.";

        var heading = H2HeadingDescriptionExtractor.ExtractAction(description);

        Assert.Contains("list or get", heading, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExtractActionFromDescription_GetOnly_UsesGetAction()
    {
        var description = "Get details of a specific resource.";

        var heading = H2HeadingDescriptionExtractor.ExtractAction(description);

        Assert.Contains("get", heading, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExtractActionFromDescription_ListOnly_UsesListAction()
    {
        var description = "List all resources in a resource group.";

        var heading = H2HeadingDescriptionExtractor.ExtractAction(description);

        Assert.Contains("list", heading, StringComparison.OrdinalIgnoreCase);
    }

    // ── "Create or update" compound pattern ─────────────────────────

    [Fact]
    public void ExtractActionFromDescription_CreateOrUpdate_PreservesCompound()
    {
        var description = "Create or update a key-value setting in the store.";

        var heading = H2HeadingDescriptionExtractor.ExtractAction(description);

        Assert.Contains("create or update", heading, StringComparison.OrdinalIgnoreCase);
    }

    // ── Plain verbs ─────────────────────────────────────────────────

    [Theory]
    [InlineData("Delete a resource group.", "delete")]
    [InlineData("Update the configuration for a web app.", "update")]
    [InlineData("Create a new storage account.", "create")]
    public void ExtractActionFromDescription_SimpleVerb_ExtractsCorrectly(string description, string expectedVerb)
    {
        var heading = H2HeadingDescriptionExtractor.ExtractAction(description);

        Assert.StartsWith(expectedVerb, heading, StringComparison.OrdinalIgnoreCase);
    }

    // ── Edge: empty/null description ─────────────────────────────────

    [Fact]
    public void ExtractActionFromDescription_NullOrEmpty_ReturnsFallback()
    {
        Assert.NotNull(H2HeadingDescriptionExtractor.ExtractAction(""));
        Assert.NotNull(H2HeadingDescriptionExtractor.ExtractAction(null!));
    }
}
