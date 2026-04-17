using System.Text;

namespace DocGeneration.Tools.Fingerprint;

/// <summary>
/// Computes deltas between two snapshots and generates a markdown diff report.
/// </summary>
internal static class SnapshotDiffer
{
    /// <summary>
    /// Computes a diff between baseline and candidate snapshots.
    /// </summary>
    public static SnapshotDiff ComputeDiff(FingerprintSnapshot baseline, FingerprintSnapshot candidate)
    {
        var namespaceDiffs = new Dictionary<string, NamespaceDiff>();

        var allNamespaces = baseline.Namespaces.Keys
            .Union(candidate.Namespaces.Keys)
            .OrderBy(n => n)
            .ToList();

        foreach (var ns in allNamespaces)
        {
            var hasBaseline = baseline.Namespaces.TryGetValue(ns, out var baseNs);
            var hasCandidate = candidate.Namespaces.TryGetValue(ns, out var candNs);

            namespaceDiffs[ns] = new NamespaceDiff
            {
                Status = (hasBaseline, hasCandidate) switch
                {
                    (true, true) => DiffStatus.Modified,
                    (false, true) => DiffStatus.Added,
                    (true, false) => DiffStatus.Removed,
                    _ => DiffStatus.Unchanged
                },
                FileCountDelta = (candNs?.FileCount ?? 0) - (baseNs?.FileCount ?? 0),
                SizeDelta = (candNs?.TotalSizeBytes ?? 0) - (baseNs?.TotalSizeBytes ?? 0),
                Baseline = baseNs,
                Candidate = candNs,
                HeadingsAdded = ComputeHeadingsAdded(baseNs, candNs),
                HeadingsRemoved = ComputeHeadingsRemoved(baseNs, candNs),
                QualityRegressions = ComputeQualityRegressions(baseNs, candNs),
                FrontmatterFieldsAdded = ComputeFieldsAdded(baseNs, candNs),
                FrontmatterFieldsRemoved = ComputeFieldsRemoved(baseNs, candNs)
            };
        }

        return new SnapshotDiff
        {
            BaselineTimestamp = baseline.Timestamp,
            CandidateTimestamp = candidate.Timestamp,
            NamespaceDiffs = namespaceDiffs
        };
    }

    /// <summary>
    /// Generates a markdown report from a snapshot diff.
    /// </summary>
    public static string GenerateReport(SnapshotDiff diff)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Fingerprint Diff Report");
        sb.AppendLine();
        sb.AppendLine($"**Baseline:** {diff.BaselineTimestamp:yyyy-MM-dd HH:mm:ss UTC}");
        sb.AppendLine($"**Candidate:** {diff.CandidateTimestamp:yyyy-MM-dd HH:mm:ss UTC}");
        sb.AppendLine();

        // Summary table
        sb.AppendLine("## Summary");
        sb.AppendLine();
        sb.AppendLine("| Namespace | Status | Files | Size | Headings | Quality |");
        sb.AppendLine("|-----------|--------|-------|------|----------|---------|");

        foreach (var (ns, nsDiff) in diff.NamespaceDiffs)
        {
            var statusIcon = nsDiff.Status switch
            {
                DiffStatus.Added => "🆕",
                DiffStatus.Removed => "🗑️",
                DiffStatus.Modified when nsDiff.QualityRegressions.Count > 0 => "⚠️",
                DiffStatus.Modified => "✏️",
                _ => "➖"
            };

            var filesDelta = FormatDelta(nsDiff.FileCountDelta);
            var sizeDelta = FormatSizeDelta(nsDiff.SizeDelta);
            var headingChanges = nsDiff.HeadingsAdded.Count + nsDiff.HeadingsRemoved.Count;
            var headingStr = headingChanges > 0 ? $"+{nsDiff.HeadingsAdded.Count}/-{nsDiff.HeadingsRemoved.Count}" : "=";
            var qualityStr = nsDiff.QualityRegressions.Count > 0
                ? $"⚠️ {nsDiff.QualityRegressions.Count}"
                : "✅";

            sb.AppendLine($"| {ns} | {statusIcon} | {filesDelta} | {sizeDelta} | {headingStr} | {qualityStr} |");
        }

