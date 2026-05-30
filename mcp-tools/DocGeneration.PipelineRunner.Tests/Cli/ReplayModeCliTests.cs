using PipelineRunner.Cli;
using Xunit;

namespace PipelineRunner.Tests.Unit;

public class ReplayModeCliTests
{
    [Fact]
    public void Parse_ReplayMode_WithFromAndStepName_ParsesCorrectly()
    {
        var result = PipelineCli.Parse([
            "--replay",
            "--from", "run-123",
            "--step-name", "tool-generation",
            "--namespace", "azure",
        ]);

        Assert.NotNull(result.Request);
        Assert.True(result.Request!.Replay);
        Assert.Equal("run-123", result.Request.ReplayFromRunId);
        Assert.Equal("tool-generation", result.Request.ReplayStepName);
        Assert.Equal("azure", result.Request.Namespace);
    }

    [Fact]
    public void Parse_ReplayMode_WithoutFrom_ReturnsValidationError()
    {
        var result = PipelineCli.Parse([
            "--replay",
            "--step-name", "tool-generation",
            "--namespace", "azure",
        ]);

        Assert.Null(result.Request);
        Assert.Contains(result.Errors, error => error.Contains("--from is required when --replay is set.", StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_ReplayMode_WithoutStepName_ReturnsValidationError()
    {
        var result = PipelineCli.Parse([
            "--replay",
            "--from", "run-123",
            "--namespace", "azure",
        ]);

        Assert.Null(result.Request);
        Assert.Contains(result.Errors, error => error.Contains("--step-name is required when --replay is set.", StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_ReplayFlagOmitted_DefaultsFalse()
    {
        var result = PipelineCli.Parse(["--namespace", "azure"]);

        Assert.NotNull(result.Request);
        Assert.False(result.Request!.Replay);
    }
}
