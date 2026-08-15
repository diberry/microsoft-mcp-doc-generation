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
/// RED tests for #813 Step 2 — runtime dependency suppression (AD-029 §2–§5).
///
/// COMPILE-RED (expected until GREEN): several tests below bind to production API that AD-029
/// specifies but that does not yet exist. These are the ONLY permitted sources of compile error
/// in this file; every other reference resolves against today's API:
///   • <c>PipelineRunner.BuildDependentsOf(IEnumerable&lt;IPipelineStep&gt;)</c>            — T1
///   • <c>PipelineRunner.SelectedTransitiveDependents(rootId, selectedIds, dependentsOf)</c> — T2–T7
///   • <c>StepResultFile.Suppressed</c> (bool?)                                              — T12, T14, T20
///   • <c>StepResultFile.BlockedByDependency</c> (nested type)                               — T12, T20
/// Because the file does not compile until those members ship, the runtime-RED tests here
/// (T8–T11, T13, T15–T19) cannot execute in the RED phase; their RED validity is established by
/// Cameron's §2 mutation matrix + §4 analysis and is realized once the file compiles.
///
/// Harness mirrors <see cref="SkipDependencyValidationTests"/>: a temp repo root with an empty
/// solution + <c>mcp-tools/scripts</c>, the shared <see cref="StaticCliMetadataLoader"/>
/// (namespaces "compute","storage" in order), and <see cref="RecordingNamespaceStep"/> doubles
/// whose graph is a faithful mirror of <see cref="StepRegistry.CreateDefault(string)"/> (proven by T8).
/// </summary>
public class DependencySuppressionTests
{
    // ── T1: full real-registry reverse adjacency ────────────────────────────────────────────
    // [C] compile-RED: PipelineRunner.BuildDependentsOf does not exist yet.
    [Fact]
    public void Registry_ReverseAdjacency_MatchesRealStepRegistryEdges()
    {
        var repoRoot = CreateRepoRoot();
        var steps = StepRegistry.CreateDefault(ScriptsRoot(repoRoot)).GetAllSteps();

        var dependentsOf = global::PipelineRunner.PipelineRunner.BuildDependentsOf(steps);

        // Exact reverse-adjacency map over the SHIPPED graph (order-insensitive, element-exact).
        var expected = new Dictionary<int, int[]>
        {
            [0] = new[] { 5, 6, 8 },
            [1] = new[] { 2, 3 },
            [2] = new[] { 3 },
            [3] = new[] { 4 },
            [4] = new[] { 7, 8 },
            [5] = Array.Empty<int>(),
            [6] = Array.Empty<int>(),
            [7] = new[] { 8 },
            [8] = Array.Empty<int>(),
        };

        Assert.Equal(
            expected.Keys.OrderBy(k => k).ToArray(),
            dependentsOf.Keys.OrderBy(k => k).ToArray());

        foreach (var (stepId, expectedDependents) in expected)
        {
            Assert.Equal(
                expectedDependents.OrderBy(x => x).ToArray(),
                dependentsOf[stepId].OrderBy(x => x).ToArray());
        }
    }

    // ── T2–T7: transitive suppression closure over the real graph ────────────────────────────
    // [C] compile-RED: PipelineRunner.SelectedTransitiveDependents does not exist yet.

    [Fact]
    public void SelectedTransitiveDependents_FatalStep1_Suppresses_2_3_4_7_8()
    {
        var actual = ClosureOnRealGraph(rootId: 1, selection: new[] { 1, 2, 3, 4, 5, 6, 7, 8 });
        Assert.Equal(new[] { 2, 3, 4, 7, 8 }, actual);
        Assert.DoesNotContain(1, actual); // root is never in its own suppression set
    }

