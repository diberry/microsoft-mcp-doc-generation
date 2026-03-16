// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using NUnit.Framework;
using HorizontalArticleGenerator.Generators;
using HorizontalArticleGenerator.Models;

namespace HorizontalArticleGenerator.Tests;

[TestFixture]
public class ArticleContentProcessorValidationTests
{
    private ArticleContentProcessor _processor = null!;

    [SetUp]
    public void Setup()
    {
        // No transformation engine — tests pure validation logic
        _processor = new ArticleContentProcessor();
    }

    // ===== Trailing Period Stripping =====

    [Test]
    public void Validate_StripsTrailingPeriod_FromServiceShortDescription()
    {
        var data = CreateMinimalData();
        data.ServiceShortDescription = "web applications and APIs.";

        var result = _processor.Validate(data, "Test");

        Assert.That(data.ServiceShortDescription, Is.EqualTo("web applications and APIs"));
        Assert.That(result.Corrections, Has.Some.Contains("serviceShortDescription"));
    }

    [Test]
    public void Validate_StripsTrailingPeriodAndSpace_FromServiceShortDescription()
    {
        var data = CreateMinimalData();
        data.ServiceShortDescription = "web applications and APIs. ";

        _processor.Validate(data, "Test");

        Assert.That(data.ServiceShortDescription, Is.EqualTo("web applications and APIs"));
    }

    [Test]
    public void Validate_NoChange_WhenServiceShortDescriptionHasNoPeriod()
    {
        var data = CreateMinimalData();
        data.ServiceShortDescription = "keys, secrets, and certificates";

        var result = _processor.Validate(data, "Test");

        Assert.That(data.ServiceShortDescription, Is.EqualTo("keys, secrets, and certificates"));
        Assert.That(result.Corrections, Has.None.Contains("serviceShortDescription"));
    }

    [Test]
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

