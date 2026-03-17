using DocGeneration.Steps.Bootstrap.ToolMetadataEnricher.Models;
using DocGeneration.Steps.Bootstrap.ToolMetadataEnricher.Services;
using Xunit;

namespace DocGeneration.Steps.Bootstrap.ToolMetadataEnricher.Tests;

public sealed class ParameterEnricherTests
{
    [Fact]
    public void Enrich_WithMatchingParameters_AddsDefaults()
    {
        var enricher = new ParameterEnricher([]);
        var cliTool = CreateCliTool("--subscription");
        var azmcpCommand = new AzmcpCommand
        {
            Parameters =
            [
                new AzmcpCommandParameter
                {
                    Name = "  --Subscription  ",
                    Default = "'sub-123'",
                    ValuePlaceholder = "subscription-id"
                }
            ]
        };

        var result = enricher.Enrich(cliTool, azmcpCommand);

        var enhancementEntry = Assert.Single(result);
        Assert.Equal("--subscription", enhancementEntry.Key);
        Assert.Equal("sub-123", enhancementEntry.Value.DefaultValue);
        Assert.Equal("subscription-id", enhancementEntry.Value.ValuePlaceholder);
        Assert.Null(enhancementEntry.Value.AllowedValues);
    }

    [Fact]
    public void Enrich_WithGlobalOptionDefaults_FallsBack()
    {
        var enricher = new ParameterEnricher(
        [
            new AzmcpGlobalOption
            {
                Name = "--subscription",
                Default = "Environment variable AZURE_SUBSCRIPTION_ID",
                ValuePlaceholder = "subscription-id"
            }
        ]);

        var cliTool = CreateCliTool("--subscription");
        var azmcpCommand = new AzmcpCommand
        {
            Parameters =
            [
                new AzmcpCommandParameter
                {
                    Name = "--subscription"
                }
            ]
        };

        var result = enricher.Enrich(cliTool, azmcpCommand);

        var enhancement = Assert.Single(result).Value;
        Assert.Equal("AZURE_SUBSCRIPTION_ID environment variable", enhancement.DefaultValue);
        Assert.Equal("subscription-id", enhancement.ValuePlaceholder);
    }

    [Fact]
    public void Enrich_WithAllowedValues_IncludesThem()
    {
        var enricher = new ParameterEnricher([]);
        var cliTool = CreateCliTool("--output");
        var azmcpCommand = new AzmcpCommand
        {
            Parameters =
            [
                new AzmcpCommandParameter
                {
                    Name = "--output",
                    AllowedValues = ["json", "yaml", "json", "  "]
                }
            ]
        };

        var result = enricher.Enrich(cliTool, azmcpCommand);

        var enhancement = Assert.Single(result).Value;
        Assert.Equal(["json", "yaml"], enhancement.AllowedValues);
    }

    [Fact]
    public void Enrich_WithNoEnhancements_ReturnsEmpty()
    {
        var enricher = new ParameterEnricher([]);
        var cliTool = CreateCliTool("--subscription");
        var azmcpCommand = new AzmcpCommand
        {
            Parameters =
            [
                new AzmcpCommandParameter
                {
                    Name = "--subscription",
                    IsRequired = true
                }
            ]
        };

        var result = enricher.Enrich(cliTool, azmcpCommand);

        Assert.Empty(result);
    }

    [Fact]
    public void Enrich_WithDashDefault_ReturnsNull()
    {
        var enricher = new ParameterEnricher([]);
        var cliTool = CreateCliTool("--subscription");
        var azmcpCommand = new AzmcpCommand
        {
            Parameters =
            [
                new AzmcpCommandParameter
                {
                    Name = "--subscription",
                    Default = "-",
                    ValuePlaceholder = "subscription-id"
                }
            ]
        };

        var result = enricher.Enrich(cliTool, azmcpCommand);

        var enhancement = Assert.Single(result).Value;
        Assert.Null(enhancement.DefaultValue);
        Assert.Equal("subscription-id", enhancement.ValuePlaceholder);
    }

    [Theory]
    [InlineData("  --Subscription  ", "--subscription")]
    [InlineData(" REGION ", "region")]
    public void NormalizeParameterName_TrimsAndLowercases(string? parameterName, string expected)
    {
        Assert.Equal(expected, ParameterEnricher.NormalizeParameterName(parameterName));
    }

    private static CliOutputTool CreateCliTool(params string[] optionNames)
    {
        return new CliOutputTool
        {
            Option = optionNames.Select(name => new CliOutputOption { Name = name }).ToList()
        };
    }
}
