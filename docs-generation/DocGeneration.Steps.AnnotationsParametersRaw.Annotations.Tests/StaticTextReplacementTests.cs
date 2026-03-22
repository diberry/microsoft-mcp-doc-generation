// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using NaturalLanguageGenerator;
using Xunit;

namespace CSharpGenerator.Tests;

/// <summary>
/// Tests for TextCleanup.ReplaceStaticText to verify branding and text normalization.
/// Fixes: #141 — CosmosDB branding not normalized.
/// </summary>
public class StaticTextReplacementTests : IClassFixture<TextCleanupFixture>
{
    private readonly TextCleanupFixture _fixture;

    public StaticTextReplacementTests(TextCleanupFixture fixture)
    {
        _fixture = fixture;
    }

    // ── CosmosDB Branding (#141) ────────────────────────────────────

    [Fact]
    public void ReplaceStaticText_CosmosDB_ReplacedWithAzureCosmosDB()
    {
        // Arrange — CLI description containing "CosmosDB" (no space)
        var input = "Add a CosmosDB database to the web app";

        // Act
        var result = TextCleanup.ReplaceStaticText(input);

        // Assert — should normalize to "Azure Cosmos DB"
        Assert.Contains("Azure Cosmos DB", result);
        Assert.DoesNotContain("CosmosDB", result);
    }

    [Theory]
    [InlineData("Connect to CosmosDB", "Connect to Azure Cosmos DB")]
    [InlineData("The CosmosDB account was created", "The Azure Cosmos DB account was created")]
    [InlineData("Use CosmosDB for NoSQL data", "Use Azure Cosmos DB for NoSQL data")]
    public void ReplaceStaticText_CosmosDB_VariousContexts(string input, string expected)
    {
        // Act
        var result = TextCleanup.ReplaceStaticText(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ReplaceStaticText_CosmosDB_NotReplacedInsideWord()
    {
        // "CosmosDB" inside a larger token should NOT be replaced
        // e.g., "MyCosmosDBApp" should not become "MyAzure Cosmos DBApp"
        var input = "The MyCosmosDBApp is running";

        // Act
        var result = TextCleanup.ReplaceStaticText(input);

        // Assert — word-boundary matching should prevent partial replacement
        Assert.Contains("MyCosmosDBApp", result);
    }

    // ── Existing Replacement Verification ───────────────────────────

    [Theory]
    [InlineData("e.g.", "for example")]
    [InlineData("i.e.", "in other words")]
    public void ReplaceStaticText_ExistingAbbreviations_StillWork(string abbrev, string expanded)
    {
        // Arrange
        var input = $"This is {abbrev} a test";

        // Act
        var result = TextCleanup.ReplaceStaticText(input);

        // Assert
        Assert.Contains(expanded, result);
    }

    [Theory]
    [InlineData("VMSS", "Virtual machine scale set (VMSS)")]
    public void ReplaceStaticText_ExistingBrandNames_StillWork(string original, string replacement)
    {
        // Arrange — VMSS as standalone word
        var input = $"Deploy to {original} instances";

        // Act
        var result = TextCleanup.ReplaceStaticText(input);

        // Assert
        Assert.Contains(replacement, result);
    }
}