        Assert.That(data.Capabilities[0], Is.EqualTo("Create and manage virtual machines"));
        Assert.That(data.Capabilities[1], Is.EqualTo("Configure network security groups"));
        Assert.That(data.Capabilities[2], Is.EqualTo("Monitor resource utilization metrics"));
    }

    [Test]
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

        Assert.That(data.BestPractices[0].Title, Is.EqualTo("Use managed identities"));
        Assert.That(data.BestPractices[1].Title, Is.EqualTo("Monitor performance"));
        // Descriptions should NOT be stripped (they are full sentences)
        Assert.That(data.BestPractices[0].Description, Is.EqualTo("Desc."));
    }

    [Test]
    public void Validate_StripsTrailingPeriods_FromPrerequisiteTitles()
    {
        var data = CreateMinimalData();
        data.ServiceSpecificPrerequisites = new List<Prerequisite>
        {
            new() { Title = "Existing Storage Account.", Description = "Required." }
        };

        _processor.Validate(data, "Test");

        Assert.That(data.ServiceSpecificPrerequisites[0].Title, Is.EqualTo("Existing Storage Account"));
        Assert.That(data.ServiceSpecificPrerequisites[0].Description, Is.EqualTo("Required."));
    }

    [Test]
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

        Assert.That(data.Scenarios[0].Title, Is.EqualTo("Add a database connection"));
    }

    // ===== Broken Sentence Fix =====

    [Test]
    public void Validate_FixesBrokenSentence_InServiceShortDescription()
    {
        var data = CreateMinimalData();
        data.ServiceShortDescription = "web applications. and APIs";

        _processor.Validate(data, "Test");

        Assert.That(data.ServiceShortDescription, Is.EqualTo("web applications and APIs"));
    }

    [Test]
    public void Validate_FixesBrokenSentence_InServiceOverview()
    {
        var data = CreateMinimalData();
        data.ServiceOverview = "is a platform. for building apps";

        _processor.Validate(data, "Test");

        Assert.That(data.ServiceOverview, Is.EqualTo("is a platform for building apps"));
    }

    [Test]
    public void Validate_PreservesLegitimateAbbreviations_InServiceOverview()
    {
        var data = CreateMinimalData();
        data.ServiceOverview = "is a platform. It provides scaling.";

        _processor.Validate(data, "Test");

        // "period + uppercase I" should NOT be modified
        Assert.That(data.ServiceOverview, Is.EqualTo("is a platform. It provides scaling."));
    }

    // ===== Redundant Words =====

    [Test]
    public void Validate_RemovesRedundantWord_AtStartOfOverview()
    {
        var data = CreateMinimalData();
        data.ServiceOverview = "Search Search enables full-text searching.";

        _processor.Validate(data, "Test");

        Assert.That(data.ServiceOverview, Is.EqualTo("Search enables full-text searching."));
    }

    [Test]
    public void Validate_NoChange_WhenNoRedundantWord()
    {
        var data = CreateMinimalData();
        data.ServiceOverview = "is a cloud search service.";

        _processor.Validate(data, "Test");

        Assert.That(data.ServiceOverview, Is.EqualTo("is a cloud search service."));
    }

    // ===== RBAC Role Validation =====

    [Test]
    public void Validate_BlocksInventedRbacRoles()
    {
        var data = CreateMinimalData();
        data.RequiredRoles = new List<RequiredRole>
        {
            new() { Name = "Search Knowledge Base Data Analyst", Purpose = "Read KB" }
        };

        var result = _processor.Validate(data, "Test");

        Assert.That(result.HasCriticalErrors, Is.True);
        Assert.That(result.CriticalErrors, Has.Some.Contains("INVENTED RBAC ROLE"));
    }

    [Test]
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

        Assert.That(result.HasCriticalErrors, Is.False);
    }

    [Test]
    public void Validate_AllowsContributorReaderRoles()
    {
        var data = CreateMinimalData();
        data.RequiredRoles = new List<RequiredRole>
        {
            new() { Name = "Cognitive Services Contributor", Purpose = "Manage AI services" },
            new() { Name = "Cosmos DB Account Reader Role", Purpose = "Read" }
        };

        var result = _processor.Validate(data, "Test");

        Assert.That(result.HasCriticalErrors, Is.False);
    }

    [Test]
    public void Validate_BlocksAdministratorSuffix()
    {
        // "Administrator" is never used in Azure built-in RBAC roles
        var data = CreateMinimalData();
        data.RequiredRoles = new List<RequiredRole>
        {
            new() { Name = "Cosmos DB Administrator", Purpose = "Admin access" }
        };

        var result = _processor.Validate(data, "Test");

        Assert.That(result.HasCriticalErrors, Is.True);
        Assert.That(result.CriticalErrors, Has.Some.Contains("Administrator"));
    }

    [Test]
    public void Validate_BlocksGenericPrefixRoles()
    {
        // "Database Contributor" is too generic — real role would be "SQL DB Contributor"
        var data = CreateMinimalData();
        data.RequiredRoles = new List<RequiredRole>
        {
            new() { Name = "Database Contributor", Purpose = "Manage databases" }
        };

        var result = _processor.Validate(data, "Test");

        Assert.That(result.HasCriticalErrors, Is.True);
        Assert.That(result.CriticalErrors, Has.Some.Contains("too generic"));
    }

    [TestCase("Database Reader")]
    [TestCase("Application Contributor")]
    [TestCase("Resource Contributor")]
    public void Validate_BlocksVariousGenericPrefixRoles(string roleName)
    {
        var data = CreateMinimalData();
        data.RequiredRoles = new List<RequiredRole>
        {
            new() { Name = roleName, Purpose = "Some purpose" }
        };

        var result = _processor.Validate(data, "Test");

        Assert.That(result.HasCriticalErrors, Is.True);
    }

    [TestCase("SQL DB Contributor")]
    [TestCase("SQL Server Contributor")]
    [TestCase("Website Contributor")]
    [TestCase("Web Plan Contributor")]
    [TestCase("Storage Account Contributor")]
    [TestCase("Key Vault Contributor")]
    public void Validate_AllowsOfficialServiceSpecificRoles(string roleName)
    {
        var data = CreateMinimalData();
        data.RequiredRoles = new List<RequiredRole>
        {
            new() { Name = roleName, Purpose = "Some purpose" }
        };

        var result = _processor.Validate(data, "Test");

        Assert.That(result.HasCriticalErrors, Is.False);
    }

    // ===== Tool Description Quality =====

    [Test]
    public void Validate_WarnsOnShortToolDescription()
    {
        var data = CreateMinimalData();
        data.Tools = new List<ToolWithAIDescription>
        {
            new() { Command = "storage list", ShortDescription = "List items" }
        };

        var result = _processor.Validate(data, "Test");

        Assert.That(result.Warnings, Has.Some.Contains("too short"));
    }

    [Test]
    public void Validate_WarnsOnGenericToolDescription()
    {
        var data = CreateMinimalData();
        data.Tools = new List<ToolWithAIDescription>
        {
            new() { Command = "storage list", ShortDescription = "Get details about storage resources and configurations" }
        };

        var result = _processor.Validate(data, "Test");

        Assert.That(result.Warnings, Has.Some.Contains("generic description"));
    }

    [Test]
    public void Validate_NoWarning_WhenToolDescriptionIsGoodQuality()
    {
        var data = CreateMinimalData();
        data.Tools = new List<ToolWithAIDescription>
        {
            new() { Command = "search index get", ShortDescription = "Retrieve index schema, field definitions, and scoring profiles" }
        };

        var result = _processor.Validate(data, "Test");

        Assert.That(result.Warnings, Has.None.Contains("too short"));
        Assert.That(result.Warnings, Has.None.Contains("generic description"));
    }

    // ===== Best Practice Count =====

    [Test]
    public void Validate_WarnsWhenFewerThanThreeBestPractices()
    {
        var data = CreateMinimalData();
        data.BestPractices = new List<BestPractice>
        {
            new() { Title = "Use Entra ID", Description = "Secure." },
            new() { Title = "Monitor", Description = "Watch." }
        };

        var result = _processor.Validate(data, "Test");

        Assert.That(result.Warnings, Has.Some.Contains("best practices"));
    }

    [Test]
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

        Assert.That(result.Warnings, Has.None.Contains("best practices"));
    }

    // ===== Combined: trailing period on serviceShortDescription mid-sentence =====

    [Test]
    public void Validate_PreventsBrokenFrontmatter_WhenDescriptionHasTrailingPeriod()
    {
        // Simulates the exact bug: "manage web applications and APIs. through AI-powered"
        var data = CreateMinimalData();
        data.ServiceShortDescription = "web applications and APIs.";

        _processor.Validate(data, "Test");

        // After validation, the period should be gone
        var rendered = $"Learn how to manage {data.ServiceShortDescription} through AI-powered interactions.";
        Assert.That(rendered, Does.Not.Contain(". through"));
        Assert.That(rendered, Is.EqualTo("Learn how to manage web applications and APIs through AI-powered interactions."));
    }

    [Test]
    public void Validate_PreventsBrokenIntro_WhenDescriptionHasTrailingPeriod()
    {
        // Simulates: "Manage web applications and APIs. using natural language"
        var data = CreateMinimalData();
        data.ServiceShortDescription = "web applications and APIs.";

        _processor.Validate(data, "Test");

        var rendered = $"Manage {data.ServiceShortDescription} using natural language conversations.";
        Assert.That(rendered, Does.Not.Contain(". using"));
        Assert.That(rendered, Is.EqualTo("Manage web applications and APIs using natural language conversations."));
    }

    // ===== Link URL Validation =====

    [Test]
    public void Validate_StripsLearnPrefixFromServiceDocLink()
    {
        var data = CreateMinimalData();
        data.ServiceDocLink = "https://learn.microsoft.com/en-us/azure/storage/blobs/overview";

        var result = _processor.Validate(data, "Test");

        Assert.That(data.ServiceDocLink, Is.EqualTo("/azure/storage/blobs/overview"));
        Assert.That(result.Corrections, Has.Some.Contains("Stripped learn.microsoft.com prefix from serviceDocLink"));
    }

    [Test]
    public void Validate_StripsLearnPrefixFromAdditionalLinks()
    {
        var data = CreateMinimalData();
        data.ServiceDocLink = "/azure/key-vault/general/overview";
        data.AdditionalLinks = new List<AdditionalLink>
        {
            new() { Title = "Best Practices", Url = "https://learn.microsoft.com/en-us/azure/key-vault/general/best-practices" }
        };

        var result = _processor.Validate(data, "Test");

        Assert.That(data.AdditionalLinks[0].Url, Is.EqualTo("/azure/key-vault/general/best-practices"));
        Assert.That(result.Corrections, Has.Some.Contains("Stripped learn.microsoft.com prefix"));
    }

    [Test]
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

        Assert.That(data.AdditionalLinks, Has.Count.EqualTo(1));
        Assert.That(data.AdditionalLinks[0].Title, Is.EqualTo("Partitioning Guide"));
        Assert.That(result.Corrections, Has.Some.Contains("fabricated URL pattern"));
    }

    [Test]
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

        Assert.That(data.AdditionalLinks, Has.Count.EqualTo(2));
    }

    [Test]
    public void Validate_NoErrorWhenAdditionalLinksEmpty()
    {
        var data = CreateMinimalData();
        data.ServiceDocLink = "/azure/ai-services/openai/overview";
        data.AdditionalLinks = new List<AdditionalLink>();

        var result = _processor.Validate(data, "Test");

        Assert.That(result.HasCriticalErrors, Is.False);
    }

    // ===== Deduplicate Additional Links =====

    [Test]
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

        Assert.That(data.AdditionalLinks, Has.Count.EqualTo(1));
        Assert.That(data.AdditionalLinks[0].Title, Is.EqualTo("Quickstart"));
        Assert.That(result.Corrections, Has.Some.Contains("Removed duplicate additional link"));
    }

    [Test]
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

        Assert.That(data.AdditionalLinks, Has.Count.EqualTo(1));
        Assert.That(data.AdditionalLinks[0].Title, Is.EqualTo("Best Practices"));
        Assert.That(result.Corrections, Has.Some.Contains("Removed duplicate additional link"));
    }

    [Test]
    public void Validate_KeepsLinksFromDifferentServiceArea()
    {
        var data = CreateMinimalData();
        data.ServiceDocLink = "/azure/storage/blobs/overview";
        data.AdditionalLinks = new List<AdditionalLink>
        {
            new() { Title = "Azure Monitor Documentation", Url = "/azure/azure-monitor/overview" }
        };

        _processor.Validate(data, "Test");

        Assert.That(data.AdditionalLinks, Has.Count.EqualTo(1));
    }

    [Test]
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

        Assert.That(data.AdditionalLinks, Has.Count.EqualTo(2));
    }

    [Test]
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
        Assert.That(data.AdditionalLinks, Has.Count.EqualTo(1));
        Assert.That(data.AdditionalLinks[0].Title, Is.EqualTo("Voice Gallery"));
        Assert.That(result.Corrections, Has.Some.Contains("fabricated URL pattern").Or.Contains("Removed duplicate"));
    }

    // ===== Capability-to-Tool Ratio Validation =====

    [Test]
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

        Assert.That(result.Warnings, Has.Some.Contains("Capabilities (2) exceed tool count (1)"));
    }

    [Test]
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

        Assert.That(result.Warnings, Has.None.Contains("exceed tool count"));
    }

    [Test]
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

        Assert.That(result.Warnings, Has.None.Contains("exceed tool count"));
    }

    // ===== Empty URL Link Removal =====

    [Test]
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

        Assert.That(data.AdditionalLinks, Has.Count.EqualTo(1));
        Assert.That(data.AdditionalLinks[0].Title, Is.EqualTo("Metrics Overview"));
        Assert.That(result.Corrections, Has.Some.Contains("Removed link with empty URL"));
    }

    [Test]
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

        Assert.That(data.AdditionalLinks, Has.Count.EqualTo(1));
        Assert.That(data.AdditionalLinks[0].Title, Is.EqualTo("Cluster Security"));
        Assert.That(result.Corrections, Has.Some.Contains("Removed link with empty URL"));
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
