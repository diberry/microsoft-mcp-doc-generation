using FluentAssertions;
using SkillsGen.Core.Models;
using SkillsGen.Core.Parsers;
using Xunit;

namespace SkillsGen.Core.Tests.Unit.Parsers;

public class SkillMarkdownParserTests
{
    private readonly SkillMarkdownParser _parser = new();

    private static string LoadFixture(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", name);
        return File.ReadAllText(path);
    }

    [Fact]
    public void Parse_WellFormedSkill_ExtractsAllSections()
    {
        var content = LoadFixture("sample-skill.md");
        var result = _parser.Parse("azure-storage", content);

        result.Name.Should().Be("azure-storage");
        result.DisplayName.Should().Be("Azure Storage");
        result.Description.Should().Contain("Azure Storage");
        result.Services.Should().NotBeEmpty();
        result.McpTools.Should().NotBeEmpty();
        result.WorkflowSteps.Should().NotBeEmpty();
        result.DecisionGuidance.Should().NotBeEmpty();
        result.Prerequisites.Should().NotBeEmpty();
        result.RawBody.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Parse_EmptyContent_ReturnsDefaultWithSkillName()
    {
        var result = _parser.Parse("azure-test", "");

        result.Name.Should().Be("azure-test");
        result.DisplayName.Should().Be("azure-test");
        result.Description.Should().BeEmpty();
        result.Services.Should().BeEmpty();
        result.McpTools.Should().BeEmpty();
    }

    [Fact]
    public void Parse_NullContent_ReturnsDefaultWithSkillName()
    {
        var result = _parser.Parse("azure-test", null!);

        result.Name.Should().Be("azure-test");
        result.Services.Should().BeEmpty();
    }

    [Fact]
    public void Parse_NoFrontmatter_GracefulFallback()
    {
        var content = "# My Skill\n\nSome content without frontmatter.\n";
        var result = _parser.Parse("azure-fallback", content);

        result.Name.Should().Be("azure-fallback");
        result.DisplayName.Should().Be("Azure Fallback");
        result.RawBody.Should().Contain("My Skill");
    }

    [Fact]
    public void Parse_UseForAndDoNotUseFor_ExtractedFromDescription()
    {
        var content = LoadFixture("sample-skill.md");
        var result = _parser.Parse("azure-storage", content);

        result.UseFor.Should().NotBeEmpty("USE FOR items are in the description");
        result.DoNotUseFor.Should().NotBeEmpty("DO NOT USE FOR items are in the description");
    }

    [Fact]
    public void Parse_Services_ParsedIntoServiceEntryRecords()
    {
        var content = LoadFixture("sample-skill.md");
        var result = _parser.Parse("azure-storage", content);

        result.Services.Should().HaveCountGreaterOrEqualTo(3);
        result.Services[0].Name.Should().Be("Azure Blob Storage");
        result.Services[0].UseWhen.Should().Contain("unstructured");
    }

    [Fact]
    public void Parse_McpTools_ParsedCorrectly()
    {
        var content = LoadFixture("sample-skill.md");
        var result = _parser.Parse("azure-storage", content);

        result.McpTools.Should().HaveCountGreaterOrEqualTo(3);
        result.McpTools[0].ToolName.Should().Be("storage_account_list");
        result.McpTools[0].Command.Should().Contain("storage account list");
    }

    [Fact]
    public void Parse_WorkflowSteps_ExtractsNumberedList()
    {
        var content = LoadFixture("sample-skill.md");
        var result = _parser.Parse("azure-storage", content);

        result.WorkflowSteps.Should().HaveCountGreaterOrEqualTo(3);
        result.WorkflowSteps[0].Should().Contain("Authenticate");
    }

    [Fact]
    public void Parse_Prerequisites_ExtractsBulletItems()
    {
        var content = LoadFixture("sample-skill.md");
        var result = _parser.Parse("azure-storage", content);

        result.Prerequisites.Should().Contain(p => p.Contains("Azure subscription"));
    }

    [Fact]
    public void Parse_RelatedSkills_ExtractsCrossReferences()
    {
        var content = LoadFixture("sample-skill.md");
        var result = _parser.Parse("azure-storage", content);

        result.RelatedSkills.Should().Contain("azure-deploy");
        result.RelatedSkills.Should().Contain("azure-diagnostics");
    }

    [Fact]
    public void Parse_DecisionGuidance_ExtractsTopicsAndOptions()
    {
        var content = LoadFixture("sample-skill.md");
        var result = _parser.Parse("azure-storage", content);

        result.DecisionGuidance.Should().NotBeEmpty();
        var firstTopic = result.DecisionGuidance[0];
        firstTopic.Topic.Should().NotBeNullOrEmpty();
        firstTopic.Options.Should().NotBeEmpty();
    }

    [Fact]
    public void Parse_MinimalSkill_ReturnsPartialData()
    {
        var content = LoadFixture("minimal-skill.md");
        var result = _parser.Parse("azure-quotas", content);

        result.Name.Should().Be("azure-quotas");
        result.DisplayName.Should().Be("Azure Quotas");
        result.Services.Should().BeEmpty();
        result.McpTools.Should().BeEmpty();
        result.WorkflowSteps.Should().BeEmpty();
    }

    [Theory]
    [InlineData("azure-storage", "Azure Storage")]
    [InlineData("azure-ai", "Azure AI")]
    [InlineData("azure-hosted-copilot-sdk", "Azure Hosted Copilot SDK")]
    [InlineData("entra-app-registration", "Entra App Registration")]
    [InlineData("microsoft-foundry", "Microsoft Foundry")]
    [InlineData("appinsights-instrumentation", "Appinsights Instrumentation")]
    [InlineData("azure-rbac", "Azure RBAC")]
    public void Parse_DerivesDisplayNameFromSlug(string slug, string expectedDisplayName)
    {
        var content = $"---\nname: {slug}\ndescription: Test\n---\n\nBody content.\n";
        var result = _parser.Parse(slug, content);
        result.DisplayName.Should().Be(expectedDisplayName);
    }

    [Fact]
    public void Parse_CleanDescription_AllCapsDowncasedToLowercase()
    {
        var content = "---\nname: test\ndescription: The ALREADY-prepared DEPLOYMENT was READY.\n---\n\nBody.\n";
        var result = _parser.Parse("test", content);
        result.Description.Should().Contain("already");
        result.Description.Should().NotContain("Already");
        result.Description.Should().NotContain("ALREADY");
    }

    [Fact]
    public void Parse_CleanDescription_DuplicateAcronymExpansionCollapsed()
    {
        var content = "---\nname: test\ndescription: Azure Kubernetes Service (Azure Kubernetes Service (AKS)) is great.\n---\n\nBody.\n";
        var result = _parser.Parse("test", content);
        result.Description.Should().Contain("Azure Kubernetes Service (AKS)");
        result.Description.Should().NotContain("Azure Kubernetes Service (Azure Kubernetes Service (AKS))");
    }

    [Fact]
    public void Parse_CleanDescription_DotAzurePathPreserved()
    {
        var content = "---\nname: test\ndescription: Uses the.azure/config file for settings.\n---\n\nBody.\n";
        var result = _parser.Parse("test", content);
        result.Description.Should().NotContain(". azure/");
    }

    // === Issue 2: Double-encoded HTML entities ===

    [Fact]
    public void Parse_DoubleEncodedEntities_DecodedInDescription()
    {
        var content = "---\nname: azure-cost\ndescription: Helps with &amp;quot;Azure costs&amp;quot; and billing.\n---\n\nBody.\n";
        var result = _parser.Parse("azure-cost", content);
        result.Description.Should().NotContain("&quot;");
        result.Description.Should().NotContain("&amp;");
    }

    [Fact]
    public void Parse_DoubleEncodedEntities_DecodedInUseForItems()
    {
        var content = "---\nname: azure-cost\ndescription: Helps. USE FOR: &amp;quot;cost analysis&amp;quot;, budgets\n---\n\nBody.\n";
        var result = _parser.Parse("azure-cost", content);
        result.UseFor.Should().NotContain(s => s.Contains("&quot;"));
        result.UseFor.Should().NotContain(s => s.Contains("&amp;"));
    }

    // === MCP Tools Extraction (Issue #369) ===

    [Fact]
    public void Parse_McpTools_TwoColumnTable_ExtractsToolAndPurpose()
    {
        var content = """
            ---
            name: azure-deploy
            description: Deploy apps.
            ---

            # Azure Deploy

            ## MCP Tools

            | Tool | Purpose |
            |------|---------|
            | `mcp_azure_mcp_subscription_list` | List available subscriptions |
            | `mcp_azure_mcp_group_list` | List resource groups in subscription |
            | `mcp_azure_mcp_azd` | Execute AZD commands |
            """;
        var result = _parser.Parse("azure-deploy", content);

        result.McpTools.Should().HaveCount(3);
        result.McpTools[0].ToolName.Should().Be("mcp_azure_mcp_subscription_list");
        result.McpTools[0].Purpose.Should().Be("List available subscriptions");
        result.McpTools[1].ToolName.Should().Be("mcp_azure_mcp_group_list");
        result.McpTools[2].ToolName.Should().Be("mcp_azure_mcp_azd");
    }

    [Fact]
    public void Parse_McpTools_ThreeColumnTable_ExtractsToolAndPurpose()
    {
        var content = """
            ---
            name: azure-kubernetes
            description: AKS skill.
            ---

            # AKS

            ## MCP Tools

            | Tool | Purpose | Key Parameters |
            |------|---------|----------------|
            | `mcp_azure_mcp_aks` | AKS MCP entry point | Discover callable tools |
            """;
        var result = _parser.Parse("azure-kubernetes", content);

        result.McpTools.Should().HaveCount(1);
        result.McpTools[0].ToolName.Should().Be("mcp_azure_mcp_aks");
        result.McpTools[0].Purpose.Should().Be("AKS MCP entry point");
    }

    [Fact]
    public void Parse_McpTools_BacktickToolNames_StrippedFromCells()
    {
        var content = """
            ---
            name: test
            description: Test.
            ---

            ## MCP Tools

            | Tool | Purpose |
            |------|---------|
            | `mcp_azure_mcp_test` | Run tests |
            """;
        var result = _parser.Parse("test", content);

        result.McpTools.Should().HaveCount(1);
        result.McpTools[0].ToolName.Should().Be("mcp_azure_mcp_test");
        result.McpTools[0].ToolName.Should().NotContain("`");
    }

    [Fact]
    public void Parse_McpTools_McpServerSection_MatchesHeading()
    {
        var content = """
            ---
            name: azure-ai
            description: AI services.
            ---

            # Azure AI

            ## MCP Server (Preferred)

            ### AI Search
            - `azure__search` with command `search_index_list` - List search indexes
            - `azure__search` with command `search_query` - Query search index

            ### Speech
            - `azure__speech` with command `speech_transcribe` - Speech to text
            """;
        var result = _parser.Parse("azure-ai", content);

        result.McpTools.Should().HaveCount(3);
        result.McpTools.Should().Contain(t => t.ToolName == "azure__search" && t.Command == "search_index_list");
        result.McpTools.Should().Contain(t => t.ToolName == "azure__search" && t.Command == "search_query");
        result.McpTools.Should().Contain(t => t.ToolName == "azure__speech" && t.Command == "speech_transcribe");
    }

    [Fact]
    public void Parse_McpTools_InlineBulletWithCommand_ParsedCorrectly()
    {
        var content = """
            ---
            name: test
            description: Test.
            ---

            ## Tools

            - `azure__search` with command `search_index_list` - List search indexes
            - `azure__speech` with command `speech_synthesize` - Text to speech
            """;
        var result = _parser.Parse("test", content);

        result.McpTools.Should().HaveCount(2);
        result.McpTools[0].ToolName.Should().Be("azure__search");
        result.McpTools[0].Command.Should().Be("search_index_list");
        result.McpTools[0].Purpose.Should().Be("List search indexes");
    }

    [Fact]
    public void Parse_McpTools_CodeBlockReferences_Extracted()
    {
        var content = """
            ---
            name: azure-diagnostics
            description: Debug Azure issues.
            ---

            # Azure Diagnostics

            ## MCP Tools

            ### AppLens

            ```
            mcp_azure_mcp_applens
              command: "diagnose"
            ```

            ### Azure Monitor

            ```
            mcp_azure_mcp_monitor
              command: "logs_query"
            ```
            """;
        var result = _parser.Parse("azure-diagnostics", content);

        result.McpTools.Should().HaveCountGreaterOrEqualTo(2);
        result.McpTools.Should().Contain(t => t.ToolName == "mcp_azure_mcp_applens");
        result.McpTools.Should().Contain(t => t.ToolName == "mcp_azure_mcp_monitor");
    }

    [Fact]
    public void Parse_McpTools_NoToolsSection_ReturnsEmpty()
    {
        var content = """
            ---
            name: test
            description: A skill with no MCP tools.
            ---

            # Test Skill

            ## Overview

            This skill has no MCP tools section.
            """;
        var result = _parser.Parse("test", content);

        result.McpTools.Should().BeEmpty();
    }

    [Fact]
    public void Parse_McpTools_Deduplication_PreservesRicherEntry()
    {
        var content = """
            ---
            name: test
            description: Test.
            ---

            ## MCP Tools

            | Tool | Purpose |
            |------|---------|
            | `mcp_azure_mcp_aks` | AKS MCP entry point for cluster operations |

            ## Also uses

            ```
            mcp_azure_mcp_aks
              command: "list"
            ```
            """;
        var result = _parser.Parse("test", content);

        // Should not have duplicates
        result.McpTools.Where(t => t.ToolName == "mcp_azure_mcp_aks").Should().HaveCount(1);
        // Should keep the richer table entry
        result.McpTools.First(t => t.ToolName == "mcp_azure_mcp_aks")
            .Purpose.Should().Contain("AKS MCP entry point");
    }

    [Fact]
    public void Parse_McpTools_InlineProseReferences_NotExtractedAsFalsePositive()
    {
        var content = """
            ---
            name: azure-rbac
            description: RBAC roles.
            ---

            # Azure RBAC

            Use the 'azure__documentation' tool to find the minimal role definition.
            Then use the 'azure__extension_cli_generate' tool to create a custom role.
            """;
        var result = _parser.Parse("azure-rbac", content);

        // Inline prose mentions should NOT be extracted as MCP tools
        result.McpTools.Should().BeEmpty();
    }

    [Fact]
    public void Parse_McpTools_QuickReferenceTable_Extracted()
    {
        var content = """
            ---
            name: azure-kubernetes
            description: AKS.
            ---

            ## Quick Reference

            | Property | Value |
            |----------|-------|
            | Best for | AKS cluster planning |
            | MCP Tools | `mcp_azure_mcp_aks` |
            | CLI | `az aks create` |
            """;
        var result = _parser.Parse("azure-kubernetes", content);

        result.McpTools.Should().HaveCount(1);
        result.McpTools[0].ToolName.Should().Be("mcp_azure_mcp_aks");
    }

    [Fact]
    public void Parse_McpTools_MixedFormats_AllExtracted()
    {
        var content = """
            ---
            name: test
            description: Test.
            ---

            ## MCP Tools

            | Tool | Purpose |
            |------|---------|
            | `mcp_azure_mcp_subscription_list` | List subscriptions |

            - `azure__search` with command `search_query` - Query search index
            """;
        var result = _parser.Parse("test", content);

        result.McpTools.Should().HaveCountGreaterOrEqualTo(2);
        result.McpTools.Should().Contain(t => t.ToolName == "mcp_azure_mcp_subscription_list");
        result.McpTools.Should().Contain(t => t.ToolName == "azure__search");
    }

    [Fact]
    public void Parse_McpTools_PurposeWithPipesOrSpecialChars_HandledGracefully()
    {
        var content = """
            ---
            name: test
            description: Test.
            ---

            ## MCP Tools

            | Tool | Purpose |
            |------|---------|
            | `mcp_azure_mcp_test` | List items (filter by name) |
            """;
        var result = _parser.Parse("test", content);

        result.McpTools.Should().HaveCount(1);
        result.McpTools[0].Purpose.Should().Be("List items (filter by name)");
    }

    [Fact]
    public void ExtractRbacRoles_AdversarialInput_ReturnsGracefully()
    {
        // Build a string with many uppercase-then-lowercase words that could cause backtracking
        var adversarial = string.Concat(Enumerable.Repeat("Aa ", 200)) + "Zzz";
        var body = $"Some text {adversarial} more text";

        // Should complete within the timeout (2s) and not hang — returns empty on timeout
        var result = SkillMarkdownParser.ExtractRbacRoles(body);

        result.Should().NotBeNull();
    }
}
