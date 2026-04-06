// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;
using HorizontalArticleGenerator.Generators;
using HorizontalArticleGenerator.Models;

namespace HorizontalArticleGenerator.Tests;

public class ArticleContentProcessorValidationTests
{
    private readonly ArticleContentProcessor _processor;

    public ArticleContentProcessorValidationTests()
    {
        // No transformation engine — tests pure validation logic
        _processor = new ArticleContentProcessor();
    }

    // ===== Trailing Period Stripping =====

    [Fact]
    public void Validate_StripsTrailingPeriod_FromServiceShortDescription()
    {
        var data = CreateMinimalData();
        data.ServiceShortDescription = "web applications and APIs.";

        var result = _processor.Validate(data, "Test");

        Assert.Equal("web applications and APIs", data.ServiceShortDescription);
        Assert.Contains(result.Corrections, c => c.Contains("serviceShortDescription"));
    }

    [Fact]
    public void Validate_StripsTrailingPeriodAndSpace_FromServiceShortDescription()
    {
        var data = CreateMinimalData();
        data.ServiceShortDescription = "web applications and APIs. ";

        _processor.Validate(data, "Test");

        Assert.Equal("web applications and APIs", data.ServiceShortDescription);
    }

    [Fact]
    public void Validate_NoChange_WhenServiceShortDescriptionHasNoPeriod()
    {
        var data = CreateMinimalData();
        data.ServiceShortDescription = "keys, secrets, and certificates";

        var result = _processor.Validate(data, "Test");

        Assert.Equal("keys, secrets, and certificates", data.ServiceShortDescription);
        Assert.DoesNotContain(result.Corrections, c => c.Contains("serviceShortDescription"));
    }

    [Fact]
    public void Validate_StripsTrailingPeriods_FromAllCapabilities()
    {
        var data = CreateMinimalData();
        data.Capabilities = new List<string>
        {
            "Create and manage virtual machines.",
            "Configure network security groups.",
            "Monitor resource utilization metrics."
        };

        _processor.Validate(data, "Test");

        Assert.Equal("Create and manage virtual machines", data.Capabilities[0]);
        Assert.Equal("Configure network security groups", data.Capabilities[1]);
        Assert.Equal("Monitor resource utilization metrics", data.Capabilities[2]);
    }

    [Fact]
    public void Validate_StripsTrailingPeriods_FromBestPracticeTitles()
    {
        var data = CreateMinimalData();
        data.BestPractices = new List<BestPractice>
        {
            new() { Title = "Use managed identities.", Description = "Desc." },
            new() { Title = "Monitor performance.", Description = "Desc." },
            new() { Title = "Optimize costs.", Description = "Desc." },
            new() { Title = "Enable logging.", Description = "Desc." }
        };

        _processor.Validate(data, "Test");

        Assert.Equal("Use managed identities", data.BestPractices[0].Title);
        Assert.Equal("Monitor performance", data.BestPractices[1].Title);
        // Descriptions should NOT be stripped (they are full sentences)
        Assert.Equal("Desc.", data.BestPractices[0].Description);
    }

    [Fact]
    public void Validate_StripsTrailingPeriods_FromPrerequisiteTitles()
    {
        var data = CreateMinimalData();
        data.ServiceSpecificPrerequisites = new List<Prerequisite>
        {
            new() { Title = "Existing Storage Account.", Description = "Required." }
        };

        _processor.Validate(data, "Test");

        Assert.Equal("Existing Storage Account", data.ServiceSpecificPrerequisites[0].Title);
        Assert.Equal("Required.", data.ServiceSpecificPrerequisites[0].Description);
    }

    [Fact]
    public void Validate_StripsTrailingPeriods_FromScenarioTitles()
    {
        var data = CreateMinimalData();
        data.Scenarios = new List<Scenario>
        {
            new()
            {
                Title = "Add a database connection.",
                Description = "Connect your app.",
                Examples = new List<string> { "Add db" },
                ExpectedOutcome = "Done."
            }
        };

        _processor.Validate(data, "Test");

        Assert.Equal("Add a database connection", data.Scenarios[0].Title);
    }

