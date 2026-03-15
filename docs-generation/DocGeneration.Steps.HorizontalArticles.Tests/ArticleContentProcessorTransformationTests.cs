// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using NUnit.Framework;
using Azure.Mcp.TextTransformation.Models;
using Azure.Mcp.TextTransformation.Services;
using HorizontalArticleGenerator.Generators;
using HorizontalArticleGenerator.Models;

namespace HorizontalArticleGenerator.Tests;

[TestFixture]
public class ArticleContentProcessorTransformationTests
{
    private TransformationEngine _engine = null!;
    private ArticleContentProcessor _processor = null!;

    [SetUp]
    public void Setup()
    {
        var config = new TransformationConfig
        {
            Lexicon = new Lexicon
            {
                Acronyms = new Dictionary<string, AcronymEntry>(),
                Abbreviations = new Dictionary<string, AbbreviationEntry>
                {
                    // Simulate "Azure Active Directory" → "Microsoft Entra ID" replacement
                    { "Azure Active Directory", new AbbreviationEntry { Canonical = "Microsoft Entra ID" } }
                },
                StopWords = new List<string>()
            },
            Services = new ServiceConfig
            {
                Mappings = new List<ServiceMapping>()
            }
        };
        _engine = new TransformationEngine(config);
        _processor = new ArticleContentProcessor(_engine);
    }

    // ===== Core bug: TransformDescription adds periods, TransformText does not =====

    [Test]
    public void ApplyTransformations_ServiceShortDescription_DoesNotGetTrailingPeriod()
    {
        var data = CreateMinimalData();
        data.ServiceShortDescription = "web applications and database connections";

        _processor.ApplyTransformations(data);

        Assert.That(data.ServiceShortDescription, Does.Not.EndWith("."));
        Assert.That(data.ServiceShortDescription, Is.EqualTo("web applications and database connections"));
    }

    [Test]
    public void ApplyTransformations_ServiceOverview_GetsTrailingPeriod()
    {
        var data = CreateMinimalData();
        data.ServiceOverview = "is a fully managed platform for building web apps";

        _processor.ApplyTransformations(data);

        Assert.That(data.ServiceOverview, Does.EndWith("."));
    }

    [Test]
    public void ApplyTransformations_Capabilities_DoNotGetTrailingPeriods()
    {
        var data = CreateMinimalData();
        data.Capabilities = new List<string>
        {
            "Create and manage virtual machines",
            "Configure network security groups",
            "Monitor resource utilization metrics"
        };

        _processor.ApplyTransformations(data);

        foreach (var cap in data.Capabilities)
        {
            Assert.That(cap, Does.Not.EndWith("."), $"Capability should not end with period: '{cap}'");
        }
    }

    [Test]
    public void ApplyTransformations_BestPracticeTitles_DoNotGetTrailingPeriods()
    {
        var data = CreateMinimalData();
        data.BestPractices = new List<BestPractice>
        {
            new() { Title = "Use managed identities", Description = "Leverage Entra ID for secure auth" },
            new() { Title = "Monitor performance", Description = "Check metrics regularly" },
            new() { Title = "Optimize costs", Description = "Right-size resources" },
            new() { Title = "Enable logging", Description = "Capture diagnostic data" }
        };

        _processor.ApplyTransformations(data);

        foreach (var bp in data.BestPractices)
        {
            Assert.That(bp.Title, Does.Not.EndWith("."), $"Best practice title should not end with period: '{bp.Title}'");
            Assert.That(bp.Description, Does.EndWith("."), $"Best practice description should end with period: '{bp.Description}'");
        }
    }

    [Test]
    public void ApplyTransformations_ScenarioTitles_DoNotGetTrailingPeriods()
    {
        var data = CreateMinimalData();
        data.Scenarios = new List<Scenario>
        {
            new()
            {
                Title = "Add a database connection",
                Description = "Connect your app to a database",
                Examples = new List<string> { "Add db for myApp" },
                ExpectedOutcome = "Database is connected"
            }
        };

        _processor.ApplyTransformations(data);

        Assert.That(data.Scenarios[0].Title, Does.Not.EndWith("."));
        Assert.That(data.Scenarios[0].Description, Does.EndWith("."));
        Assert.That(data.Scenarios[0].ExpectedOutcome, Does.EndWith("."));
    }

    [Test]
    public void ApplyTransformations_PrerequisiteDescriptions_GetTrailingPeriods()
    {
        var data = CreateMinimalData();
        data.ServiceSpecificPrerequisites = new List<Prerequisite>
        {
            new() { Title = "Existing Storage Account", Description = "You need a Storage Account provisioned" }
        };

        _processor.ApplyTransformations(data);

        Assert.That(data.ServiceSpecificPrerequisites[0].Description, Does.EndWith("."));
    }

    [Test]
    public void ApplyTransformations_RolePurposes_GetTrailingPeriods()
    {
        var data = CreateMinimalData();
        data.RequiredRoles = new List<RequiredRole>
        {
            new() { Name = "Storage Account Contributor", Purpose = "Manage Storage Account resources" }
        };

        _processor.ApplyTransformations(data);

        Assert.That(data.RequiredRoles[0].Purpose, Does.EndWith("."));
    }

