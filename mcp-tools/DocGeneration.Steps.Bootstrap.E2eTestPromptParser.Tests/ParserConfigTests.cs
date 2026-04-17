using System.Text.Json;
using DocGeneration.TestInfrastructure;
using E2eTestPromptParser.Models;
using Xunit;

namespace E2eTestPromptParser.Tests;

public class ParserConfigTests
{
    private static string GetConfigPath()
    {
        var docsGenRoot = ProjectRootFinder.FindMcpToolsRoot();
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
        Assert.Contains("release/azure/2.x", config!.RemoteUrl, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/main/", config.RemoteUrl, StringComparison.OrdinalIgnoreCase);
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
        Assert.StartsWith("https://raw.githubusercontent.com/microsoft/mcp/", config!.RemoteUrl);
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

    [Fact]
    public void Config_RemoteUrlTemplate_HasBranchPlaceholder()
    {
        var configPath = GetConfigPath();
        var json = File.ReadAllText(configPath);
        var config = JsonSerializer.Deserialize<ParserConfig>(json);

        Assert.NotNull(config);
        Assert.NotNull(config!.RemoteUrlTemplate);
        Assert.Contains("{branch}", config.RemoteUrlTemplate);
    }

    // --- GetEffectiveUrl tests ---

    [Fact]
    public void GetEffectiveUrl_NoBranchOverride_ReturnsFallbackRemoteUrl()
    {
        var config = new ParserConfig
        {
            RemoteUrl = "https://example.com/main/file.md",
            RemoteUrlTemplate = "https://example.com/{branch}/file.md"
        };

        Assert.Equal("https://example.com/main/file.md", config.GetEffectiveUrl(null));
    }

    [Fact]
    public void GetEffectiveUrl_WithBranchOverride_SubstitutesTemplate()
    {
        var config = new ParserConfig
        {
            RemoteUrl = "https://example.com/main/file.md",
            RemoteUrlTemplate = "https://example.com/{branch}/file.md"
        };

        Assert.Equal("https://example.com/release/azure/2.x/file.md",
            config.GetEffectiveUrl("release/azure/2.x"));
    }

    [Fact]
    public void GetEffectiveUrl_WithBranchOverrideButNoTemplate_ReturnsFallbackRemoteUrl()
    {
        var config = new ParserConfig
        {
            RemoteUrl = "https://example.com/main/file.md",
            RemoteUrlTemplate = null
        };

        Assert.Equal("https://example.com/main/file.md", config.GetEffectiveUrl("release/azure/2.x"));
    }

    [Fact]
    public void GetEffectiveUrl_BlankBranchOverride_ReturnsFallbackRemoteUrl()
    {
        var config = new ParserConfig
        {
            RemoteUrl = "https://example.com/main/file.md",
            RemoteUrlTemplate = "https://example.com/{branch}/file.md"
        };

        Assert.Equal("https://example.com/main/file.md", config.GetEffectiveUrl("  "));
    }

    [Fact]
    public void GetEffectiveUrl_TrimsOverrideBranch()
    {
        var config = new ParserConfig
        {
            RemoteUrl = "https://example.com/main/file.md",
            RemoteUrlTemplate = "https://example.com/{branch}/file.md"
        };

        // Use a branch different from fallback URL to prove template substitution actually happened
        Assert.Equal("https://example.com/release/azure/2.x/file.md",
            config.GetEffectiveUrl("  release/azure/2.x  "));
    }
}