    // ===== Broken Sentence Fix =====

    [Fact]
    public void Validate_FixesBrokenSentence_InServiceShortDescription()
    {
        var data = CreateMinimalData();
        data.ServiceShortDescription = "web applications. and APIs";

        _processor.Validate(data, "Test");

        Assert.Equal("web applications and APIs", data.ServiceShortDescription);
    }

    [Fact]
    public void Validate_FixesBrokenSentence_InServiceOverview()
    {
        var data = CreateMinimalData();
        data.ServiceOverview = "is a platform. for building apps";

        _processor.Validate(data, "Test");

        Assert.Equal("is a platform for building apps", data.ServiceOverview);
    }

    [Fact]
    public void Validate_PreservesLegitimateAbbreviations_InServiceOverview()
    {
        var data = CreateMinimalData();
        data.ServiceOverview = "is a platform. It provides scaling.";

        _processor.Validate(data, "Test");

        // "period + uppercase I" should NOT be modified
        Assert.Equal("is a platform. It provides scaling.", data.ServiceOverview);
    }

    // ===== Redundant Words =====

    [Fact]
    public void Validate_RemovesRedundantWord_AtStartOfOverview()
    {
        var data = CreateMinimalData();
        data.ServiceOverview = "Search Search enables full-text searching.";

        _processor.Validate(data, "Test");

        Assert.Equal("Search enables full-text searching.", data.ServiceOverview);
    }

    [Fact]
    public void Validate_NoChange_WhenNoRedundantWord()
    {
        var data = CreateMinimalData();
        data.ServiceOverview = "is a cloud search service.";

        _processor.Validate(data, "Test");

        Assert.Equal("is a cloud search service.", data.ServiceOverview);
    }

    // ===== RBAC Role Validation =====

    [Fact]
    public void Validate_BlocksInventedRbacRoles()
    {
        var data = CreateMinimalData();
        data.RequiredRoles = new List<RequiredRole>
        {
            new() { Name = "Search Knowledge Base Data Analyst", Purpose = "Read KB" }
        };

        var result = _processor.Validate(data, "Test");

        Assert.True(result.HasCriticalErrors);
        Assert.Contains(result.CriticalErrors, e => e.Contains("INVENTED RBAC ROLE"));
    }

    [Fact]
    public void Validate_AllowsOfficialRbacRoles()
    {
        var data = CreateMinimalData();
        data.RequiredRoles = new List<RequiredRole>
        {
            new() { Name = "Storage Blob Data Contributor", Purpose = "Write blobs" },
            new() { Name = "Key Vault Secrets User", Purpose = "Read secrets" },
            new() { Name = "Search Index Data Reader", Purpose = "Query indexes" }
        };

        var result = _processor.Validate(data, "Test");

        Assert.False(result.HasCriticalErrors);
    }

    [Fact]
    public void Validate_AllowsContributorReaderRoles()
    {
        var data = CreateMinimalData();
        data.RequiredRoles = new List<RequiredRole>
        {
            new() { Name = "Cognitive Services Contributor", Purpose = "Manage AI services" },
            new() { Name = "Cosmos DB Account Reader Role", Purpose = "Read" }
        };

        var result = _processor.Validate(data, "Test");

        Assert.False(result.HasCriticalErrors);
    }

    [Fact]
    public void Validate_BlocksAdministratorSuffix()
    {
        // "Administrator" is never used in Azure built-in RBAC roles
        var data = CreateMinimalData();
        data.RequiredRoles = new List<RequiredRole>
        {
            new() { Name = "Cosmos DB Administrator", Purpose = "Admin access" }
        };

        var result = _processor.Validate(data, "Test");

        Assert.True(result.HasCriticalErrors);
        Assert.Contains(result.CriticalErrors, e => e.Contains("Administrator"));
    }

