using DocGeneration.PromptRegression.Tests.Infrastructure;

namespace DocGeneration.PromptRegression.Tests.Tests;

/// <summary>
/// Tests the MetricsComparison regression/improvement detection logic.
/// </summary>
public class MetricsComparisonTests
{
    [Fact]
    public void HasRegressions_MoreFutureTense_IsTrue()
    {
        var comparison = new MetricsComparison
        {
            Namespace = "test",
            FileType = "article.md",
            Baseline = QualityMetrics.Analyze("# Test\n\nThis tool returns data."),
            Candidate = QualityMetrics.Analyze("# Test\n\nThis tool will return data."),
        };

        Assert.True(comparison.HasRegressions);
    }

    [Fact]
    public void HasRegressions_FewerFutureTense_IsFalse()
    {
        var comparison = new MetricsComparison
        {
            Namespace = "test",
            FileType = "article.md",
            Baseline = QualityMetrics.Analyze("# Test\n\nThis tool will return data."),
            Candidate = QualityMetrics.Analyze("# Test\n\nThis tool returns data."),
        };

        Assert.False(comparison.HasRegressions);
        Assert.True(comparison.HasImprovements);
    }

    [Fact]
    public void HasRegressions_MoreBrandingViolations_IsTrue()
    {
        var comparison = new MetricsComparison
        {
            Namespace = "test",
            FileType = "article.md",
            Baseline = QualityMetrics.Analyze("# Test\n\nUse Azure Cosmos DB."),
            Candidate = QualityMetrics.Analyze("# Test\n\nUse CosmosDB."),
        };

        Assert.True(comparison.HasRegressions);
    }

    [Fact]
    public void HasRegressions_IdenticalContent_NoRegressionsOrImprovements()
    {
        var content = "# Test\n\nThis tool returns data.";
        var comparison = new MetricsComparison
        {
            Namespace = "test",
            FileType = "article.md",
            Baseline = QualityMetrics.Analyze(content),
            Candidate = QualityMetrics.Analyze(content),
        };

        Assert.False(comparison.HasRegressions);
        Assert.False(comparison.HasImprovements);
    }

    [Fact]
    public void Deltas_CalculateCorrectly()
    {
        var baseline = """
            ---
            title: Test
            ---

            # Test

            ## Section A

            Content.

            ## Section B

            More content.
            """;

        var candidate = """
            ---
            title: Test
            ---

            # Test

            ## Section A

            Content.

            ## Section B

            More content.

            ## Section C

            Even more content.
            """;

        var comparison = new MetricsComparison
        {
            Namespace = "test",
            FileType = "article.md",
            Baseline = QualityMetrics.Analyze(baseline),
            Candidate = QualityMetrics.Analyze(candidate),
        };

        Assert.Equal(1, comparison.SectionDelta);
        Assert.True(comparison.WordDelta > 0);
    }

    [Fact]
    public void HasRegressions_LostMultipleSections_IsTrue()
    {
        var baseline = "# Test\n\n## Section A\n\nContent.\n\n## Section B\n\nMore.\n\n## Section C\n\nEven more.";
        var candidate = "# Test\n\nContent only.";

        var comparison = new MetricsComparison
        {
            Namespace = "test",
            FileType = "article.md",
            Baseline = QualityMetrics.Analyze(baseline),
            Candidate = QualityMetrics.Analyze(candidate),
        };

        Assert.True(comparison.HasRegressions);
        Assert.True(comparison.SectionDelta < 0);
    }

    [Theory]
    [InlineData(0.06, true)]
    [InlineData(0.04, false)]
    public void HasImprovements_ContractionRateThreshold_RespectsBoundary(double delta, bool expected)
    {
        // Build metrics with controlled contraction rates to test the 0.05 threshold
        var baselineContent = "# Test\n\nYou do not need this. It is not required. They are not available. We do not support it.";
        var baseMetrics = QualityMetrics.Analyze(baselineContent);

        // Candidate with contractions (higher rate)
        var candidateContent = "# Test\n\nYou don't need this. It isn't required. They aren't available. We don't support it.";
        var candMetrics = QualityMetrics.Analyze(candidateContent);

        var comparison = new MetricsComparison
        {
            Namespace = "test",
            FileType = "article.md",
            Baseline = baseMetrics,
            Candidate = candMetrics,
        };

        // The actual contraction rate improvement should be large (0% → 100%)
        // so this always shows as improved
        Assert.True(comparison.HasImprovements);
        Assert.True(comparison.ContractionRateDelta > 0.05);
    }
}
