using DocGeneration.Steps.Bootstrap.ToolMetadataEnricher.Models;
using DocGeneration.Steps.Bootstrap.ToolMetadataEnricher.Services;
using Xunit;

namespace DocGeneration.Steps.Bootstrap.ToolMetadataEnricher.Tests;

public sealed class ToolMatcherTests
{
    [Fact]
    public void Match_ExactCommandName_ReturnsCommand()
    {
        var expected = new AzmcpCommand { CommandText = "azmcp storage account get" };
        var matcher = CreateMatcher(expected);

        var result = matcher.Match("storage account get");

        Assert.Same(expected, result);
    }

    [Fact]
    public void Match_WithAzmcpPrefix_Normalizes()
    {
        var expected = new AzmcpCommand { CommandText = "azmcp storage account get" };
        var matcher = CreateMatcher(expected);

        var result = matcher.Match("azmcp storage account get");

        Assert.Same(expected, result);
    }

    [Fact]
    public void Match_CaseInsensitive_Matches()
    {
        var expected = new AzmcpCommand { CommandText = "azmcp storage account get" };
        var matcher = CreateMatcher(expected);

        var result = matcher.Match("StoRage AccoUnt GET");

        Assert.Same(expected, result);
    }

    [Fact]
    public void Match_NoMatch_ReturnsNull()
    {
        var matcher = CreateMatcher(new AzmcpCommand { CommandText = "azmcp storage account get" });

        var result = matcher.Match("storage account delete");

        Assert.Null(result);
    }

    [Fact]
    public void Match_ExampleCommand_Excluded()
    {
        var matcher = CreateMatcher(new AzmcpCommand
        {
            CommandText = "azmcp storage account get",
            IsExample = true
        });

        var result = matcher.Match("storage account get");

        Assert.Null(result);
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    public void NormalizeCommand_NullOrEmpty_ReturnsEmpty(string? commandText, string expected)
    {
        Assert.Equal(expected, ToolMatcher.NormalizeCommand(commandText));
    }

    [Fact]
    public void NormalizeCommand_StripsAzmcpPrefix()
    {
        var result = ToolMatcher.NormalizeCommand("  AzMcP storage account get  ");

        Assert.Equal("storage account get", result);
    }

    [Fact]
    public void NormalizeCommand_CollapsesWhitespace()
    {
        var result = ToolMatcher.NormalizeCommand("  storage\t  account   get  ");

        Assert.Equal("storage account get", result);
    }

    private static ToolMatcher CreateMatcher(params AzmcpCommand[] commands)
    {
        return new ToolMatcher(new AzmcpCommandsDocument
        {
            ServiceSections =
            [
                new AzmcpServiceSection
                {
                    Heading = "Storage",
                    AreaName = "storage",
                    Commands = [.. commands]
                }
            ]
        });
    }
}
