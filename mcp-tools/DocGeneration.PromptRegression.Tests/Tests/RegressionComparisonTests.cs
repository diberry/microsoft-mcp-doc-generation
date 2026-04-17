using DocGeneration.PromptRegression.Tests.Infrastructure;

namespace DocGeneration.PromptRegression.Tests.Tests;

/// <summary>
/// Compares baseline articles against candidate articles for quality regressions.
/// Baselines are committed golden files. Candidates are generated at test time.
/// Run after regenerating output: ./prompt-regression.sh compare
/// </summary>
public class RegressionComparisonTests
{
    private readonly BaselineManager _manager = new();

    [Fact]
    public void AllBaselineNamespaces_HaveCandidates_WhenCandidatesExist()
    {
        var namespaces = _manager.GetBaselineNamespaces();
        if (namespaces.Count == 0)
            return;

        // Only run in comparison mode — when candidates have been populated
        var hasCandidates = namespaces.Any(ns =>
        {
            var nsDir = Path.Combine(_manager.CandidatesRoot, ns);
            return Directory.Exists(nsDir) && Directory.GetFiles(nsDir, "*.md").Length > 0;
        });
        if (!hasCandidates)
            return;

        foreach (var ns in namespaces)
        {
            var baselineFiles = _manager.ListBaselineFiles(ns);
            foreach (var file in baselineFiles)
            {
                var candidate = _manager.ReadCandidate(ns, file);
                Assert.True(candidate is not null,
                    $"Candidate missing for {ns}/{file}. Run './prompt-regression.sh compare' to populate candidates.");
            }
        }
    }

    [Fact]
    public void NoQualityRegressions_AcrossAllNamespaces()
    {
        var namespaces = _manager.GetBaselineNamespaces();
        if (namespaces.Count == 0)
            return;

        var comparisons = new List<MetricsComparison>();
        foreach (var ns in namespaces)
        {
            var files = _manager.ListBaselineFiles(ns);
            foreach (var file in files)
            {
                var comparison = _manager.Compare(ns, file);
                if (comparison is not null)
                    comparisons.Add(comparison);
            }
        }

        if (comparisons.Count == 0)
            return; // No candidates to compare

        // Generate report for inspection
        var report = DiffReporter.GenerateReport(comparisons);
        var reportsDir = Path.Combine(_manager.BaselinesRoot, "..", "Reports");
        Directory.CreateDirectory(reportsDir);
        File.WriteAllText(Path.Combine(reportsDir, "regression-report.md"), report);

        // Assert no regressions
        var regressions = comparisons.Where(c => c.HasRegressions).ToList();
        Assert.True(regressions.Count == 0,
            $"Quality regressions detected in {regressions.Count} file(s):\n" +
            string.Join("\n", regressions.Select(r =>
                $"  - {r.Namespace}/{r.FileType}: " +
                FormatRegressionDetails(r))));
    }

    [Fact]
    public void ToolFamilyArticles_MaintainSectionCount()
    {
        var namespaces = _manager.GetBaselineNamespaces();
        if (namespaces.Count == 0)
            return;

        foreach (var ns in namespaces)
        {
            var comparison = _manager.Compare(ns, "tool-family.md");
            if (comparison is null) continue;

            // Losing more than 1 section is a regression (allows minor heading adjustments)
            Assert.True(comparison.SectionDelta >= -1,
                $"{ns}: Tool-family lost {Math.Abs(comparison.SectionDelta)} sections " +
                $"(baseline: {comparison.Baseline.SectionCount}, candidate: {comparison.Candidate.SectionCount})");
        }
    }

    [Fact]
    public void HorizontalArticles_MaintainRequiredSections()
    {
        var namespaces = _manager.GetBaselineNamespaces();
        if (namespaces.Count == 0)
            return;

        foreach (var ns in namespaces)
        {
            var comparison = _manager.Compare(ns, "horizontal-article.md");
            if (comparison is null) continue;

            // Candidate should not have MORE missing sections than baseline
            Assert.True(comparison.MissingSectionsDelta <= 0,
                $"{ns}: Horizontal article lost required sections. " +
                $"Missing in candidate: [{string.Join(", ", comparison.Candidate.MissingSections)}]");
        }
    }

    [Fact]
    public void ContentVolume_DoesNotDropSignificantly()
    {
        var namespaces = _manager.GetBaselineNamespaces();
        if (namespaces.Count == 0)
            return;

        foreach (var ns in namespaces)
        {
            var files = _manager.ListBaselineFiles(ns);
            foreach (var file in files)
            {
                var comparison = _manager.Compare(ns, file);
                if (comparison is null) continue;

                // Only flag drops, not increases
                if (comparison.WordDelta < 0)
                {
                    var dropPercent = (double)-comparison.WordDelta / comparison.Baseline.WordCount;
                    Assert.True(dropPercent < 0.20,
                        $"{ns}/{file}: Word count dropped {dropPercent:P0} " +
                        $"(baseline: {comparison.Baseline.WordCount}, candidate: {comparison.Candidate.WordCount})");
                }
            }
        }
    }

    private static string FormatRegressionDetails(MetricsComparison c)
    {
        var details = new List<string>();
        if (c.SectionDelta < -1) details.Add($"sections {c.SectionDelta}");
        if (c.MissingSectionsDelta > 0) details.Add($"missing sections +{c.MissingSectionsDelta}");
        if (c.FutureTenseDelta > 0) details.Add($"future tense +{c.FutureTenseDelta}");
        if (c.FabricatedUrlDelta > 0) details.Add($"fabricated URLs +{c.FabricatedUrlDelta}");
        if (c.BrandingDelta > 0) details.Add($"branding +{c.BrandingDelta}");
        return string.Join(", ", details);
    }
}
