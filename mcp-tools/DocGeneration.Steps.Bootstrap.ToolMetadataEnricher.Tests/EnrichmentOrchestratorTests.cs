using DocGeneration.Steps.Bootstrap.ToolMetadataEnricher.Models;
using DocGeneration.Steps.Bootstrap.ToolMetadataEnricher.Services;
using Xunit;

namespace DocGeneration.Steps.Bootstrap.ToolMetadataEnricher.Tests;

public sealed class EnrichmentOrchestratorTests
{
    [Fact]
    public void Enrich_FullPipeline_ProducesEnrichedOutput()
    {
        var azmcpCommands = new AzmcpCommandsDocument
        {
            GlobalOptions =
            [
                new AzmcpGlobalOption
                {
                    Name = "--subscription",
                    Default = "Environment variable AZURE_SUBSCRIPTION_ID",
                    ValuePlaceholder = "subscription-id"
                }
            ],
            ServiceSections =
            [
                new AzmcpServiceSection
                {
                    Heading = "Storage",
                    AreaName = "storage",
                    Commands =
                    [
                        new AzmcpCommand
                        {
                            CommandText = "azmcp storage account get",
                            RawBlock = "Example: azmcp storage account get --subscription <subscription-id>",
                            Parameters =
                            [
                                new AzmcpCommandParameter { Name = "--subscription" },
                                new AzmcpCommandParameter
                                {
                                    Name = "--output",
                                    ValuePlaceholder = "format",
                                    AllowedValues = ["json", "yaml"]
                                }
                            ]
                        },
                        new AzmcpCommand
                        {
                            CommandText = "azmcp storage account list",
                            RawBlock = "Example: azmcp storage account list --subscription <subscription-id>",
                            Parameters =
                            [
                                new AzmcpCommandParameter { Name = "--subscription" }
                            ]
                        }
                    ]
                }
            ]
        };

        var orchestrator = CreateOrchestrator(azmcpCommands);
        var cliOutput = new CliOutputDocument
        {
            Results =
            [
                CreateCliTool(
                    command: "storage account get",
                    description: "Gets a storage account. Requires at least one of --sku, --region.",
                    optionNames: ["--subscription", "--output"]),
                CreateCliTool(
                    command: "storage account list",
                    description: "Lists storage accounts.",
                    optionNames: ["--subscription"]),
                CreateCliTool(
                    command: "storage account delete",
                    description: "Deletes a storage account.",
                    optionNames: ["--subscription"])
            ]
        };

        var result = orchestrator.Enrich(cliOutput);

        Assert.Equal(3, result.Results.Count);

        var first = result.Results[0];
        Assert.True(first.Enrichment.Matched);
        var conditionalGroup = Assert.Single(first.Enrichment.ConditionalGroups);
        Assert.Equal(["--sku", "--region"], conditionalGroup.Parameters);
        Assert.Equal("AZURE_SUBSCRIPTION_ID environment variable", first.Enrichment.ParameterEnhancements["--subscription"].DefaultValue);
        Assert.Equal("format", first.Enrichment.ParameterEnhancements["--output"].ValuePlaceholder);
        Assert.Equal(["json", "yaml"], first.Enrichment.ParameterEnhancements["--output"].AllowedValues);
        Assert.Equal("Example: azmcp storage account get --subscription <subscription-id>", first.Enrichment.Examples);

        var second = result.Results[1];
        Assert.True(second.Enrichment.Matched);
        Assert.Equal("Example: azmcp storage account list --subscription <subscription-id>", second.Enrichment.Examples);

        var third = result.Results[2];
        Assert.False(third.Enrichment.Matched);
        Assert.Empty(third.Enrichment.ParameterEnhancements);
        Assert.Null(third.Enrichment.Examples);

        Assert.Equal(3, result.EnrichmentMetadata.TotalTools);
        Assert.Equal(2, result.EnrichmentMetadata.MatchedTools);
        Assert.Equal(1, result.EnrichmentMetadata.UnmatchedTools);
        Assert.Equal(1, result.EnrichmentMetadata.ConditionalGroupsFound);
        Assert.NotEqual(default, result.EnrichmentMetadata.Timestamp);
    }

    [Fact]
    public void Enrich_EmptyInput_ReturnsEmptyResults()
    {
        var orchestrator = CreateOrchestrator(new AzmcpCommandsDocument());
        var cliOutput = new CliOutputDocument();

        var result = orchestrator.Enrich(cliOutput);

        Assert.Empty(result.Results);
        Assert.Equal(0, result.EnrichmentMetadata.TotalTools);
        Assert.Equal(0, result.EnrichmentMetadata.MatchedTools);
        Assert.Equal(0, result.EnrichmentMetadata.UnmatchedTools);
        Assert.Equal(0, result.EnrichmentMetadata.ConditionalGroupsFound);
    }

    [Fact]
    public void Enrich_UnmatchedTools_SetsMatchedFalse()
    {
        var orchestrator = CreateOrchestrator(new AzmcpCommandsDocument());
        var cliOutput = new CliOutputDocument
        {
            Results =
            [
                CreateCliTool(
                    command: "storage account delete",
                    description: "Deletes a storage account.",
                    optionNames: ["--subscription"])
            ]
        };

        var result = orchestrator.Enrich(cliOutput);

        var tool = Assert.Single(result.Results);
        Assert.False(tool.Enrichment.Matched);
        Assert.Empty(tool.Enrichment.ConditionalGroups);
        Assert.Empty(tool.Enrichment.ParameterEnhancements);
        Assert.Null(tool.Enrichment.Examples);
    }

    [Fact]
    public void Enrich_Metadata_CountsCorrectly()
    {
        var azmcpCommands = new AzmcpCommandsDocument
        {
            ServiceSections =
            [
                new AzmcpServiceSection
                {
                    Heading = "Storage",
                    AreaName = "storage",
                    Commands =
                    [
                        new AzmcpCommand { CommandText = "azmcp storage account get" },
                        new AzmcpCommand { CommandText = "azmcp storage account list" }
                    ]
                }
            ]
        };

        var orchestrator = CreateOrchestrator(azmcpCommands);
        var cliOutput = new CliOutputDocument
        {
            Results =
            [
                CreateCliTool(
                    command: "storage account get",
                    description: "Requires at least one of --sku or --region.",
                    optionNames: []),
                CreateCliTool(
                    command: "storage account list",
                    description: "Requires at least one of --subscription or --tenant. Requires at least one of --output or --format.",
                    optionNames: []),
                CreateCliTool(
                    command: "storage account delete",
                    description: "Deletes a storage account.",
                    optionNames: [])
            ]
        };

        var result = orchestrator.Enrich(cliOutput);

        Assert.Equal(3, result.EnrichmentMetadata.TotalTools);
        Assert.Equal(2, result.EnrichmentMetadata.MatchedTools);
        Assert.Equal(1, result.EnrichmentMetadata.UnmatchedTools);
        Assert.Equal(3, result.EnrichmentMetadata.ConditionalGroupsFound);
    }

    private static EnrichmentOrchestrator CreateOrchestrator(AzmcpCommandsDocument azmcpCommands)
    {
        return new EnrichmentOrchestrator(
            new ToolMatcher(azmcpCommands),
            new ConditionalParamExtractor(),
            new ParameterEnricher(azmcpCommands.GlobalOptions));
    }

    private static CliOutputTool CreateCliTool(string command, string description, string[] optionNames)
    {
        return new CliOutputTool
        {
            Command = command,
            Name = command,
            Description = description,
            Area = "storage",
            Option = optionNames.Select(name => new CliOutputOption { Name = name }).ToList()
        };
    }
}
