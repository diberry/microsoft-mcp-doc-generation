// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using HorizontalArticleGenerator.Models;
using Xunit;
using ArticleGenerator = HorizontalArticleGenerator.Generators.HorizontalArticleGenerator;

namespace HorizontalArticleGenerator.Tests;

/// <summary>
/// Unit tests for HorizontalArticleGenerator.AggregateAIData.
/// Covers normal aggregation, empty-summary mapping, capability/scenario filtering,
/// and MoreInfoLink lookup by Command.
/// </summary>
public class AggregateAIDataTests
{
    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static StaticArticleData MakeStaticData(params (string command, string moreInfoLink)[] tools)
    {
        return new StaticArticleData
        {
            ServiceBrandName   = "Azure Key Vault",
            ServiceIdentifier  = "keyvault",
            Tools = tools.Select(t => new HorizontalToolSummary
            {
                Command     = t.command,
                MoreInfoLink = t.moreInfoLink
            }).ToList()
        };
    }

    private static PerToolAIData MakePerToolData(string command, string capability, string? scenarioTitle = null)
    {
        return new PerToolAIData
        {
            Command          = command,
            ShortDescription = $"Short description for {command}.",
            Capability       = capability,
            Scenario = scenarioTitle is null ? null : new Scenario
            {
                Title           = scenarioTitle,
                Description     = $"Scenario for {command}.",
                ExpectedOutcome = "Success."
            }
        };
    }

    private static NamespaceSummaryAIData MakeSummary(
        string shortDesc  = "Manage secrets in Azure Key Vault.",
        string overview   = "Azure Key Vault lets you store and control access to tokens, passwords, and certificates.")
    {
        return new NamespaceSummaryAIData
        {
            ServiceShortDescription      = shortDesc,
            ServiceOverview              = overview,
            ServiceSpecificPrerequisites = [new Prerequisite { Title = "Key Vault instance", Description = "A vault must exist." }],
            RequiredRoles                = [new RequiredRole { Name = "Key Vault Secrets User", Purpose = "Read secrets." }],
            BestPractices                = [new BestPractice { Title = "Rotate secrets", Description = "Rotate secrets regularly." }],
            ServiceDocLink               = "/azure/key-vault/general/overview",
            AdditionalLinks              = [new AdditionalLink { Title = "Quickstart", Url = "/azure/key-vault/secrets/quick-create-portal" }]
        };
    }

    // ---------------------------------------------------------------------------
    // Test 1: Normal aggregation — N per-tool results + populated summary
    // ---------------------------------------------------------------------------

    [Fact]
    public void AggregateAIData_NormalCase_AggregatesAllFields()
    {
        var staticData = MakeStaticData(
            ("keyvault secret list",   "../parameters/keyvault-secret-list-parameters.md"),
            ("keyvault secret set",    "../parameters/keyvault-secret-set-parameters.md"),
            ("keyvault secret delete", "../parameters/keyvault-secret-delete-parameters.md")
        );

        var perToolResults = new List<PerToolAIData>
        {
            MakePerToolData("keyvault secret list",   "List secrets in Azure Key Vault",    "Audit stored secrets"),
            MakePerToolData("keyvault secret set",    "Create or update secrets",           "Store application credentials"),
            MakePerToolData("keyvault secret delete", "Delete a secret from Key Vault",     "Remove stale credentials")
        };

        var summary = MakeSummary();

        var result = ArticleGenerator.AggregateAIData(staticData, perToolResults, summary);

        // Namespace-level fields come from summary
        Assert.Equal("Manage secrets in Azure Key Vault.", result.ServiceShortDescription);
        Assert.Equal("Azure Key Vault lets you store and control access to tokens, passwords, and certificates.", result.ServiceOverview);
        Assert.Single(result.ServiceSpecificPrerequisites);
        Assert.Equal("Key Vault instance", result.ServiceSpecificPrerequisites[0].Title);
        Assert.Single(result.RequiredRoles);
        Assert.Equal("Key Vault Secrets User", result.RequiredRoles[0].Name);
        Assert.NotNull(result.BestPractices);
        Assert.Single(result.BestPractices!);
        Assert.Equal("/azure/key-vault/general/overview", result.ServiceDocLink);
        Assert.Single(result.AdditionalLinks);

        // Capabilities: one per tool
        Assert.Equal(3, result.Capabilities.Count);
        Assert.Contains("List secrets in Azure Key Vault", result.Capabilities);
        Assert.Contains("Create or update secrets", result.Capabilities);
        Assert.Contains("Delete a secret from Key Vault", result.Capabilities);

        // Scenarios: one per tool (all three have scenarios)
        Assert.Equal(3, result.Scenarios.Count);
        Assert.Contains(result.Scenarios, s => s.Title == "Audit stored secrets");

        // Tools list
        Assert.Equal(3, result.Tools.Count);
        Assert.All(result.Tools, t => Assert.False(string.IsNullOrEmpty(t.ShortDescription)));
    }

