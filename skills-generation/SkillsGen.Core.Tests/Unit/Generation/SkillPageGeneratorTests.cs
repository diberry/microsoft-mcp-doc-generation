using System.Text.RegularExpressions;
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
    public void NaturalizeItems_ShortKeywords_GroupedWithManageAndConfigure()
    {
        var result = SkillPageGenerator.NaturalizeItems(
            ["AI Search", "vector search", "hybrid search", "semantic search"], "Azure AI");

        result.Should().ContainSingle();
        result[0].Should().StartWith("Manage and configure ");
        result[0].Should().Contain("AI Search");
        result[0].Should().Contain("semantic search");
    }

    [Fact]
    public void NaturalizeItems_TwoItems_NoOxfordComma()
    {
        var result = SkillPageGenerator.NaturalizeItems(
            ["blob", "queues"], "Azure Storage");

        result.Should().ContainSingle();
        result[0].Should().Be("Manage and configure blob and queues in Azure");
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
    public void NaturalizeItems_SingleShortItem_ManageAndConfigure()
    {
        var result = SkillPageGenerator.NaturalizeItems(
            ["storage"], "Azure Storage");

        result.Should().ContainSingle();
        result[0].Should().Be("Manage and configure storage in Azure");
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
        // Short items get "Manage and configure" prefix, but acronyms still get fixed
        var result = SkillPageGenerator.NaturalizeItems(
            ["ai Search"], "Azure AI");
        result.Should().ContainSingle();
        result[0].Should().Be("Manage and configure AI Search in Azure");
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
        result.Should().Contain("Manage and configure Cosmos DB operations and Network security groups in Azure");
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

// === Issue #377: Acrolinx compliance fixes ===

public class AcrolinxComplianceTests
{
    private readonly ILogger<SkillPageGenerator> _logger = Substitute.For<ILogger<SkillPageGenerator>>();

    // Use the real template for integration-level tests
    private static string LoadRealTemplate()
    {
        // Navigate from test bin to templates directory
        var dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "skills-generation.slnx")))
            dir = Directory.GetParent(dir)?.FullName;
        if (dir == null) throw new InvalidOperationException("Cannot find solution root");
        return File.ReadAllText(Path.Combine(dir, "templates", "skill-page-template.hbs"));
    }

    // --- Fix 1: No more "Work with X" sentence fragments ---

    [Fact]
    public void FlushShortItems_DoesNotProduceWorkWithPattern()
    {
        // Short noun phrases should NOT produce "Work with X"
        var result = SkillPageGenerator.NaturalizeItems(
            ["blob storage", "file shares", "table storage"], "Azure Storage");
        result.Should().NotContain(s => s.StartsWith("Work with", StringComparison.OrdinalIgnoreCase),
            "FlushShortItems should not produce 'Work with' fragments");
    }

    [Fact]
    public void FlushShortItems_ProducesCompleteSentence()
    {
        var result = SkillPageGenerator.NaturalizeItems(
            ["blob storage", "file shares", "table storage"], "Azure Storage");

        result.Should().ContainSingle();
        // Should be a complete action sentence
        result[0].Should().StartWith("Manage and configure");
        result[0].Should().Contain("blob storage");
        result[0].Should().Contain("table storage");
    }

    [Fact]
    public void FlushShortItems_SingleItem_ProducesCompleteSentence()
    {
        var result = SkillPageGenerator.NaturalizeItems(["AI Search"], "Azure AI");
        result.Should().ContainSingle();
        result[0].Should().StartWith("Manage and configure");
        result[0].Should().Contain("AI Search");
        result[0].Should().NotStartWith("Work with");
    }

    [Fact]
    public void FlushShortItems_TwoItems_ProducesCompleteSentence()
    {
        var result = SkillPageGenerator.NaturalizeItems(["blob", "queues"], "Azure Storage");
        result.Should().ContainSingle();
        result[0].Should().StartWith("Manage and configure");
        result[0].Should().Contain("blob");
        result[0].Should().Contain("queues");
    }

    // --- Fix 2: No more boilerplate in "What it provides" ---

    [Fact]
    public void BuildContext_WhatItProvides_NoGenericBoilerplate()
    {
        // Use a template that renders whatItProvides
        var template = @"## What it provides
{{{whatItProvides}}}";
        var gen = new SkillPageGenerator(template, _logger);
        var skill = new SkillData
        {
            Name = "azure-storage",
            DisplayName = "Azure Storage",
            Description = "Manage blob, file, queue, and table storage resources."
        };
        var triggers = new TriggerData([], [], null);
        var tier = new TierAssessment(1, [], "Test", false, false, false, false, false);
        var prereqs = new SkillPrerequisites();

        var result = gen.Generate(skill, triggers, tier, prereqs);

        result.Should().NotContain("specialized knowledge",
            "'specialized knowledge' boilerplate should not appear in What it provides");
    }

    [Fact]
    public void BuildContext_WhatItProvides_UsesDescriptionDirectly()
    {
        var template = @"{{{whatItProvides}}}";
        var gen = new SkillPageGenerator(template, _logger);
        var skill = new SkillData
        {
            Name = "azure-storage",
            DisplayName = "Azure Storage",
            Description = "Manage blob, file, queue, and table storage resources."
        };
        var triggers = new TriggerData([], [], null);
        var tier = new TierAssessment(1, [], "Test", false, false, false, false, false);
        var prereqs = new SkillPrerequisites();

        var result = gen.Generate(skill, triggers, tier, prereqs);

        // Description should be used directly, not wrapped in boilerplate
        result.Trim().Should().Be("Manage blob, file, queue, and table storage resources.");
    }

    [Fact]
    public void BuildContext_WhatItProvides_FallbackWhenNoDescription()
    {
        var template = @"{{{whatItProvides}}}";
        var gen = new SkillPageGenerator(template, _logger);
        var skill = new SkillData
        {
            Name = "azure-storage",
            DisplayName = "Azure Storage",
            Description = ""
        };
        var triggers = new TriggerData([], [], null);
        var tier = new TierAssessment(1, [], "Test", false, false, false, false, false);
        var prereqs = new SkillPrerequisites();

        var result = gen.Generate(skill, triggers, tier, prereqs);

        result.Should().NotContain("specialized knowledge");
        result.Should().Contain("Azure Storage");
    }

    // --- Fix 3: No duplicate GitHub Copilot prerequisite ---

    [Fact]
    public void Template_NoDuplicateGitHubCopilot()
    {
        var template = LoadRealTemplate();
        var gen = new SkillPageGenerator(template, _logger);
        var skill = new SkillData
        {
            Name = "azure-storage",
            DisplayName = "Azure Storage",
            Description = "Test description."
        };
        var triggers = new TriggerData(["Create a storage account"], [], null);
        var tier = new TierAssessment(1, [], "Test", true, true, false, false, true);
        var prereqs = new SkillPrerequisites
        {
            Tools = [new ToolRequirement("GitHub Copilot", Required: true),
                     new ToolRequirement("Azure CLI", MinVersion: "2.60.0")]
        };

        var result = gen.Generate(skill, triggers, tier, prereqs);

        // Count occurrences of "GitHub Copilot" as a prerequisite bullet
        var copilotBulletCount = Regex.Matches(result, @"^\s*-\s+\*\*GitHub Copilot\*\*", RegexOptions.Multiline).Count;
        copilotBulletCount.Should().Be(1,
            "GitHub Copilot should appear as a prerequisite bullet exactly once, not duplicated");
    }

    [Fact]
    public void Template_GitHubCopilotAppearsViaToolPrereqs()
    {
        var template = LoadRealTemplate();
        var gen = new SkillPageGenerator(template, _logger);
        var skill = new SkillData
        {
            Name = "azure-storage",
            DisplayName = "Azure Storage",
            Description = "Test description."
        };
        var triggers = new TriggerData([], [], null);
        var tier = new TierAssessment(1, [], "Test", false, false, false, false, false);
        var prereqs = new SkillPrerequisites
        {
            Tools = [new ToolRequirement("GitHub Copilot", Required: true)]
        };

        var result = gen.Generate(skill, triggers, tier, prereqs);

        // Should still appear in Required tools section
        result.Should().Contain("**GitHub Copilot**");
    }

    // --- Fix 4: Trigger post-processing ---

    [Fact]
    public void Generate_AppliesTriggerProcessorToExamplePrompts()
    {
        var template = @"{{#each shouldTrigger}}- ""{{{this}}}""
{{/each}}";
        var gen = new SkillPageGenerator(template, _logger);
        var skill = new SkillData
        {
            Name = "azure-storage",
            DisplayName = "Azure Storage",
            Description = "Test."
        };
        var triggers = new TriggerData(["setup Azure Active Directory auth"], [], null);
        var tier = new TierAssessment(1, [], "Test", false, false, false, false, true);
        var prereqs = new SkillPrerequisites();

        // Provide a trigger processor that uppercases text
        var result = gen.Generate(skill, triggers, tier, prereqs, t => t.ToUpperInvariant());

        result.Should().Contain("SETUP AZURE ACTIVE DIRECTORY AUTH",
            "Trigger processor should be applied to example prompts");
    }

    [Fact]
    public void Generate_WithoutTriggerProcessor_TriggersUnchanged()
    {
        var template = @"{{#each shouldTrigger}}- ""{{{this}}}""
{{/each}}";
        var gen = new SkillPageGenerator(template, _logger);
        var skill = new SkillData
        {
            Name = "azure-storage",
            DisplayName = "Azure Storage",
            Description = "Test."
        };
        var triggers = new TriggerData(["Create a storage account"], [], null);
        var tier = new TierAssessment(1, [], "Test", false, false, false, false, true);
        var prereqs = new SkillPrerequisites();

        // No trigger processor — should remain unchanged
        var result = gen.Generate(skill, triggers, tier, prereqs);

        result.Should().Contain("Create a storage account");
    }
}
