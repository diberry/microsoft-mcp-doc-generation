// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using TemplateEngine;
using Xunit;

namespace HorizontalArticleGenerator.Tests;

/// <summary>
/// Tests that the skills section renders correctly in the horizontal article template.
/// Uses ProcessTemplateString to test the Handlebars template fragment directly.
/// </summary>
public class SkillsSectionTemplateTests
{
    private const string SkillsSectionTemplate = @"## Best practices

When using Azure MCP Server with {{serviceBrandName}}:

- **Be specific**: Include resource names.

{{#if skills}}
## GitHub Copilot extensions

The following GitHub Copilot extensions can help you work with {{serviceBrandName}}:

| Extension | Description |
|-----------|-------------|
{{#each skills}}
| [{{name}}]({{sourceUrl}}) | {{description}} |
{{/each}}
{{/if}}

## Related content

* [Azure MCP Server overview](../overview.md)";

    [Fact]
    public void SkillsSection_RendersWhenSkillsExist()
    {
        var data = new Dictionary<string, object>
        {
            ["serviceBrandName"] = "Azure Storage",
            ["skills"] = new List<Dictionary<string, object>>
            {
                new()
                {
                    ["name"] = "Azure Storage Explorer",
                    ["description"] = "Manage blobs and queues",
                    ["sourceUrl"] = "https://github.com/microsoft/skills/storage.md"
                },
                new()
                {
                    ["name"] = "Blob Helper",
                    ["description"] = "Work with blob containers",
                    ["sourceUrl"] = "https://github.com/test/blob.md"
                }
            }
        };

        var rendered = HandlebarsTemplateEngine.ProcessTemplateString(SkillsSectionTemplate, data);

        Assert.Contains("## GitHub Copilot extensions", rendered);
        Assert.Contains("The following GitHub Copilot extensions can help you work with Azure Storage:", rendered);
        Assert.Contains("| [Azure Storage Explorer](https://github.com/microsoft/skills/storage.md) | Manage blobs and queues |", rendered);
        Assert.Contains("| [Blob Helper](https://github.com/test/blob.md) | Work with blob containers |", rendered);
        Assert.Contains("| Extension | Description |", rendered);
    }

    [Fact]
    public void SkillsSection_OmittedWhenNoSkills()
    {
        var data = new Dictionary<string, object>
        {
            ["serviceBrandName"] = "Azure Cosmos DB"
        };

        var rendered = HandlebarsTemplateEngine.ProcessTemplateString(SkillsSectionTemplate, data);

        Assert.DoesNotContain("## GitHub Copilot extensions", rendered);
        Assert.DoesNotContain("Extension | Description", rendered);
        Assert.Contains("## Best practices", rendered);
        Assert.Contains("## Related content", rendered);
    }

    [Fact]
    public void SkillsSection_OmittedWhenSkillsEmpty()
    {
        var data = new Dictionary<string, object>
        {
            ["serviceBrandName"] = "Azure Key Vault",
            ["skills"] = new List<Dictionary<string, object>>()
        };

        var rendered = HandlebarsTemplateEngine.ProcessTemplateString(SkillsSectionTemplate, data);

        Assert.DoesNotContain("## GitHub Copilot extensions", rendered);
    }

    [Fact]
    public void SkillsSection_PlacedAfterBestPracticesBeforeRelatedContent()
    {
        var data = new Dictionary<string, object>
        {
            ["serviceBrandName"] = "Azure Monitor",
            ["skills"] = new List<Dictionary<string, object>>
            {
                new()
                {
                    ["name"] = "Monitor Skill",
                    ["description"] = "Monitor things",
                    ["sourceUrl"] = "https://github.com/test/monitor.md"
                }
            }
        };

        var rendered = HandlebarsTemplateEngine.ProcessTemplateString(SkillsSectionTemplate, data);

        var bestPracticesIndex = rendered.IndexOf("## Best practices");
        var skillsIndex = rendered.IndexOf("## GitHub Copilot extensions");
        var relatedIndex = rendered.IndexOf("## Related content");

        Assert.True(bestPracticesIndex < skillsIndex, "Skills section should appear after Best practices");
        Assert.True(skillsIndex < relatedIndex, "Skills section should appear before Related content");
    }

    [Fact]
    public void SkillsSection_EscapedPipesRenderCorrectly()
    {
        var data = new Dictionary<string, object>
        {
            ["serviceBrandName"] = "Azure Redis",
            ["skills"] = new List<Dictionary<string, object>>
            {
                new()
                {
                    ["name"] = "Redis Skill",
                    ["description"] = @"Manage cache \| sessions",
                    ["sourceUrl"] = "https://github.com/test/redis.md"
                }
            }
        };

        var rendered = HandlebarsTemplateEngine.ProcessTemplateString(SkillsSectionTemplate, data);

        Assert.Contains(@"Manage cache \| sessions", rendered);
    }
}
