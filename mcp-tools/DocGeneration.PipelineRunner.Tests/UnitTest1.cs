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

    // ── Issue #666 regression: readable timestamp format ─────────────────────
    // Old format: "yyyyMMddTHHmmssfffZ" → .\\generated-compute-20260522T152517000Z (unreadable)
    // New format: "yyyy-MM-dd-HHmmss"   → .\\generated-compute-2026-05-22-152517  (human-readable)
    // The two tests below update the old assertions and the three that follow are new regressions.

    [Fact]
    public void GetDefaultOutputPath_SingleNamespace_UsesReadableTimestampFormat()
    {
        // Regression for #666: dashes between date parts, no milliseconds, no Z suffix.
        var outputPath = PipelineRequest.GetDefaultOutputPath(
            "compute",
            new FixedTimeProvider(new DateTimeOffset(2026, 05, 22, 15, 25, 17, TimeSpan.Zero)));

        Assert.Equal(".\\generated-compute-2026-05-22-152517", outputPath);
    }

    [Fact]
    public void GetDefaultOutputPath_NoNamespace_UsesReadableTimestampFormat()
    {
        // Regression for #666: no-namespace path also uses the new format.
        var outputPath = PipelineRequest.GetDefaultOutputPath(
            null,
            new FixedTimeProvider(new DateTimeOffset(2026, 05, 22, 15, 25, 17, TimeSpan.Zero)));

        Assert.Equal(".\\generated-2026-05-22-152517", outputPath);
    }

    [Fact]
    public void GetDefaultOutputPath_WithNamespace_MatchesReadablePattern()
    {
        // Pattern test — useful when exact timestamp is non-deterministic (no injected clock).
        var result = PipelineRequest.GetDefaultOutputPath("appconfig");

        // yyyy-MM-dd-HHmmss: four-digit year, two-digit month, two-digit day, six-digit time
        Assert.Matches(@"^\.\\generated-appconfig-\d{4}-\d{2}-\d{2}-\d{6}$", result);
    }

    [Fact]
    public void GetDefaultOutputPath_WithoutNamespace_MatchesReadablePattern()
    {
        var result = PipelineRequest.GetDefaultOutputPath(null);

        Assert.Matches(@"^\.\\generated-\d{4}-\d{2}-\d{2}-\d{6}$", result);
    }

    [Fact]
    public void GetDefaultOutputPath_DoesNotContainMilliseconds()
    {
        // Old format included milliseconds (fff) + Z, e.g., 20260531T062940173Z.
        // Those are hard to read at a glance; the new format omits them.
        var result = PipelineRequest.GetDefaultOutputPath("storage");

        Assert.DoesNotMatch(@"\d{3}Z", result); // No millisecond triplet + Z suffix
    }

    [Fact]
    public void GetDefaultOutputPath_WhitespaceNamespace_TreatsAsNoNamespace()
    {
        // Blank/whitespace namespace is treated the same as null — no namespace segment in path.
        var result = PipelineRequest.GetDefaultOutputPath("   ");

        Assert.Matches(@"^\.\\generated-\d{4}-\d{2}-\d{2}-\d{6}$", result);
    }
}
