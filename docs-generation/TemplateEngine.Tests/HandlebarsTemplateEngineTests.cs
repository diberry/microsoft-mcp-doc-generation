// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;

namespace TemplateEngine.Tests;

public class HandlebarsTemplateEngineTests
{
    [Fact]
    public void CreateEngine_RegistersHelpers_WithoutException()
    {
        var engine = HandlebarsTemplateEngine.CreateEngine();
        Assert.NotNull(engine);
    }

    [Fact]
    public void ProcessTemplateString_BasicSubstitution()
    {
        var data = new Dictionary<string, object> { ["name"] = "Azure" };
        var result = HandlebarsTemplateEngine.ProcessTemplateString("Hello {{name}}", data);
        Assert.Equal("Hello Azure", result);
    }

    [Fact]
    public void ProcessTemplateString_MultipleVariables()
    {
        var data = new Dictionary<string, object>
        {
            ["service"] = "Storage",
            ["operation"] = "list"
        };
        var result = HandlebarsTemplateEngine.ProcessTemplateString(
            "{{service}} {{operation}}", data);
        Assert.Equal("Storage list", result);
    }

    [Fact]
    public void ProcessTemplateString_EachLoop()
    {
        var data = new Dictionary<string, object>
        {
            ["items"] = new List<string> { "a", "b", "c" }
        };
        var result = HandlebarsTemplateEngine.ProcessTemplateString(
            "{{#each items}}{{this}},{{/each}}", data);
        Assert.Equal("a,b,c,", result);
    }

    [Fact]
    public void ProcessTemplateString_IfConditional_True()
    {
        var data = new Dictionary<string, object> { ["flag"] = true };
        var result = HandlebarsTemplateEngine.ProcessTemplateString(
            "{{#if flag}}yes{{else}}no{{/if}}", data);
        Assert.Equal("yes", result);
    }

    [Fact]
    public void ProcessTemplateString_IfConditional_False()
    {
        var data = new Dictionary<string, object> { ["flag"] = false };
        var result = HandlebarsTemplateEngine.ProcessTemplateString(
            "{{#if flag}}yes{{else}}no{{/if}}", data);
        Assert.Equal("no", result);
    }

    [Fact]
    public void ProcessTemplateString_MissingVariable_ProducesEmptyString()
    {
        var data = new Dictionary<string, object> { ["name"] = "Azure" };
        var result = HandlebarsTemplateEngine.ProcessTemplateString(
            "Hello {{missing}}", data);
        Assert.Equal("Hello ", result);
    }

    [Fact]
    public void ProcessTemplateString_EmptyData()
    {
        var data = new Dictionary<string, object>();
        var result = HandlebarsTemplateEngine.ProcessTemplateString("static text", data);
        Assert.Equal("static text", result);
    }

    [Fact]
    public async Task ProcessTemplateAsync_ReadsFileAndRendersTemplate()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, "Tool: {{tool}}");
            var data = new Dictionary<string, object> { ["tool"] = "azmcp storage list" };

            var result = await HandlebarsTemplateEngine.ProcessTemplateAsync(tempFile, data);

            Assert.Equal("Tool: azmcp storage list", result);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ProcessTemplateAsync_FileWithHelpers()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile,
                "{{kebabCase name}} - {{requiredIcon isRequired}}");
            var data = new Dictionary<string, object>
            {
                ["name"] = "My Tool Name",
                ["isRequired"] = true
            };

            var result = await HandlebarsTemplateEngine.ProcessTemplateAsync(tempFile, data);

            // Handlebars HTML-encodes emoji characters
            Assert.Equal("my-tool-name - &#9989;", result);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