    [Fact]
    public void SelectedTransitiveDependents_FatalStep2_Suppresses_3_4_7_8()
    {
        var actual = ClosureOnRealGraph(rootId: 2, selection: new[] { 1, 2, 3, 4, 5, 6, 7, 8 });
        Assert.Equal(new[] { 3, 4, 7, 8 }, actual);
        Assert.DoesNotContain(2, actual);
    }

    [Fact]
    public void SelectedTransitiveDependents_FatalStep3_Suppresses_4_7_8()
    {
        var actual = ClosureOnRealGraph(rootId: 3, selection: new[] { 1, 2, 3, 4, 5, 6, 7, 8 });
        Assert.Equal(new[] { 4, 7, 8 }, actual);
        Assert.DoesNotContain(3, actual);
    }

    [Fact]
    public void SelectedTransitiveDependents_FatalStep4_Suppresses_7_8()
    {
        var actual = ClosureOnRealGraph(rootId: 4, selection: new[] { 1, 2, 3, 4, 5, 6, 7, 8 });
        Assert.Equal(new[] { 7, 8 }, actual);
        Assert.DoesNotContain(4, actual);
    }

    [Fact]
    public void SelectedTransitiveDependents_Step2Root_Selection_1_2_3_4_7_Suppresses_3_4_7()
    {
        // Closure is intersected with the SELECTED steps (AD-029 §3 example).
        var actual = ClosureOnRealGraph(rootId: 2, selection: new[] { 1, 2, 3, 4, 7 });
        Assert.Equal(new[] { 3, 4, 7 }, actual);
        Assert.DoesNotContain(8, actual); // 8 is unselected, so it is not suppressed
        Assert.DoesNotContain(2, actual); // root excluded
    }

    [Fact]
    public void SelectedTransitiveDependents_Step4Root_Selection_1_2_3_4_6_SuppressesNothing()
    {
        // Step 4's dependents are {7,8}; neither is selected, so the closure is empty and the
        // independent Step 6 (depends on global Step 0) survives — its execution is proven by T9.
        var actual = ClosureOnRealGraph(rootId: 4, selection: new[] { 1, 2, 3, 4, 6 });
        // Assert.Empty pins the exact expected value (zero elements) in the xUnit-idiomatic way;
        // the redundant Assert.Equal(0, actual.Length) is removed to satisfy xUnit2013.
        Assert.Empty(actual);
    }

    // ── T8: doubles faithfully mirror the real registry graph (bridge) ───────────────────────
    // [R] runtime: fails only if the mirror drifts; guards trustworthiness of T9–T20.
    [Fact]
    public void StepDoubleRegistry_MirrorsRealStepRegistryGraph()
    {
        var repoRoot = CreateRepoRoot();
        var scriptsRoot = ScriptsRoot(repoRoot);
        var realById = StepRegistry.CreateDefault(scriptsRoot).GetAllSteps().ToDictionary(step => step.Id);
        var doubles = MirroredRegistry.CreateDoublesMatchingDefault(scriptsRoot);

        Assert.Equal(realById.Count, doubles.Count);
        Assert.Equal(realById.Keys.OrderBy(id => id).ToArray(), doubles.Select(d => d.Id).OrderBy(id => id).ToArray());

        foreach (var mirrored in doubles)
        {
            var real = realById[mirrored.Id];
            Assert.Equal(real.Name, mirrored.Name);
            Assert.Equal(real.Scope, mirrored.Scope);
            Assert.Equal(real.FailurePolicy, mirrored.FailurePolicy);
            Assert.Equal(real.MaxRetries, mirrored.MaxRetries);
            Assert.Equal(real.DependsOn.OrderBy(x => x).ToArray(), mirrored.DependsOn.OrderBy(x => x).ToArray());
        }
    }

