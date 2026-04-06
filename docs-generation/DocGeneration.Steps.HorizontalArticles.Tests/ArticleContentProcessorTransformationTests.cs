// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;
using Azure.Mcp.TextTransformation.Models;
using Azure.Mcp.TextTransformation.Services;
using HorizontalArticleGenerator.Generators;
using HorizontalArticleGenerator.Models;

namespace HorizontalArticleGenerator.Tests;

public class ArticleContentProcessorTransformationTests
{
    private readonly TransformationEngine _engine;
    private readonly ArticleContentProcessor _processor;

    public ArticleContentProcessorTransformationTests()
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

    [Fact]
    public void ApplyTransformations_ServiceShortDescription_DoesNotGetTrailingPeriod()
    {
        var data = CreateMinimalData();
        data.ServiceShortDescription = "web applications and database connections";

        _processor.ApplyTransformations(data);

        Assert.False(data.ServiceShortDescription.EndsWith("."));
        Assert.Equal("web applications and database connections", data.ServiceShortDescription);
    }

    [Fact]
    public void ApplyTransformations_ServiceOverview_GetsTrailingPeriod()
    {
        var data = CreateMinimalData();
        data.ServiceOverview = "is a fully managed platform for building web apps";

        _processor.ApplyTransformations(data);

        Assert.EndsWith(".", data.ServiceOverview);
    }

    [Fact]
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
            Assert.False(cap.EndsWith("."), $"Capability should not end with period: '{cap}'");
        }
    }

    [Fact]
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
            Assert.False(bp.Title.EndsWith("."), $"Best practice title should not end with period: '{bp.Title}'");
            Assert.True(bp.Description.EndsWith("."), $"Best practice description should end with period: '{bp.Description}'");
        }
    }

    [Fact]
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

        Assert.False(data.Scenarios[0].Title.EndsWith("."));
        Assert.EndsWith(".", data.Scenarios[0].Description);
        Assert.EndsWith(".", data.Scenarios[0].ExpectedOutcome);
    }

    [Fact]
    public void ApplyTransformations_PrerequisiteDescriptions_GetTrailingPeriods()
    {
        var data = CreateMinimalData();
        data.ServiceSpecificPrerequisites = new List<Prerequisite>
        {
            new() { Title = "Existing Storage Account", Description = "You need a Storage Account provisioned" }
        };

        _processor.ApplyTransformations(data);

        Assert.EndsWith(".", data.ServiceSpecificPrerequisites[0].Description);
    }

    [Fact]
    public void ApplyTransformations_RolePurposes_GetTrailingPeriods()
    {
        var data = CreateMinimalData();
        data.RequiredRoles = new List<RequiredRole>
        {
            new() { Name = "Storage Account Contributor", Purpose = "Manage Storage Account resources" }
        };

        _processor.ApplyTransformations(data);

        Assert.EndsWith(".", data.RequiredRoles[0].Purpose);
    }

    // ===== Static text replacement integration =====

    [Fact]
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

        Assert.Equal("Use Microsoft Entra ID", data.BestPractices[0].Title);
        Assert.Contains("Microsoft Entra ID", data.BestPractices[0].Description);
        Assert.DoesNotContain("Azure Active Directory", data.BestPractices[0].Description);
    }

    [Fact]
    public void ApplyTransformations_ReplacesAzureActiveDirectory_InServiceOverview()
    {
        var data = CreateMinimalData();
        data.ServiceOverview = "is a service that integrates with Azure Active Directory for authentication";

        _processor.ApplyTransformations(data);

        Assert.Contains("Microsoft Entra ID", data.ServiceOverview);
        Assert.DoesNotContain("Azure Active Directory", data.ServiceOverview);
    }

    // ===== Full pipeline: Validate + Transform =====

    [Fact]
    public void Process_FullPipeline_ServiceShortDescription_NeverEndsWithPeriod()
    {
        // This is THE critical test — simulates the exact bug:
        // AI returns "web applications and APIs." → Validate strips it → Transform must NOT re-add it
        var data = CreateMinimalData();
        data.ServiceShortDescription = "web applications and APIs.";

        _processor.Process(data, "TestService");

        Assert.False(data.ServiceShortDescription.EndsWith("."),
            "After full pipeline, serviceShortDescription must NEVER end with a period");
        Assert.Equal("web applications and APIs", data.ServiceShortDescription);
    }

    [Fact]
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
            Assert.False(cap.EndsWith("."),
                $"After full pipeline, capability must NOT end with period: '{cap}'");
        }
    }

    [Fact]
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
            Assert.False(bp.Title.EndsWith("."),
                $"After full pipeline, best practice title must NOT end with period: '{bp.Title}'");
        }
    }

    [Fact]
    public void Process_FullPipeline_RenderedFrontmatter_HasNoBreak()
    {
        // End-to-end: simulate the template interpolation that was producing broken text
        var data = CreateMinimalData();
        data.ServiceShortDescription = "database connections for web applications.";

        _processor.Process(data, "TestService");

        // Simulate template: description: Learn how to ... manage {{genai-serviceShortDescription}} through AI-powered ...
        var frontmatter = $"description: Learn how to use the Azure MCP Server to manage {data.ServiceShortDescription} through AI-powered natural language interactions.";
        Assert.DoesNotContain(". through", frontmatter);

        // Simulate template: Manage {{genai-serviceShortDescription}} using natural language ...
        var intro = $"Manage {data.ServiceShortDescription} using natural language conversations.";
        Assert.DoesNotContain(". using", intro);

        // Simulate template: I want to manage {{genai-serviceShortDescription}} using natural language ...
        var customerIntent = $"I want to manage {data.ServiceShortDescription} using natural language conversations.";
        Assert.DoesNotContain(". using", customerIntent);
    }

    [Fact]
    public void Process_FullPipeline_OverviewStillGetsPeriod()
    {
        var data = CreateMinimalData();
        data.ServiceOverview = "is a fully managed platform for building web apps";

        _processor.Process(data, "TestService");

        Assert.EndsWith(".", data.ServiceOverview);
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
