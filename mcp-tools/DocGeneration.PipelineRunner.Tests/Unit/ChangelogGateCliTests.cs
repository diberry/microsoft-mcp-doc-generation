using PipelineRunner.Cli;
using Xunit;

namespace PipelineRunner.Tests.Unit;

public class ChangelogGateCliTests
{
    [Fact]
    public void Parse_SkipChangelogGateFlag_ParsedCorrectly()
    {
        var result = PipelineCli.Parse(["--namespace", "compute", "--skip-changelog-gate"]);

        Assert.NotNull(result.Request);
        Assert.True(result.Request!.SkipChangelogGate);
    }

    [Fact]
    public void Parse_NoSkipChangelogGateFlag_DefaultsFalse()
    {
        var result = PipelineCli.Parse(["--namespace", "compute"]);

        Assert.NotNull(result.Request);
        Assert.False(result.Request!.SkipChangelogGate);
    }

    [Fact]
    public void Parse_SkipChangelogGateWithOtherFlags_ParsedCorrectly()
    {
        var result = PipelineCli.Parse(["--namespace", "storage", "--skip-changelog-gate", "--skip-build", "--steps", "1,2"]);

        Assert.NotNull(result.Request);
        Assert.True(result.Request!.SkipChangelogGate);
        Assert.True(result.Request.SkipBuild);
        Assert.Equal(new[] { 1, 2 }, result.Request.Steps);
    }
}