        sb.AppendLine();

        // Detailed sections for namespaces with changes
        foreach (var (ns, nsDiff) in diff.NamespaceDiffs.Where(d =>
            d.Value.Status != DiffStatus.Unchanged || d.Value.QualityRegressions.Count > 0))
        {
            sb.AppendLine($"## {ns}");
            sb.AppendLine();

            if (nsDiff.Status == DiffStatus.Added)
            {
                sb.AppendLine($"**New namespace** — {nsDiff.Candidate?.FileCount} files, {FormatSize(nsDiff.Candidate?.TotalSizeBytes ?? 0)}");
                sb.AppendLine();
                continue;
            }

            if (nsDiff.Status == DiffStatus.Removed)
            {
                sb.AppendLine($"**Removed namespace** — was {nsDiff.Baseline?.FileCount} files, {FormatSize(nsDiff.Baseline?.TotalSizeBytes ?? 0)}");
                sb.AppendLine();
                continue;
            }

            // File/size changes
            if (nsDiff.FileCountDelta != 0 || nsDiff.SizeDelta != 0)
            {
                sb.AppendLine($"- Files: {nsDiff.Baseline?.FileCount} → {nsDiff.Candidate?.FileCount} ({FormatDelta(nsDiff.FileCountDelta)})");
                sb.AppendLine($"- Size: {FormatSize(nsDiff.Baseline?.TotalSizeBytes ?? 0)} → {FormatSize(nsDiff.Candidate?.TotalSizeBytes ?? 0)} ({FormatSizeDelta(nsDiff.SizeDelta)})");
            }

            // Heading changes
            if (nsDiff.HeadingsAdded.Count > 0)
                sb.AppendLine($"- **Headings added:** {string.Join(", ", nsDiff.HeadingsAdded)}");
            if (nsDiff.HeadingsRemoved.Count > 0)
                sb.AppendLine($"- **Headings removed:** {string.Join(", ", nsDiff.HeadingsRemoved)}");

            // Frontmatter changes
            if (nsDiff.FrontmatterFieldsAdded.Count > 0)
                sb.AppendLine($"- **Frontmatter fields added:** {string.Join(", ", nsDiff.FrontmatterFieldsAdded)}");
            if (nsDiff.FrontmatterFieldsRemoved.Count > 0)
                sb.AppendLine($"- **Frontmatter fields removed:** {string.Join(", ", nsDiff.FrontmatterFieldsRemoved)}");

            // Quality regressions
            if (nsDiff.QualityRegressions.Count > 0)
            {
                sb.AppendLine("- **Quality regressions:**");
                foreach (var regression in nsDiff.QualityRegressions)
                    sb.AppendLine($"  - {regression}");
            }

            sb.AppendLine();
        }

        // Verdict
        var regressionCount = diff.NamespaceDiffs.Values.Count(d => d.QualityRegressions.Count > 0);
        var removedCount = diff.NamespaceDiffs.Values.Count(d => d.Status == DiffStatus.Removed);
        sb.AppendLine("## Verdict");
        sb.AppendLine();
        if (regressionCount > 0 || removedCount > 0)
            sb.AppendLine($"⚠️ **{regressionCount} namespace(s) with quality regressions, {removedCount} namespace(s) removed.** Review before merging.");
        else
            sb.AppendLine("✅ **No quality regressions detected.**");