    // ── T9: fatal Step 2 suppresses dependents, runs independent Step 6, continues ───────────
    // [R] runtime-RED: on current abort code Step 6 never runs (Executions 0). SINGLE namespace so
    // the shared-double execution counts pin exactly (a second namespace would run Step 3/6 too).
    [Fact]
    public async Task RunAsync_FatalStep2_SuppressesDependents_RunsIndependentStep6_AndContinues()
    {
        var repoRoot = CreateRepoRoot();
        var doubles = MirroredRegistry.CreateDoublesMatchingDefault(ScriptsRoot(repoRoot));
        Step(doubles, 2).Outcome = _ => Failure();
        var (runner, _) = BuildRunner(repoRoot, doubles);

        var exit = await runner.RunAsync(Request("compute", new[] { 1, 2, 3, 4, 6 }), CancellationToken.None);

        Assert.Equal(1, Step(doubles, 1).Executions); // positive control: pre-failure step ran
        Assert.Equal(1, Step(doubles, 2).Executions); // positive control: the root itself executed
        Assert.Equal(0, Step(doubles, 3).Executions); // suppressed (direct dependent of 2)
        Assert.Equal(0, Step(doubles, 4).Executions); // suppressed (transitive dependent via 3)
        Assert.Equal(1, Step(doubles, 6).Executions); // independent → runs even after the fatal root
        Assert.Equal(1, exit);
    }

    // ── T10: a fatal root in namespace 1 does not stop namespace 2 ───────────────────────────
    // [R] runtime-RED: current abort returns after compute, so storage never runs.
    // compute fails at Step 1 (root) ⇒ Steps 2/3/4 suppressed for compute ⇒ they execute ONLY
    // for storage, pinning each shared double to Executions==1 / ExecutedNamespaces==["storage"].
    [Fact]
    public async Task RunAsync_FatalNamespaceStep_LaterNamespaceStillExecutes()
    {
        var repoRoot = CreateRepoRoot();
        var doubles = MirroredRegistry.CreateDoublesMatchingDefault(ScriptsRoot(repoRoot));
        Step(doubles, 1).Outcome = ns => ns == "compute" ? Failure() : Success();
        var (runner, _) = BuildRunner(repoRoot, doubles);

        var exit = await runner.RunAsync(Request(namespaceName: null, new[] { 1, 2, 3, 4 }), CancellationToken.None);

        Assert.Equal(1, Step(doubles, 2).Executions);
        Assert.Equal(new[] { "storage" }, Step(doubles, 2).ExecutedNamespaces.ToArray());
        Assert.Equal(1, Step(doubles, 3).Executions);
        Assert.Equal(new[] { "storage" }, Step(doubles, 3).ExecutedNamespaces.ToArray());
        Assert.Equal(1, Step(doubles, 4).Executions);
        Assert.Equal(new[] { "storage" }, Step(doubles, 4).ExecutedNamespaces.ToArray());

        // Positive control: Step 1 ran for BOTH namespaces — proves continuation, not a global no-op.
        Assert.Equal(2, Step(doubles, 1).Executions);
        Assert.Equal(new[] { "compute", "storage" }, Step(doubles, 1).ExecutedNamespaces.ToArray());
        Assert.Equal(1, exit);
    }

    // ── T11: suppression state does not leak across namespaces ───────────────────────────────
    // [R] runtime-RED: current abort returns after compute; leak would drive storage 3/4 to 0.
    [Fact]
    public async Task RunAsync_StateResetsBetweenNamespaces_SuppressionDoesNotLeak()
    {
        var repoRoot = CreateRepoRoot();
        var doubles = MirroredRegistry.CreateDoublesMatchingDefault(ScriptsRoot(repoRoot));
        Step(doubles, 2).Outcome = ns => ns == "compute" ? Failure() : Success();
        var (runner, _) = BuildRunner(repoRoot, doubles);

        var exit = await runner.RunAsync(Request(namespaceName: null, new[] { 1, 2, 3, 4 }), CancellationToken.None);

        // storage's Step 3/4 must run normally — a leaked SuppressedIds set would keep them at 0.
        Assert.Equal(1, Step(doubles, 3).Executions);
        Assert.Equal(new[] { "storage" }, Step(doubles, 3).ExecutedNamespaces.ToArray());
        Assert.Equal(1, Step(doubles, 4).Executions);
        Assert.Equal(new[] { "storage" }, Step(doubles, 4).ExecutedNamespaces.ToArray());

        // Positive control: the root step ran for both namespaces.
        Assert.Equal(new[] { "compute", "storage" }, Step(doubles, 2).ExecutedNamespaces.ToArray());
        Assert.Equal(1, exit);
    }

