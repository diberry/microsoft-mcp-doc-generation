namespace DocGeneration.Tools.Fingerprint.Tests;

public class SnapshotDifferTests
{
    [Fact]
    public void ComputeDiff_IdenticalSnapshots_NoChanges()
    {
        var snapshot = CreateSampleSnapshot("advisor", fileCount: 71, sizeBytes: 4000);
        var diff = SnapshotDiffer.ComputeDiff(snapshot, snapshot);

        Assert.Single(diff.NamespaceDiffs);
        var nsDiff = diff.NamespaceDiffs["advisor"];
        Assert.Equal(0, nsDiff.FileCountDelta);
        Assert.Equal(0, nsDiff.SizeDelta);
        Assert.Empty(nsDiff.QualityRegressions);
    }

    [Fact]
    public void ComputeDiff_NewNamespace_MarkedAsAdded()
    {
        var baseline = new FingerprintSnapshot();
        var candidate = CreateSampleSnapshot("advisor", fileCount: 71, sizeBytes: 4000);

        var diff = SnapshotDiffer.ComputeDiff(baseline, candidate);

        Assert.Single(diff.NamespaceDiffs);
        Assert.Equal(DiffStatus.Added, diff.NamespaceDiffs["advisor"].Status);
    }

    [Fact]
    public void ComputeDiff_RemovedNamespace_MarkedAsRemoved()
    {
        var baseline = CreateSampleSnapshot("advisor", fileCount: 71, sizeBytes: 4000);
        var candidate = new FingerprintSnapshot();

        var diff = SnapshotDiffer.ComputeDiff(baseline, candidate);

        Assert.Single(diff.NamespaceDiffs);
        Assert.Equal(DiffStatus.Removed, diff.NamespaceDiffs["advisor"].Status);
    }

    [Fact]
    public void ComputeDiff_FileCountChange_ReportsDelta()
    {
        var baseline = CreateSampleSnapshot("storage", fileCount: 90, sizeBytes: 4000);
        var candidate = CreateSampleSnapshot("storage", fileCount: 95, sizeBytes: 4500);

        var diff = SnapshotDiffer.ComputeDiff(baseline, candidate);

        Assert.Equal(5, diff.NamespaceDiffs["storage"].FileCountDelta);
        Assert.Equal(500, diff.NamespaceDiffs["storage"].SizeDelta);
    }

    [Fact]
    public void ComputeDiff_HeadingAdded_ReportsAddition()
    {
        var baseline = CreateSnapshotWithHeadings("advisor", ["## Get recommendations", "## Related content"]);
        var candidate = CreateSnapshotWithHeadings("advisor", ["## Get recommendations", "## List configs", "## Related content"]);

        var diff = SnapshotDiffer.ComputeDiff(baseline, candidate);

        Assert.Contains("## List configs", diff.NamespaceDiffs["advisor"].HeadingsAdded);
        Assert.Empty(diff.NamespaceDiffs["advisor"].HeadingsRemoved);
    }

    [Fact]
    public void ComputeDiff_HeadingRemoved_ReportsRemoval()
    {
        var baseline = CreateSnapshotWithHeadings("advisor", ["## Get recommendations", "## Deprecated tool", "## Related content"]);
        var candidate = CreateSnapshotWithHeadings("advisor", ["## Get recommendations", "## Related content"]);

        var diff = SnapshotDiffer.ComputeDiff(baseline, candidate);

        Assert.Contains("## Deprecated tool", diff.NamespaceDiffs["advisor"].HeadingsRemoved);
        Assert.Empty(diff.NamespaceDiffs["advisor"].HeadingsAdded);
    }

    [Fact]
    public void ComputeDiff_QualityRegression_FutureTense()
    {
        var baseline = CreateSnapshotWithQuality("storage", futureTense: 0, fabricated: 0, branding: 0, contraction: 0.8);
        var candidate = CreateSnapshotWithQuality("storage", futureTense: 3, fabricated: 0, branding: 0, contraction: 0.8);

        var diff = SnapshotDiffer.ComputeDiff(baseline, candidate);

        Assert.NotEmpty(diff.NamespaceDiffs["storage"].QualityRegressions);
        Assert.Contains(diff.NamespaceDiffs["storage"].QualityRegressions,
            r => r.Contains("Future tense"));
    }

    [Fact]
    public void ComputeDiff_QualityImprovement_NoRegression()
    {
        var baseline = CreateSnapshotWithQuality("storage", futureTense: 5, fabricated: 2, branding: 1, contraction: 0.5);
        var candidate = CreateSnapshotWithQuality("storage", futureTense: 0, fabricated: 0, branding: 0, contraction: 0.9);

        var diff = SnapshotDiffer.ComputeDiff(baseline, candidate);

        Assert.Empty(diff.NamespaceDiffs["storage"].QualityRegressions);
    }

