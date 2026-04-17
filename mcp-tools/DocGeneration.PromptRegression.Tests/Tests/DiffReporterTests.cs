using DocGeneration.PromptRegression.Tests.Infrastructure;

namespace DocGeneration.PromptRegression.Tests.Tests;

/// <summary>
/// Tests for the DiffReporter markdown report generation.
/// </summary>
public class DiffReporterTests
{
    [Fact]
    public void GenerateReport_EmptyComparisons_ProducesValidMarkdown()
    {
        var report = DiffReporter.GenerateReport([]);

        Assert.Contains("# Prompt Regression Report", report);
        Assert.Contains("## Summary", report);
        Assert.Contains("## Verdict", report);
        Assert.Contains("No significant changes detected", report);
    }

    [Fact]
    public void GenerateReport_WithImprovement_ShowsImproved()
    {
        var comparisons = new List<MetricsComparison>
        {
            new()
            {
                Namespace = "storage",
                FileType = "horizontal-article.md",
                Baseline = QualityMetrics.Analyze(BuildArticle(futureTense: true)),
                Candidate = QualityMetrics.Analyze(BuildArticle(futureTense: false)),
            }
        };

        var report = DiffReporter.GenerateReport(comparisons);

        Assert.Contains("✅", report);
        Assert.Contains("storage", report);
        Assert.Contains("Improved", report);
    }

    [Fact]
    public void GenerateReport_WithRegression_ShowsWarning()
    {
        var comparisons = new List<MetricsComparison>
        {
            new()
            {
                Namespace = "advisor",
                FileType = "tool-family.md",
                Baseline = QualityMetrics.Analyze(BuildArticle(futureTense: false)),
                Candidate = QualityMetrics.Analyze(BuildArticle(futureTense: true)),
            }
        };

        var report = DiffReporter.GenerateReport(comparisons);

        Assert.Contains("⚠️", report);
        Assert.Contains("Regression", report);
    }

    [Fact]
    public void GenerateMetricsSummary_ProducesReadableOutput()
    {
        var metrics = QualityMetrics.Analyze(BuildArticle());
        var summary = DiffReporter.GenerateMetricsSummary("test", metrics);

        Assert.Contains("Quality Metrics for test", summary);
        Assert.Contains("Sections:", summary);
        Assert.Contains("Words:", summary);
        Assert.Contains("Frontmatter: valid", summary);
    }

    private static string BuildArticle(bool futureTense = false)
    {
        var verb = futureTense ? "will return" : "returns";
        return $"""
            ---
            title: Azure MCP Server tools for Test
            ms.topic: concept-article
            ms.date: 03/27/2026
            ---

            # Azure MCP Server tools for Test

            This tool {verb} resources from your subscription.

            ## Prerequisites

            You need an Azure subscription.

            ## Best practices

            Don't hardcode credentials.

            ## Related content

            - [Azure docs](/azure/)
            """;
    }
}