    // ===== Static text replacement integration =====

    [Test]
    public void ApplyTransformations_ReplacesAzureActiveDirectory_WithEntraID()
    {
        var data = CreateMinimalData();
        data.BestPractices = new List<BestPractice>
        {
            new() { Title = "Use Azure Active Directory", Description = "Rely on Azure Active Directory for auth" },
            new() { Title = "Security", Description = "D." },
            new() { Title = "Cost", Description = "D." },
            new() { Title = "Perf", Description = "D." }
        };

        _processor.ApplyTransformations(data);

        Assert.That(data.BestPractices[0].Title, Is.EqualTo("Use Microsoft Entra ID"));
        Assert.That(data.BestPractices[0].Description, Does.Contain("Microsoft Entra ID"));
        Assert.That(data.BestPractices[0].Description, Does.Not.Contain("Azure Active Directory"));
    }

    [Test]
    public void ApplyTransformations_ReplacesAzureActiveDirectory_InServiceOverview()
    {
        var data = CreateMinimalData();
        data.ServiceOverview = "is a service that integrates with Azure Active Directory for authentication";

        _processor.ApplyTransformations(data);

        Assert.That(data.ServiceOverview, Does.Contain("Microsoft Entra ID"));
        Assert.That(data.ServiceOverview, Does.Not.Contain("Azure Active Directory"));
    }

    // ===== Full pipeline: Validate + Transform =====

    [Test]
    public void Process_FullPipeline_ServiceShortDescription_NeverEndsWithPeriod()
    {
        // This is THE critical test — simulates the exact bug:
        // AI returns "web applications and APIs." → Validate strips it → Transform must NOT re-add it
        var data = CreateMinimalData();
        data.ServiceShortDescription = "web applications and APIs.";

        _processor.Process(data, "TestService");

        Assert.That(data.ServiceShortDescription, Does.Not.EndWith("."),
            "After full pipeline, serviceShortDescription must NEVER end with a period");
        Assert.That(data.ServiceShortDescription, Is.EqualTo("web applications and APIs"));
    }

    [Test]
    public void Process_FullPipeline_Capabilities_NeverEndWithPeriods()
    {
        var data = CreateMinimalData();
        data.Capabilities = new List<string>
        {
            "Deploy and configure web applications.",
            "Create container instances in resource groups.",
            "Monitor app performance and health."
        };

        _processor.Process(data, "TestService");

        foreach (var cap in data.Capabilities)
        {
            Assert.That(cap, Does.Not.EndWith("."),
                $"After full pipeline, capability must NOT end with period: '{cap}'");
        }
    }

    [Test]
    public void Process_FullPipeline_BestPracticeTitles_NeverEndWithPeriods()
    {
        var data = CreateMinimalData();
        data.BestPractices = new List<BestPractice>
        {
            new() { Title = "Use Azure Active Directory for auth.", Description = "Rely on Entra ID." },
            new() { Title = "Monitor performance.", Description = "Check metrics." },
            new() { Title = "Optimize costs.", Description = "Right-size." },
            new() { Title = "Enable logging.", Description = "Diagnostic data." }
        };

        _processor.Process(data, "TestService");

        foreach (var bp in data.BestPractices)
        {
            Assert.That(bp.Title, Does.Not.EndWith("."),
                $"After full pipeline, best practice title must NOT end with period: '{bp.Title}'");
        }
    }

    [Test]
    public void Process_FullPipeline_RenderedFrontmatter_HasNoBreak()
    {
        // End-to-end: simulate the template interpolation that was producing broken text
        var data = CreateMinimalData();
        data.ServiceShortDescription = "database connections for web applications.";

        _processor.Process(data, "TestService");

        // Simulate template: description: Learn how to ... manage {{genai-serviceShortDescription}} through AI-powered ...
        var frontmatter = $"description: Learn how to use the Azure MCP Server to manage {data.ServiceShortDescription} through AI-powered natural language interactions.";
        Assert.That(frontmatter, Does.Not.Contain(". through"));

        // Simulate template: Manage {{genai-serviceShortDescription}} using natural language ...
        var intro = $"Manage {data.ServiceShortDescription} using natural language conversations.";
        Assert.That(intro, Does.Not.Contain(". using"));

        // Simulate template: I want to manage {{genai-serviceShortDescription}} using natural language ...
        var customerIntent = $"I want to manage {data.ServiceShortDescription} using natural language conversations.";
        Assert.That(customerIntent, Does.Not.Contain(". using"));
    }

    [Test]
    public void Process_FullPipeline_OverviewStillGetsPeriod()
    {
        var data = CreateMinimalData();
        data.ServiceOverview = "is a fully managed platform for building web apps";

        _processor.Process(data, "TestService");

        Assert.That(data.ServiceOverview, Does.EndWith("."),
            "ServiceOverview is a full sentence and should end with a period");
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
