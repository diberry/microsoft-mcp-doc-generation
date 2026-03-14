using PipelineRunner.Cli;
using Xunit;

namespace PipelineRunner.Tests.Unit;

public class PipelineRequestTests
{
    [Fact]
    public void TryParseSteps_ValidCsv_ReturnsOrderedValues()
    {
        var success = PipelineRequest.TryParseSteps("1,2,3", out var steps, out var error);

        Assert.True(success);
        Assert.Null(error);
        Assert.Equal(new[] { 1, 2, 3 }, steps);
    }

    [Fact]
    public void Validate_DuplicateSteps_ReturnsError()
    {
        var request = new PipelineRequest("compute", new[] { 1, 1, 2 }, ".\\generated-compute", SkipBuild: false, SkipValidation: false, DryRun: false);

        var errors = request.Validate();

        Assert.Contains(errors, error => error.Contains("Duplicate step identifiers", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_UnsupportedStep_ReturnsError()
    {
        var request = new PipelineRequest("compute", new[] { 1, 7 }, ".\\generated-compute", SkipBuild: false, SkipValidation: false, DryRun: false);

        var errors = request.Validate();

        Assert.Contains(errors, error => error.Contains("Unsupported step identifiers", StringComparison.Ordinal));
    }

    [Fact]
    public void GetDefaultOutputPath_SingleNamespace_UsesNamespaceSuffix()
    {
        var outputPath = PipelineRequest.GetDefaultOutputPath("compute");

        Assert.Equal(".\\generated-compute", outputPath);
    }
}