    // ── T12: suppressed step writes a suppressed envelope with blockedByDependency ───────────
    // [C] compile-RED: StepResultFile.Suppressed / .BlockedByDependency do not exist yet.
    [Fact]
    public async Task RunAsync_SuppressedStep_WritesSuppressedEnvelope_WithBlockedByDependency()
    {
        var repoRoot = CreateRepoRoot();
        var doubles = MirroredRegistry.CreateDoublesMatchingDefault(ScriptsRoot(repoRoot));
        Step(doubles, 2).Outcome = _ => Failure();
        var (runner, _) = BuildRunner(repoRoot, doubles);

        await runner.RunAsync(Request("compute", new[] { 1, 2, 3, 4 }), CancellationToken.None);

        var directory = ObservabilityDir(OutputPathOf(repoRoot), Step(doubles, 3));
        Assert.True(StepResultReader.TryRead(directory, out var envelope));
        Assert.NotNull(envelope);
        Assert.True(envelope!.Suppressed);
        Assert.NotNull(envelope.BlockedByDependency);
        Assert.Equal("compute", envelope.BlockedByDependency!.Namespace);
        Assert.Equal(2, envelope.BlockedByDependency.FailedRootStepId);
        Assert.Equal("Generate example prompts", envelope.BlockedByDependency.FailedRootStepName);
        Assert.Equal("compute.02.root", envelope.BlockedByDependency.RootFailureId);
    }

    // ── T13: suppressed step emits NO critical-failure JSON, but the root does ───────────────
    // [M] mutation-proven RED (§3 revert makes suppressed Step 3/4 execute → their JSON appears).
    [Fact]
    public async Task RunAsync_SuppressedStep_EmitsNoCriticalFailureJson_ButRootDoes()
    {
        var repoRoot = CreateRepoRoot();
        var doubles = MirroredRegistry.CreateDoublesMatchingDefault(ScriptsRoot(repoRoot));
        Step(doubles, 2).Outcome = _ => Failure();
        var (runner, _) = BuildRunner(repoRoot, doubles);

        await runner.RunAsync(Request("compute", new[] { 1, 2, 3, 4 }), CancellationToken.None);

        var criticalDir = Path.Combine(OutputPathOf(repoRoot), "critical-failures");
        Assert.True(Directory.Exists(criticalDir));
        // Positive control: the fatal ROOT (Step 2) records exactly one critical-failure JSON.
        Assert.Single(Directory.GetFiles(criticalDir, "*-step-02-*.json"));
        // Suppressed dependents never reach CriticalFailureRecorder ⇒ no records.
        Assert.Empty(Directory.GetFiles(criticalDir, "*-step-03-*.json"));
        Assert.Empty(Directory.GetFiles(criticalDir, "*-step-04-*.json"));
    }

