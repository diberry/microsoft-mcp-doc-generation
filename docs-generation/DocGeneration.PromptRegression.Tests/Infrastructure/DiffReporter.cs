using System.Text;

namespace DocGeneration.PromptRegression.Tests.Infrastructure;

/// <summary>
/// Generates human-readable markdown diff reports comparing baseline vs candidate metrics.
/// Reports are designed for team review in PR comments.
/// </summary>
public sealed class DiffReporter
{
    /// <summary>
    /// Generates a full comparison report for multiple namespaces.
    /// </summary>
    public static string GenerateReport(IReadOnlyList<MetricsComparison> comparisons)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Prompt Regression Report");
        sb.AppendLine();
        sb.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss UTC}");
        sb.AppendLine();

        // Summary table
        sb.AppendLine("## Summary");
        sb.AppendLine();
        sb.AppendLine("| Namespace | File | Sections | Words | Contractions | Future Tense | Fabricated URLs | Branding | Status |");
        sb.AppendLine("|-----------|------|----------|-------|--------------|--------------|-----------------|----------|--------|");

        foreach (var c in comparisons)
        {
            var status = c.HasRegressions ? "⚠️ Regression" : c.HasImprovements ? "✅ Improved" : "➖ No change";
            sb.AppendLine($"| {c.Namespace} | {c.FileType} | {FormatDelta(c.SectionDelta)} | {FormatDelta(c.WordDelta)} | {FormatPercent(c.ContractionRateDelta)} | {FormatDelta(-c.FutureTenseDelta)} | {FormatDelta(-c.FabricatedUrlDelta)} | {FormatDelta(-c.BrandingDelta)} | {status} |");
        }

        sb.AppendLine();

        // Detailed per-namespace breakdowns
        foreach (var c in comparisons)
        {
            sb.AppendLine($"## {c.Namespace} — {c.FileType}");
            sb.AppendLine();
            sb.AppendLine("| Metric | Baseline | Candidate | Delta |");
            sb.AppendLine("|--------|----------|-----------|-------|");
            sb.AppendLine($"| Sections | {c.Baseline.SectionCount} | {c.Candidate.SectionCount} | {FormatDelta(c.SectionDelta)} |");
            sb.AppendLine($"| Word count | {c.Baseline.WordCount} | {c.Candidate.WordCount} | {FormatDelta(c.WordDelta)} |");
            sb.AppendLine($"| Char count | {c.Baseline.CharCount} | {c.Candidate.CharCount} | {FormatDelta(c.Candidate.CharCount - c.Baseline.CharCount)} |");
            sb.AppendLine($"| Contractions | {c.Baseline.ContractionRate:P0} | {c.Candidate.ContractionRate:P0} | {FormatPercent(c.ContractionRateDelta)} |");
            sb.AppendLine($"| Future tense violations | {c.Baseline.FutureTenseViolations} | {c.Candidate.FutureTenseViolations} | {FormatDelta(-c.FutureTenseDelta)} |");
            sb.AppendLine($"| Fabricated URLs | {c.Baseline.FabricatedUrlCount} | {c.Candidate.FabricatedUrlCount} | {FormatDelta(-c.FabricatedUrlDelta)} |");
            sb.AppendLine($"| Branding violations | {c.Baseline.BrandingViolations} | {c.Candidate.BrandingViolations} | {FormatDelta(-c.BrandingDelta)} |");
            sb.AppendLine($"| Missing sections | {c.Baseline.MissingSections.Count} | {c.Candidate.MissingSections.Count} | {FormatDelta(-c.MissingSectionsDelta)} |");
            sb.AppendLine($"| Frontmatter valid | {c.Baseline.HasValidFrontmatter} | {c.Candidate.HasValidFrontmatter} | — |");
            sb.AppendLine();

            if (c.Candidate.MissingSections.Count > 0)
            {
                sb.AppendLine($"**Missing sections in candidate:** {string.Join(", ", c.Candidate.MissingSections)}");
                sb.AppendLine();
            }
        }

        // Overall verdict
        var regressions = comparisons.Count(c => c.HasRegressions);
        var improvements = comparisons.Count(c => c.HasImprovements);
        sb.AppendLine("## Verdict");
        sb.AppendLine();
        if (regressions > 0)
            sb.AppendLine($"⚠️ **{regressions} regression(s) detected.** Review before merging.");
        else if (improvements > 0)
            sb.AppendLine($"✅ **{improvements} improvement(s), 0 regressions.** Safe to merge.");
        else
            sb.AppendLine("➖ **No significant changes detected.**");

        return sb.ToString();
    }

    /// <summary>
    /// Generates a single-namespace metrics summary (for test output).
    /// </summary>
    public static string GenerateMetricsSummary(string ns, QualityMetrics metrics)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Quality Metrics for {ns}:");
        sb.AppendLine($"  Sections: {metrics.SectionCount}");
        sb.AppendLine($"  Words: {metrics.WordCount}");
        sb.AppendLine($"  Frontmatter: {(metrics.HasValidFrontmatter ? "valid" : "MISSING")}");
        sb.AppendLine($"  Contraction rate: {metrics.ContractionRate:P0}");
        sb.AppendLine($"  Future tense violations: {metrics.FutureTenseViolations}");
        sb.AppendLine($"  Fabricated URLs: {metrics.FabricatedUrlCount}");
        sb.AppendLine($"  Branding violations: {metrics.BrandingViolations}");
        sb.AppendLine($"  Missing sections: {metrics.MissingSections.Count}");
        return sb.ToString();
    }

    private static string FormatDelta(int delta) => delta switch
    {
        > 0 => $"+{delta} ✅",
        < 0 => $"{delta} ⚠️",
        _ => "="
    };

    private static string FormatPercent(double delta) => delta switch
    {
        > 0.01 => $"+{delta:P0} ✅",
        < -0.01 => $"{delta:P0} ⚠️",
        _ => "="
    };
}
