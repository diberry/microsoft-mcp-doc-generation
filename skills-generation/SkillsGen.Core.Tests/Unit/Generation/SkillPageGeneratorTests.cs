using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SkillsGen.Core.Generation;
using SkillsGen.Core.Models;
using SkillsGen.Core.PostProcessing;
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
    public void NaturalizeItems_ShortKeywords_GroupedAsCompleteSentence()
    {
        var result = SkillPageGenerator.NaturalizeItems(
            ["AI Search", "vector search", "hybrid search", "semantic search"], "Azure AI");

        result.Should().ContainSingle();
        // Should NOT use the vague "Work with" fragment pattern
        result[0].Should().NotStartWith("Work with ");
        // Should be a complete sentence with concrete verb
        result[0].Should().Contain("AI Search");
        result[0].Should().Contain("semantic search");
    }

    [Fact]
    public void NaturalizeItems_TwoItems_CompleteSentenceNotFragment()
    {
        var result = SkillPageGenerator.NaturalizeItems(
            ["blob", "queues"], "Azure Storage");

        result.Should().ContainSingle();
        // Should NOT produce "Work with blob and queues" fragment
        result[0].Should().NotStartWith("Work with ");
        result[0].Should().Contain("blob");
        result[0].Should().Contain("queues");
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
    public void NaturalizeItems_SingleShortItem_CompleteSentenceNotFragment()
    {
        var result = SkillPageGenerator.NaturalizeItems(
            ["storage"], "Azure Storage");

        result.Should().ContainSingle();
        // Should NOT produce fragment "Work with storage"
        result[0].Should().NotStartWith("Work with ");
        result[0].Should().Contain("storage");
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
        // Short items get sentence prefix, and acronyms still get fixed
        var result = SkillPageGenerator.NaturalizeItems(
            ["ai Search"], "Azure AI");
        result.Should().ContainSingle();
        result[0].Should().Contain("AI Search");
        result[0].Should().NotStartWith("Work with ");
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
        // Should NOT contain "Work with" fragment
        result.Should().NotContain("Work with Cosmos");
        result.Should().Contain("Cosmos DB operations");
        result.Should().Contain("Network security groups");
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

// === Issue #377: Acrolinx compliance tests ===

public class AcrolinxComplianceTests
{
    private readonly ILogger<SkillPageGenerator> _logger = Substitute.For<ILogger<SkillPageGenerator>>();

    private static readonly string FullTemplate = @"---
title: Azure skill for {{{displayName}}}
description: {{{description}}}
ms.topic: reference
ms.date: {{{generatedDate}}}
---
# Azure skill for {{{displayName}}}

{{{description}}}

**Skill:** `{{{name}}}`

## Prerequisites

{{#if prerequisites.azure.requiresAzureLogin}}
- **Azure authentication**—Sign in with `az login` or use a service principal.
{{/if}}
{{#if prerequisites.azure.requiresSubscription}}
- **Azure subscription**—An active Azure subscription is required.
{{/if}}
- **GitHub Copilot**—GitHub Copilot with the Azure extension enabled.

{{#if hasToolPrereqs}}
### Required tools

{{#each prerequisites.tools}}
- **{{{name}}}**{{#if minVersion}} (v{{{minVersion}}}+){{/if}}{{#if installCommand}}—Install: `{{{installCommand}}}`{{/if}}
{{/each}}
{{/if}}

{{#if hasUseFor}}
### When to use this skill

Use the **{{{displayName}}}** skill when you need to:

{{#each useFor}}
- {{{this}}}
{{/each}}
{{/if}}

## What it provides

{{{whatItProvides}}}

## Related content

- [Azure MCP Server](/azure/developer/azure-mcp-server/overview)
";

    private SkillPageGenerator CreateGenerator(string? template = null)
    {
        return new SkillPageGenerator(template ?? FullTemplate, _logger);
    }

    // --- "What it provides" should not just echo the description ---

    [Fact]
    public void WhatItProvides_DoesNotRepeatDescriptionVerbatim()
    {
        var gen = CreateGenerator();
        var skill = new SkillData
        {
            Name = "azure-storage",
            DisplayName = "Azure Storage",
            Description = "Azure Storage Services including Blob Storage and File Shares.",
            Services = [new ServiceEntry("Blob Storage", "Unstructured data")],
        };
        var triggers = new TriggerData([], [], null);
        var tier = new TierAssessment(1, [], "Test", false, false, false, false, false);
        var prereqs = new SkillPrerequisites();

        var result = gen.Generate(skill, triggers, tier, prereqs);

        // Extract "What it provides" section
        var whatIdx = result.IndexOf("## What it provides");
        whatIdx.Should().BeGreaterThan(-1);
        var afterWhat = result[(whatIdx + "## What it provides".Length)..];
        var nextSection = afterWhat.IndexOf("\n## ");
        var whatContent = nextSection > 0 ? afterWhat[..nextSection] : afterWhat;

        // Should NOT just echo the exact description string
        whatContent.Should().NotContain("Azure Storage Services including Blob Storage and File Shares.");
    }

    [Fact]
    public void WhatItProvides_IncludesConcreteCapabilitiesFromServices()
    {
        var gen = CreateGenerator();
        var skill = new SkillData
        {
            Name = "azure-storage",
            DisplayName = "Azure Storage",
            Description = "Azure Storage services.",
            Services =
            [
                new ServiceEntry("Blob Storage", "Unstructured data"),
                new ServiceEntry("Queue Storage", "Async messaging"),
            ],
        };
        var triggers = new TriggerData([], [], null);
        var tier = new TierAssessment(1, [], "Test", false, false, false, false, false);
        var prereqs = new SkillPrerequisites();

        var result = gen.Generate(skill, triggers, tier, prereqs);

        var whatIdx = result.IndexOf("## What it provides");
        var afterWhat = result[(whatIdx + "## What it provides".Length)..];
        var nextSection = afterWhat.IndexOf("\n## ");
        var whatContent = nextSection > 0 ? afterWhat[..nextSection] : afterWhat;

        // Should reference concrete services
        whatContent.Should().Contain("Blob Storage");
        whatContent.Should().Contain("Queue Storage");
    }

    [Fact]
    public void WhatItProvides_IncludesConcreteCapabilitiesFromTools()
    {
        var gen = CreateGenerator();
        var skill = new SkillData
        {
            Name = "azure-monitor",
            DisplayName = "Azure Monitor",
            Description = "Azure monitoring services.",
            McpTools =
            [
                new McpToolEntry("monitor_query", "monitor query", "Query metrics and logs"),
                new McpToolEntry("monitor_alerts", "monitor alerts", "Manage alert rules"),
            ],
        };
        var triggers = new TriggerData([], [], null);
        var tier = new TierAssessment(1, [], "Test", false, false, false, false, false);
        var prereqs = new SkillPrerequisites();

        var result = gen.Generate(skill, triggers, tier, prereqs);

        var whatIdx = result.IndexOf("## What it provides");
        var afterWhat = result[(whatIdx + "## What it provides".Length)..];
        var nextSection = afterWhat.IndexOf("\n## ");
        var whatContent = nextSection > 0 ? afterWhat[..nextSection] : afterWhat;

        // Should reference concrete capabilities from tools
        whatContent.Should().ContainAny("query", "Query", "metrics", "logs", "alert");
    }

    // --- Prerequisite deduplication ---

    [Fact]
    public void Generate_DeduplicatesGitHubCopilotFromToolsList()
    {
        var gen = CreateGenerator();
        var skill = new SkillData
        {
            Name = "azure-storage",
            DisplayName = "Azure Storage",
            Description = "Storage services.",
        };
        var triggers = new TriggerData([], [], null);
        var tier = new TierAssessment(1, [], "Test", false, false, false, false, false);
        var prereqs = new SkillPrerequisites
        {
            Tools =
            [
                new ToolRequirement("GitHub Copilot"),
                new ToolRequirement("Azure CLI", "2.60.0", "curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash"),
            ],
        };

        var result = gen.Generate(skill, triggers, tier, prereqs);

        // The "Required tools" section should NOT contain "GitHub Copilot" (already in main prereqs)
        // But the main prerequisites section should still have it
        result.Should().Contain("GitHub Copilot");

        // If Required tools section exists, it should only have Azure CLI
        if (result.Contains("### Required tools"))
        {
            var toolsIdx = result.IndexOf("### Required tools");
            var afterTools = result[(toolsIdx + "### Required tools".Length)..];
            var nextSection = afterTools.IndexOf("\n#");
            var toolsContent = nextSection > 0 ? afterTools[..nextSection] : afterTools;

            toolsContent.Should().NotContain("GitHub Copilot",
                "GitHub Copilot should be removed from Required tools to avoid duplication");
            toolsContent.Should().Contain("Azure CLI");
        }
    }

    // --- Full-page "When to use" bullets should be complete sentences ---

    [Fact]
    public void Generate_UseForBullets_NoWorkWithFragments()
    {
        var gen = CreateGenerator();
        var skill = new SkillData
        {
            Name = "azure-compute",
            DisplayName = "Azure Compute",
            Description = "Compute services.",
            UseFor = [],
        };
        var triggers = new TriggerData(
            ["Create VM", "Scale VMSS", "Monitor performance"],
            [], null);
        var tier = new TierAssessment(1, [], "Test", false, false, false, false, false);
        var prereqs = new SkillPrerequisites();

        var result = gen.Generate(skill, triggers, tier, prereqs);

        // No bullet should be a "Work with X" fragment
        var lines = result.Split('\n');
        var bullets = lines.Where(l => l.TrimStart().StartsWith("- ")).ToList();
        foreach (var bullet in bullets)
        {
            bullet.Should().NotContain("Work with ",
                $"bullet '{bullet.Trim()}' should not use the vague 'Work with' fragment pattern");
        }
    }

    // --- Diverse skill archetype tests per AD-008 ---

    [Fact]
    public void Generate_MonitoringSkill_UseForBulletsAreComplete()
    {
        var gen = CreateGenerator();
        var skill = new SkillData
        {
            Name = "azure-diagnostics",
            DisplayName = "Azure Diagnostics",
            Description = "Diagnostic and troubleshooting services.",
            UseFor = ["logs", "metrics", "traces"],
        };
        var triggers = new TriggerData([], [], null);
        var tier = new TierAssessment(2, [], "Test", false, false, false, false, false);
        var prereqs = new SkillPrerequisites();

        var result = gen.Generate(skill, triggers, tier, prereqs);

        var lines = result.Split('\n');
        var bullets = lines.Where(l => l.TrimStart().StartsWith("- ") &&
            !l.Contains("Azure authentication") &&
            !l.Contains("Azure subscription") &&
            !l.Contains("GitHub Copilot") &&
            !l.Contains("Azure MCP Server")).ToList();

        foreach (var bullet in bullets)
        {
            bullet.Should().NotContain("Work with ",
                $"monitoring skill bullet '{bullet.Trim()}' should not use 'Work with' fragment");
        }
    }

    [Fact]
    public void Generate_FallbackUseFor_NoWorkWithFragment()
    {
        var gen = CreateGenerator();
        // Skill with NO UseFor and NO triggers — hits the fallback path
        var skill = new SkillData
        {
            Name = "azure-validate",
            DisplayName = "Azure Validate",
            Description = "Validation services.",
            UseFor = [],
        };
        var triggers = new TriggerData([], [], null);
        var tier = new TierAssessment(2, [], "Test", false, false, false, false, false);
        var prereqs = new SkillPrerequisites();

        var result = gen.Generate(skill, triggers, tier, prereqs);

        // The fallback in BuildContext uses "Work with {DisplayName} resources"
        result.Should().NotContain("Work with Azure Validate resources");
    }

    private static int CountOccurrences(string text, string search)
    {
        int count = 0;
        int idx = 0;
        while ((idx = text.IndexOf(search, idx, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            count++;
            idx += search.Length;
        }
        return count;
    }
}

// === Post-processor idempotence test ===

public class AcrolinxPostProcessorIdempotenceTests
{
    private readonly ILogger<AcrolinxPostProcessor> _logger = Substitute.For<ILogger<AcrolinxPostProcessor>>();

    private static readonly string SampleReplacementsJson = """
    [
        { "Parameter": "utilize", "NaturalLanguage": "use" },
        { "Parameter": "Azure Active Directory", "NaturalLanguage": "Microsoft Entra ID" }
    ]
    """;

    private static readonly string SampleAcronymsJson = """
    [
        { "Acronym": "RBAC", "Expansion": "role-based access control" },
        { "Acronym": "MCP", "Expansion": "Model Context Protocol" }
    ]
    """;

    [Fact]
    public void Process_IsIdempotent()
    {
        var processor = new AcrolinxPostProcessor(SampleReplacementsJson, SampleAcronymsJson, _logger);
        var input = """
            ---
            title: Azure skill for Azure Storage
            description: Storage services
            ---
            # Azure skill for Azure Storage

            Configure RBAC roles for the azure-storage service.
            However the tool utilizes Azure Active Directory.
            See https://learn.microsoft.com/en-us/azure/storage for details.
            """;

        var firstPass = processor.Process(input);
        var secondPass = processor.Process(firstPass);

        secondPass.Should().Be(firstPass, "post-processing should be idempotent");
    }
}