    [Fact]
    public void Validate_BlocksGenericPrefixRoles()
    {
        // "Database Contributor" is too generic — real role would be "SQL DB Contributor"
        var data = CreateMinimalData();
        data.RequiredRoles = new List<RequiredRole>
        {
            new() { Name = "Database Contributor", Purpose = "Manage databases" }
        };

        var result = _processor.Validate(data, "Test");

        Assert.True(result.HasCriticalErrors);
        Assert.Contains(result.CriticalErrors, e => e.Contains("too generic"));
    }

    [Theory]
    [InlineData("Database Reader")]
    [InlineData("Application Contributor")]
    [InlineData("Resource Contributor")]
    public void Validate_BlocksVariousGenericPrefixRoles(string roleName)
    {
        var data = CreateMinimalData();
        data.RequiredRoles = new List<RequiredRole>
        {
            new() { Name = roleName, Purpose = "Some purpose" }
        };

        var result = _processor.Validate(data, "Test");

        Assert.True(result.HasCriticalErrors);
    }

    [Theory]
    [InlineData("SQL DB Contributor")]
    [InlineData("SQL Server Contributor")]
    [InlineData("Website Contributor")]
    [InlineData("Web Plan Contributor")]
    [InlineData("Storage Account Contributor")]
    [InlineData("Key Vault Contributor")]
    public void Validate_AllowsOfficialServiceSpecificRoles(string roleName)
    {
        var data = CreateMinimalData();
        data.RequiredRoles = new List<RequiredRole>
        {
            new() { Name = roleName, Purpose = "Some purpose" }
        };

        var result = _processor.Validate(data, "Test");

        Assert.False(result.HasCriticalErrors);
    }

    // ===== Tool Description Quality =====

    [Fact]
    public void Validate_WarnsOnShortToolDescription()
    {
        var data = CreateMinimalData();
        data.Tools = new List<ToolWithAIDescription>
        {
            new() { Command = "storage list", ShortDescription = "List items" }
        };

        var result = _processor.Validate(data, "Test");

        Assert.Contains(result.Warnings, w => w.Contains("too short"));
    }

    [Fact]
    public void Validate_WarnsOnGenericToolDescription()
    {
        var data = CreateMinimalData();
        data.Tools = new List<ToolWithAIDescription>
        {
            new() { Command = "storage list", ShortDescription = "Get details about storage resources and configurations" }
        };

        var result = _processor.Validate(data, "Test");

        Assert.Contains(result.Warnings, w => w.Contains("generic description"));
    }

    [Fact]
    public void Validate_NoWarning_WhenToolDescriptionIsGoodQuality()
    {
        var data = CreateMinimalData();
        data.Tools = new List<ToolWithAIDescription>
        {
            new() { Command = "search index get", ShortDescription = "Retrieve index schema, field definitions, and scoring profiles" }
        };

        var result = _processor.Validate(data, "Test");

        Assert.DoesNotContain(result.Warnings, w => w.Contains("too short"));
        Assert.DoesNotContain(result.Warnings, w => w.Contains("generic description"));
    }

    // ===== Best Practice Count =====

    [Fact]
    public void Validate_WarnsWhenFewerThanThreeBestPractices()
    {
        var data = CreateMinimalData();
        data.BestPractices = new List<BestPractice>
        {
            new() { Title = "Use Entra ID", Description = "Secure." },
            new() { Title = "Monitor", Description = "Watch." }
        };

        var result = _processor.Validate(data, "Test");

        Assert.Contains(result.Warnings, w => w.Contains("best practices"));
    }

    [Fact]
    public void Validate_NoWarning_WhenThreeOrMoreBestPractices()
    {
        var data = CreateMinimalData();
        data.BestPractices = new List<BestPractice>
        {
            new() { Title = "Security", Description = "D." },
            new() { Title = "Reliability", Description = "D." },
            new() { Title = "Cost", Description = "D." },
            new() { Title = "Performance", Description = "D." }
        };

        var result = _processor.Validate(data, "Test");

        Assert.DoesNotContain(result.Warnings, w => w.Contains("best practices"));
    }

    // ===== Combined: trailing period on serviceShortDescription mid-sentence =====

