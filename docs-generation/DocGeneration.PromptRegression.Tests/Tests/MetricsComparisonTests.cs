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
}
