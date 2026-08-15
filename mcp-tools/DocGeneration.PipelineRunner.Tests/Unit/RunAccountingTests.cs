using System.Text.Json;
using PipelineRunner.Cli;
using PipelineRunner.Context;
using PipelineRunner.Contracts;
using PipelineRunner.Registry;
using PipelineRunner.Services;
using PipelineRunner.Tests.Fixtures;
using Shared;
using Xunit;

namespace PipelineRunner.Tests.Unit;

/// <summary>
/// RED tests for #813 Step 2 — run-accounting summary (AD-029 §6). These assert a NEW output file,
/// <c>run-accounting.json</c>, written at <c>CompleteRun</c> under the run's OutputPath. It does not
/// exist on current code, so T21–T24 / T26–T29 fail at runtime the moment the file is read.
///
/// COMPILE-RED: T25 additionally cross-checks the suppressed Step 3 envelope's
/// <c>StepResultFile.BlockedByDependency</c> (AD-029 §2), which is not on today's envelope — so this
/// whole file does not compile until that member ships (the intended compile-RED per Cameron §5).
///
/// PROPOSED run-accounting.json contract asserted here (Cameron §1.B — pin whatever the team agrees):
/// {
///   "schemaVersion": "1.0",
///   "successfulNamespaces": ["storage"],
///   "rootFailedNamespaces": [
///     { "namespace": "compute", "rootStepId": 2, "rootStepName": "Generate example prompts",
///       "rootFailureId": "compute.02.root", "exitCode": 1 }
///   ],
///   "warningOnlyFailures":  [ { "namespace": "compute", "stepId": 7, "stepName": "Validate article health" } ],
///   "suppressedSteps":      [ { "namespace": "compute", "stepId": 3, "rootFailureId": "compute.02.root" },
///                             { "namespace": "compute", "stepId": 4, "rootFailureId": "compute.02.root" } ],
///   "reconciliation": {
///     "logicalRecordTotal": 34, "physicalCopyTotal": 68,
///     "categoryCounts": { "successful": .., "rootFailed": .., "warningOnly": .., "suppressed": ..,
///                         "cascadeImported": 10, "unclassifiedDiagnostic": 1 }
///   }
/// }
/// The reconciliation section overlays a live partition onto the FROZEN beta34 baseline manifest
/// (mcp-tools/DocGeneration.Baseline.Beta34.Tests/Fixtures/beta34-baseline-manifest.json), located by
/// walking up for mcp-doc-generation.sln (BaselineContext.FindRepoRoot replicated — no project ref, read-only).
/// Root failure id format: {namespaceSlug}.{rootStepId:D2}.root.
///
/// ADDENDUM A (six-category summary surfaces): T30/T31 additionally assert the PipelineRunner
/// console summary (CompleteRun) surfaces the same six categories to <c>reports.Messages</c> as
/// labeled <c>Label (N)</c> lines. These are RUNTIME-RED (no new API): the run reaches CompleteRun
/// on current code, but the six-category console block is not emitted, so the Assert.Single label
/// lookups find 0 matching messages until GREEN.
/// </summary>
public class RunAccountingTests
{
    // ── T21: run-accounting.json is emitted at CompleteRun ───────────────────────────────────
    [Fact]
    public async Task RunAccounting_IsEmittedAsJson_AtCompleteRun()
    {
        var repoRoot = CreateRepoRoot();
        var doubles = MirroredRegistry.CreateDoublesMatchingDefault(ScriptsRoot(repoRoot));
        var (runner, _) = BuildRunner(repoRoot, doubles);

        var exit = await runner.RunAsync(Request(namespaceName: null, new[] { 1, 2, 3, 4 }), CancellationToken.None);

        Assert.Equal(0, exit); // positive control: the run reached CompleteRun successfully
        Assert.True(File.Exists(Path.Combine(OutputPathOf(repoRoot), "run-accounting.json")));
    }

