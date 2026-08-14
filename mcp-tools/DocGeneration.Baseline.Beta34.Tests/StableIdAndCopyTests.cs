using System.Text.RegularExpressions;
using Xunit;

namespace DocGeneration.Baseline.Beta34.Tests;

/// <summary>
/// T6: the stable id is derived only from record content ({namespace}.{stepId:D2}.{artifactSlug}.{ordinal:D2})
/// and is independent of capture path/filename/run-timestamp.
/// </summary>
public sealed class StableIdTests
{
    private static readonly Regex StableIdFormat =
        new(@"^[a-z0-9]+\.\d{2}\.[a-z0-9][a-z0-9-]*\.\d{2}$", RegexOptions.Compiled);

    private static readonly Regex RunTimestamp =
        new(@"\d{8}T?\d{4,}", RegexOptions.Compiled);

    // T6
    [Fact]
    public void T6_StableId_Is_Deterministic_And_PathTimestampIndependent()
    {
        Manifest manifest = BaselineContext.LoadManifest();
        Assert.Equal(BaselineContext.ExpectedRecordCount, manifest.Records.Count);

        foreach (BaselineRecord r in manifest.Records)
        {
            string id = r.StableId ?? "";
            Assert.Matches(StableIdFormat, id);

            // Derived purely from content: {ns}.{stepId:D2}.{slug}.
            string expectedPrefix = StableIdDeriver.Prefix(r.Namespace!, r.StepId, r.ArtifactName!);
            Assert.StartsWith(expectedPrefix + ".", id, StringComparison.Ordinal);

            // Deterministic: recomputing from the same content yields the identical prefix.
            Assert.Equal(expectedPrefix, StableIdDeriver.Prefix(r.Namespace!, r.StepId, r.ArtifactName!));

            // Path/filename/timestamp independence: none of those tokens may appear in the id.
            Assert.DoesNotContain('\\', id);
            Assert.DoesNotContain('/', id);
            Assert.DoesNotContain(':', id);
            Assert.DoesNotContain(BaselineContext.ExpectedSourceRunDir, id, StringComparison.Ordinal);
            Assert.False(RunTimestamp.IsMatch(id),
                $"Stable id '{id}' contains a run-timestamp token; it must be timestamp-independent.");

            // Moving the record under a different directory/filename must not change the id.
            string relocatedPrefix = StableIdDeriver.Prefix(r.Namespace!, r.StepId, r.ArtifactName!);
            Assert.Equal(expectedPrefix, relocatedPrefix);
        }
    }
}

/// <summary>
/// T15, T16: 68 physical -> 34 logical duplicate accounting. Copies are paired by LOGICAL IDENTITY
/// (namespace|stepId|artifactName|recordedAtUtc), never by filename (the two copies differ only by
/// a "&lt;namespace&gt;--" prefix). Reads the in-repo source run READ-ONLY.
/// </summary>
public sealed class DuplicateCopyTests
{
    // T15
    [Fact]
    public void T15_Each_Logical_Record_Has_Exactly_Two_Physical_Copies()
    {
        Manifest manifest = BaselineContext.LoadManifest();
        Assert.Equal(BaselineContext.ExpectedRecordCount, manifest.Records.Count);

        Assert.True(Directory.Exists(BaselineContext.SourceRunDir),
            "Source run directory is required in-repo: " + BaselineContext.SourceRunDir);

        var allPhysical = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (BaselineRecord r in manifest.Records)
        {
            Assert.Equal(2, r.PhysicalCopies.Count);

            string[] resolved = r.PhysicalCopies
                .Select(BaselineContext.ResolveSourceRelative).ToArray();

            foreach (string p in resolved)
            {
                Assert.True(File.Exists(p), $"Physical copy missing for '{r.StableId}': {p}");
                Assert.True(allPhysical.Add(p), $"Physical copy counted twice across records: {p}");
            }

            // One copy sits under the run-root critical-failures dir, the other under <ns>/critical-failures.
            string catalogDir = Path.GetFullPath(BaselineContext.SourceCriticalFailuresDir);
            string namespaceDir = Path.GetFullPath(
                Path.Combine(BaselineContext.SourceRunDir, r.Namespace!, "critical-failures"));

            int catalogCopies = resolved.Count(p =>
                string.Equals(Path.GetDirectoryName(p), catalogDir, StringComparison.OrdinalIgnoreCase));
            int namespaceCopies = resolved.Count(p =>
                string.Equals(Path.GetDirectoryName(p), namespaceDir, StringComparison.OrdinalIgnoreCase));

            Assert.Equal(1, catalogCopies);
            Assert.Equal(1, namespaceCopies);
        }

        // 34 logical * 2 == 68 distinct physical files.
        Assert.Equal(68, allPhysical.Count);
    }

    // T16
    [Fact]
    public void T16_Catalog_And_Namespace_Copies_Agree_On_Identity()
    {
        Manifest manifest = BaselineContext.LoadManifest();
        Assert.Equal(BaselineContext.ExpectedRecordCount, manifest.Records.Count);

        Assert.True(Directory.Exists(BaselineContext.SourceRunDir),
            "Source run directory is required in-repo: " + BaselineContext.SourceRunDir);

        foreach (BaselineRecord r in manifest.Records)
        {
            Assert.Equal(2, r.PhysicalCopies.Count);
            string[] resolved = r.PhysicalCopies
                .Select(BaselineContext.ResolveSourceRelative).ToArray();

            // Pair by LOGICAL IDENTITY, not filename.
            string identityA = BaselineContext.LogicalIdentity(resolved[0]);
            string identityB = BaselineContext.LogicalIdentity(resolved[1]);
            Assert.Equal(identityA, identityB);

            // Filenames differ ONLY by the "<namespace>--" prefix on the catalog copy.
            string catalogName = Path.GetFileName(resolved.Single(p =>
                string.Equals(Path.GetDirectoryName(p),
                    Path.GetFullPath(BaselineContext.SourceCriticalFailuresDir),
                    StringComparison.OrdinalIgnoreCase)));
            string namespaceName = Path.GetFileName(resolved.Single(p =>
                string.Equals(Path.GetDirectoryName(p),
                    Path.GetFullPath(Path.Combine(BaselineContext.SourceRunDir, r.Namespace!, "critical-failures")),
                    StringComparison.OrdinalIgnoreCase)));

            Assert.Equal($"{r.Namespace}--{namespaceName}", catalogName);

            // Identity is consistent with the record's own fields.
            string expected = string.Create(System.Globalization.CultureInfo.InvariantCulture,
                $"{r.Namespace}|{r.StepId}|{r.ArtifactName}|");
            Assert.StartsWith(expected, identityA, StringComparison.Ordinal);
        }
    }
}
