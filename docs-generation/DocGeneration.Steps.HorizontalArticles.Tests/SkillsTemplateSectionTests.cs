// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using HandlebarsDotNet;
using Xunit;

namespace HorizontalArticleGenerator.Tests;

public class SkillsTemplateSectionTests
{
    private const string SkillsSectionTemplate = @"## Best practices

When using Azure MCP Server with {{serviceBrandName}}:

- **Be specific**: Include resource names and details.

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
    public void Template_WithSkills_RendersSkillsSection()
    {
        var handlebars = Handlebars.Create();
        var template = handlebars.Compile(SkillsSectionTemplate);

        var data = new Dictionary<string, object>
        {
            ["serviceBrandName"] = "Azure Storage",
            ["skills"] = new List<Dictionary<string, object>>
            {
                new()
                {
                    ["name"] = "Copilot for Azure",
                    ["sourceUrl"] = "https://github.com/microsoft/copilot-azure",
                    ["description"] = "Azure resource management skill"
                },
                new()
                {
                    ["name"] = "Storage Helper",
                    ["sourceUrl"] = "https://github.com/test/storage-helper",
                    ["description"] = "Blob and queue operations"
                }
            }
        };

        var result = template(data);

        Assert.Contains("## GitHub Copilot extensions", result);
        Assert.Contains("The following GitHub Copilot extensions can help you work with Azure Storage:", result);
        Assert.Contains("| [Copilot for Azure](https://github.com/microsoft/copilot-azure) | Azure resource management skill |", result);
        Assert.Contains("| [Storage Helper](https://github.com/test/storage-helper) | Blob and queue operations |", result);
        Assert.Contains("| Extension | Description |", result);
    }

    [Fact]
    public void Template_WithoutSkills_DoesNotRenderSkillsSection()
    {
        var handlebars = Handlebars.Create();
        var template = handlebars.Compile(SkillsSectionTemplate);

        var data = new Dictionary<string, object>
        {
            ["serviceBrandName"] = "Azure Cosmos DB",
            ["skills"] = new List<Dictionary<string, object>>()
        };

        var result = template(data);

        Assert.DoesNotContain("## GitHub Copilot extensions", result);
        Assert.DoesNotContain("Extension | Description", result);
        Assert.Contains("## Best practices", result);
        Assert.Contains("## Related content", result);
    }

    [Fact]
    public void Template_SkillsNotProvided_DoesNotRenderSkillsSection()
    {
        var handlebars = Handlebars.Create();
        var template = handlebars.Compile(SkillsSectionTemplate);

        var data = new Dictionary<string, object>
        {
            ["serviceBrandName"] = "Azure Key Vault"
        };

        var result = template(data);

        Assert.DoesNotContain("## GitHub Copilot extensions", result);
        Assert.Contains("## Best practices", result);
        Assert.Contains("## Related content", result);
    }

    [Fact]
    public void Template_SingleSkill_RendersOneTableRow()
    {
        var handlebars = Handlebars.Create();
        var template = handlebars.Compile(SkillsSectionTemplate);

        var data = new Dictionary<string, object>
        {
            ["serviceBrandName"] = "Azure Monitor",
            ["skills"] = new List<Dictionary<string, object>>
            {
                new()
                {
                    ["name"] = "Monitor Diagnostics",
                    ["sourceUrl"] = "https://github.com/test/monitor",
                    ["description"] = "Diagnostic queries and alerts"
                }
            }
        };

        var result = template(data);

        Assert.Contains("## GitHub Copilot extensions", result);
        Assert.Contains("| [Monitor Diagnostics](https://github.com/test/monitor) | Diagnostic queries and alerts |", result);
    }

    [Fact]
    public void Template_SkillWithEscapedPipe_RendersCorrectly()
    {
        var handlebars = Handlebars.Create();
        var template = handlebars.Compile(SkillsSectionTemplate);

        var data = new Dictionary<string, object>
        {
            ["serviceBrandName"] = "Azure Functions",
            ["skills"] = new List<Dictionary<string, object>>
            {
                new()
                {
                    ["name"] = "Functions Helper",
                    ["sourceUrl"] = "https://github.com/test/functions",
                    ["description"] = @"Handles input \| output bindings"
                }
            }
        };

        var result = template(data);

        Assert.Contains(@"Handles input \| output bindings", result);
    }
}
