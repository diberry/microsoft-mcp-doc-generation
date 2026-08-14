using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace DocGeneration.Baseline.Beta34.Tests;

/// <summary>
/// T5, T7–T14, T24: classification pinning, error-class accounting, Class-D dependency accounting,
/// and manifest schema completeness. All values are pinned exactly (no set-membership-only checks).
/// </summary>
public sealed class ClassificationTests
{
    private static readonly Regex CoverageSignature =
        new(@"missing '.*?' in example prompt", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ReconstructionSignature =
        new(@"parameter\(s\) documented but not present in source CLI JSON",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static Manifest Manifest() => BaselineContext.LoadManifest();

    // T5
    [Fact]
    public void T5_StableIds_Are_Unique_And_34()
    {
        Manifest manifest = Manifest();
        Assert.Equal(BaselineContext.ExpectedRecordCount, manifest.Records.Count);

        List<string> ids = manifest.Records.Select(r => r.StableId ?? "").ToList();
        Assert.DoesNotContain(ids, string.IsNullOrWhiteSpace);
        Assert.Equal(BaselineContext.ExpectedRecordCount, ids.Distinct(StringComparer.Ordinal).Count());
    }

    // T7
    [Fact]
    public void T7_EveryRecord_Classified_Exactly_Once()
    {
        Manifest manifest = Manifest();
        Assert.Equal(BaselineContext.ExpectedRecordCount, manifest.Records.Count);

        foreach (BaselineRecord r in manifest.Records)
        {
            Assert.Contains(r.Classification, BaselineContext.ValidClassifications);
        }

        // Bijection fixture <-> record: every fixture has exactly one record and vice versa.
        string[] fixtures = BaselineContext.FixtureFiles();
        Assert.Equal(BaselineContext.ExpectedRecordCount, fixtures.Length);

        HashSet<string> fixtureNames = fixtures.Select(Path.GetFileName).ToHashSet(StringComparer.Ordinal)!;
        HashSet<string> recordFixtureNames =
            manifest.Records.Select(r => r.FixtureFileName).ToHashSet(StringComparer.Ordinal);

        Assert.Equal(BaselineContext.ExpectedRecordCount, recordFixtureNames.Count);
        Assert.True(fixtureNames.SetEquals(recordFixtureNames),
            "Fixture files and manifest-linked fixture names are not a 1:1 bijection.");
    }

    // T8
    [Fact]
    public void T8_Classification_Union_Covers_All_34_No_Overlap()
    {
        Manifest manifest = Manifest();

        var buckets = BaselineContext.ValidClassifications.ToDictionary(
            c => c,
            c => manifest.Records.Where(r => r.Classification == c)
                .Select(r => r.StableId!).ToHashSet(StringComparer.Ordinal),
            StringComparer.Ordinal);

        // Pairwise disjoint.
        foreach (string a in BaselineContext.ValidClassifications)
        {
            foreach (string b in BaselineContext.ValidClassifications)
            {
                if (a == b) continue;
                Assert.Empty(buckets[a].Intersect(buckets[b], StringComparer.Ordinal));
            }
        }

        HashSet<string> union = new(StringComparer.Ordinal);
        foreach (string c in BaselineContext.ValidClassifications)
        {
            union.UnionWith(buckets[c]);
        }
        Assert.Equal(BaselineContext.ExpectedRecordCount, union.Count);
    }

    // T9
    [Fact]
    public void T9_ClassificationCounts_Are_Pinned()
    {
        Manifest manifest = Manifest();
        Assert.Equal(BaselineContext.ExpectedRecordCount, manifest.Records.Count);

        Assert.Equal(21, manifest.Records.Count(r => r.Classification == "root"));
        Assert.Equal(9, manifest.Records.Count(r => r.Classification == "cascade"));
        Assert.Equal(3, manifest.Records.Count(r => r.Classification == "mixed"));
        Assert.Equal(1, manifest.Records.Count(r => r.Classification == "diagnostic"));
    }

    // T10
    [Fact]
    public void T10_ErrorClassCounts_Are_Pinned()
    {
        Manifest manifest = Manifest();
        Assert.Equal(BaselineContext.ExpectedRecordCount, manifest.Records.Count);

        // Distinct A/B taxonomy with a 3-record A∩B overlap represented as the literal "A+B".
        Assert.Equal(29, manifest.Records.Count(r => (r.ErrorClass ?? "").Contains('A')));
        Assert.Equal(7, manifest.Records.Count(r => (r.ErrorClass ?? "").Contains('B')));
        Assert.Equal(3, manifest.Records.Count(r => r.ErrorClass == "A+B"));
        Assert.Equal(1, manifest.Records.Count(r => r.ErrorClass == "C"));

        // Distinct union A∪B∪C == 34 (each record has exactly one errorClass token/string).
        Assert.Equal(BaselineContext.ExpectedRecordCount,
            manifest.Records.Count(r => (r.ErrorClass ?? "").Contains('A')
                                     || (r.ErrorClass ?? "").Contains('B')
                                     || r.ErrorClass == "C"));
    }

    // T11
    [Fact]
    public void T11_ClassD_DependencyPairs_Equal_10_Via_UpstreamFlag()
    {
        Manifest manifest = Manifest();

        Assert.Equal(10, manifest.Records.Count(r => r.HasUpstreamStep2));

        BaselineRecord foundry = Single(manifest, "foundryextensions", 4);
        Assert.Equal("mixed", foundry.Classification);
        Assert.True(foundry.HasUpstreamStep2,
            "foundryextensions step-4 must record hasUpstreamStep2=true so Class-D accounting (10) reconciles independently of role=cascade (9).");
    }

    // T12
    [Fact]
    public void T12_Eventhubs_Step4_Is_Root_Not_Cascade()
    {
        Manifest manifest = Manifest();
        BaselineRecord eventhubs = Single(manifest, "eventhubs", 4);

        Assert.Equal("root", eventhubs.Classification);
        Assert.Equal("A", eventhubs.ErrorClass);
        Assert.False(eventhubs.HasUpstreamStep2);
    }

    // T13
    [Fact]
    public void T13_Diagnostic_Is_Exactly_Monitor_WebtestsGet()
    {
        Manifest manifest = Manifest();

        List<BaselineRecord> diagnostics =
            manifest.Records.Where(r => r.Classification == "diagnostic").ToList();
        Assert.Single(diagnostics);

        BaselineRecord d = diagnostics[0];
        Assert.Equal("monitor", d.Namespace);
        Assert.Equal(2, d.StepId);
        Assert.Equal("monitor webtests get", d.ArtifactName);
        Assert.Equal("C", d.ErrorClass);

        // C discriminator: empty validatorResults in the frozen fixture.
        JsonElement validators = ReadFixtureArray(d, "validatorResults");
        Assert.Equal(JsonValueKind.Array, validators.ValueKind);
        Assert.Equal(0, validators.GetArrayLength());
    }

    // T14
    [Fact]
    public void T14_MixedRecords_Have_Both_Signatures_And_NonMixed_Lack_Both()
    {
        Manifest manifest = Manifest();
        Assert.Equal(BaselineContext.ExpectedRecordCount, manifest.Records.Count);

        List<BaselineRecord> mixed =
            manifest.Records.Where(r => r.Classification == "mixed").ToList();
        Assert.Equal(3, mixed.Count);

        // Forward direction: every mixed record HAS both a coverage-divergence AND a reconstruction
        // signature, and is tagged A+B.
        foreach (BaselineRecord r in mixed)
        {
            string details = ReadFixtureDetailsText(r);
            Assert.True(CoverageSignature.IsMatch(details),
                $"Mixed record '{r.StableId}' is missing a coverage-divergence signature.");
            Assert.True(ReconstructionSignature.IsMatch(details),
                $"Mixed record '{r.StableId}' is missing a reconstruction signature.");
            Assert.Equal("A+B", r.ErrorClass);
        }

        // Reverse direction: every NON-mixed record must NOT carry BOTH signatures (only mixed does),
        // and must not be tagged A+B. This makes the discriminator bidirectional (Cameron note 4).
        List<BaselineRecord> nonMixed =
            manifest.Records.Where(r => r.Classification != "mixed").ToList();
        Assert.Equal(BaselineContext.ExpectedRecordCount - 3, nonMixed.Count);

        foreach (BaselineRecord r in nonMixed)
        {
            string details = ReadFixtureDetailsText(r);
            bool hasCoverage = CoverageSignature.IsMatch(details);
            bool hasReconstruction = ReconstructionSignature.IsMatch(details);
            Assert.False(hasCoverage && hasReconstruction,
                $"Non-mixed record '{r.StableId}' [{r.Classification}/{r.ErrorClass}] carries BOTH " +
                "signatures; only 'mixed' records may have both.");
            Assert.NotEqual("A+B", r.ErrorClass);
        }
    }

    // T24
    [Fact]
    public void T24_Manifest_Every_Entry_Has_All_Required_Fields()
    {
        Manifest manifest = Manifest();
        Assert.Equal(BaselineContext.ExpectedRecordCount, manifest.Records.Count);

        foreach (BaselineRecord r in manifest.Records)
        {
            Assert.False(string.IsNullOrWhiteSpace(r.StableId), "stableId missing");
            Assert.False(string.IsNullOrWhiteSpace(r.Namespace), $"namespace missing on {r.StableId}");
            Assert.True(r.StepId is 2 or 4, $"stepId invalid on {r.StableId}: {r.StepId}");
            Assert.False(string.IsNullOrWhiteSpace(r.ArtifactName), $"artifactName missing on {r.StableId}");
            Assert.False(string.IsNullOrWhiteSpace(r.SourceRelativePath), $"sourceRelativePath missing on {r.StableId}");
            AssertSha256(r.SourceSha256, r.StableId, "sourceSha256");
            AssertSha256(r.SanitizedSha256, r.StableId, "sanitizedSha256");
            Assert.Contains(r.Classification, BaselineContext.ValidClassifications);
            Assert.Contains(r.ErrorClass, BaselineContext.ValidErrorClasses);
            Assert.Equal(2, r.PhysicalCopies.Count);
            Assert.False(string.IsNullOrWhiteSpace(r.Rationale), $"rationale missing on {r.StableId}");
        }
    }

    private static void AssertSha256(string? value, string? id, string field)
    {
        Assert.False(string.IsNullOrWhiteSpace(value), $"{field} missing on {id}");
        Assert.Matches("^[0-9a-f]{64}$", value!.ToLowerInvariant());
    }

    private static BaselineRecord Single(Manifest manifest, string ns, int step)
    {
        List<BaselineRecord> matches =
            manifest.Records.Where(r => r.Namespace == ns && r.StepId == step).ToList();
        Assert.Single(matches);
        return matches[0];
    }

    private static JsonElement ReadFixtureArray(BaselineRecord r, string property)
    {
        string path = Path.Combine(BaselineContext.FixtureCriticalFailuresDir, r.FixtureFileName);
        Assert.True(File.Exists(path), $"Fixture missing for {r.StableId}: {path}");
        using JsonDocument doc = BaselineContext.ParseJson(File.ReadAllBytes(path));
        // Clone so the element survives disposal.
        return doc.RootElement.GetProperty(property).Clone();
    }

    private static string ReadFixtureDetailsText(BaselineRecord r)
    {
        JsonElement details = ReadFixtureArray(r, "details");
        return string.Join("\n", details.EnumerateArray().Select(e => e.GetString() ?? ""));
    }
}
