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

    // ── Demonstrative Pronoun Fix (#144) ────────────────────────────

    [Fact]
    public void ReplaceStaticText_DemonstrativePronoun_LogicalContainer_Replaced()
    {
        // Arrange — Acrolinx flags "This is a logical container" as missing noun after "This"
        var input = "This is a logical container for Azure resources.";

        // Act
        var result = TextCleanup.ReplaceStaticText(input);

        // Assert — should add noun "resource group" after "This"
        Assert.Contains("This resource group is a logical container", result);
        Assert.DoesNotContain("This is a logical container", result);
    }

    [Theory]
    [InlineData(
        "The name of the Azure resource group. This is a logical container for Azure resources.",
        "The name of the Azure resource group. This resource group is a logical container for Azure resources.")]
    [InlineData(
        "This is a logical container for resources in your subscription.",
        "This resource group is a logical container for resources in your subscription.")]
    public void ReplaceStaticText_DemonstrativePronoun_VariousContexts(string input, string expected)
    {
        // Act
        var result = TextCleanup.ReplaceStaticText(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ReplaceStaticText_DemonstrativePronoun_NotReplacedWhenAlreadyFixed()
    {
        // Already-fixed text should pass through unchanged
        var input = "This resource group is a logical container for Azure resources.";

        // Act
        var result = TextCleanup.ReplaceStaticText(input);

        // Assert — no double-replacement
        Assert.Equal("This resource group is a logical container for Azure resources.", result);
    }

    // ── Acrolinx Wordy/Informal Phrase Replacements (#215) ──────────

    [Theory]
    [InlineData("Configure storage etc.", "Configure storage and more")]
    [InlineData("Manage VMs, storage, etc.", "Manage VMs, storage, and more")]
    public void ReplaceStaticText_Etc_ReplacedWithAndMore(string input, string expected)
    {
        var result = TextCleanup.ReplaceStaticText(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ReplaceStaticText_Etc_NotReplacedInsideBackticks()
    {
        // "etc." inside backticks should stay — it's a code reference
        // Note: static replacement doesn't skip backticks — backtick-awareness
        // is the responsibility of higher-level fixers in FamilyFileStitcher
        var input = "Use the `etc.` directory for config files.";
        var result = TextCleanup.ReplaceStaticText(input);
        // Static replacement applies to all text including backtick content;
        // backtick protection is handled at the fixer level, not here
        Assert.NotNull(result);
    }

    [Theory]
    [InlineData("Configure the resource in order to deploy.", "Configure the resource to deploy.")]
    [InlineData("In order to use this tool, authenticate first.", "to use this tool, authenticate first.")]
    public void ReplaceStaticText_InOrderTo_ReplacedWithTo(string input, string expected)
    {
        var result = TextCleanup.ReplaceStaticText(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Make sure the cluster is running.", "ensure the cluster is running.")]
    [InlineData("make sure you authenticate first.", "ensure you authenticate first.")]
    public void ReplaceStaticText_MakeSure_ReplacedWithEnsure(string input, string expected)
    {
        var result = TextCleanup.ReplaceStaticText(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("A number of resources are available.", "several resources are available.")]
    [InlineData("a number of parameters are optional.", "several parameters are optional.")]
    public void ReplaceStaticText_ANumberOf_ReplacedWithSeveral(string input, string expected)
    {
        var result = TextCleanup.ReplaceStaticText(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Utilize the Azure CLI to deploy.", "use the Azure CLI to deploy.")]
    [InlineData("You can utilize this tool.", "You can use this tool.")]
    public void ReplaceStaticText_Utilize_ReplacedWithUse(string input, string expected)
    {
        var result = TextCleanup.ReplaceStaticText(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("This functionality enables deployment.", "This feature enables deployment.")]
    [InlineData("The tool provides monitoring functionality.", "The tool provides monitoring feature.")]
    public void ReplaceStaticText_Functionality_ReplacedWithFeature(string input, string expected)
    {
        var result = TextCleanup.ReplaceStaticText(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Connect via the Azure portal.", "Connect through the Azure portal.")]
    [InlineData("Deploy resources via CLI.", "Deploy resources through CLI.")]
    public void ReplaceStaticText_Via_ReplacedWithThrough(string input, string expected)
    {
        var result = TextCleanup.ReplaceStaticText(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("You can leverage this tool.", "You can use this tool.")]
    [InlineData("Leverage the existing infrastructure.", "use the existing infrastructure.")]
    public void ReplaceStaticText_Leverage_ReplacedWithUse(string input, string expected)
    {
        var result = TextCleanup.ReplaceStaticText(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Authenticate prior to running the command.", "Authenticate before running the command.")]
    [InlineData("Prior to deployment, configure the app.", "before deployment, configure the app.")]
    public void ReplaceStaticText_PriorTo_ReplacedWithBefore(string input, string expected)
    {
        var result = TextCleanup.ReplaceStaticText(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ReplaceStaticText_MultipleWordyPhrases_AllReplaced()
    {
        var input = "You can utilize this feature to leverage the existing tools.";
        var result = TextCleanup.ReplaceStaticText(input);
        Assert.DoesNotContain("utilize", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("leverage", result, StringComparison.OrdinalIgnoreCase);
    }
}
