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
    public void Parse_OmittedOutput_UsesTimestampedDefaultConvention()
    {
        var result = PipelineCli.Parse(["--namespace", "compute"]);

        Assert.NotNull(result.Request);
        Assert.Matches(@"^\.\\generated-compute-\d{8}T\d{9}Z$", result.Request!.OutputPath);
        Assert.Equal(PipelineRequest.DefaultSteps, result.Request.Steps);
    }

    [Fact]
    public void Parse_ExplicitOutput_UsesValueAsIs()
    {
        var result = PipelineCli.Parse(["--namespace", "compute", "--output", ".\\my-output"]);

        Assert.NotNull(result.Request);
        Assert.Equal(".\\my-output", result.Request!.OutputPath);
    }

    [Fact]
    public void Parse_StepsIncludingBootstrap_AcceptsStepZero()
    {
        var result = PipelineCli.Parse(["--namespace", "compute", "--steps", "0,1"]);

        Assert.NotNull(result.Request);
        Assert.Equal(new[] { 0, 1 }, result.Request!.Steps);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Parse_BootstrapOnlyStep_AcceptsStepZero()
    {
        var result = PipelineCli.Parse(["--namespace", "compute", "--steps", "0"]);

        Assert.NotNull(result.Request);
        Assert.Equal(new[] { 0 }, result.Request!.Steps);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Parse_StepsIncludingBootstrapAndInvalidStep_RejectsRequest()
    {
        var result = PipelineCli.Parse(["--namespace", "compute", "--steps", "0,9"]);

        Assert.Null(result.Request);
        Assert.Contains(result.Errors, error => error.Contains("Unsupported step identifiers: 9. Valid step identifiers: 0-7.", StringComparison.Ordinal));
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
        Assert.Contains("Unsupported step identifiers: 9. Valid step identifiers: 0-7.", errorWriter.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_SkipNpmUpdate_SetsFlag()
    {
        var result = PipelineCli.Parse(["--namespace", "compute", "--skip-npm-update"]);

        Assert.NotNull(result.Request);
        Assert.True(result.Request!.SkipNpmUpdate);
    }

    [Fact]
    public void Parse_SkipNpmUpdateOmitted_DefaultsFalse()
    {
        var result = PipelineCli.Parse(["--namespace", "compute"]);

        Assert.NotNull(result.Request);
        Assert.False(result.Request!.SkipNpmUpdate);
    }
}