    // ── T22: success bucket = every step succeeded, zero roots ───────────────────────────────
    [Fact]
    public async Task RunAccounting_SuccessfulNamespaces_AllStepsSucceeded_ZeroRoots()
    {
        var repoRoot = CreateRepoRoot();
        var doubles = MirroredRegistry.CreateDoublesMatchingDefault(ScriptsRoot(repoRoot));
        var (runner, _) = BuildRunner(repoRoot, doubles);

        await runner.RunAsync(Request(namespaceName: null, new[] { 1, 2, 3, 4 }), CancellationToken.None);

        var accounting = ReadRunAccounting(OutputPathOf(repoRoot));
        Assert.Equal(new[] { "compute", "storage" }, accounting.SuccessfulNamespaces.OrderBy(x => x, StringComparer.Ordinal).ToArray());
        Assert.Empty(accounting.RootFailedNamespaces);
    }

    // ── T23: root bucket names each root with a stable id ────────────────────────────────────
    [Fact]
    public async Task RunAccounting_RootFailedNamespaces_ReportEachRoot_WithStableRootFailureId()
    {
        var repoRoot = CreateRepoRoot();
        var doubles = MirroredRegistry.CreateDoublesMatchingDefault(ScriptsRoot(repoRoot));
        Step(doubles, 2).Outcome = ns => ns == "compute" ? Failure() : Success();
        var (runner, _) = BuildRunner(repoRoot, doubles);

        await runner.RunAsync(Request(namespaceName: null, new[] { 1, 2, 3, 4 }), CancellationToken.None);

        var accounting = ReadRunAccounting(OutputPathOf(repoRoot));
        Assert.Single(accounting.RootFailedNamespaces);
        var root = accounting.RootFailedNamespaces[0];
        Assert.Equal("compute", root.Namespace);
        Assert.Equal(2, root.RootStepId);
        Assert.Equal("Generate example prompts", root.RootStepName);
        Assert.Equal("compute.02.root", root.RootFailureId);
    }

    // ── T24: warning-only failures reported separately, never as a root ──────────────────────
    [Fact]
    public async Task RunAccounting_WarningOnlyFailures_ReportedSeparately_NotAsRoot()
    {
        var repoRoot = CreateRepoRoot();
        var doubles = MirroredRegistry.CreateDoublesMatchingDefault(ScriptsRoot(repoRoot));
        Step(doubles, 7).Outcome = _ => Failure(); // Step 7 is a Warn-policy step
        var (runner, _) = BuildRunner(repoRoot, doubles);

        await runner.RunAsync(Request("compute", new[] { 1, 2, 3, 4, 7 }), CancellationToken.None);

        var accounting = ReadRunAccounting(OutputPathOf(repoRoot));
        Assert.Empty(accounting.RootFailedNamespaces); // a warn failure is NOT a root
        Assert.Single(accounting.WarningOnlyFailures);
        var warning = accounting.WarningOnlyFailures[0];
        Assert.Equal("compute", warning.Namespace);
        Assert.Equal(7, warning.StepId);
        Assert.Equal("Validate article health", warning.StepName);
    }

    // ── T25: suppressed bucket carries step id + root failure id (cross-checked vs envelope) ─
    // [C] compile-RED: StepResultFile.BlockedByDependency does not exist yet.
    [Fact]
    public async Task RunAccounting_SuppressedSteps_ReportStepIdAndRootFailureId()
    {
        var repoRoot = CreateRepoRoot();
        var doubles = MirroredRegistry.CreateDoublesMatchingDefault(ScriptsRoot(repoRoot));
        Step(doubles, 2).Outcome = _ => Failure();
        var (runner, _) = BuildRunner(repoRoot, doubles);

        await runner.RunAsync(Request("compute", new[] { 1, 2, 3, 4 }), CancellationToken.None);

        var outputPath = OutputPathOf(repoRoot);
        var accounting = ReadRunAccounting(outputPath);

        var suppressed = accounting.SuppressedSteps.OrderBy(s => s.StepId).ToArray();
        Assert.Equal(2, suppressed.Length);
        Assert.Equal("compute", suppressed[0].Namespace);
        Assert.Equal(3, suppressed[0].StepId);
        Assert.Equal("compute.02.root", suppressed[0].RootFailureId);
        Assert.Equal("compute", suppressed[1].Namespace);
        Assert.Equal(4, suppressed[1].StepId);
        Assert.Equal("compute.02.root", suppressed[1].RootFailureId);

        // Cross-check: the suppressed Step 3 envelope carries the SAME root id (compile-RED member).
        var directory = ObservabilityDir(outputPath, Step(doubles, 3));
        Assert.True(StepResultReader.TryRead(directory, out var envelope));
        Assert.NotNull(envelope);
        Assert.NotNull(envelope!.BlockedByDependency);
        Assert.Equal("compute.02.root", envelope.BlockedByDependency!.RootFailureId);
    }