    [Fact]
    public void Validate_PreventsBrokenFrontmatter_WhenDescriptionHasTrailingPeriod()
    {
        // Simulates the exact bug: "manage web applications and APIs. through AI-powered"
        var data = CreateMinimalData();
        data.ServiceShortDescription = "web applications and APIs.";

        _processor.Validate(data, "Test");

        // After validation, the period should be gone
        var rendered = $"Learn how to manage {data.ServiceShortDescription} through AI-powered interactions.";
        Assert.DoesNotContain(". through", rendered);
        Assert.Equal("Learn how to manage web applications and APIs through AI-powered interactions.", rendered);
    }

    [Fact]
    public void Validate_PreventsBrokenIntro_WhenDescriptionHasTrailingPeriod()
    {
        // Simulates: "Manage web applications and APIs. using natural language"
        var data = CreateMinimalData();
        data.ServiceShortDescription = "web applications and APIs.";

        _processor.Validate(data, "Test");

        var rendered = $"Manage {data.ServiceShortDescription} using natural language conversations.";
        Assert.DoesNotContain(". using", rendered);
        Assert.Equal("Manage web applications and APIs using natural language conversations.", rendered);
    }

    // ===== Link URL Validation =====

    [Fact]
    public void Validate_StripsLearnPrefixFromServiceDocLink()
    {
        var data = CreateMinimalData();
        data.ServiceDocLink = "https://learn.microsoft.com/en-us/azure/storage/blobs/overview";

        var result = _processor.Validate(data, "Test");

        Assert.Equal("/azure/storage/blobs/overview", data.ServiceDocLink);
        Assert.Contains(result.Corrections, c => c.Contains("Stripped learn.microsoft.com prefix from serviceDocLink"));
    }

    [Fact]
    public void Validate_StripsLearnPrefixFromAdditionalLinks()
    {
        var data = CreateMinimalData();
        data.ServiceDocLink = "/azure/key-vault/general/overview";
        data.AdditionalLinks = new List<AdditionalLink>
        {
            new() { Title = "Best Practices", Url = "https://learn.microsoft.com/en-us/azure/key-vault/general/best-practices" }
        };

        var result = _processor.Validate(data, "Test");

        Assert.Equal("/azure/key-vault/general/best-practices", data.AdditionalLinks[0].Url);
        Assert.Contains(result.Corrections, c => c.Contains("Stripped learn.microsoft.com prefix"));
    }

    [Fact]
    public void Validate_RemovesCatchAllServiceDocLink()
    {
        var data = CreateMinimalData();
        data.ServiceDocLink = "https://learn.microsoft.com/en-us/azure/extension/";

        var result = _processor.Validate(data, "Azure Extension", "extension");

        Assert.Null(data.ServiceDocLink);
        Assert.Contains(result.Corrections, c => c.Contains("Removed invalid serviceDocLink for catch-all namespace 'extension'"));
    }

    [Fact]
    public void Validate_RemovesLinksWithFabricatedDocsPath()
    {
        var data = CreateMinimalData();
        data.ServiceDocLink = "/azure/cosmos-db/introduction";
        data.AdditionalLinks = new List<AdditionalLink>
        {
            new() { Title = "Azure Cosmos DB Documentation", Url = "/azure/cosmos-db/docs" },
            new() { Title = "Partitioning Guide", Url = "/azure/cosmos-db/partitioning-overview" }
        };

        var result = _processor.Validate(data, "Test");

        Assert.Equal(1, data.AdditionalLinks.Count);
        Assert.Equal("Partitioning Guide", data.AdditionalLinks[0].Title);
        Assert.Contains(result.Corrections, c => c.Contains("fabricated URL pattern"));
    }

    [Fact]
    public void Validate_KeepsNonFabricatedLinks()
    {
        var data = CreateMinimalData();
        data.ServiceDocLink = "/azure/storage/blobs/overview";
        data.AdditionalLinks = new List<AdditionalLink>
        {
            new() { Title = "Best Practices", Url = "/azure/storage/blobs/best-practices" },
            new() { Title = "Security Guide", Url = "/azure/storage/blobs/security-recommendations" }
        };

        _processor.Validate(data, "Test");

        Assert.Equal(2, data.AdditionalLinks.Count);
    }

