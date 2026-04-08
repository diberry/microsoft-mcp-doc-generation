using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SkillsGen.Core.Generation;
using SkillsGen.Core.Models;
using Xunit;

namespace SkillsGen.Core.Tests.Unit.Generation;

public class NaturalizeItemsTests
{
    [Fact]
    public void NaturalizeItems_EmptyList_ReturnsEmpty()
    {
        var result = SkillPageGenerator.NaturalizeItems([], "Azure Storage");
        result.Should().BeEmpty();
    }

    [Fact]
    public void NaturalizeItems_VerbLedItems_KeptOrExtended()
    {
        var result = SkillPageGenerator.NaturalizeItems(
            ["Create storage accounts", "Deploy to Azure"], "Azure Storage");

        // 3-word verb phrase without "Azure" gets " in Azure" suffix
        result.Should().Contain("Create storage accounts in Azure");
        // Already contains "Azure" — no suffix
        result.Should().Contain("Deploy to Azure");
    }

    [Fact]
    public void NaturalizeItems_ShortKeywords_GroupedWithWorkWith()
    {
        var result = SkillPageGenerator.NaturalizeItems(
            ["AI Search", "vector search", "hybrid search", "semantic search"], "Azure AI");

        result.Should().ContainSingle();
        result[0].Should().StartWith("Work with ");
        result[0].Should().Contain("AI Search");
        result[0].Should().Contain("semantic search");
    }

    [Fact]
    public void NaturalizeItems_TwoItems_NoOxfordComma()
    {
        var result = SkillPageGenerator.NaturalizeItems(
            ["blob", "queues"], "Azure Storage");

        result.Should().ContainSingle();
        result[0].Should().Be("Work with blob and queues");
    }

    [Fact]
    public void NaturalizeItems_LongPhrases_KeptAsIs()
    {
        var result = SkillPageGenerator.NaturalizeItems(
            ["Configure lifecycle management for blobs"], "Azure Storage");

        result.Should().ContainSingle();
        result[0].Should().Be("Configure lifecycle management for blobs");
    }

    [Fact]
    public void NaturalizeItems_SingleShortItem_PrefixedWithWorkWith()
    {
        var result = SkillPageGenerator.NaturalizeItems(
            ["storage"], "Azure Storage");

        result.Should().ContainSingle();
        result[0].Should().Be("Work with storage");
    }

    // === Issue 1: Acronym casing fixes ===

    [Theory]
    [InlineData("Search ai indexes and documents", "Search AI indexes and documents")]
    [InlineData("Use ai models for inference", "Use AI models for inference")]
    [InlineData("Deploy aks clusters to production", "Deploy AKS clusters to production")]
    [InlineData("Configure api management gateways", "Configure API management gateways")]
    [InlineData("Install sdk packages for development", "Install SDK packages for development")]
    [InlineData("Run kql queries against data explorer", "Run KQL queries against data explorer")]
    [InlineData("Create vm instances in azure region", "Create VM instances in azure region")]
    [InlineData("Setup aws integration with azure services", "Setup AWS integration with azure services")]
    [InlineData("Manage gcp resources from azure portal", "Manage GCP resources from azure portal")]
    [InlineData("Configure msal tokens for authentication", "Configure MSAL tokens for authentication")]
    [InlineData("Build llm applications with azure openai", "Build LLM applications with azure openai")]
    [InlineData("Query adx databases for telemetry data", "Query ADX databases for telemetry data")]
    [InlineData("Use resource id for lookup operations", "Use resource ID for lookup operations")]
    public void NaturalizeItems_FixesAcronymCasing(string input, string expected)
    {
        var result = SkillPageGenerator.NaturalizeItems([input], "Test");
        result.Should().Contain(expected);
    }

    [Fact]
    public void NaturalizeItems_AcronymCasing_DoesNotAffectNonAcronyms()
    {
        // "Aid" should NOT become "AID" — word boundary prevents partial match
        var result = SkillPageGenerator.NaturalizeItems(
            ["Find aid workers in deployment regions"], "Test");
        result.Should().Contain("Find aid workers in deployment regions");
    }

    [Fact]
    public void NaturalizeItems_AcronymCasing_ShortItemsAlsoFixed()
    {
        // Short items get "Work with" prefix, but acronyms still get fixed
        var result = SkillPageGenerator.NaturalizeItems(
            ["ai Search"], "Azure AI");
        result.Should().ContainSingle();
        result[0].Should().Be("Work with AI Search");
    }

    // === Issue 3: Single-word verb filtering ===

    [Theory]
    [InlineData("Recommend")]
    [InlineData("Compare")]
    [InlineData("Connect")]
    [InlineData("Learning")]
    [InlineData("Simulation")]
    public void NaturalizeItems_SingleWordVerbs_Filtered(string singleWord)
    {
        var result = SkillPageGenerator.NaturalizeItems([singleWord], "Test");
        result.Should().BeEmpty($"'{singleWord}' is a single-word verb/keyword with no context");
    }