    // ── T26: buckets are mutually exclusive (first-match partition) ──────────────────────────
    [Fact]
    public async Task RunAccounting_Partition_RecordAssignedByFirstMatchOrder_MutuallyExclusive()
    {
        var repoRoot = CreateRepoRoot();
        var doubles = MirroredRegistry.CreateDoublesMatchingDefault(ScriptsRoot(repoRoot));
        Step(doubles, 2).Outcome = ns => ns == "compute" ? Failure() : Success();
        var (runner, _) = BuildRunner(repoRoot, doubles);

        await runner.RunAsync(Request(namespaceName: null, new[] { 1, 2, 3, 4 }), CancellationToken.None);

        var accounting = ReadRunAccounting(OutputPathOf(repoRoot));
        Assert.Equal(new[] { "storage" }, accounting.SuccessfulNamespaces.ToArray());
        Assert.Single(accounting.RootFailedNamespaces);
        Assert.Equal("compute", accounting.RootFailedNamespaces[0].Namespace);

        // Mutual exclusivity: compute is a root (not successful); storage is successful (not a root).
        Assert.DoesNotContain("compute", accounting.SuccessfulNamespaces);
        Assert.DoesNotContain("storage", accounting.RootFailedNamespaces.Select(r => r.Namespace));

        // A suppressed step lives ONLY in the suppressed bucket.
        Assert.Contains(accounting.SuppressedSteps, s => s.Namespace == "compute" && s.StepId == 3);
    }

    // ── T27: cascade category derives from chainRole (10), not classification (9) [L-004] ────
    [Fact]
    public async Task RunAccounting_CascadeCategory_UsesChainRoleCount10_NotClassification9()
    {
        var beta34 = ReadBeta34Accounting();
        var chainRoleCascade = beta34.GetProperty("chainRoleCounts").GetProperty("cascade").GetInt32();
        var classificationCascade = beta34.GetProperty("classificationCounts").GetProperty("cascade").GetInt32();
        var classificationMixed = beta34.GetProperty("classificationCounts").GetProperty("mixed").GetInt32();
        Assert.Equal(10, chainRoleCascade);      // frozen baseline sanity
        Assert.Equal(9, classificationCascade);
        Assert.Equal(3, classificationMixed);

        var repoRoot = CreateRepoRoot();
        var doubles = MirroredRegistry.CreateDoublesMatchingDefault(ScriptsRoot(repoRoot));
        var (runner, _) = BuildRunner(repoRoot, doubles);
        await runner.RunAsync(Request(namespaceName: null, new[] { 1, 2, 3, 4 }), CancellationToken.None);

        var reconciliation = ReadRunAccounting(OutputPathOf(repoRoot)).Reconciliation;
        Assert.NotNull(reconciliation);
        var cascade = reconciliation!.CategoryCounts.CascadeImported;
        Assert.Equal(10, cascade);
        Assert.Equal(chainRoleCascade, cascade); // sourced from chainRoleCounts, not classificationCounts
        Assert.NotEqual(9, cascade);
        Assert.NotEqual(3, cascade);
    }

    // ── T28: unclassified/diagnostic category is exactly one (beta34 diagnostic:1) ───────────
    [Fact]
    public async Task RunAccounting_UnclassifiedCategory_Beta34DiagnosticIsExactlyOne()
    {
        var beta34 = ReadBeta34Accounting();
        var diagnostic = beta34.GetProperty("classificationCounts").GetProperty("diagnostic").GetInt32();
        Assert.Equal(1, diagnostic);

        var repoRoot = CreateRepoRoot();
        var doubles = MirroredRegistry.CreateDoublesMatchingDefault(ScriptsRoot(repoRoot));
        var (runner, _) = BuildRunner(repoRoot, doubles);
        await runner.RunAsync(Request(namespaceName: null, new[] { 1, 2, 3, 4 }), CancellationToken.None);

        var reconciliation = ReadRunAccounting(OutputPathOf(repoRoot)).Reconciliation;
        Assert.NotNull(reconciliation);
        Assert.Equal(1, reconciliation!.CategoryCounts.UnclassifiedDiagnostic);
        Assert.Equal(diagnostic, reconciliation.CategoryCounts.UnclassifiedDiagnostic);
    }