    [Fact]
    public void ComputeDiff_ContractionRateDrop_ReportsRegression()
    {
        var baseline = CreateSnapshotWithQuality("cosmos", futureTense: 0, fabricated: 0, branding: 0, contraction: 0.9);
        var candidate = CreateSnapshotWithQuality("cosmos", futureTense: 0, fabricated: 0, branding: 0, contraction: 0.7);

        var diff = SnapshotDiffer.ComputeDiff(baseline, candidate);

        Assert.NotEmpty(diff.NamespaceDiffs["cosmos"].QualityRegressions);
        Assert.Contains(diff.NamespaceDiffs["cosmos"].QualityRegressions,
            r => r.Contains("Contraction rate"));
    }

    [Fact]
    public void ComputeDiff_MultipleNamespaces_TracksAll()
    {
        var baseline = new FingerprintSnapshot
        {
            Namespaces = new()
            {
                ["advisor"] = new NamespaceFingerprint { FileCount = 71, TotalSizeBytes = 4000 },
                ["storage"] = new NamespaceFingerprint { FileCount = 95, TotalSizeBytes = 5000 }
            }
        };
        var candidate = new FingerprintSnapshot
        {
            Namespaces = new()
            {
                ["advisor"] = new NamespaceFingerprint { FileCount = 72, TotalSizeBytes = 4100 },
                ["storage"] = new NamespaceFingerprint { FileCount = 95, TotalSizeBytes = 5000 },
                ["cosmos"] = new NamespaceFingerprint { FileCount = 50, TotalSizeBytes = 3000 }
            }
        };

        var diff = SnapshotDiffer.ComputeDiff(baseline, candidate);

        Assert.Equal(3, diff.NamespaceDiffs.Count);
        Assert.Equal(DiffStatus.Modified, diff.NamespaceDiffs["advisor"].Status);
        Assert.Equal(DiffStatus.Modified, diff.NamespaceDiffs["storage"].Status);
        Assert.Equal(DiffStatus.Added, diff.NamespaceDiffs["cosmos"].Status);
    }

    [Fact]
    public void ComputeDiff_FrontmatterFieldAdded_ReportsChange()
    {
        var baseline = CreateSnapshotWithFrontmatter("advisor", ["title", "description", "ms.date"]);
        var candidate = CreateSnapshotWithFrontmatter("advisor", ["title", "description", "ms.date", "ms.reviewer"]);

        var diff = SnapshotDiffer.ComputeDiff(baseline, candidate);

        Assert.Contains("ms.reviewer", diff.NamespaceDiffs["advisor"].FrontmatterFieldsAdded);
    }

    [Fact]
    public void ComputeDiff_FrontmatterFieldRemoved_ReportsChange()
    {
        var baseline = CreateSnapshotWithFrontmatter("advisor", ["title", "description", "ms.date", "ms.reviewer"]);
        var candidate = CreateSnapshotWithFrontmatter("advisor", ["title", "description", "ms.date"]);

        var diff = SnapshotDiffer.ComputeDiff(baseline, candidate);

        Assert.Contains("ms.reviewer", diff.NamespaceDiffs["advisor"].FrontmatterFieldsRemoved);
    }

    [Fact]
    public void GenerateReport_ContainsExpectedSections()
    {
        var baseline = CreateSampleSnapshot("advisor", fileCount: 71, sizeBytes: 4000);
        var candidate = CreateSampleSnapshot("advisor", fileCount: 75, sizeBytes: 4500);

        var diff = SnapshotDiffer.ComputeDiff(baseline, candidate);
        var report = SnapshotDiffer.GenerateReport(diff);

        Assert.Contains("# Fingerprint Diff Report", report);
        Assert.Contains("## Summary", report);
        Assert.Contains("## Verdict", report);
        Assert.Contains("advisor", report);
    }

    [Fact]
    public void GenerateReport_NoRegressions_ShowsCleanVerdict()
    {
        var snapshot = CreateSampleSnapshot("advisor", fileCount: 71, sizeBytes: 4000);
        var diff = SnapshotDiffer.ComputeDiff(snapshot, snapshot);
        var report = SnapshotDiffer.GenerateReport(diff);

        Assert.Contains("No quality regressions detected", report);
    }

    [Fact]
    public void GenerateReport_WithRegressions_ShowsWarning()
    {
        var baseline = CreateSnapshotWithQuality("storage", futureTense: 0, fabricated: 0, branding: 0, contraction: 0.8);
        var candidate = CreateSnapshotWithQuality("storage", futureTense: 5, fabricated: 0, branding: 0, contraction: 0.8);

        var diff = SnapshotDiffer.ComputeDiff(baseline, candidate);
        var report = SnapshotDiffer.GenerateReport(diff);

        Assert.Contains("⚠️", report);
        Assert.Contains("quality regressions", report);
    }

    [Fact]
    public void FormatSize_FormatsCorrectly()
    {
        Assert.Equal("0 B", SnapshotDiffer.FormatSize(0));
        Assert.Equal("512 B", SnapshotDiffer.FormatSize(512));
        Assert.Equal("1.0 KB", SnapshotDiffer.FormatSize(1024));
        Assert.Equal("1.5 KB", SnapshotDiffer.FormatSize(1536));
        Assert.Equal("1.0 MB", SnapshotDiffer.FormatSize(1_048_576));
        Assert.Equal("4.3 MB", SnapshotDiffer.FormatSize(4_500_000));
    }