    // ── T14: suppressed envelope schema pins (AD-029 §2) ─────────────────────────────────────
    // [C] compile-RED: StepResultFile.Suppressed does not exist yet.
    [Fact]
    public async Task RunAsync_SuppressedEnvelope_KeepsSchemaVersion1_0_AndVersion4_StatusFailure_ValidationSkipped()
    {
        var repoRoot = CreateRepoRoot();
        var doubles = MirroredRegistry.CreateDoublesMatchingDefault(ScriptsRoot(repoRoot));
        Step(doubles, 2).Outcome = _ => Failure();
        var (runner, _) = BuildRunner(repoRoot, doubles);

        await runner.RunAsync(Request("compute", new[] { 1, 2, 3, 4 }), CancellationToken.None);

        var directory = ObservabilityDir(OutputPathOf(repoRoot), Step(doubles, 3));
        Assert.True(StepResultReader.TryRead(directory, out var envelope));
        Assert.NotNull(envelope);
        Assert.Equal("1.0", envelope!.SchemaVersion);            // schemaVersion NOT bumped
        Assert.Equal(4, envelope.Version);                       // informational Version 3 → 4
        Assert.Equal(StepResultStatus.Failure, envelope.Status); // conservative non-success status
        Assert.Equal(ValidationStatus.Skipped, envelope.ValidationStatus);
        Assert.True(envelope.Suppressed);                        // authoritative signal
    }

    // ── T15: a warning-policy failure does NOT suppress dependents; catalog still succeeds ───
    // [M] guard (passes on current runtime — warn maps to exit 0). RED under the §6 warn-as-root mutation.
    [Fact]
    public async Task RunAsync_WarnStepFails_DoesNotSuppressDependents_AndCatalogSucceeds()
    {
        var repoRoot = CreateRepoRoot();
        var doubles = MirroredRegistry.CreateDoublesMatchingDefault(ScriptsRoot(repoRoot));
        Step(doubles, 7).Outcome = _ => Failure(); // Step 7 is a Warn-policy step in the real graph
        var (runner, _) = BuildRunner(repoRoot, doubles);

        var exit = await runner.RunAsync(Request("compute", new[] { 1, 2, 3, 4, 7, 8 }), CancellationToken.None);

        Assert.Equal(1, Step(doubles, 7).Executions); // positive control: the warn step ran & "failed"
        Assert.Equal(1, Step(doubles, 8).Executions); // dependent of Step 7 is NOT suppressed
        Assert.Equal(0, exit);                         // warn-only failure never fails the catalog
    }

    // ── T16: a global fatal (Step 0) aborts the whole catalog — no namespace runs ────────────
    // [G] guard (passes on current code). RED only under the §4 mutation routing global via suppression.
    [Fact]
    public async Task RunAsync_GlobalFatalStep0_AbortsCatalog_NoNamespaceRuns()
    {
        var repoRoot = CreateRepoRoot();
        var doubles = MirroredRegistry.CreateDoublesMatchingDefault(ScriptsRoot(repoRoot));
        Step(doubles, 0).Outcome = _ => Failure(); // Step 0 is Global scope
        var (runner, _) = BuildRunner(repoRoot, doubles);

        var exit = await runner.RunAsync(Request("compute", new[] { 1, 2, 3, 4 }), CancellationToken.None);

        Assert.Equal(1, Step(doubles, 0).Executions); // positive control: the global step ran & failed
        Assert.Equal(0, Step(doubles, 1).Executions); // no namespace-scope step executes
        Assert.Equal(1, exit);
    }

    // ── T17: worst exit code preserved across two fatal roots in different namespaces ────────
    // [R] runtime-RED: current abort returns compute's exit (2) before storage runs.
    [Fact]
    public async Task RunAsync_TwoFatalRootsAcrossNamespaces_WorstExitCodeIsOne()
    {
        // compute = human-review root (exit 2, FIRST); storage = hard-fatal root (exit 1, SECOND).
        var repoRoot = CreateRepoRoot();
        var doubles = MirroredRegistry.CreateDoublesMatchingDefault(ScriptsRoot(repoRoot));
        Step(doubles, 2).Outcome = ns => ns == "compute" ? HumanReview() : Failure();
        var (runner, _) = BuildRunner(repoRoot, doubles);

        var exit = await runner.RunAsync(Request(namespaceName: null, new[] { 1, 2 }), CancellationToken.None);

        Assert.Equal(1, exit); // Worse(2,1) == 1 — hard fatal dominates human-review

        // Positive control (catches the Worse mutation in the other direction): a human-review-only
        // run yields exit 2, so exit 1 above is not a constant.
        var controlRoot = CreateRepoRoot();
        var controlDoubles = MirroredRegistry.CreateDoublesMatchingDefault(ScriptsRoot(controlRoot));
        Step(controlDoubles, 2).Outcome = _ => HumanReview();
        var (controlRunner, _) = BuildRunner(controlRoot, controlDoubles);

        var controlExit = await controlRunner.RunAsync(Request("compute", new[] { 1, 2 }), CancellationToken.None);
        Assert.Equal(2, controlExit);
    }