    // ── T29: six-category partition reconciles to the 34-logical / 68-physical baseline ──────
    [Fact]
    public async Task RunAccounting_SixCategoryPartition_ReconcilesTo34Logical68Physical()
    {
        var beta34 = ReadBeta34Accounting();
        var logicalBaseline = beta34.GetProperty("logicalRecords").GetInt32();
        var physicalBaseline = beta34.GetProperty("physicalCopies").GetInt32();
        var chainRoot = beta34.GetProperty("chainRoleCounts").GetProperty("root").GetInt32();
        var chainCascade = beta34.GetProperty("chainRoleCounts").GetProperty("cascade").GetInt32();
        Assert.Equal(34, logicalBaseline);
        Assert.Equal(68, physicalBaseline);
        Assert.Equal(34, chainRoot + chainCascade); // chainRole partition (24 + 10) reconciles to logical total

        var repoRoot = CreateRepoRoot();
        var doubles = MirroredRegistry.CreateDoublesMatchingDefault(ScriptsRoot(repoRoot));
        var (runner, _) = BuildRunner(repoRoot, doubles);
        await runner.RunAsync(Request(namespaceName: null, new[] { 1, 2, 3, 4 }), CancellationToken.None);

        var reconciliation = ReadRunAccounting(OutputPathOf(repoRoot)).Reconciliation;
        Assert.NotNull(reconciliation);
        Assert.Equal(34, reconciliation!.LogicalRecordTotal);
        Assert.Equal(68, reconciliation.PhysicalCopyTotal);

        var counts = reconciliation.CategoryCounts;
        var sum = counts.Successful + counts.RootFailed + counts.WarningOnly
                + counts.Suppressed + counts.CascadeImported + counts.UnclassifiedDiagnostic;
        Assert.Equal(34, sum); // six buckets reconcile to the logical total, not a coincidental single match
        Assert.Equal(reconciliation.LogicalRecordTotal, sum);
    }

    // ── T30: console summary reports all six categories (ADDENDUM A — S2, M27–M30) ───────────
    // RUNTIME-RED: CompleteRun currently prints only the legacy lines; the six-category console
    // block does not exist yet, so the first Assert.Single below finds 0 matching messages.
    [Fact]
    public async Task RunAccounting_ConsoleSummary_ReportsAllSixCategories_WithLiveIdentifiersAndBaselineConstants()
    {
        var repoRoot = CreateRepoRoot();
        var doubles = MirroredRegistry.CreateDoublesMatchingDefault(ScriptsRoot(repoRoot));
        Step(doubles, 2).Outcome = ns => ns == "compute" ? Failure() : Success(); // fatal root in compute → suppresses {3,4}
        Step(doubles, 5).Outcome = ns => ns == "compute" ? Failure() : Success(); // Warn step warn-fails in compute → warning-only
        var (runner, reports) = BuildRunner(repoRoot, doubles);

        // steps [1,2,3,4,5]; skipDeps:true only waives planning validation (AD-029 §5) — it never
        // affects suppression, so compute still records root(2)+suppressed(3,4)+warningOnly(5).
        var exit = await runner.RunAsync(Request(namespaceName: null, new[] { 1, 2, 3, 4, 5 }, skipDeps: true), CancellationToken.None);

        Assert.Equal(1, exit); // positive control: fatal root ⇒ exit 1, and the run reached CompleteRun

        var messages = reports.Messages;

        // Cat 1 — successful namespaces: storage only (compute has a fatal root).
        var successfulLine = Assert.Single(messages, message => message.Contains("Successful namespaces (1)"));
        Assert.Contains("storage", successfulLine);
        Assert.DoesNotContain("compute", successfulLine); // control: the same line DOES contain "storage"

        // Cat 2 — root-failed namespaces: compute, named with its stable root id.
        var rootLine = Assert.Single(messages, message => message.Contains("Root-failed namespaces (1)"));
        Assert.Contains("compute", rootLine);
        Assert.Contains("compute.02.root", rootLine);
        Assert.DoesNotContain("storage", rootLine); // control: the same line DOES contain "compute"

        // Cat 3 — warning-only failures: compute step 5, reported separately from the root.
        var warningLine = Assert.Single(messages, message => message.Contains("Warning-only failures (1)"));
        Assert.Contains("compute", warningLine);
        Assert.Contains("5", warningLine);

        // Cat 4 — suppressed steps: 3 and 4, both attributed to the same root id. Count (2) is
        // distinct from the (1) categories above.
        var suppressedLine = Assert.Single(messages, message => message.Contains("Suppressed steps (2)"));
        Assert.Contains("compute.02.root", suppressedLine);

        // Cat 5 — cascades imported from the frozen beta34 baseline (chainRole cascade == 10), constant.
        Assert.Single(messages, message => message.Contains("Cascades imported from historical fixtures (10)"));
        Assert.DoesNotContain(messages, message => message.Contains("Cascades imported from historical fixtures (0)"));

        // Cat 6 — unclassified/diagnostic records from the baseline (diagnostic == 1), constant.
        Assert.Single(messages, message => message.Contains("Unclassified records (1)"));
        Assert.DoesNotContain(messages, message => message.Contains("Unclassified records (0)"));
    }

