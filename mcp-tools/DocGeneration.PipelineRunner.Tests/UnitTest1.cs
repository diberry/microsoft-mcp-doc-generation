using PipelineRunner.Cli;
using Xunit;

namespace PipelineRunner.Tests.Unit;

public class PipelineRequestTests
{
    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

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
        var request = new PipelineRequest("compute", new[] { 1, 99 }, ".\\generated-compute", SkipBuild: false, SkipValidation: false, DryRun: false);

        var errors = request.Validate();

        Assert.Contains(errors, error => error.Contains("Unsupported step identifiers", StringComparison.Ordinal));
    }

    [Fact]
    public void GetDefaultOutputPath_SingleNamespace_UsesNamespaceSuffixAndTimestamp()
    {
        var outputPath = PipelineRequest.GetDefaultOutputPath(
            "compute",
            new FixedTimeProvider(new DateTimeOffset(2026, 05, 22, 15, 25, 17, TimeSpan.Zero)));

        Assert.Equal(".\\generated-compute-20260522T152517000Z", outputPath);
    }

    [Fact]
    public void GetDefaultOutputPath_NoNamespace_UsesTimestampedGeneratedDirectory()
    {
        var outputPath = PipelineRequest.GetDefaultOutputPath(
            null,
            new FixedTimeProvider(new DateTimeOffset(2026, 05, 22, 15, 25, 17, TimeSpan.Zero)));

        Assert.Equal(".\\generated-20260522T152517000Z", outputPath);
    }
}