    [Fact]
    public void Validate_NoErrorWhenAdditionalLinksEmpty()
    {
        var data = CreateMinimalData();
        data.ServiceDocLink = "/azure/ai-services/openai/overview";
        data.AdditionalLinks = new List<AdditionalLink>();

        var result = _processor.Validate(data, "Test");

        Assert.False(result.HasCriticalErrors);
    }

    // ===== Deduplicate Additional Links =====

    [Fact]
    public void Validate_RemovesDuplicateExactUrlMatch()
    {
        var data = CreateMinimalData();
        data.ServiceDocLink = "/azure/ai-services/speech-service/overview";
        data.AdditionalLinks = new List<AdditionalLink>
        {
            new() { Title = "Speech Service Overview", Url = "/azure/ai-services/speech-service/overview" },
            new() { Title = "Quickstart", Url = "/azure/ai-services/speech-service/get-started" }
        };

        var result = _processor.Validate(data, "Test");

        Assert.Equal(1, data.AdditionalLinks.Count);
        Assert.Equal("Quickstart", data.AdditionalLinks[0].Title);
        Assert.Contains(result.Corrections, c => c.Contains("Removed duplicate additional link"));
    }

    [Fact]
    public void Validate_RemovesDuplicateDocumentationTitle()
    {
        var data = CreateMinimalData();
        data.ServiceDocLink = "/azure/key-vault/general/overview";
        data.AdditionalLinks = new List<AdditionalLink>
        {
            new() { Title = "Azure Key Vault Documentation", Url = "/azure/key-vault/general/basic-concepts" },
            new() { Title = "Best Practices", Url = "/azure/key-vault/general/best-practices" }
        };

        var result = _processor.Validate(data, "Test");

        Assert.Equal(1, data.AdditionalLinks.Count);
        Assert.Equal("Best Practices", data.AdditionalLinks[0].Title);
        Assert.Contains(result.Corrections, c => c.Contains("Removed duplicate additional link"));
    }

    [Fact]
    public void Validate_KeepsLinksFromDifferentServiceArea()
    {
        var data = CreateMinimalData();
        data.ServiceDocLink = "/azure/storage/blobs/overview";
        data.AdditionalLinks = new List<AdditionalLink>
        {
            new() { Title = "Azure Monitor Documentation", Url = "/azure/azure-monitor/overview" }
        };

        _processor.Validate(data, "Test");

        Assert.Equal(1, data.AdditionalLinks.Count);
    }

    [Fact]
    public void Validate_KeepsNonDocumentationLinksInSameServiceArea()
    {
        var data = CreateMinimalData();
        data.ServiceDocLink = "/azure/cosmos-db/introduction";
        data.AdditionalLinks = new List<AdditionalLink>
        {
            new() { Title = "Partitioning Best Practices", Url = "/azure/cosmos-db/partitioning-overview" },
            new() { Title = "Request Units", Url = "/azure/cosmos-db/request-units" }
        };

        _processor.Validate(data, "Test");

        Assert.Equal(2, data.AdditionalLinks.Count);
    }

    [Fact]
    public void Validate_RemovesFabricatedAndDuplicateLinksEndToEnd()
    {
        // Verifies fabricated /docs path removal and near-duplicate detection together
        var data = CreateMinimalData();
        data.ServiceDocLink = "/azure/cognitive-services/speech-service/overview";
        data.AdditionalLinks = new List<AdditionalLink>
        {
            new() { Title = "Speech Service Documentation", Url = "/azure/cognitive-services/speech-service/docs" },
            new() { Title = "Voice Gallery", Url = "/azure/cognitive-services/speech-service/language-support" }
        };

        var result = _processor.Validate(data, "Test");

        // The fabricated /docs link should be removed
        Assert.Equal(1, data.AdditionalLinks.Count);
        Assert.Equal("Voice Gallery", data.AdditionalLinks[0].Title);
        Assert.Contains(result.Corrections, c => c.Contains("fabricated URL pattern") || c.Contains("Removed duplicate"));
    }