    // ── T31: clean catalog run still prints all six labels; live categories are zero (ADDENDUM A) ─
    // RUNTIME-RED: same missing console block — even at count 0 every label must print, and the
    // baseline constants must remain non-zero, proving the channel is live rather than dead.
    [Fact]
    public async Task RunAccounting_ConsoleSummary_CleanCatalogRun_StillPrintsAllSixLabels_LiveCategoriesZero()
    {
        var repoRoot = CreateRepoRoot();
        var doubles = MirroredRegistry.CreateDoublesMatchingDefault(ScriptsRoot(repoRoot));
        var (runner, reports) = BuildRunner(repoRoot, doubles); // no Outcome overrides → both namespaces succeed

        var exit = await runner.RunAsync(Request(namespaceName: null, new[] { 1, 2, 3, 4, 5 }, skipDeps: true), CancellationToken.None);

        Assert.Equal(0, exit); // positive control: a clean run completes successfully

        var messages = reports.Messages;

        // Cat 1 — both namespaces succeed; the successful line aggregates the two.
        var successfulLine = Assert.Single(messages, message => message.Contains("Successful namespaces (2)"));
        Assert.Contains("compute", successfulLine);
        Assert.Contains("storage", successfulLine);

        // Labels-always-print (absent-value) trio: every live category prints even at count 0.
        Assert.Single(messages, message => message.Contains("Root-failed namespaces (0)"));
        Assert.Single(messages, message => message.Contains("Warning-only failures (0)"));
        Assert.Single(messages, message => message.Contains("Suppressed steps (0)"));

        // Baseline constants stay non-zero in a clean run — the positive control paired with the
        // (0) trio above.
        Assert.Single(messages, message => message.Contains("Cascades imported from historical fixtures (10)"));
        Assert.Single(messages, message => message.Contains("Unclassified records (1)"));

        // Negative control: the zeros are genuine, not a mislabeled non-zero root count.
        Assert.DoesNotContain(messages, message => message.Contains("Root-failed namespaces (1)"));
    }

    // ────────────────────────────── run-accounting.json model ──────────────────────────────

    private sealed record RunAccounting(
        string? SchemaVersion,
        List<string> SuccessfulNamespaces,
        List<RootFailedNamespace> RootFailedNamespaces,
        List<WarningOnlyFailure> WarningOnlyFailures,
        List<SuppressedStepRecord> SuppressedSteps,
        Reconciliation? Reconciliation);

    private sealed record RootFailedNamespace(string Namespace, int RootStepId, string RootStepName, string RootFailureId, int ExitCode);

    private sealed record WarningOnlyFailure(string Namespace, int StepId, string StepName);

    private sealed record SuppressedStepRecord(string Namespace, int StepId, string RootFailureId);

    private sealed record Reconciliation(int LogicalRecordTotal, int PhysicalCopyTotal, CategoryCounts CategoryCounts);

    private sealed record CategoryCounts(int Successful, int RootFailed, int WarningOnly, int Suppressed, int CascadeImported, int UnclassifiedDiagnostic);

