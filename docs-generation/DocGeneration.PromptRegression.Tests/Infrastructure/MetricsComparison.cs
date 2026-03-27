namespace DocGeneration.PromptRegression.Tests.Infrastructure;

/// <summary>
/// Comparison result between baseline and candidate metrics.
/// Positive deltas are improvements, negative are regressions.
/// </summary>
public sealed class MetricsComparison
{
    public required string Namespace { get; init; }
    public required string FileType { get; init; }
    public required QualityMetrics Baseline { get; init; }
    public required QualityMetrics Candidate { get; init; }

    public int SectionDelta => Candidate.SectionCount - Baseline.SectionCount;
    public int WordDelta => Candidate.WordCount - Baseline.WordCount;
    public double ContractionRateDelta => Candidate.ContractionRate - Baseline.ContractionRate;
    public int FutureTenseDelta => Candidate.FutureTenseViolations - Baseline.FutureTenseViolations;
    public int FabricatedUrlDelta => Candidate.FabricatedUrlCount - Baseline.FabricatedUrlCount;
    public int BrandingDelta => Candidate.BrandingViolations - Baseline.BrandingViolations;
    public int MissingSectionsDelta => Candidate.MissingSections.Count - Baseline.MissingSections.Count;

    /// <summary>
    /// True if any metric regressed (got worse).
    /// Regressions: lost sections, more violations, more missing required sections.
    /// </summary>
    public bool HasRegressions =>
        SectionDelta < -1 ||
        MissingSectionsDelta > 0 ||
        FutureTenseDelta > 0 ||
        FabricatedUrlDelta > 0 ||
        BrandingDelta > 0;

    /// <summary>
    /// True if any metric improved.
    /// </summary>
    public bool HasImprovements =>
        MissingSectionsDelta < 0 ||
        FutureTenseDelta < 0 ||
        FabricatedUrlDelta < 0 ||
        BrandingDelta < 0 ||
        ContractionRateDelta > 0.05;
}
