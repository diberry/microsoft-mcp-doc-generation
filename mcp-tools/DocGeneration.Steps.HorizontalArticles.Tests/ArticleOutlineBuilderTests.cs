using System.Text.Json;
using CSharpGenerator.Models;
using HorizontalArticleGenerator.Builders;
using HorizontalArticleGenerator.Models;
using Xunit;

namespace DocGeneration.Steps.HorizontalArticles.Tests;

public sealed class ArticleOutlineBuilderTests : IDisposable
{
    private readonly string _testRoot;

    public ArticleOutlineBuilderTests()
    {
        _testRoot = Path.Combine(AppContext.BaseDirectory, "article-outline-builder-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testRoot);
    }

    [Fact]
    public async Task BuildAsync_ExtractsStandardSectionsForTargetNamespace()
    {
        await WriteCliOutputAsync(new CliOutput
        {
            Results =
            [
                new Tool
                {
                    Command = "compute vm create",
                    Name = "compute vm create",
                    Description = "Create virtual machines.",
                    Option =
                    [
                        new Option { Name = "--name" },
                        new Option { Name = "--subscription" }
                    ],
                    Metadata = new ToolMetadata
                    {
                        Destructive = new MetadataValue { Value = true },
                        ReadOnly = new MetadataValue { Value = false },
                        Secret = new MetadataValue { Value = false }
                    }
                },
                new Tool
                {
                    Command = "compute vm list",
                    Name = "compute vm list",
                    Description = "List virtual machines.",
                    Option =
                    [
                        new Option { Name = "--resource-group" }
                    ],
                    Metadata = new ToolMetadata
                    {
                        Destructive = new MetadataValue { Value = false },
                        ReadOnly = new MetadataValue { Value = true },
                        Secret = new MetadataValue { Value = false }
                    }
                },
                new Tool
                {
                    Command = "storage account list",
                    Name = "storage account list",
                    Description = "List storage accounts."
                }
            ]
        });

        var builder = new ArticleOutlineBuilder();

        var result = await builder.BuildAsync(_testRoot, "compute", CancellationToken.None);

        Assert.Equal("compute", result.ServiceIdentifier);
        Assert.Equal(
            ["Introduction", "Prerequisites", "Tool overview", "Common scenarios", "Best practices"],
            result.Sections.Select(section => section.Heading).ToArray());
        Assert.Equal(
            ["overview", "checklist", "reference", "scenario-list", "guidance"],
            result.Sections.Select(section => section.ContentType).ToArray());
    }

    [Fact]
    public async Task BuildAsync_MissingCliOutput_ReturnsStandardOutlineWithoutThrowing()
    {
        var builder = new ArticleOutlineBuilder();

        var result = await builder.BuildAsync(_testRoot, "storage", CancellationToken.None);

        Assert.Equal("storage", result.ServiceIdentifier);
        Assert.Equal(5, result.Sections.Count);
        Assert.Empty(result.Sections.Single(section => section.Heading == "Tool overview").EvidenceItems);
    }

    [Fact]
    public async Task BuildAsync_SchemaVersion_IsAlwaysOnePointZero()
    {
        await WriteCliOutputAsync(new CliOutput
        {
            Results =
            [
                new Tool
                {
                    Command = "search index list",
                    Name = "search index list",
                    Description = "List indexes."
                }
            ]
        });

        var builder = new ArticleOutlineBuilder();

        var result = await builder.BuildAsync(_testRoot, "search", CancellationToken.None);

        Assert.Equal("1.0", result.SchemaVersion);
    }

    [Fact]
    public async Task BuildAsync_PopulatesExpectedEvidenceItemsPerSection()
    {        await WriteCliOutputAsync(new CliOutput
        {
            Results =
            [
                new Tool
                {
                    Command = "compute vm create",
                    Name = "compute vm create",
                    Description = "Create virtual machines.",
                    Option =
                    [
                        new Option { Name = "--name" },
                        new Option { Name = "--auth-method" }
                    ],
                    Metadata = new ToolMetadata
                    {
                        Destructive = new MetadataValue { Value = true },
                        ReadOnly = new MetadataValue { Value = false },
                        Secret = new MetadataValue { Value = true }
                    }
                },
                new Tool
                {
                    Command = "compute vm list",
                    Name = "compute vm list",
                    Description = "List virtual machines.",
                    Metadata = new ToolMetadata
                    {
                        Destructive = new MetadataValue { Value = false },
                        ReadOnly = new MetadataValue { Value = true },
                        Secret = new MetadataValue { Value = false }
                    }
                }
            ]
        });

        var builder = new ArticleOutlineBuilder();

        var result = await builder.BuildAsync(_testRoot, "compute", CancellationToken.None);
        var introduction = result.Sections.Single(section => section.Heading == "Introduction");
        var prerequisites = result.Sections.Single(section => section.Heading == "Prerequisites");
        var toolOverview = result.Sections.Single(section => section.Heading == "Tool overview");
        var scenarios = result.Sections.Single(section => section.Heading == "Common scenarios");
        var bestPractices = result.Sections.Single(section => section.Heading == "Best practices");

        Assert.Contains("xref:../tool-family/azure-compute.md", introduction.EvidenceItems);
        Assert.Contains("capability:management", introduction.EvidenceItems);
        Assert.Contains("capability:data", introduction.EvidenceItems);

        Assert.Contains("capability:secret", prerequisites.EvidenceItems);
        Assert.Contains("xref:../parameters/compute-vm-create-parameters.md", prerequisites.EvidenceItems);

        Assert.Equal(2, toolOverview.EvidenceItems.Count);
        using var createTool = JsonDocument.Parse(toolOverview.EvidenceItems[0]);
        Assert.Equal("tool", createTool.RootElement.GetProperty("kind").GetString());
        Assert.Equal("compute vm create", createTool.RootElement.GetProperty("command").GetString());
        Assert.Equal(1, createTool.RootElement.GetProperty("parameterCount").GetInt32());
        Assert.Equal("management", createTool.RootElement.GetProperty("plane").GetString());

        Assert.Contains("scenario-tool:compute vm create", scenarios.EvidenceItems);
        Assert.Contains("scenario-tool:compute vm list", scenarios.EvidenceItems);

        Assert.Contains("capability:destructive", bestPractices.EvidenceItems);
        Assert.Contains("capability:secret", bestPractices.EvidenceItems);
    }

    [Fact]
    public async Task BuildAsync_MissingCliOutput_ArticleTitleIsDerivedFromNamespaceTitleCase()
    {
        // No cli-output.json written — builder derives the article title from the namespace.
        var builder = new ArticleOutlineBuilder();

        var result = await builder.BuildAsync(_testRoot, "myservice", CancellationToken.None);

        Assert.Equal("Myservice", result.ArticleTitle);
        Assert.Equal("myservice", result.ServiceIdentifier);
    }

    [Fact]
    public async Task BuildAsync_FiltersCanonicalCommonParameters_FromParameterCount()
    {
        // Real CLI output uses "--"-prefixed option names. Common parameters must be filtered
        // using the canonical names from common-parameters.json, leaving only tool-specific params.
        await WriteCliOutputAsync(new CliOutput
        {
            Results =
            [
                new Tool
                {
                    Command = "acr registry list",
                    Name = "acr registry list",
                    Description = "List container registries.",
                    Option =
                    [
                        new Option { Name = "--tenant" },
                        new Option { Name = "--subscription" },
                        new Option { Name = "--resource-group" },
                        new Option { Name = "--retry-delay" },
                        new Option { Name = "--learn" },
                        new Option { Name = "--registry" }
                    ]
                }
            ]
        });

        var builder = new ArticleOutlineBuilder();

        var result = await builder.BuildAsync(_testRoot, "acr", CancellationToken.None);

        var toolOverview = result.Sections.Single(section => section.Heading == "Tool overview");
        using var tool = JsonDocument.Parse(toolOverview.EvidenceItems.Single());
        // Only "--registry" is tool-specific; the five common parameters are filtered out.
        Assert.Equal(1, tool.RootElement.GetProperty("parameterCount").GetInt32());
    }

    [Fact]
    public async Task BuildAsync_MalformedCliOutput_ReturnsStandardOutlineWithoutThrowing()
    {
        var cliDirectory = Path.Combine(_testRoot, "cli");
        Directory.CreateDirectory(cliDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(cliDirectory, "cli-output.json"),
            "{ \"results\": [ { \"command\": \"acr registry list\"  <<< not valid json");

        var builder = new ArticleOutlineBuilder();

        var result = await builder.BuildAsync(_testRoot, "acr", CancellationToken.None);

        Assert.Equal("acr", result.ServiceIdentifier);
        Assert.Equal(5, result.Sections.Count);
        Assert.Empty(result.Sections.Single(section => section.Heading == "Tool overview").EvidenceItems);
    }

    private async Task WriteCliOutputAsync(CliOutput cliOutput)
    {
        var cliDirectory = Path.Combine(_testRoot, "cli");
        Directory.CreateDirectory(cliDirectory);
        await File.WriteAllTextAsync(Path.Combine(cliDirectory, "cli-output.json"), JsonSerializer.Serialize(cliOutput));
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, recursive: true);
        }
    }
}