        return sb.ToString();
    }

    private static List<string> ComputeHeadingsAdded(NamespaceFingerprint? baseline, NamespaceFingerprint? candidate)
    {
        var baseHeadings = baseline?.ToolFamilyArticle?.H2Headings ?? [];
        var candHeadings = candidate?.ToolFamilyArticle?.H2Headings ?? [];
        return candHeadings.Except(baseHeadings).ToList();
    }

    private static List<string> ComputeHeadingsRemoved(NamespaceFingerprint? baseline, NamespaceFingerprint? candidate)
    {
        var baseHeadings = baseline?.ToolFamilyArticle?.H2Headings ?? [];
        var candHeadings = candidate?.ToolFamilyArticle?.H2Headings ?? [];
        return baseHeadings.Except(candHeadings).ToList();
    }

    private static List<string> ComputeQualityRegressions(NamespaceFingerprint? baseline, NamespaceFingerprint? candidate)
    {
        if (baseline?.QualityMetrics is null || candidate?.QualityMetrics is null) return [];

        var regressions = new List<string>();
        var bq = baseline.QualityMetrics;
        var cq = candidate.QualityMetrics;

        if (cq.FutureTenseViolations > bq.FutureTenseViolations)
            regressions.Add($"Future tense violations: {bq.FutureTenseViolations} → {cq.FutureTenseViolations}");
        if (cq.FabricatedUrlCount > bq.FabricatedUrlCount)
            regressions.Add($"Fabricated URLs: {bq.FabricatedUrlCount} → {cq.FabricatedUrlCount}");
        if (cq.BrandingViolations > bq.BrandingViolations)
            regressions.Add($"Branding violations: {bq.BrandingViolations} → {cq.BrandingViolations}");
        if (cq.ContractionRate < bq.ContractionRate - 0.05)
            regressions.Add($"Contraction rate dropped: {bq.ContractionRate:P0} → {cq.ContractionRate:P0}");

        return regressions;
    }

    private static List<string> ComputeFieldsAdded(NamespaceFingerprint? baseline, NamespaceFingerprint? candidate)
    {
        var baseFields = baseline?.ToolFamilyArticle?.FrontmatterFields ?? [];
        var candFields = candidate?.ToolFamilyArticle?.FrontmatterFields ?? [];
        return candFields.Except(baseFields).ToList();
    }

    private static List<string> ComputeFieldsRemoved(NamespaceFingerprint? baseline, NamespaceFingerprint? candidate)
    {
        var baseFields = baseline?.ToolFamilyArticle?.FrontmatterFields ?? [];
        var candFields = candidate?.ToolFamilyArticle?.FrontmatterFields ?? [];
        return baseFields.Except(candFields).ToList();
    }

    private static string FormatDelta(int delta) => delta switch
    {
        > 0 => $"+{delta}",
        < 0 => $"{delta}",
        _ => "="
    };

    private static string FormatSizeDelta(long delta)
    {
        if (delta == 0) return "=";
        var prefix = delta > 0 ? "+" : "";
        return $"{prefix}{FormatSize(Math.Abs(delta))}";
    }

    internal static string FormatSize(long bytes) => bytes switch
    {
        >= 1_048_576 => $"{bytes / 1_048_576.0:F1} MB",
        >= 1024 => $"{bytes / 1024.0:F1} KB",
        _ => $"{bytes} B"
    };
}

/// <summary>
/// Result of comparing two snapshots.
/// </summary>
public sealed class SnapshotDiff
{
    public DateTime BaselineTimestamp { get; init; }
    public DateTime CandidateTimestamp { get; init; }
    public Dictionary<string, NamespaceDiff> NamespaceDiffs { get; init; } = new();
}

/// <summary>
/// Per-namespace diff result.
/// </summary>
public sealed class NamespaceDiff
{
    public DiffStatus Status { get; init; }
    public int FileCountDelta { get; init; }
    public long SizeDelta { get; init; }
    public NamespaceFingerprint? Baseline { get; init; }
    public NamespaceFingerprint? Candidate { get; init; }
    public List<string> HeadingsAdded { get; init; } = [];
    public List<string> HeadingsRemoved { get; init; } = [];
    public List<string> QualityRegressions { get; init; } = [];
    public List<string> FrontmatterFieldsAdded { get; init; } = [];
    public List<string> FrontmatterFieldsRemoved { get; init; } = [];
}

public enum DiffStatus
{
    Unchanged,
    Modified,
    Added,
    Removed
}
