using Xunit;
using ExamplePromptGeneratorStandalone.Models;

namespace ExamplePromptGeneratorStandalone.Tests;

public class ModelTests
{
    // ─────────────────────────────────────────────────
    // Tool model
    // ─────────────────────────────────────────────────

    [Fact]
    public void Tool_DefaultProperties()
    {
        var tool = new Tool();

        Assert.Null(tool.Name);
        Assert.Null(tool.Command);
        Assert.Null(tool.Description);
        Assert.Null(tool.Option);
    }

    [Fact]
    public void Tool_SetProperties()
    {
        var tool = new Tool
        {
            Name = "storage_account_list",
            Command = "storage account list",
            Description = "List storage accounts",
            Option = new List<Option>
            {
                new() { Name = "--subscription", Required = true, Description = "Azure subscription ID" }
            }
        };

        Assert.Equal("storage_account_list", tool.Name);
        Assert.Equal("storage account list", tool.Command);
        Assert.Equal("List storage accounts", tool.Description);
        Assert.Single(tool.Option!);
    }

    // ─────────────────────────────────────────────────
    // Option model
    // ─────────────────────────────────────────────────

    [Fact]
    public void Option_RequiredDefault()
    {
        var option = new Option();
        Assert.False(option.Required);
    }

    [Fact]
    public void Option_SetProperties()
    {
        var option = new Option
        {
            Name = "--vault-name",
            Required = true,
            Description = "The name of the key vault"
        };

        Assert.Equal("--vault-name", option.Name);
        Assert.True(option.Required);
        Assert.Equal("The name of the key vault", option.Description);
    }

    // ─────────────────────────────────────────────────
    // ExamplePromptsResponse model
    // ─────────────────────────────────────────────────

    [Fact]
    public void ExamplePromptsResponse_DefaultPromptsList()
    {
        var response = new ExamplePromptsResponse();

        Assert.Null(response.ToolName);
        Assert.NotNull(response.Prompts);
        Assert.Empty(response.Prompts);
    }

    [Fact]
    public void ExamplePromptsResponse_WithData()
    {
        var response = new ExamplePromptsResponse
        {
            ToolName = "cosmos db database list",
            Prompts = new List<string>
            {
                "List all databases in my Cosmos DB account",
                "Show databases in account mycosmosdb"
            }
        };

        Assert.Equal("cosmos db database list", response.ToolName);
        Assert.Equal(2, response.Prompts.Count);
    }

    // ─────────────────────────────────────────────────
    // CliOutput model
    // ─────────────────────────────────────────────────

    [Fact]
    public void CliOutput_DefaultResultsList()
    {
        var output = new CliOutput();

        Assert.NotNull(output.Results);
        Assert.Empty(output.Results);
    }

    [Fact]
    public void CliOutput_WithTools()
    {
        var output = new CliOutput
        {
            Results = new List<Tool>
            {
                new() { Command = "advisor recommendation list" },
                new() { Command = "storage account list" }
            }
        };

        Assert.Equal(2, output.Results.Count);
    }

    // ─────────────────────────────────────────────────
    // E2eTestPromptsData model
    // ─────────────────────────────────────────────────

    [Fact]
    public void E2eTestPromptsData_DefaultValues()
    {
        var data = new E2eTestPromptsData();

        Assert.Equal(string.Empty, data.Title);
        Assert.Equal(0, data.TotalSections);
        Assert.Equal(0, data.TotalTools);
        Assert.Equal(0, data.TotalPrompts);
        Assert.NotNull(data.Sections);
        Assert.Empty(data.Sections);
    }

    [Fact]
    public void E2eSection_DefaultValues()
    {
        var section = new E2eSection();

        Assert.Equal(string.Empty, section.Heading);
        Assert.Equal(0, section.ToolCount);
        Assert.Equal(0, section.PromptCount);
        Assert.NotNull(section.Tools);
        Assert.Empty(section.Tools);
    }

    [Fact]
    public void E2eToolEntry_DefaultValues()
    {
        var entry = new E2eToolEntry();

        Assert.Equal(string.Empty, entry.ToolName);
        Assert.NotNull(entry.TestPrompts);
        Assert.Empty(entry.TestPrompts);
    }
}
