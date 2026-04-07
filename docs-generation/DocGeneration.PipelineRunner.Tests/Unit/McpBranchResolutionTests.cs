using PipelineRunner.Cli;
using Xunit;

namespace PipelineRunner.Tests.Unit;

public class McpBranchResolutionTests
{
    [Fact]
    public void ResolvedMcpBranch_DefaultsTo2x_WhenNothingSet()
    {
        // Ensure env var is not set for this test
        var originalValue = Environment.GetEnvironmentVariable("MCP_BRANCH");
        try
        {
            Environment.SetEnvironmentVariable("MCP_BRANCH", null);
            var request = new PipelineRequest(null, [1], ".\\generated", false, false, false, McpBranch: null);
            Assert.Equal(PipelineRequest.DefaultMcpBranch, request.ResolvedMcpBranch);
            Assert.Equal("release/azure/2.x", request.ResolvedMcpBranch);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MCP_BRANCH", originalValue);
        }
    }

    [Fact]
    public void ResolvedMcpBranch_CliOverridesDefault()
    {
        var request = new PipelineRequest(null, [1], ".\\generated", false, false, false, McpBranch: "main");
        Assert.Equal("main", request.ResolvedMcpBranch);
    }

    [Fact]
    public void ResolvedMcpBranch_CliOverridesEnvVar()
    {
        var originalValue = Environment.GetEnvironmentVariable("MCP_BRANCH");
        try
        {
            Environment.SetEnvironmentVariable("MCP_BRANCH", "release/azure/1.x");
            var request = new PipelineRequest(null, [1], ".\\generated", false, false, false, McpBranch: "main");
            Assert.Equal("main", request.ResolvedMcpBranch);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MCP_BRANCH", originalValue);
        }
    }

    [Fact]
    public void ResolvedMcpBranch_EnvVarOverridesDefault()
    {
        var originalValue = Environment.GetEnvironmentVariable("MCP_BRANCH");
        try
        {
            Environment.SetEnvironmentVariable("MCP_BRANCH", "release/azure/1.x");
            var request = new PipelineRequest(null, [1], ".\\generated", false, false, false, McpBranch: null);
            Assert.Equal("release/azure/1.x", request.ResolvedMcpBranch);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MCP_BRANCH", originalValue);
        }
    }

    [Fact]
    public void ResolvedMcpBranch_BlankCliValueFallsToDefault()
    {
        var originalValue = Environment.GetEnvironmentVariable("MCP_BRANCH");
        try
        {
            Environment.SetEnvironmentVariable("MCP_BRANCH", null);
            var request = new PipelineRequest(null, [1], ".\\generated", false, false, false, McpBranch: "  ");
            Assert.Equal(PipelineRequest.DefaultMcpBranch, request.ResolvedMcpBranch);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MCP_BRANCH", originalValue);
        }
    }

    [Fact]
    public void ResolvedMcpBranch_BlankEnvVarFallsToDefault()
    {
        var originalValue = Environment.GetEnvironmentVariable("MCP_BRANCH");
        try
        {
            Environment.SetEnvironmentVariable("MCP_BRANCH", "  ");
            var request = new PipelineRequest(null, [1], ".\\generated", false, false, false, McpBranch: null);
            Assert.Equal(PipelineRequest.DefaultMcpBranch, request.ResolvedMcpBranch);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MCP_BRANCH", originalValue);
        }
    }

    [Fact]
    public void ResolvedMcpBranch_TrimsWhitespace()
    {
        var request = new PipelineRequest(null, [1], ".\\generated", false, false, false, McpBranch: "  main  ");
        Assert.Equal("main", request.ResolvedMcpBranch);
    }

    [Fact]
    public void Parse_McpBranchFlag_Parsed()
    {
        var result = PipelineCli.Parse(["--mcp-branch", "main"]);
        Assert.NotNull(result.Request);
        Assert.Equal("main", result.Request!.McpBranch);
    }

    [Fact]
    public void Parse_McpBranchFlagWithNamespace_Parsed()
    {
        var result = PipelineCli.Parse(["--namespace", "compute", "--mcp-branch", "release/azure/1.x"]);
        Assert.NotNull(result.Request);
        Assert.Equal("compute", result.Request!.Namespace);
        Assert.Equal("release/azure/1.x", result.Request.McpBranch);
    }

    [Fact]
    public void Parse_NoMcpBranchFlag_DefaultsToNull()
    {
        var result = PipelineCli.Parse(["--namespace", "compute"]);
        Assert.NotNull(result.Request);
        Assert.Null(result.Request!.McpBranch);
    }
}
