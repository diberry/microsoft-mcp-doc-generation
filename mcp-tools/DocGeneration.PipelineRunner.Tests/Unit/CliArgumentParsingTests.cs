using PipelineRunner.Cli;
using Xunit;

namespace PipelineRunner.Tests.Unit;

public class CliArgumentParsingTests
{
    [Fact]
    public void Parse_NamedArguments_ReturnsExpectedRequest()
    {
        var result = PipelineCli.Parse(["--namespace", "compute", "--steps", "1,2", "--skip-build", "--skip-validation", "--skip-env-validation", "--dry-run"]);

        Assert.NotNull(result.Request);
        Assert.Equal("compute", result.Request!.Namespace);
        Assert.Equal(new[] { 1, 2 }, result.Request.Steps);
        Assert.True(result.Request.SkipBuild);
        Assert.True(result.Request.SkipValidation);
        Assert.True(result.Request.SkipEnvValidation);
        Assert.True(result.Request.DryRun);
    }

    [Fact]
    public void Parse_OmittedOutput_UsesLegacyDefaultConvention()
    {
        var result = PipelineCli.Parse(["--namespace", "compute"]);

        Assert.NotNull(result.Request);
        Assert.Equal(".\\generated-compute", result.Request!.OutputPath);
        Assert.Equal(PipelineRequest.DefaultSteps, result.Request.Steps);
    }

    [Fact]
    public async Task InvokeAsync_InvalidArguments_ReturnsInvalidUsageExitCode()
    {
        var handlerCalled = false;
        using var errorWriter = new StringWriter();

        var exitCode = await PipelineCli.InvokeAsync(
            ["--steps", "1,9"],
            (_, _) =>
            {
                handlerCalled = true;
                return Task.FromResult(0);
            },
            errorWriter: errorWriter);

        Assert.False(handlerCalled);
        Assert.Equal(global::PipelineRunner.PipelineRunner.InvalidArgumentsExitCode, exitCode);
        Assert.Contains("Unsupported step identifiers", errorWriter.ToString(), StringComparison.Ordinal);
    }
}
