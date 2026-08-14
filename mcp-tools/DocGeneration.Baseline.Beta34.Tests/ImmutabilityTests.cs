using Xunit;

namespace DocGeneration.Baseline.Beta34.Tests;

/// <summary>
/// T1–T4: fixture inventory + capture/immutability hashes. These read the frozen fixtures and
/// manifest; T2/T4 additionally read the in-repo source run READ-ONLY to prove capture integrity.
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

        Assert.Equal(BaselineContext.ExpectedRecordCount, manifest.Records.Count);

        // Read-only integrity cross-check against the committed source capture.
        Assert.True(Directory.Exists(BaselineContext.SourceCriticalFailuresDir),
            "Source run critical-failures directory is required in-repo: " +
            BaselineContext.SourceCriticalFailuresDir);
        int sourceCount = BaselineContext.SourceCatalogFiles().Length;
        Assert.Equal(BaselineContext.ExpectedRecordCount, sourceCount);
        Assert.Equal(sourceCount, manifest.Records.Count);
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

    // T4
    [Fact]
    public void T4_EachSource_Sha256_Matches_Manifest_SourceSha256()
    {
        Manifest manifest = BaselineContext.LoadManifest();
        Assert.Equal(BaselineContext.ExpectedRecordCount, manifest.Records.Count);

        Assert.True(Directory.Exists(BaselineContext.SourceRunDir),
            "Source run directory is required in-repo for capture-integrity verification: " +
            BaselineContext.SourceRunDir);

        foreach (BaselineRecord record in manifest.Records)
        {
            Assert.False(string.IsNullOrWhiteSpace(record.SourceRelativePath),
                $"Record '{record.StableId}' has no sourceRelativePath.");
            string sourcePath = BaselineContext.ResolveSourceRelative(record.SourceRelativePath!);
            Assert.True(File.Exists(sourcePath),
                $"Source capture missing for '{record.StableId}': {sourcePath}");

            string actual = BaselineContext.Sha256Hex(File.ReadAllBytes(sourcePath));
            Assert.False(string.IsNullOrWhiteSpace(record.SourceSha256),
                $"Manifest sourceSha256 missing for '{record.StableId}'.");
            Assert.Equal(record.SourceSha256!.ToLowerInvariant(), actual);
        }
    }
}