    // ── T18: --skip-deps never rescues a suppressed dependent ────────────────────────────────
    // [R] runtime-RED: suppression must be independent of SkipDependencyValidation.
    [Fact]
    public async Task RunAsync_SkipDeps_SelectedStep3Fails_Step4StillSuppressed()
    {
        var repoRoot = CreateRepoRoot();
        var doubles = MirroredRegistry.CreateDoublesMatchingDefault(ScriptsRoot(repoRoot));
        Step(doubles, 3).Outcome = _ => Failure();
        var (runner, _) = BuildRunner(repoRoot, doubles);

        var exit = await runner.RunAsync(Request("compute", new[] { 3, 4 }, skipDeps: true), CancellationToken.None);

        Assert.Equal(1, Step(doubles, 3).Executions); // positive control: Step 3 ran & failed
        Assert.Equal(0, Step(doubles, 4).Executions); // suppressed despite --skip-deps
        Assert.Equal(1, exit);

        // Control: with Step 3 SUCCEEDING under --skip-deps, Step 4 runs — suppression is
        // failure-driven, not a blanket skip.
        var okRoot = CreateRepoRoot();
        var okDoubles = MirroredRegistry.CreateDoublesMatchingDefault(ScriptsRoot(okRoot));
        Step(okDoubles, 3).Outcome = _ => Success();
        var (okRunner, _) = BuildRunner(okRoot, okDoubles);

        var okExit = await okRunner.RunAsync(Request("compute", new[] { 3, 4 }, skipDeps: true), CancellationToken.None);
        Assert.Equal(1, Step(okDoubles, 4).Executions);
        Assert.Equal(0, okExit);
    }

    // ── T19: a cancellation stops the remaining namespaces ───────────────────────────────────
    // [R] runtime (guard): compute's Step 4 cancels; storage steps never run and the call throws.
    [Fact]
    public async Task RunAsync_CancellationRequested_StopsRemainingNamespaces()
    {
        using var cts = new CancellationTokenSource();
        var repoRoot = CreateRepoRoot();
        var doubles = MirroredRegistry.CreateDoublesMatchingDefault(ScriptsRoot(repoRoot));
        Step(doubles, 4).Outcome = _ =>
        {
            cts.Cancel();
            return Success();
        };
        var (runner, _) = BuildRunner(repoRoot, doubles);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => runner.RunAsync(Request(namespaceName: null, new[] { 1, 2, 3, 4 }), cts.Token));