    [Fact]
    public void ComputeDiff_BothEmpty_NoNamespaceDiffs()
    {
        var diff = SnapshotDiffer.ComputeDiff(new FingerprintSnapshot(), new FingerprintSnapshot());
        Assert.Empty(diff.NamespaceDiffs);
    }

    [Theory]
    [InlineData(0.90, 0.85, false)]  // Exactly 0.05 drop — NOT a regression (< not <=)
    [InlineData(0.90, 0.849, true)]  // Just past threshold — IS a regression
    [InlineData(0.90, 0.851, false)] // Just under threshold — NOT a regression
    [InlineData(0.50, 0.50, false)]  // No change — NOT a regression
    public void ComputeDiff_ContractionRateThreshold_BoundaryBehavior(
        double baselineRate, double candidateRate, bool expectRegression)
    {
        var baseline = CreateSnapshotWithQuality("test", futureTense: 0, fabricated: 0, branding: 0, contraction: baselineRate);
        var candidate = CreateSnapshotWithQuality("test", futureTense: 0, fabricated: 0, branding: 0, contraction: candidateRate);

        var diff = SnapshotDiffer.ComputeDiff(baseline, candidate);

        if (expectRegression)
            Assert.NotEmpty(diff.NamespaceDiffs["test"].QualityRegressions);
        else
            Assert.Empty(diff.NamespaceDiffs["test"].QualityRegressions);
    }

    [Fact]
    public void ComputeDiff_FabricatedUrlRegression_Detected()
    {
        var baseline = CreateSnapshotWithQuality("search", futureTense: 0, fabricated: 0, branding: 0, contraction: 0.8);
        var candidate = CreateSnapshotWithQuality("search", futureTense: 0, fabricated: 3, branding: 0, contraction: 0.8);

        var diff = SnapshotDiffer.ComputeDiff(baseline, candidate);

        Assert.NotEmpty(diff.NamespaceDiffs["search"].QualityRegressions);
        Assert.Contains(diff.NamespaceDiffs["search"].QualityRegressions,
            r => r.Contains("Fabricated URLs"));
    }

    [Fact]
    public void ComputeDiff_BrandingRegression_Detected()
    {
        var baseline = CreateSnapshotWithQuality("keyvault", futureTense: 0, fabricated: 0, branding: 1, contraction: 0.8);
        var candidate = CreateSnapshotWithQuality("keyvault", futureTense: 0, fabricated: 0, branding: 4, contraction: 0.8);

        var diff = SnapshotDiffer.ComputeDiff(baseline, candidate);

        Assert.NotEmpty(diff.NamespaceDiffs["keyvault"].QualityRegressions);
        Assert.Contains(diff.NamespaceDiffs["keyvault"].QualityRegressions,
            r => r.Contains("Branding violations"));
    }

    // --- Helpers ---

    private static FingerprintSnapshot CreateSampleSnapshot(string ns, int fileCount, long sizeBytes)
    {
        return new FingerprintSnapshot
        {
            Namespaces = new()
            {
                [ns] = new NamespaceFingerprint
                {
                    FileCount = fileCount,
                    TotalSizeBytes = sizeBytes
                }
            }
        };
    }

    private static FingerprintSnapshot CreateSnapshotWithHeadings(string ns, List<string> headings)
    {
        return new FingerprintSnapshot
        {
            Namespaces = new()
            {
                [ns] = new NamespaceFingerprint
                {
                    FileCount = 10,
                    TotalSizeBytes = 1000,
                    ToolFamilyArticle = new ArticleFingerprint
                    {
                        FileName = $"{ns}.md",
                        H2Headings = headings
                    }
                }
            }
        };
    }

    private static FingerprintSnapshot CreateSnapshotWithQuality(string ns,
        int futureTense, int fabricated, int branding, double contraction)
    {
        return new FingerprintSnapshot
        {
            Namespaces = new()
            {
                [ns] = new NamespaceFingerprint
                {
                    FileCount = 50,
                    TotalSizeBytes = 3000,
                    QualityMetrics = new QualityFingerprint
                    {
                        FutureTenseViolations = futureTense,
                        FabricatedUrlCount = fabricated,
                        BrandingViolations = branding,
                        ContractionRate = contraction
                    }
                }
            }
        };
    }

    private static FingerprintSnapshot CreateSnapshotWithFrontmatter(string ns, List<string> fields)
    {
        return new FingerprintSnapshot
        {
            Namespaces = new()
            {
                [ns] = new NamespaceFingerprint
                {
                    FileCount = 10,
                    TotalSizeBytes = 1000,
                    ToolFamilyArticle = new ArticleFingerprint
                    {
                        FileName = $"{ns}.md",
                        FrontmatterFields = fields
                    }
                }
            }
        };
    }
}