    // ===== Capability-to-Tool Ratio Validation =====

    [Fact]
    public void Validate_WarnsWhenCapabilitiesExceedToolCount()
    {
        var data = CreateMinimalData();
        data.Tools = new List<ToolWithAIDescription>
        {
            new() { Command = "keyvault secret set", ShortDescription = "Set a secret value in an Azure Key Vault." }
        };
        data.Capabilities = new List<string>
        {
            "Store secrets in a key vault",
            "Configure secret attributes"
        };

        var result = _processor.Validate(data, "Test");

        Assert.Contains(result.Warnings, w => w.Contains("Capabilities (2) exceed tool count (1)"));
    }

    [Fact]
    public void Validate_NoWarningWhenCapabilitiesMatchToolCount()
    {
        var data = CreateMinimalData();
        data.Tools = new List<ToolWithAIDescription>
        {
            new() { Command = "storage account create", ShortDescription = "Create a storage account in the specified region." },
            new() { Command = "storage account list", ShortDescription = "List all storage accounts in the subscription." },
            new() { Command = "storage account delete", ShortDescription = "Delete a storage account by name." }
        };
        data.Capabilities = new List<string>
        {
            "Create and configure storage accounts",
            "List existing storage accounts",
            "Delete storage accounts"
        };

        var result = _processor.Validate(data, "Test");

        Assert.DoesNotContain(result.Warnings, w => w.Contains("exceed tool count"));
    }

    [Fact]
    public void Validate_NoWarningWhenSingleToolHasSingleCapability()
    {
        var data = CreateMinimalData();
        data.Tools = new List<ToolWithAIDescription>
        {
            new() { Command = "keyvault secret set", ShortDescription = "Set a secret value in an Azure Key Vault." }
        };
        data.Capabilities = new List<string>
        {
            "Store and update secrets in a key vault"
        };

        var result = _processor.Validate(data, "Test");

        Assert.DoesNotContain(result.Warnings, w => w.Contains("exceed tool count"));
    }

    // ===== Empty URL Link Removal =====

    [Fact]
    public void Validate_RemovesLinksWithEmptyUrl()
    {
        var data = CreateMinimalData();
        data.ServiceDocLink = "/azure/monitor/overview";
        data.AdditionalLinks = new List<AdditionalLink>
        {
            new() { Title = "Azure Monitor Best Practices", Url = "" },
            new() { Title = "Metrics Overview", Url = "/azure/azure-monitor/essentials/data-platform-metrics" }
        };

        var result = _processor.Validate(data, "Test");

        Assert.Equal(1, data.AdditionalLinks.Count);
        Assert.Equal("Metrics Overview", data.AdditionalLinks[0].Title);
        Assert.Contains(result.Corrections, c => c.Contains("Removed link with empty URL"));
    }

    [Fact]
    public void Validate_RemovesLinksWithWhitespaceOnlyUrl()
    {
        var data = CreateMinimalData();
        data.ServiceDocLink = "/azure/aks/intro";
        data.AdditionalLinks = new List<AdditionalLink>
        {
            new() { Title = "AKS Best Practices", Url = "   " },
            new() { Title = "Cluster Security", Url = "/azure/aks/concepts-security" }
        };

        var result = _processor.Validate(data, "Test");

        Assert.Equal(1, data.AdditionalLinks.Count);
        Assert.Equal("Cluster Security", data.AdditionalLinks[0].Title);
        Assert.Contains(result.Corrections, c => c.Contains("Removed link with empty URL"));
    }

    // ===== Helper =====

    private static AIGeneratedArticleData CreateMinimalData()
    {
        return new AIGeneratedArticleData
        {
            ServiceShortDescription = "test resources",
            ServiceOverview = "is a test service.",
            Capabilities = new List<string> { "Manage test resources" },
            BestPractices = new List<BestPractice>
            {
                new() { Title = "Security", Description = "D." },
                new() { Title = "Reliability", Description = "D." },
                new() { Title = "Cost", Description = "D." },
                new() { Title = "Performance", Description = "D." }
            }
        };
    }
}
