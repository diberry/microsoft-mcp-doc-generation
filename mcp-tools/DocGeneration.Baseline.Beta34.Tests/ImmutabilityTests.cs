using Xunit;

namespace DocGeneration.Baseline.Beta34.Tests;

/// <summary>
/// T1–T4: fixture inventory + capture/immutability hashes. These read ONLY the committed fixtures,
/// manifest, and source-inventory.json, so they pass on a clean checkout / CI (Cameron BLOCKING-1 /
/// Ellis blocking-2). T4b is an additive, opt-in deep check that hashes the live (gitignored) source
/// run when BETA34_VERIFY_SOURCE_RUN=1.
/// </summary>
public sealed class ImmutabilityTests
{
    // T1
    [Fact]
    public void T1_FixtureCount_Is_Exactly_34()
    {
        string[] fixtures = BaselineContext.FixtureFiles();
        Assert.Equal(BaselineContext.ExpectedRecordCount, fixtures.Length);
    }

    // T2
    [Fact]
    public void T2_ManifestRecordCount_Equals_SourceCatalogCount()
    {
        Manifest manifest = BaselineContext.LoadManifest();
        SourceInventory inventory = BaselineContext.LoadSourceInventory();

        // Primary assertion source is the COMMITTED inventory, so this runs on a clean checkout
        // without the gitignored source run directory (Cameron BLOCKING-1 / Ellis blocking-2).
        Assert.Equal(BaselineContext.ExpectedRecordCount, manifest.Records.Count);
        Assert.Equal(BaselineContext.ExpectedRecordCount, inventory.LogicalRecordCount);

        int distinctStableIds = inventory.PhysicalCopies
            .Select(c => c.StableId ?? "").Distinct(StringComparer.Ordinal).Count();
        Assert.Equal(BaselineContext.ExpectedRecordCount, distinctStableIds);

        int distinctCatalogCopies = inventory.PhysicalCopies
            .Where(c => string.Equals(c.CopyKind, "catalog", StringComparison.Ordinal))
            .Select(c => c.StableId ?? "").Distinct(StringComparer.Ordinal).Count();
        Assert.Equal(BaselineContext.ExpectedRecordCount, distinctCatalogCopies);
    }

    // T4
    [Fact]
    public void T4_EachSource_Sha256_Matches_Manifest_SourceSha256()
    {
        Manifest manifest = BaselineContext.LoadManifest();
        SourceInventory inventory = BaselineContext.LoadSourceInventory();
        Assert.Equal(BaselineContext.ExpectedRecordCount, manifest.Records.Count);

        // Map each stableId to its CATALOG copy sha256 from the committed inventory.
        Dictionary<string, string> catalogShaByStableId = inventory.PhysicalCopies
            .Where(c => string.Equals(c.CopyKind, "catalog", StringComparison.Ordinal))
            .ToDictionary(c => c.StableId!, c => c.Sha256!.ToLowerInvariant(), StringComparer.Ordinal);
        Assert.Equal(BaselineContext.ExpectedRecordCount, catalogShaByStableId.Count);

        foreach (BaselineRecord record in manifest.Records)
        {
            Assert.False(string.IsNullOrWhiteSpace(record.SourceSha256),
                $"Manifest sourceSha256 missing for '{record.StableId}'.");
            Assert.True(catalogShaByStableId.TryGetValue(record.StableId!, out string? catalogSha),
                $"Inventory has no catalog copy for manifest record '{record.StableId}'.");
            Assert.Equal(catalogSha, record.SourceSha256!.ToLowerInvariant());
        }
    }

    // T4b — additive DEEP verification: hash the live (gitignored) source run and prove every
    // recorded sha256 matches the on-disk bytes. It cannot run on CI, so it is an EXPLICIT opt-in
    // (BETA34_VERIFY_SOURCE_RUN=1). The skip is VISIBLE (reported as a skipped test with a reason via
    // SkippableFact) and never masks a failure: once opted in, a missing source run or any hash
    // mismatch FAILS the test.
    [SkippableFact]
    public void T4b_DeepVerify_LiveSourceRun_Hashes_Match_Inventory()
    {
        Skip.IfNot(
            string.Equals(Environment.GetEnvironmentVariable("BETA34_VERIFY_SOURCE_RUN"), "1", StringComparison.Ordinal),
            "Deep source-run verification is opt-in (set BETA34_VERIFY_SOURCE_RUN=1). " +
            "The source run 'generated-20260813T162453/' is gitignored and absent on CI/fresh clones, " +
            "so the primary immutability guarantee is asserted against the committed source-inventory.json.");

        SourceInventory inventory = BaselineContext.LoadSourceInventory();
        Assert.Equal(68, inventory.PhysicalCopies.Count);

        foreach (InventoryCopy copy in inventory.PhysicalCopies)
        {
            Assert.False(string.IsNullOrWhiteSpace(copy.RelativePath), "Inventory copy has no relativePath.");
            string path = BaselineContext.ResolveRepoRelative(copy.RelativePath!);
            Assert.True(File.Exists(path),
                $"Opted-in deep verification but source copy is missing on disk: {path}");

            string actual = BaselineContext.Sha256Hex(File.ReadAllBytes(path));
            Assert.False(string.IsNullOrWhiteSpace(copy.Sha256), $"Inventory sha256 missing for '{copy.RelativePath}'.");
            Assert.Equal(copy.Sha256!.ToLowerInvariant(), actual);
        }
    }

    // T3
    [Fact]
    public void T3_EachFixture_Sha256_Matches_Manifest_SanitizedSha256()
    {
        string[] fixtures = BaselineContext.FixtureFiles();
        Assert.Equal(BaselineContext.ExpectedRecordCount, fixtures.Length);

        Manifest manifest = BaselineContext.LoadManifest();
        Dictionary<string, BaselineRecord> byFixture =
            manifest.Records.ToDictionary(r => r.FixtureFileName, StringComparer.Ordinal);

        foreach (string fixture in fixtures)
        {
            string name = Path.GetFileName(fixture);
            Assert.True(byFixture.TryGetValue(name, out BaselineRecord? record),
                $"No manifest record links to fixture '{name}'.");

            string actual = BaselineContext.Sha256Hex(File.ReadAllBytes(fixture));
            Assert.False(string.IsNullOrWhiteSpace(record!.SanitizedSha256),
                $"Manifest sanitizedSha256 missing for fixture '{name}'.");
            Assert.Equal(record.SanitizedSha256!.ToLowerInvariant(), actual);
        }
    }
}
