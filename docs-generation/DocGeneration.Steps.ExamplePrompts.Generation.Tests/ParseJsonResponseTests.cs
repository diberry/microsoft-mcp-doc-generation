using Xunit;
using ExamplePromptGeneratorStandalone.Generators;

namespace ExamplePromptGeneratorStandalone.Tests;

public class ParseJsonResponseTests
{
    [Fact]
    public void ParsesValidJson_SingleToolEntry()
    {
        var json = """{"storage account list": ["List storage accounts", "Show all accounts"]}""";

        var result = ExamplePromptGenerator.ParseJsonResponse(json);

        Assert.NotNull(result);
        Assert.Equal("storage account list", result!.ToolName);
        Assert.Equal(2, result.Prompts.Count);
        Assert.Equal("List storage accounts", result.Prompts[0]);
    }

    [Fact]
    public void ParsesValidJson_MultipleToolEntries_TakesFirst()
    {
        var json = """
            {
              "advisor recommendation list": ["List recommendations"],
              "advisor recommendation get": ["Get a recommendation"]
            }
            """;

        var result = ExamplePromptGenerator.ParseJsonResponse(json);

        Assert.NotNull(result);
        Assert.Equal("advisor recommendation list", result!.ToolName);
        Assert.Single(result.Prompts);
    }

    [Fact]
    public void ParsesJson_WithTrailingComma()
    {
        var json = """
            {
              "redis cache list": [
                "List all Redis caches",
                "Show Redis instances in rg-prod",
              ]
            }
            """;

        var result = ExamplePromptGenerator.ParseJsonResponse(json);

        Assert.NotNull(result);
        Assert.Equal(2, result!.Prompts.Count);
    }

    [Fact]
    public void ParsesJson_WrappedInCodeBlock()
    {
        var response = """
            Here are the prompts:
            ```json
            {"cosmos db database list": ["List databases in my Cosmos DB account"]}
            ```
            """;

        var result = ExamplePromptGenerator.ParseJsonResponse(response);

        Assert.NotNull(result);
        Assert.Equal("cosmos db database list", result!.ToolName);
    }

    [Fact]
    public void ParsesJson_WithPreambleText()
    {
        var response = """
            STEP 1: Analyzing parameters
            STEP 2: Generating prompts

            {"monitor alert list": ["List all active alerts", "Show alerts in resource group rg-dev"]}
            """;

        var result = ExamplePromptGenerator.ParseJsonResponse(response);

        Assert.NotNull(result);
        Assert.Equal("monitor alert list", result!.ToolName);
        Assert.Equal(2, result.Prompts.Count);
    }

    [Fact]
    public void ReturnsNull_ForEmptyJson()
    {
        var result = ExamplePromptGenerator.ParseJsonResponse("{}");
        Assert.Null(result);
    }

    [Fact]
    public void ReturnsNull_ForNullInput()
    {
        var result = ExamplePromptGenerator.ParseJsonResponse(null!);
        Assert.Null(result);
    }

    [Fact]
    public void ReturnsNull_ForEmptyString()
    {
        var result = ExamplePromptGenerator.ParseJsonResponse("");
        Assert.Null(result);
    }

    [Fact]
    public void ReturnsNull_ForInvalidJson()
    {
        var result = ExamplePromptGenerator.ParseJsonResponse("not json at all");
        Assert.Null(result);
    }

    [Fact]
    public void ParsesJson_FivePrompts()
    {
        var json = """
            {
              "aks cluster list": [
                "List all AKS clusters in my subscription",
                "Show Kubernetes clusters in resource group rg-aks",
                "What AKS clusters do I have in westus2",
                "Display all managed Kubernetes clusters",
                "List AKS clusters with their current status"
              ]
            }
            """;

        var result = ExamplePromptGenerator.ParseJsonResponse(json);

        Assert.NotNull(result);
        Assert.Equal("aks cluster list", result!.ToolName);
        Assert.Equal(5, result.Prompts.Count);
    }
}
