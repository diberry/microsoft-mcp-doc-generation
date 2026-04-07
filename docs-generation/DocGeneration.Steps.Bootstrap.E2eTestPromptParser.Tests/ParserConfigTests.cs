using System.Text.Json;
using DocGeneration.TestInfrastructure;
using E2eTestPromptParser.Models;
using Xunit;

namespace E2eTestPromptParser.Tests;

public class ParserConfigTests
{
    private static string GetConfigPath()
    {
        var docsGenRoot = ProjectRootFinder.FindDocsGenerationRoot();
        return Path.Combine(docsGenRoot,
            "DocGeneration.Steps.Bootstrap.E2eTestPromptParser",
            "config.json");
    }

    [Fact]
    public void Config_RemoteUrl_PointsTo2xBranch()
    {
        var configPath = GetConfigPath();
        Assert.True(File.Exists(configPath), $"config.json not found at {configPath}");

        var json = File.ReadAllText(configPath);
        var config = JsonSerializer.Deserialize<ParserConfig>(json);

        Assert.NotNull(config);
        Assert.Contains("release/azure/2.x", config!.RemoteUrl,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/main/", config.RemoteUrl,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Config_RemoteUrl_PointsToE2eTestPromptsFile()
    {
        var configPath = GetConfigPath();
        var json = File.ReadAllText(configPath);
        var config = JsonSerializer.Deserialize<ParserConfig>(json);

        Assert.NotNull(config);
        Assert.EndsWith("e2eTestPrompts.md", config!.RemoteUrl);
        Assert.Contains("servers/Azure.Mcp.Server/docs/", config.RemoteUrl);
    }

    [Fact]
    public void Config_RemoteUrl_IsValidRawGitHubUrl()
    {
        var configPath = GetConfigPath();
        var json = File.ReadAllText(configPath);
        var config = JsonSerializer.Deserialize<ParserConfig>(json);

        Assert.NotNull(config);
        Assert.StartsWith("https://raw.githubusercontent.com/microsoft/mcp/",
            config!.RemoteUrl);
    }

    [Fact]
    public void Config_LocalFileName_IsE2eTestPromptsMd()
    {
        var configPath = GetConfigPath();
        var json = File.ReadAllText(configPath);
        var config = JsonSerializer.Deserialize<ParserConfig>(json);

        Assert.NotNull(config);
        Assert.Equal("e2eTestPrompts.md", config!.LocalFileName);
    }
}
