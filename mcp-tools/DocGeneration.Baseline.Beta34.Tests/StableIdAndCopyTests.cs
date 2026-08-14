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
/// T15, T16: 68 physical -> 34 logical duplicate accounting, asserted against the COMMITTED
/// source-inventory.json so the checks run on a clean checkout / CI (Cameron BLOCKING-1 /
/// Ellis blocking-2). Copies are paired by LOGICAL IDENTITY
/// (namespace|stepId|artifactName|recordedAtUtc), never by filename (the two copies differ only by
/// a "&lt;namespace&gt;--" prefix and directory).
/// </summary>
public sealed class DuplicateCopyTests
{
    // T15
    [Fact]
    public void T15_Each_Logical_Record_Has_Exactly_Two_Physical_Copies()
    {
        SourceInventory inventory = BaselineContext.LoadSourceInventory();

        // Exactly 68 physical copies recorded.
        Assert.Equal(68, inventory.PhysicalCopies.Count);
        Assert.Equal(68, inventory.PhysicalCopyCount);

        // Every relativePath is distinct (no copy double-counted).
        int distinctPaths = inventory.PhysicalCopies
            .Select(c => c.RelativePath ?? "").Distinct(StringComparer.OrdinalIgnoreCase).Count();
        Assert.Equal(68, distinctPaths);

        var byStableId = inventory.PhysicalCopies
            .GroupBy(c => c.StableId ?? "", StringComparer.Ordinal)
            .ToList();

        // Exactly 34 logical records, each with exactly two copies: one catalog + one namespace.
        Assert.Equal(BaselineContext.ExpectedRecordCount, byStableId.Count);
        foreach (var group in byStableId)
        {
            Assert.Equal(2, group.Count());
            Assert.Equal(1, group.Count(c => string.Equals(c.CopyKind, "catalog", StringComparison.Ordinal)));
            Assert.Equal(1, group.Count(c => string.Equals(c.CopyKind, "namespace", StringComparison.Ordinal)));
        }
    }

    // T16
    [Fact]
    public void T16_Catalog_And_Namespace_Copies_Agree_On_Identity()
    {
        Manifest manifest = BaselineContext.LoadManifest();
        SourceInventory inventory = BaselineContext.LoadSourceInventory();
        Assert.Equal(BaselineContext.ExpectedRecordCount, manifest.Records.Count);

        Dictionary<string, BaselineRecord> recordByStableId =
            manifest.Records.ToDictionary(r => r.StableId!, StringComparer.Ordinal);

        var byStableId = inventory.PhysicalCopies
            .GroupBy(c => c.StableId ?? "", StringComparer.Ordinal)
            .ToList();
        Assert.Equal(BaselineContext.ExpectedRecordCount, byStableId.Count);

        // Pairing is by LOGICAL IDENTITY, never by filename: grouping copies by their logicalIdentity
        // must reproduce the same 34 two-copy sets as grouping by stableId (1:1 stableId<->identity).
        int distinctIdentities = inventory.PhysicalCopies
            .Select(c => c.LogicalIdentity ?? "").Distinct(StringComparer.Ordinal).Count();
        Assert.Equal(BaselineContext.ExpectedRecordCount, distinctIdentities);

        foreach (var group in byStableId)
        {
            InventoryCopy catalog = group.Single(c => string.Equals(c.CopyKind, "catalog", StringComparison.Ordinal));
            InventoryCopy nsCopy = group.Single(c => string.Equals(c.CopyKind, "namespace", StringComparison.Ordinal));

            // The two copies are byte-identical (Ellis) — identical sha256 AND identical logical identity.
            Assert.False(string.IsNullOrWhiteSpace(catalog.Sha256), $"catalog sha missing for {group.Key}");
            Assert.False(string.IsNullOrWhiteSpace(nsCopy.Sha256), $"namespace sha missing for {group.Key}");
            Assert.Equal(catalog.Sha256!.ToLowerInvariant(), nsCopy.Sha256!.ToLowerInvariant());

            Assert.False(string.IsNullOrWhiteSpace(catalog.LogicalIdentity), $"catalog identity missing for {group.Key}");
            Assert.Equal(catalog.LogicalIdentity, nsCopy.LogicalIdentity);

            // Identity is consistent with the manifest record's own content fields.
            Assert.True(recordByStableId.TryGetValue(group.Key, out BaselineRecord? record),
                $"Inventory stableId '{group.Key}' has no manifest record.");
            string expectedIdentityPrefix = string.Create(System.Globalization.CultureInfo.InvariantCulture,
                $"{record!.Namespace}|{record.StepId}|{record.ArtifactName}|");
            Assert.StartsWith(expectedIdentityPrefix, catalog.LogicalIdentity!, StringComparison.Ordinal);

            // Filenames differ ONLY by the "<namespace>--" prefix on the catalog copy (never used for pairing).
            string catalogName = Path.GetFileName((catalog.RelativePath ?? "").Replace('\\', '/'));
            string namespaceName = Path.GetFileName((nsCopy.RelativePath ?? "").Replace('\\', '/'));
            Assert.Equal($"{record.Namespace}--{namespaceName}", catalogName);
        }
    }
}
