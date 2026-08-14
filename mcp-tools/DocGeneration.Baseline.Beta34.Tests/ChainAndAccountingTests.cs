using Xunit;

namespace DocGeneration.Baseline.Beta34.Tests;

/// <summary>
/// New-contract tests introduced by Quinn's regenerated manifest (chainRole / errorClasses /
/// upstreamStableIds / accounting). These close Ellis blocking-3 and blocking-4: the Class-D
/// dependency accounting and the error-overlap taxonomy are reconciled INDEPENDENTLY of the
/// classification/role fields, and every number in the top-level <c>accounting</c> block is
/// recomputed from the records + inventory rather than echoed. All values are pinned exactly
/// (AD-010: no vacuous / set-membership-only assertions).
/// </summary>
public sealed class ChainAndAccountingTests
{
    // Proof cases resolved from the manifest (see PR #814 evidence): mixed error-overlap does NOT
    // dictate chain role — two mixed records are chain roots, one is a chain cascade.
    private const string PostgresStep4 = "postgres.04.postgres.01";
    private const string LoadTestingStep4 = "loadtesting.04.loadtesting.01";
    private const string FoundryStep4 = "foundryextensions.04.foundryextensions.01";
    private const string StorageStep4 = "storage.04.storage.01";

    private static Manifest Manifest() => BaselineContext.LoadManifest();

    private static BaselineRecord ById(Manifest m, string stableId)
    {
        BaselineRecord? r = m.Records.SingleOrDefault(x => x.StableId == stableId);
        Assert.True(r is not null, $"Expected exactly one manifest record with stableId '{stableId}'.");
        return r!;
    }

    [Fact]
    public void ChainRole_Counts_Are_Pinned()
    {
        Manifest m = Manifest();
        Assert.Equal(BaselineContext.ExpectedRecordCount, m.Records.Count);

        // Every record carries exactly one valid chain role.
        foreach (BaselineRecord r in m.Records)
        {
            Assert.Contains(r.ChainRole, BaselineContext.ValidChainRoles);
        }

        int root = m.Records.Count(r => r.ChainRole == "root");
        int cascade = m.Records.Count(r => r.ChainRole == "cascade");

        Assert.Equal(24, root);
        Assert.Equal(10, cascade);
        Assert.Equal(BaselineContext.ExpectedRecordCount, root + cascade);
    }

    [Fact]
    public void ChainRole_Is_Decoupled_From_ErrorOverlap()
    {
        Manifest m = Manifest();

        // Two A+B (mixed) records are chain ROOTS — error overlap does not force a cascade role.
        foreach (string id in new[] { PostgresStep4, LoadTestingStep4 })
        {
            BaselineRecord r = ById(m, id);
            Assert.Equal("mixed", r.Classification);
            Assert.Equal("A+B", r.ErrorClass);
            Assert.Equal("root", r.ChainRole);
        }

        // One A+B (mixed) record IS a chain cascade — proving the two axes are independent.
        BaselineRecord foundry = ById(m, FoundryStep4);
        Assert.Equal("mixed", foundry.Classification);
        Assert.Equal("A+B", foundry.ErrorClass);
        Assert.Equal("cascade", foundry.ChainRole);
    }

    [Fact]
    public void UpstreamStableIds_Reconcile_ClassD_Accounting()
    {
        Manifest m = Manifest();
        Assert.Equal(BaselineContext.ExpectedRecordCount, m.Records.Count);

        var byId = m.Records.ToDictionary(r => r.StableId!, StringComparer.Ordinal);

        // Exactly 10 dependent records; exactly 16 dependency links in total.
        int dependentRecords = m.Records.Count(r => r.UpstreamStableIds.Count > 0);
        int totalLinks = m.Records.Sum(r => r.UpstreamStableIds.Count);
        Assert.Equal(10, dependentRecords);
        Assert.Equal(16, totalLinks);

        // The storage Step-4 record fans out to exactly two upstream Step-2 records.
        BaselineRecord storage = ById(m, StorageStep4);
        Assert.Equal(2, storage.UpstreamStableIds.Count);

        foreach (BaselineRecord r in m.Records)
        {
            // hasUpstreamStep2 is the boolean projection of the array for ALL 34 records.
            Assert.Equal(r.UpstreamStableIds.Count > 0, r.HasUpstreamStep2);

            foreach (string upstreamId in r.UpstreamStableIds)
            {
                Assert.True(byId.TryGetValue(upstreamId, out BaselineRecord? upstream),
                    $"Record '{r.StableId}' references missing upstream '{upstreamId}'.");
                Assert.Equal(2, upstream!.StepId);
                Assert.Equal(r.Namespace, upstream.Namespace);
            }
        }
    }

