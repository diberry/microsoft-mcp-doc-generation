using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SkillsGen.Core.Generation;
using SkillsGen.Core.Models;
using Xunit;

namespace SkillsGen.Core.Tests.Unit.Generation;

public class SkillPageGeneratorTests
{
    private static readonly string SimpleTemplate = @"---
title: {{displayName}}
description: {{description}}
---
# {{displayName}}

{{description}}

## Prerequisites

- **GitHub Copilot** — Required.
{{#if hasUseFor}}

## When to use this skill

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
        result.Should().Contain("title: Azure Storage");
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