    [Fact]
    public void NaturalizeItems_SingleWordVerb_FilteredAmongOtherItems()
    {
        var result = SkillPageGenerator.NaturalizeItems(
            ["Recommend", "Create storage accounts", "Connect"], "Azure Storage");
        result.Should().ContainSingle()
            .Which.Should().Be("Create storage accounts in Azure");
    }

    [Fact]
    public void NaturalizeItems_ShortItemsLessThan4Chars_Filtered()
    {
        var result = SkillPageGenerator.NaturalizeItems(["No", "ok"], "Test");
        result.Should().BeEmpty();
    }
}

// === Issue 4: DoNotUseFor shouldNotTrigger filtering ===

public class BuildContextDoNotUseForTests
{
    private static readonly string SimpleTemplate = @"---
title: Azure skill for {{displayName}}
---
# Azure skill for {{displayName}}
{{#if hasDoNotUseFor}}
## When NOT to use
{{#each doNotUseFor}}
- {{this}}
{{/each}}
{{/if}}
";

    private readonly ILogger<SkillPageGenerator> _logger = Substitute.For<ILogger<SkillPageGenerator>>();

    [Fact]
    public void Generate_DoNotUseFor_FromSkillData_Kept()
    {
        var gen = new SkillPageGenerator(SimpleTemplate, _logger);
        var skill = new SkillData
        {
            Name = "azure-storage",
            DisplayName = "Azure Storage",
            Description = "Test.",
            DoNotUseFor = ["Cosmos DB operations", "Network security groups"]
        };
        var triggers = new TriggerData([], [], null);
        var tier = new TierAssessment(1, [], "Test", false, false, false, false, false);
        var prereqs = new SkillPrerequisites();

        var result = gen.Generate(skill, triggers, tier, prereqs);

        result.Should().Contain("When NOT to use");
        result.Should().Contain("Work with Cosmos DB operations and Network security groups");
    }

    [Fact]
    public void Generate_DoNotUseFor_EmptySkillData_ShouldNotTriggerIgnored()
    {
        var gen = new SkillPageGenerator(SimpleTemplate, _logger);
        var skill = new SkillData
        {
            Name = "azure-storage",
            DisplayName = "Azure Storage",
            Description = "Test.",
            DoNotUseFor = []
        };
        var triggers = new TriggerData([], ["What is the weather today?", "Help me write a poem", "Explain quantum computing"], null);
        var tier = new TierAssessment(1, [], "Test", false, false, false, false, false);
        var prereqs = new SkillPrerequisites();

        var result = gen.Generate(skill, triggers, tier, prereqs);

        result.Should().NotContain("When NOT to use");
        result.Should().NotContain("weather");
        result.Should().NotContain("poem");
    }

    [Fact]
    public void Generate_DoNotUseFor_BothEmpty_SectionHidden()
    {
        var gen = new SkillPageGenerator(SimpleTemplate, _logger);
        var skill = new SkillData
        {
            Name = "azure-storage",
            DisplayName = "Azure Storage",
            Description = "Test.",
            DoNotUseFor = []
        };
        var triggers = new TriggerData([], [], null);
        var tier = new TierAssessment(1, [], "Test", false, false, false, false, false);
        var prereqs = new SkillPrerequisites();

        var result = gen.Generate(skill, triggers, tier, prereqs);

        result.Should().NotContain("When NOT to use");
    }
}

public class SkillPageGeneratorTests
{
    private static readonly string SimpleTemplate = @"---
title: Azure skill for {{displayName}}
description: {{description}}
---
# Azure skill for {{displayName}}

{{description}}

## Prerequisites

- **GitHub Copilot** — Required.
{{#if hasUseFor}}

### When to use this skill

{{#each useFor}}
- {{this}}
{{/each}}
{{/if}}

## What it provides

The skill provides knowledge about {{displayName}}.
{{#if showToolsSection}}
{{#if hasMcpTools}}

### Related tools

| Tool | Command | Purpose |
|------|---------|---------|
{{#each mcpTools}}
| {{toolName}} | `{{command}}` | {{purpose}} |
{{/each}}
{{/if}}
{{/if}}

## Related content

- [Azure MCP Server](/azure/copilot/azure-mcp-server)
";

    private readonly ILogger<SkillPageGenerator> _logger = Substitute.For<ILogger<SkillPageGenerator>>();

    private SkillPageGenerator CreateGenerator(string? template = null)
    {
        return new SkillPageGenerator(template ?? SimpleTemplate, _logger);
    }

    private static SkillData CreateTestSkillData() => new()
    {
        Name = "azure-storage",
        DisplayName = "Azure Storage",
        Description = "Manage Azure Storage accounts and resources.",
        UseFor = ["Creating storage accounts", "Managing blobs"],
        DoNotUseFor = ["Cosmos DB"],
        Services = [new ServiceEntry("Blob Storage", "Unstructured data")],
        McpTools = [new McpToolEntry("storage_list", "storage list", "List accounts")]
    };

    [Fact]
    public void Generate_WithValidData_ContainsFrontmatter()
    {
        var generator = CreateGenerator();
        var skill = CreateTestSkillData();
        var triggers = new TriggerData(["How do I create storage?"], [], null);
        var tier = new TierAssessment(1, [], "Test", true, true, false, false, true);
        var prereqs = new SkillPrerequisites();

        var result = generator.Generate(skill, triggers, tier, prereqs);

        result.Should().Contain("---");
        result.Should().Contain("title: Azure skill for Azure Storage");
        result.Should().Contain("description: Manage Azure Storage");
    }

    [Fact]
    public void Generate_WithValidData_ContainsSkillName()
    {
        var generator = CreateGenerator();
        var skill = CreateTestSkillData();
        var triggers = new TriggerData([], [], null);
        var tier = new TierAssessment(2, [], "Test", false, false, false, false, false);
        var prereqs = new SkillPrerequisites();

        var result = generator.Generate(skill, triggers, tier, prereqs);

        result.Should().Contain("Azure Storage");
    }

    [Fact]
    public void Generate_WithUseFor_RendersBulletList()
    {
        var generator = CreateGenerator();
        var skill = CreateTestSkillData();
        var triggers = new TriggerData([], [], null);
        var tier = new TierAssessment(1, [], "Test", false, false, false, false, false);
        var prereqs = new SkillPrerequisites();

        var result = generator.Generate(skill, triggers, tier, prereqs);

        result.Should().Contain("- Creating storage accounts");
        result.Should().Contain("- Managing blobs");
    }

    [Fact]
    public void Generate_WithToolsSection_RendersToolsTable()
    {
        var generator = CreateGenerator();
        var skill = CreateTestSkillData();
        var triggers = new TriggerData([], [], null);
        var tier = new TierAssessment(1, [], "Test", true, false, false, false, false);
        var prereqs = new SkillPrerequisites();

        var result = generator.Generate(skill, triggers, tier, prereqs);

        result.Should().Contain("storage_list");
        result.Should().Contain("`storage list`");
    }

    [Fact]
    public void Generate_WithToolsSectionHidden_DoesNotRenderTools()
    {
        var generator = CreateGenerator();
        var skill = CreateTestSkillData();
        var triggers = new TriggerData([], [], null);
        var tier = new TierAssessment(2, [], "Test", false, false, false, false, false);
        var prereqs = new SkillPrerequisites();

        var result = generator.Generate(skill, triggers, tier, prereqs);

        result.Should().NotContain("### Related tools");
    }

    [Fact]
    public void Generate_ContainsPrerequisites()
    {
        var generator = CreateGenerator();
        var skill = CreateTestSkillData();
        var triggers = new TriggerData([], [], null);
        var tier = new TierAssessment(1, [], "Test", false, false, false, false, false);
        var prereqs = new SkillPrerequisites();

        var result = generator.Generate(skill, triggers, tier, prereqs);

        result.Should().Contain("## Prerequisites");
        result.Should().Contain("GitHub Copilot");
    }
}

// === MCP Tools Template Rendering (Issue #369) ===

public class McpToolsTemplateRenderingTests
{
    private readonly ILogger<SkillPageGenerator> _logger = Substitute.For<ILogger<SkillPageGenerator>>();

    [Fact]
    public void Generate_WithMcpTools_RendersMcpToolsSection()
    {
        var templatePath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "templates", "skill-page-template.hbs");
        var template = File.ReadAllText(templatePath);
        var generator = new SkillPageGenerator(template, _logger);

        var skill = new SkillData
        {
            Name = "azure-deploy",
            DisplayName = "Azure Deploy",
            Description = "Deploy applications.",
            McpTools =
            [
                new McpToolEntry("mcp_azure_mcp_subscription_list", "subscription list", "List available subscriptions"),
                new McpToolEntry("mcp_azure_mcp_azd", "azd", "Execute AZD commands")
            ]
        };
        var triggers = new TriggerData(["Deploy my app"], [], null);
        var tier = new TierAssessment(1, [], "Test", true, true, false, false, false);
        var prereqs = new SkillPrerequisites();

        var result = generator.Generate(skill, triggers, tier, prereqs);

        result.Should().Contain("## MCP tools");
        result.Should().Contain("mcp_azure_mcp_subscription_list");
        result.Should().Contain("List available subscriptions");
        result.Should().Contain("mcp_azure_mcp_azd");
        result.Should().Contain("Execute AZD commands");
    }

    [Fact]
    public void Generate_WithoutMcpTools_OmitsMcpToolsSection()
    {
        var templatePath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "templates", "skill-page-template.hbs");
        var template = File.ReadAllText(templatePath);
        var generator = new SkillPageGenerator(template, _logger);

        var skill = new SkillData
        {
            Name = "azure-quotas",
            DisplayName = "Azure Quotas",
            Description = "Check quotas.",
            McpTools = []
        };
        var triggers = new TriggerData(["Check my quotas"], [], null);
        var tier = new TierAssessment(1, [], "Test", true, true, false, false, false);
        var prereqs = new SkillPrerequisites();

        var result = generator.Generate(skill, triggers, tier, prereqs);

        result.Should().NotContain("## MCP tools");
    }
}