    [Fact]
    public void ErrorClasses_Array_Reconciles_With_ErrorClass_Counts()
    {
        Manifest m = Manifest();
        Assert.Equal(BaselineContext.ExpectedRecordCount, m.Records.Count);

        // Every record has a non-empty errorClasses array drawn only from the {A,B,C} alphabet, and
        // the array is consistent with the scalar errorClass string.
        foreach (BaselineRecord r in m.Records)
        {
            Assert.NotEmpty(r.ErrorClasses);
            foreach (string token in r.ErrorClasses)
            {
                Assert.Contains(token, new[] { "A", "B", "C" });
            }
            string joined = string.Join("+", r.ErrorClasses);
            Assert.Equal(r.ErrorClass, joined);
        }

        int aContaining = m.Records.Count(r => r.ErrorClasses.Contains("A"));
        int bContaining = m.Records.Count(r => r.ErrorClasses.Contains("B"));
        int both = m.Records.Count(r => r.ErrorClasses.Contains("A") && r.ErrorClasses.Contains("B"));
        int cOnly = m.Records.Count(r => r.ErrorClasses.Contains("C"));

        Assert.Equal(29, aContaining);
        Assert.Equal(7, bContaining);
        Assert.Equal(3, both);
        Assert.Equal(1, cOnly);
    }

    [Fact]
    public void Accounting_Block_Matches_Independently_Computed_Values()
    {
        Manifest m = Manifest();
        SourceInventory inventory = BaselineContext.LoadSourceInventory();
        Assert.NotNull(m.Accounting);
        Accounting a = m.Accounting!;

        // Recompute EVERY accounting number from the records + inventory (never from the accounting
        // block itself), then assert equality.
        Assert.Equal(m.Records.Count, a.LogicalRecords);

        int physicalFromInventory = inventory.PhysicalCopies.Count;
        int physicalFromRecords = m.Records.Sum(r => r.PhysicalCopies.Count);
        Assert.Equal(physicalFromInventory, a.PhysicalCopies);
        Assert.Equal(physicalFromRecords, a.PhysicalCopies);

        Assert.Equal(m.Records.Count(r => r.StepId == 2), a.Step2Records);
        Assert.Equal(m.Records.Count(r => r.StepId == 4), a.Step4Records);
        Assert.Equal(m.Records.Count(r => r.UpstreamStableIds.Count > 0), a.DependentRecords);
        Assert.Equal(m.Records.Sum(r => r.UpstreamStableIds.Count), a.DependencyLinks);

        AssertCount(a.ChainRoleCounts, "root", m.Records.Count(r => r.ChainRole == "root"));
        AssertCount(a.ChainRoleCounts, "cascade", m.Records.Count(r => r.ChainRole == "cascade"));

        AssertCount(a.ClassificationCounts, "root", m.Records.Count(r => r.Classification == "root"));
        AssertCount(a.ClassificationCounts, "cascade", m.Records.Count(r => r.Classification == "cascade"));
        AssertCount(a.ClassificationCounts, "mixed", m.Records.Count(r => r.Classification == "mixed"));
        AssertCount(a.ClassificationCounts, "diagnostic", m.Records.Count(r => r.Classification == "diagnostic"));

        AssertCount(a.ErrorClassCounts, "A", m.Records.Count(r => r.ErrorClasses.Contains("A")));
        AssertCount(a.ErrorClassCounts, "B", m.Records.Count(r => r.ErrorClasses.Contains("B")));
        AssertCount(a.ErrorClassCounts, "AB",
            m.Records.Count(r => r.ErrorClasses.Contains("A") && r.ErrorClasses.Contains("B")));
        AssertCount(a.ErrorClassCounts, "C", m.Records.Count(r => r.ErrorClasses.Contains("C")));

        // Sanity: the recomputed totals also match the hard-pinned baseline shape.
        Assert.Equal(34, a.LogicalRecords);
        Assert.Equal(68, a.PhysicalCopies);
    }

    private static void AssertCount(IReadOnlyDictionary<string, int> counts, string key, int expected)
    {
        Assert.True(counts.TryGetValue(key, out int actual),
            $"accounting is missing the '{key}' bucket.");
        Assert.Equal(expected, actual);
    }
}
