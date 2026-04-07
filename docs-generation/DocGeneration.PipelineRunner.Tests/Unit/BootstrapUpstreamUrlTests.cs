using PipelineRunner.Steps;
using Xunit;

namespace PipelineRunner.Tests.Unit;

public class BootstrapUpstreamUrlTests
{
    [Fact]
    public void BuildUpstreamUrl_2xBranch_ReturnsCorrectUrl()
    {
        var url = BootstrapStep.BuildUpstreamUrl("release/azure/2.x", "azmcp-commands.md");
        Assert.Equal(
            "https://raw.githubusercontent.com/microsoft/mcp/release/azure/2.x/servers/Azure.Mcp.Server/docs/azmcp-commands.md",
            url);
    }

    [Fact]
    public void BuildUpstreamUrl_MainBranch_ReturnsCorrectUrl()
    {
        var url = BootstrapStep.BuildUpstreamUrl("main", "e2eTestPrompts.md");
        Assert.Equal(
            "https://raw.githubusercontent.com/microsoft/mcp/main/servers/Azure.Mcp.Server/docs/e2eTestPrompts.md",
            url);
    }

    [Fact]
    public void BuildUpstreamUrl_1xBranch_ReturnsCorrectUrl()
    {
        var url = BootstrapStep.BuildUpstreamUrl("release/azure/1.x", "azmcp-commands.md");
        Assert.Contains("release/azure/1.x", url);
        Assert.EndsWith("azmcp-commands.md", url);
    }

    [Fact]
    public void McpDocsPath_MatchesExpectedUpstreamLocation()
    {
        Assert.Equal("servers/Azure.Mcp.Server/docs", BootstrapStep.McpDocsPath);
    }

    [Fact]
    public void BuildUpstreamUrl_BothFiles_UseSameBranchAndPath()
    {
        const string branch = "release/azure/2.x";
        var azmcpUrl = BootstrapStep.BuildUpstreamUrl(branch, "azmcp-commands.md");
        var e2eUrl = BootstrapStep.BuildUpstreamUrl(branch, "e2eTestPrompts.md");

        // Both URLs share the same base and branch
        var commonPrefix = $"https://raw.githubusercontent.com/microsoft/mcp/{branch}/{BootstrapStep.McpDocsPath}/";
        Assert.StartsWith(commonPrefix, azmcpUrl);
        Assert.StartsWith(commonPrefix, e2eUrl);
    }
}