    private static readonly JsonSerializerOptions RunAccountingJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static RunAccounting ReadRunAccounting(string outputPath)
    {
        var path = Path.Combine(outputPath, "run-accounting.json");
        Assert.True(File.Exists(path), $"Expected run-accounting.json at {path}");
        var accounting = JsonSerializer.Deserialize<RunAccounting>(File.ReadAllText(path), RunAccountingJsonOptions);
        Assert.NotNull(accounting);
        return accounting!;
    }

    // ────────────────────────────── frozen beta34 manifest (read-only) ──────────────────────

    private static JsonElement ReadBeta34Accounting()
    {
        var manifestPath = Path.Combine(
            FindRepoRoot(),
            "mcp-tools",
            "DocGeneration.Baseline.Beta34.Tests",
            "Fixtures",
            "beta34-baseline-manifest.json");
        Assert.True(File.Exists(manifestPath), $"Frozen beta34 manifest not found at {manifestPath}");
        using var document = JsonDocument.Parse(File.ReadAllBytes(manifestPath));
        return document.RootElement.GetProperty("accounting").Clone();
    }

    // Replicates BaselineContext.FindRepoRoot: walk up from the test bin dir for mcp-doc-generation.sln.
    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "mcp-doc-generation.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate repo root (mcp-doc-generation.sln) walking up from " + AppContext.BaseDirectory);
    }

    // ────────────────────────────── harness (mirrors SkipDependencyValidationTests) ─────────

    private static RecordingNamespaceStep Step(IReadOnlyList<RecordingNamespaceStep> doubles, int id)
        => doubles.Single(step => step.Id == id);

    private static string CreateRepoRoot()
    {
        var repoRoot = Path.Combine(Path.GetTempPath(), $"pipeline-accounting-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(repoRoot, "mcp-tools", "scripts"));
        File.WriteAllText(Path.Combine(repoRoot, "mcp-doc-generation.sln"), string.Empty);
        return repoRoot;
    }

    private static string ScriptsRoot(string repoRoot) => Path.Combine(repoRoot, "mcp-tools", "scripts");

    private static string OutputPathOf(string repoRoot) => Path.Combine(repoRoot, "generated");

    private static (global::PipelineRunner.PipelineRunner Runner, BufferedReportWriter Reports) BuildRunner(
        string repoRoot,
        IReadOnlyList<RecordingNamespaceStep> doubles)
    {
        var reports = new BufferedReportWriter();
        var contextFactory = new PipelineContextFactory(
            new RecordingProcessRunner(),
            new WorkspaceManager(),
            new StaticCliMetadataLoader(),
            new TargetMatcher(),
            new StubFilteredCliWriter(),
            new StubBuildCoordinator(),
            new StubAiCapabilityProbe(),
            reports,
            repoRoot);
        var runner = new global::PipelineRunner.PipelineRunner(
            new StepRegistry(doubles),
            contextFactory,
            changelogGate: null,
            brandMappingLoader: new StubBrandMappingLoader());
        return (runner, reports);
    }

    private static PipelineRequest Request(string? namespaceName, int[] steps, bool skipDeps = false)
        => new(
            namespaceName,
            steps,
            ".\\generated",
            SkipBuild: true,
            SkipValidation: true,
            DryRun: false,
            SkipEnvValidation: true,
            SkipDependencyValidation: skipDeps,
            SkipChangelogGate: true);

    private static StepResult Success() => StepResult.DryRun(Array.Empty<string>());

    private static StepResult Failure()
        => new(false, new[] { "boom" }, TimeSpan.Zero, Array.Empty<string>(), Array.Empty<string>(),
               Array.Empty<ValidatorResult>(), Array.Empty<ArtifactFailure>());

    private static string ObservabilityDir(string outputPath, IPipelineStep step)
        => Path.Combine(outputPath, "observability", $"{step.Id}-{Slugify(step.Name)}");

    private static string Slugify(string value)
    {
        var buffer = new char[value.Length];
        var length = 0;
        var previousDash = false;

        foreach (var character in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                buffer[length++] = character;
                previousDash = false;
                continue;
            }

            if (!previousDash)
            {
                buffer[length++] = '-';
                previousDash = true;
            }
        }

        return new string(buffer, 0, length).Trim('-');
    }
}