    // ---------------------------------------------------------------------------
    // Test 2: Empty NamespaceSummaryAIData (all defaults) — fields map to empty strings
    // ---------------------------------------------------------------------------

    [Fact]
    public void AggregateAIData_EmptySummary_ProducesEmptyServiceFields()
    {
        var staticData = MakeStaticData(("cosmos db container list", "../parameters/cosmos-db-container-list-parameters.md"));

        var perToolResults = new List<PerToolAIData>
        {
            MakePerToolData("cosmos db container list", "List Cosmos DB containers")
        };

        var emptySummary = new NamespaceSummaryAIData(); // all defaults — empty strings / empty lists

        var result = ArticleGenerator.AggregateAIData(staticData, perToolResults, emptySummary);

        // These are the fields that the validation gate in GenerateSingleArticleAsync will reject
        Assert.True(string.IsNullOrWhiteSpace(result.ServiceShortDescription));
        Assert.True(string.IsNullOrWhiteSpace(result.ServiceOverview));

        // Other fields still aggregate normally from per-tool results
        Assert.Single(result.Capabilities);
        Assert.Equal("List Cosmos DB containers", result.Capabilities[0]);
    }

    // ---------------------------------------------------------------------------
    // Test 3: perToolResults with empty capability or null scenario — filtered out
    // ---------------------------------------------------------------------------

    [Fact]
    public void AggregateAIData_EmptyCapabilityAndNullScenario_FilteredFromCollections()
    {
        var staticData = MakeStaticData(
            ("monitor alert list",   "../parameters/monitor-alert-list-parameters.md"),
            ("monitor alert create", "../parameters/monitor-alert-create-parameters.md"),
            ("monitor alert delete", "../parameters/monitor-alert-delete-parameters.md")
        );

        var perToolResults = new List<PerToolAIData>
        {
            // Good entry
            MakePerToolData("monitor alert list",   "List Azure Monitor alerts", "Audit active alerts"),
            // Empty capability — should not appear in Capabilities
            new PerToolAIData { Command = "monitor alert create", Capability = "   ", Scenario = null, ShortDescription = "Create an alert." },
            // Null scenario — should not appear in Scenarios
            MakePerToolData("monitor alert delete", "Delete an Azure Monitor alert", scenarioTitle: null)
        };

        var result = ArticleGenerator.AggregateAIData(staticData, perToolResults, MakeSummary());

        // Only 2 non-whitespace capabilities
        Assert.Equal(2, result.Capabilities.Count);
        Assert.DoesNotContain("   ", result.Capabilities);

        // Only 1 scenario (the one that had a non-null Scenario)
        Assert.Single(result.Scenarios);
        Assert.Equal("Audit active alerts", result.Scenarios[0].Title);

        // All 3 tools still appear in the Tools list (ShortDescription comes through regardless)
        Assert.Equal(3, result.Tools.Count);
    }

    // ---------------------------------------------------------------------------
    // Test 4: MoreInfoLink lookup — exact match found vs not found
    // ---------------------------------------------------------------------------

    [Fact]
    public void AggregateAIData_MoreInfoLink_ExactMatchFound_UsesStaticLink()
    {
        const string expectedLink = "../parameters/storage-blob-list-parameters.md";

        var staticData = MakeStaticData(
            ("storage blob list",   expectedLink),
            ("storage blob upload", "../parameters/storage-blob-upload-parameters.md")
        );

        var perToolResults = new List<PerToolAIData>
        {
            MakePerToolData("storage blob list",   "List blobs in Azure Storage"),
            MakePerToolData("storage blob upload", "Upload a blob to Azure Storage")
        };

        var result = ArticleGenerator.AggregateAIData(staticData, perToolResults, MakeSummary());

        var listTool = result.Tools.Single(t => t.Command == "storage blob list");
        Assert.Equal(expectedLink, listTool.MoreInfoLink);
    }

    [Fact]
    public void AggregateAIData_MoreInfoLink_NoMatchFound_FallsBackToEmpty()
    {
        // StaticData has no entry matching the per-tool command
        var staticData = MakeStaticData(("aks nodepool list", "../parameters/aks-nodepool-list-parameters.md"));

        var perToolResults = new List<PerToolAIData>
        {
            // Command that is NOT in staticData.Tools
            MakePerToolData("aks cluster list", "List AKS clusters")
        };

        var result = ArticleGenerator.AggregateAIData(staticData, perToolResults, MakeSummary());

        var tool = result.Tools.Single(t => t.Command == "aks cluster list");
        Assert.Equal(string.Empty, tool.MoreInfoLink);
    }
}