        // Second namespace (storage) never executed a single step.
        Assert.Equal(new[] { "compute" }, Step(doubles, 1).ExecutedNamespaces.ToArray());
        // Positive control: compute's Step 4 did execute (proves the observation channel works).
        Assert.Equal(1, Step(doubles, 4).Executions);
        Assert.Equal(new[] { "compute" }, Step(doubles, 4).ExecutedNamespaces.ToArray());
    }

    // ── T20: rerunning the same OutputPath overwrites the suppressed envelope ─────────────────
    // [C] compile-RED then runtime: single overwritten file carrying the second run's root id.
    [Fact]
    public async Task RunAsync_RerunSameOutputPath_SuppressedEnvelopeOverwrittenOnCleanRerun()
    {
        var repoRoot = CreateRepoRoot();

        var firstDoubles = MirroredRegistry.CreateDoublesMatchingDefault(ScriptsRoot(repoRoot));
        Step(firstDoubles, 2).Outcome = _ => Failure();
        var (firstRunner, _) = BuildRunner(repoRoot, firstDoubles);
        await firstRunner.RunAsync(Request("compute", new[] { 1, 2, 3, 4 }), CancellationToken.None);

        var secondDoubles = MirroredRegistry.CreateDoublesMatchingDefault(ScriptsRoot(repoRoot));
        Step(secondDoubles, 2).Outcome = _ => Failure();
        var (secondRunner, _) = BuildRunner(repoRoot, secondDoubles);
        await secondRunner.RunAsync(Request("compute", new[] { 1, 2, 3, 4 }), CancellationToken.None);

        var directory = ObservabilityDir(OutputPathOf(repoRoot), Step(secondDoubles, 3));
        Assert.True(StepResultReader.TryRead(directory, out var envelope));
        Assert.NotNull(envelope);
        Assert.NotNull(envelope!.BlockedByDependency);
        Assert.Equal("compute.02.root", envelope.BlockedByDependency!.RootFailureId);
        // A clean rerun overwrites in place — exactly one envelope file, no stale duplicate.
        Assert.Single(Directory.GetFiles(directory, "step-result.json"));
    }

    // ── T18a / T18b: referenced existing planning guards (NOT rewritten) ─────────────────────
    // Cameron §1.A cites these as guards. The actual identifiers in
    // mcp-tools/DocGeneration.PipelineRunner.Tests/Unit/SkipDependencyValidationTests.cs are:
    //   • T18a → SkipDependencyValidationTests.RunAsync_WithoutSkipDeps_Step4Alone_ReturnsDepError
    //   • T18b → SkipDependencyValidationTests.RunAsync_WithSkipDeps_Step4Alone_SkipsDependencyCheck
    // (Cameron's matrix cited placeholder names "RunAsync_Step4WithoutStep3_*"; per his §1.A note
    // Parker confirmed and uses the real identifiers above.) They remain untouched.

    // ────────────────────────────── helpers ──────────────────────────────

    private static int[] ClosureOnRealGraph(int rootId, int[] selection)
    {
        var repoRoot = CreateRepoRoot();
        var steps = StepRegistry.CreateDefault(ScriptsRoot(repoRoot)).GetAllSteps();
        var dependentsOf = global::PipelineRunner.PipelineRunner.BuildDependentsOf(steps);
        var selectedIds = new HashSet<int>(selection);
        var suppressed = global::PipelineRunner.PipelineRunner.SelectedTransitiveDependents(rootId, selectedIds, dependentsOf);
        return suppressed.OrderBy(x => x).ToArray();
    }

    private static RecordingNamespaceStep Step(IReadOnlyList<RecordingNamespaceStep> doubles, int id)
        => doubles.Single(step => step.Id == id);

    private static string CreateRepoRoot()
    {
        var repoRoot = Path.Combine(Path.GetTempPath(), $"pipeline-suppress-{Guid.NewGuid():N}");
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

    // Human-review outcome MUST be Success:false with an override of 2. MapStepFailureExitCode maps
    // any *success* step to exit 0 regardless of the override, so a "success + override 2" would
    // yield exit 0, not the human-review code.
    private static StepResult HumanReview()
        => new(false, new[] { "needs human review" }, TimeSpan.Zero, Array.Empty<string>(), Array.Empty<string>(),
               Array.Empty<ValidatorResult>(), Array.Empty<ArtifactFailure>(), ExitCodeOverride: 2);

    private static string ObservabilityDir(string outputPath, IPipelineStep step)
        => Path.Combine(outputPath, "observability", $"{step.Id}-{Slugify(step.Name)}");

    // Mirror of PipelineRunner.Slugify so tests compute the same observability directory names.
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
