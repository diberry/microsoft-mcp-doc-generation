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

    // ══════════════════════════════════════════════════════════════════════════════════════════
    // ADDENDUM B (Ellis FAIL remediation, #813 Step 2). Sources:
    //   • .squad/agents/cameron/813-step2-test-matrix.md §B (binding spec: T32–T40, M35–M42)
    //   • .squad/decisions/inbox/riley-ad-029-runtime-dependency-suppression.md §A0–A8 (AMENDMENT 1)
    //   • .squad/agents/ellis/813-step2-evaluation.md (VERDICT: FAIL — BLOCKING-1/-2, F1 confirmed)
    //
    // NEW compile-RED source (ADDITIONAL to the seams in the class header, which have since landed):
    // the corrected root predicate
    //   internal static bool PipelineRunner.IsFatalRoot(
    //       FailurePolicy policy, int mappedExitCode, IReadOnlyList<ArtifactFailure> artifactFailures)
    // does NOT exist yet (AD-029 §A2). T32/T33 bind to it DIRECTLY — never hand-rolling the predicate —
    // so the whole test project fails to COMPILE until Rowan lands the seam. That compile failure IS the
    // RED signal for this addendum. The runtime-RED tests (T34–T38) and the two guard tests (T39/T40)
    // therefore cannot execute in the RED phase; their RED/guard validity is realized once the file
    // compiles GREEN, exactly as the class header documents for the earlier runtime-RED tests.
    //
    // Root cause under test (D1 / BLOCKING-1): the real Step-2 failure returns Success=true with a
    // non-empty ArtifactFailures list (ExamplePromptsStep.cs:141) ⇒ mapped exit 0 ⇒ today's runtime
    // records NO root ⇒ 0 of 10 historical cascades are suppressed. Every prior double injected
    // Success=false, so the existing suite could not catch this. StepOutcomes.* supply the real shapes.
    // ══════════════════════════════════════════════════════════════════════════════════════════

    // ── T32: IsFatalRoot truth table — the corrected root predicate keys on ArtifactFailures ─────
    // [C] compile-RED: PipelineRunner.IsFatalRoot does not exist yet. Bind to the seam; do NOT
    // reimplement the predicate. Varied Azure services per the Universal Design Principle.
    [Fact]
    public void IsFatalRoot_TruthTable_KeysOnArtifactFailuresNotValidators()
    {
        var noFailures = Array.Empty<ArtifactFailure>();
        var oneFailure = new[]
        {
            ArtifactFailure.Create(
                "tool", "keyvault secret get",
                "Example prompt validation failed for this tool after automatic retries."),
        };

        // (1) THE D1 SHAPE — Fatal + mapped exit 0 (Success=true) + non-empty ArtifactFailures ⇒ ROOT
        //     via clause C2. An exit-code-only predicate would (wrongly) return false here.
        Assert.True(global::PipelineRunner.PipelineRunner.IsFatalRoot(
            FailurePolicy.Fatal, SuccessExitCode, oneFailure));

        // (2) Fatal + FatalExitCode + non-empty ArtifactFailures ⇒ ROOT (C1).
        Assert.True(global::PipelineRunner.PipelineRunner.IsFatalRoot(
            FailurePolicy.Fatal, FatalExitCode, oneFailure));

        // (3) Fatal + FatalExitCode + EMPTY ArtifactFailures ⇒ ROOT (C1 alone — the classic hard fail).
        Assert.True(global::PipelineRunner.PipelineRunner.IsFatalRoot(
            FailurePolicy.Fatal, FatalExitCode, noFailures));

        // (4) Fatal + HumanReviewExitCode + EMPTY ArtifactFailures ⇒ ROOT (any non-success exit is C1).
        Assert.True(global::PipelineRunner.PipelineRunner.IsFatalRoot(
            FailurePolicy.Fatal, HumanReviewExitCode, noFailures));

        // (5) NEGATIVE CONTROL — Fatal + mapped exit 0 + EMPTY ArtifactFailures ⇒ NOT a root.
        //     This is the pre-AI non-fatal skip (Success=true, failed validator, no artifact failures):
        //     because C2 keys on ArtifactFailures (not validators), it must stay non-fatal.
        Assert.False(global::PipelineRunner.PipelineRunner.IsFatalRoot(
            FailurePolicy.Fatal, SuccessExitCode, noFailures));

        // (6) NEGATIVE CONTROL — Warn policy is NEVER a root, even with non-empty ArtifactFailures.
        Assert.False(global::PipelineRunner.PipelineRunner.IsFatalRoot(
            FailurePolicy.Warn, SuccessExitCode, oneFailure));
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════
    // ADDENDUM C (post-#813 follow-up — Step 2 example-prompt PARAMETER-VALIDATION warnings must
    // not block Steps 3-6). Source: user report — validator exit 1 on "Required parameters missing
    // from example prompts: soft-delete" suppressed the rest of the namespace's pipeline.
    //
    // AD-029 §A2's C2 clause ("any non-empty ArtifactFailures ⇒ root") is deliberately narrowed here:
    // an <see cref="ArtifactFailure"/> now carries <c>IsBlocking</c> (default true — every existing
    // call site is unaffected). <see cref="global::PipelineRunner.PipelineRunner.IsFatalRoot"/> keys on
    // "any BLOCKING artifact failure" rather than "any artifact failure". This is additive/narrowing:
    // C1 (nonzero mapped exit) is untouched, and every other step's ArtifactFailure.Create(...) call
    // keeps the implicit isBlocking:true default, so T32/T33/T34/T35 above are unaffected (verified —
    // none of them pass isBlocking explicitly). Only ExamplePromptsStep's specific "required parameter
    // missing after retries exhausted" shape sets isBlocking:false (see NamespaceStepTests.cs).
    // ══════════════════════════════════════════════════════════════════════════════════════════

    // ── T32b: IsFatalRoot ignores NON-blocking artifact failures (the Step-2 warn-only shape) ────
    [Fact]
    public void IsFatalRoot_NonBlockingArtifactFailures_AreNotARoot()
    {
        var allNonBlocking = new[]
        {
            ArtifactFailure.Create(
                "tool", "storage account create",
                "Example prompt validation failed for this tool after automatic retries.",
                isBlocking: false),
        };

        // (1) Fatal + mapped exit 0 (Success=true) + ONLY non-blocking ArtifactFailures ⇒ NOT a root.
        //     This is the exact user-reported shape: a required-parameter validation warning must not
        //     suppress Steps 3-6.
        Assert.False(global::PipelineRunner.PipelineRunner.IsFatalRoot(
            FailurePolicy.Fatal, SuccessExitCode, allNonBlocking));

        // (2) POSITIVE CONTROL — a mix of one non-blocking + one blocking failure is STILL a root; a
        //     hard failure (missing prerequisites/process launch/missing artifacts) is never masked by
        //     a co-occurring soft parameter-validation warning.
        var mixed = new[]
        {
            allNonBlocking[0],
            ArtifactFailure.Create("tool", "storage account delete", "Example prompt generation failed for this tool."),
        };
        Assert.True(global::PipelineRunner.PipelineRunner.IsFatalRoot(
            FailurePolicy.Fatal, SuccessExitCode, mixed));

        // (3) POSITIVE CONTROL — C1 (nonzero mapped exit) still roots even with only non-blocking
        //     ArtifactFailures; IsBlocking narrows C2 only, never C1.
        Assert.True(global::PipelineRunner.PipelineRunner.IsFatalRoot(
            FailurePolicy.Fatal, FatalExitCode, allNonBlocking));

        // (4) Default `ArtifactFailure.Create(...)` (no isBlocking argument) is still blocking:true —
        //     every pre-existing call site (this file's T32/T33/T34/T35 included) is unaffected.
        Assert.True(ArtifactFailure.Create("tool", "storage account create", "x").IsBlocking);
    }

    // ── T33: replay the frozen beta.34 corpus through IsFatalRoot (Ellis BLOCKING-1) ─────────────
    // [C] compile-RED: binds to PipelineRunner.IsFatalRoot. Proves 16 of 17 real Step-2 roots return
    // Success=true (mapped exit 0) and are ROOTS only because C2 keys on ArtifactFailures — the exact
    // 0/10 cascade miss Ellis flagged. Corpus is READ-ONLY (frozen Baseline.Beta34 fixtures).
    [Fact]
    public void Beta34Corpus_EveryStep2Failure_IsFatalRoot_EvenWhenSuccessTrue()
    {
        var corpus = Beta34Corpus.Load();
        var step2 = corpus.Records.Where(r => r.StepId == 2).ToList();

        // Reconstruct each record's runtime shape from the sanitized fixture and route it through the
        // SAME production seams the runtime uses (MapStepFailureExitCode + IsFatalRoot). No hand-rolling.
        static int MappedExit(Beta34Corpus.Entry r)
            => global::PipelineRunner.PipelineRunner.MapStepFailureExitCode(
                FailurePolicy.Fatal, r.HasValidatorResults, null);

        static bool IsRoot(Beta34Corpus.Entry r)
            => global::PipelineRunner.PipelineRunner.IsFatalRoot(
                FailurePolicy.Fatal,
                MappedExit(r),
                new[] { ArtifactFailure.Create("tool", r.ArtifactName, "beta.34 replay") });

        // (1) Corpus shape pins, cross-checked against the manifest accounting block.
        Assert.Equal(17, corpus.Records.Count(r => r.StepId == 2));
        Assert.Equal(17, corpus.Accounting.Step2Records);
        Assert.Equal(16, step2.Count(r => r.HasValidatorResults));   // the D1 shape (Success=true)
        Assert.Equal(1, step2.Count(r => !r.HasValidatorResults));   // the lone Success=false generator failure

        // (2) Identity pin: the single Success=false Step-2 record is monitor's webtests-get.
        var successFalse = step2.Single(r => !r.HasValidatorResults);
        Assert.Equal("monitor.02.webtests-get.01", successFalse.StableId);

        // (3) THE REGRESSION — an exit-code-only predicate detects only 1 of 17 roots …
        Assert.Equal(1, step2.Count(r => MappedExit(r) != SuccessExitCode));
        //     … while the corrected predicate detects ALL 17, 16 of them via C2 at mapped exit 0.
        Assert.Equal(17, step2.Count(IsRoot));
        Assert.Equal(16, step2.Count(r => MappedExit(r) == SuccessExitCode && IsRoot(r)));

        // (4) Cascade linkage — 10 dependent (cascade) records, 16 dependency links, EVERY link is a
        //     Step-2 (stepId==02) upstream that resolves to one of the 17 classified Step-2 roots.
        var cascades = corpus.Records.Where(r => r.ChainRole == "cascade").ToList();
        Assert.Equal(10, corpus.Records.Count(r => r.ChainRole == "cascade"));
        Assert.Equal(10, corpus.Accounting.ChainRoleCascade);
        Assert.Equal(10, corpus.Accounting.DependentRecords);
        Assert.Equal(16, cascades.Sum(c => c.UpstreamStableIds.Count));
        Assert.Equal(16, corpus.Accounting.DependencyLinks);

        var upstreamIds = cascades.SelectMany(c => c.UpstreamStableIds).ToList();
        Assert.Equal(16, upstreamIds.Count(id => Beta34Corpus.StepIdOf(id) == 2));  // every link is stepId==02
        var step2StableIds = step2.Select(r => r.StableId).ToHashSet();
        Assert.Equal(16, upstreamIds.Count(step2StableIds.Contains));               // every link resolves to a root

        // (5) Root accounting cross-check — 24 total roots == 17 Step-2 roots (via IsFatalRoot) + 7
        //     Step-4 roots. Ties the seam's 17 back to the manifest's authoritative root count.
        Assert.Equal(24, corpus.Accounting.ChainRoleRoot);
        Assert.Equal(7, corpus.Records.Count(r => r.StepId == 4 && r.ChainRole == "root"));
        Assert.Equal(24, step2.Count(IsRoot) + corpus.Records.Count(r => r.StepId == 4 && r.ChainRole == "root"));
    }

    // ── T34: a REAL-shape fatal root (Success=true + ArtifactFailures) suppresses its transitive
    //         dependents, records the root, and does NOT abort later namespaces (AD-029 §A2/§A3) ──
    // [R] runtime-RED: on current code the D1 shape maps to exit 0 ⇒ no root ⇒ Steps 3/4/7/8 execute
    // for the failing namespace too. storage is the SECOND namespace, so its Step 2 is the fatal root
    // and its suppressed envelopes are the surviving ones (the harness output path is namespace-shared);
    // compute (first) fully runs, proving continuation is scoped, not a global abort.
    [Fact]
    public async Task RunAsync_RealShapeFatalRoot_SuppressesTransitiveDependents_AndContinues()
    {
        var repoRoot = CreateRepoRoot();
        var doubles = MirroredRegistry.CreateDoublesMatchingDefault(ScriptsRoot(repoRoot));
        Step(doubles, 2).Outcome = ns => ns == "storage"
            ? StepOutcomes.ValidationAfterRetriesFailure("storage account create")
            : Success();
        var (runner, _) = BuildRunner(repoRoot, doubles);

        var exit = await runner.RunAsync(
            Request(namespaceName: null, new[] { 1, 2, 3, 4, 6, 7, 8 }), CancellationToken.None);

        // closure(2) = {3,4,7,8}: each ran ONLY for compute (suppressed for storage) — 0 storage runs.
        Assert.Equal(new[] { "compute" }, Step(doubles, 3).ExecutedNamespaces.ToArray());
        Assert.Equal(new[] { "compute" }, Step(doubles, 4).ExecutedNamespaces.ToArray());
        Assert.Equal(new[] { "compute" }, Step(doubles, 7).ExecutedNamespaces.ToArray());
        Assert.Equal(new[] { "compute" }, Step(doubles, 8).ExecutedNamespaces.ToArray());

        // Independent Step 6 and the root Step 2 ran for BOTH namespaces (continuation, not abort).
        Assert.Equal(new[] { "compute", "storage" }, Step(doubles, 6).ExecutedNamespaces.ToArray());
        Assert.Equal(new[] { "compute", "storage" }, Step(doubles, 2).ExecutedNamespaces.ToArray());
        // Positive control: pre-root Step 1 ran for both.
        Assert.Equal(new[] { "compute", "storage" }, Step(doubles, 1).ExecutedNamespaces.ToArray());

        Assert.Equal(FatalExitCode, exit);

        // The surviving suppressed envelopes (storage ran last) name storage's Step-2 root.
        foreach (var suppressedStepId in new[] { 3, 4 })
        {
            var dir = ObservabilityDir(OutputPathOf(repoRoot), Step(doubles, suppressedStepId));
            Assert.True(StepResultReader.TryRead(dir, out var env));
            Assert.NotNull(env);
            Assert.True(env!.Suppressed);
            Assert.NotNull(env.BlockedByDependency);
            Assert.Equal("storage.02.root", env.BlockedByDependency!.RootFailureId);
        }

        // Positive control: the fatal ROOT recorded exactly one critical-failure JSON (Step 2); the
        // suppressed dependents recorded none.
        Assert.Single(CriticalFiles(repoRoot, "*-step-02-*.json"));
        Assert.Empty(CriticalFiles(repoRoot, "*-step-03-*.json"));
        Assert.Empty(CriticalFiles(repoRoot, "*-step-04-*.json"));
    }

    // ── T35: retire F1 — a suppressed dependent that WOULD fail loudly emits no step JSON ────────
    // [R] runtime-RED, genuinely discriminating: on current code the D1-shape root maps to exit 0 ⇒
    // Step 4 is NOT suppressed ⇒ it EXECUTES FailingDependentWithArtifacts and persists a step-04
    // critical JSON. Single namespace so the Executions==0 pins are literal (storage-continuation is
    // covered by T34); the "no step-04 JSON" assertion can only pass if Step 4 never ran.
    [Fact]
    public async Task RunAsync_RealShapeRoot_SuppressedFailingDependent_EmitsNoDependentJson()
    {
        var repoRoot = CreateRepoRoot();
        var doubles = MirroredRegistry.CreateDoublesMatchingDefault(ScriptsRoot(repoRoot));
        Step(doubles, 2).Outcome = _ => StepOutcomes.ValidationAfterRetriesFailure("cosmos database create");
        // Step 4 would persist its OWN critical JSON if (incorrectly) executed — the F1 discriminator.
        Step(doubles, 4).Outcome = _ => StepOutcomes.FailingDependentWithArtifacts("cosmos");
        var (runner, _) = BuildRunner(repoRoot, doubles);

        var exit = await runner.RunAsync(Request("compute", new[] { 1, 2, 3, 4 }), CancellationToken.None);

        Assert.Equal(1, Step(doubles, 2).Executions);  // positive control: the root ran
        Assert.Equal(0, Step(doubles, 3).Executions);  // suppressed
        Assert.Equal(0, Step(doubles, 4).Executions);  // suppressed ⇒ its failing body never runs
        Assert.Equal(FatalExitCode, exit);

        // Positive control: the ROOT (Step 2) persisted exactly one critical JSON …
        Assert.Single(CriticalFiles(repoRoot, "*-step-02-*.json"));
        // … and the suppressed dependent Step 4 persisted NONE (it would be exactly one had it run).
        Assert.Empty(CriticalFiles(repoRoot, "*-step-03-*.json"));
        Assert.Empty(CriticalFiles(repoRoot, "*-step-04-*.json"));
    }

    // ── T36: SelectedTransitiveDependents traverses the FULL graph, then filters (AD-029 §A3) ────
    // [R] runtime-RED: today's implementation intersects with the selected set at ENQUEUE time, so a
    // path leaving the selection (2 → 3 → 4 with 3 unselected) is severed and {4} is missed. Compiles
    // against the existing seam; realized once the traversal is corrected.
    [Fact]
    public void SelectedTransitiveDependents_TraversesThroughUnselectedIntermediate()
    {
        // Selection {2,4} omits the intermediate Step 3. Full-graph closure(2) = {3,4,7,8}; filtered
        // to the selection ⇒ {4}. A stop-at-unselected traversal returns {} instead.
        var suppressed = ClosureOnRealGraph(rootId: 2, selection: new[] { 2, 4 });

        Assert.Equal(new[] { 4 }, suppressed);        // exact — Step 4 IS reached through unselected 3
        Assert.DoesNotContain(2, suppressed);         // the root itself is never its own dependent
        Assert.DoesNotContain(3, suppressed);         // unselected intermediate is not emitted
        Assert.DoesNotContain(7, suppressed);         // unselected leaves are not emitted
        Assert.DoesNotContain(8, suppressed);

        // Positive control: when the whole chain is selected, the full closure returns — proving the
        // {4} result above is a real selection filter, not an empty-graph artefact.
        var full = ClosureOnRealGraph(rootId: 2, selection: new[] { 2, 3, 4, 7, 8 });
        Assert.Equal(new[] { 3, 4, 7, 8 }, full);
    }

    // ── T37: --skip-deps {2,4}, real-shape root at Step 2 suppresses Step 4 THROUGH unselected 3 ──
    // [R] runtime-RED: needs BOTH fixes — real-shape root detection (§A2) AND full-graph traversal
    // (§A3). On current code Step 2 maps to exit 0 (no root) and the traversal would sever at 3 anyway.
    [Fact]
    public async Task RunAsync_SkipDeps_RealShapeRoot_SuppressesDependentThroughUnselectedIntermediate()
    {
        var repoRoot = CreateRepoRoot();
        var doubles = MirroredRegistry.CreateDoublesMatchingDefault(ScriptsRoot(repoRoot));
        Step(doubles, 2).Outcome = _ => StepOutcomes.ValidationAfterRetriesFailure("speech transcription create");
        var (runner, _) = BuildRunner(repoRoot, doubles);

        var exit = await runner.RunAsync(Request("compute", new[] { 2, 4 }, skipDeps: true), CancellationToken.None);

        Assert.Equal(1, Step(doubles, 2).Executions);  // positive control: the root ran
        Assert.Equal(0, Step(doubles, 4).Executions);  // suppressed through the unselected intermediate 3
        Assert.Equal(FatalExitCode, exit);

        // The suppressed Step-4 envelope names the Step-2 root.
        var dir = ObservabilityDir(OutputPathOf(repoRoot), Step(doubles, 4));
        Assert.True(StepResultReader.TryRead(dir, out var env));
        Assert.NotNull(env);
        Assert.True(env!.Suppressed);
        Assert.NotNull(env.BlockedByDependency);
        Assert.Equal("compute.02.root", env.BlockedByDependency!.RootFailureId);

        // Control: with Step 2 SUCCEEDING under the same {2,4}+skip-deps, Step 4 runs — suppression is
        // driven by the real-shape failure, not by the selection shape.
        var okRoot = CreateRepoRoot();
        var okDoubles = MirroredRegistry.CreateDoublesMatchingDefault(ScriptsRoot(okRoot));
        var (okRunner, _) = BuildRunner(okRoot, okDoubles);
        var okExit = await okRunner.RunAsync(Request("compute", new[] { 2, 4 }, skipDeps: true), CancellationToken.None);
        Assert.Equal(1, Step(okDoubles, 4).Executions);
        Assert.Equal(SuccessExitCode, okExit);
    }

    // ── T38: the suppressed envelope is written to the CANONICAL step dir, overwriting a prior
    //         successful run's envelope (AD-029 §A4 — the D4 canonical/observability split) ─────────
    // [R] runtime-RED: today the suppressed envelope lands ONLY in the observability dir, so a reader
    // consulting the canonical step directory still sees the STALE success from Run 1.
    [Fact]
    public async Task RunAsync_SuppressedStep_WritesCanonicalEnvelope_OverwritingStaleSuccess()
    {
        var repoRoot = CreateRepoRoot();

        // Run 1: Steps 3 and 4 execute successfully and write canonical success envelopes.
        var firstDoubles = MirroredRegistry.CreateDoublesMatchingDefault(ScriptsRoot(repoRoot));
        var (firstRunner, _) = BuildRunner(repoRoot, firstDoubles);
        await firstRunner.RunAsync(Request("compute", new[] { 3, 4 }, skipDeps: true), CancellationToken.None);

        var canonicalStep4 = CanonicalDir(OutputPathOf(repoRoot), Step(firstDoubles, 4));
        Assert.True(StepResultReader.TryRead(canonicalStep4, out var afterRun1));
        Assert.NotNull(afterRun1);
        Assert.Equal(StepResultStatus.Success, afterRun1!.Status);   // baseline: a real success envelope …
        Assert.True(afterRun1.Suppressed != true);                   // … not suppressed

        // Run 2: same output path; Step 2 fails with the real shape ⇒ Step 4 is suppressed.
        var secondDoubles = MirroredRegistry.CreateDoublesMatchingDefault(ScriptsRoot(repoRoot));
        Step(secondDoubles, 2).Outcome = _ => StepOutcomes.ValidationAfterRetriesFailure("monitor workspace create");
        var (secondRunner, _) = BuildRunner(repoRoot, secondDoubles);
        await secondRunner.RunAsync(Request("compute", new[] { 2, 4 }, skipDeps: true), CancellationToken.None);

        // The CANONICAL step-4 envelope must now be the suppressed one, not Run 1's stale success.
        Assert.True(StepResultReader.TryRead(canonicalStep4, out var afterRun2));
        Assert.NotNull(afterRun2);
        Assert.True(afterRun2!.Suppressed);                          // overwritten in place
        Assert.Equal(StepResultStatus.Failure, afterRun2.Status);
        Assert.Equal(ValidationStatus.Skipped, afterRun2.ValidationStatus);
        Assert.Equal(4, afterRun2.Version);
        Assert.Equal("1.0", afterRun2.SchemaVersion);
        Assert.Equal(0, afterRun2.OutputFileCount);
        Assert.True(afterRun2.OutputArtifacts is null || afterRun2.OutputArtifacts.Count == 0);
        Assert.NotNull(afterRun2.BlockedByDependency);
        Assert.Equal("compute.02.root", afterRun2.BlockedByDependency!.RootFailureId);
        Assert.Single(Directory.GetFiles(canonicalStep4, "step-result.json"));  // overwrite, not duplicate

        // The observability copy carries the same suppressed signal (parity, not divergence).
        var observabilityStep4 = ObservabilityDir(OutputPathOf(repoRoot), Step(secondDoubles, 4));
        Assert.True(StepResultReader.TryRead(observabilityStep4, out var obs));
        Assert.NotNull(obs);
        Assert.True(obs!.Suppressed);
    }

    // ── T39: the real shape under a WARN policy is NOT a root — dependents run, catalog succeeds ──
    // [R] guard: GREEN on the corrected predicate (Warn is never a root). RED under mutation M38 (drop
    // the Fatal-policy guard), which would let a warn step with ArtifactFailures suppress Step 8. This
    // is the real-shape complement to T15 (which drives the warn step with the OLD Success=false shape).
    [Fact]
    public async Task RunAsync_WarnStep_RealShapeArtifactFailure_DoesNotSuppressDependents()
    {
        var repoRoot = CreateRepoRoot();
        var doubles = MirroredRegistry.CreateDoublesMatchingDefault(ScriptsRoot(repoRoot));
        // Step 7 is a Warn-policy step in the real graph; drive it with the real Step-2 failure shape.
        Step(doubles, 7).Outcome = _ => StepOutcomes.ValidationAfterRetriesFailure("keyvault secret get");
        var (runner, _) = BuildRunner(repoRoot, doubles);

        var exit = await runner.RunAsync(Request("compute", new[] { 1, 2, 3, 4, 7, 8 }), CancellationToken.None);

        Assert.Equal(1, Step(doubles, 7).Executions);  // positive control: the warn step ran
        Assert.Equal(1, Step(doubles, 8).Executions);  // dependent of Step 7 is NOT suppressed
        Assert.Equal(SuccessExitCode, exit);           // a warn-only artifact failure never fails the catalog

        // Positive control: the warn step DID record its artifact failure (so the "no suppression"
        // result is not because the failure was silently dropped).
        Assert.Single(CriticalFiles(repoRoot, "*-step-07-*.json"));
    }

    // ── T40: the pre-AI non-fatal skip (Success=true, EMPTY ArtifactFailures) is NOT a root ──────
    // [R] guard: GREEN on the corrected predicate because C2 keys on ArtifactFailures. RED under
    // mutation M36 (re-key C2 onto validator results), which would make this failed-validator/empty-
    // artifacts shape a root and suppress Steps 3/4.
    [Fact]
    public async Task RunAsync_PreAiNonFatalSkip_EmptyArtifactFailures_IsNotARoot()
    {
        var repoRoot = CreateRepoRoot();
        var doubles = MirroredRegistry.CreateDoublesMatchingDefault(ScriptsRoot(repoRoot));
        Step(doubles, 2).Outcome = _ => StepOutcomes.PreAiSkipNonFatalOutcome();
        var (runner, _) = BuildRunner(repoRoot, doubles);

        var exit = await runner.RunAsync(Request("compute", new[] { 1, 2, 3, 4 }), CancellationToken.None);

        Assert.Equal(1, Step(doubles, 2).Executions);  // the step ran
        Assert.Equal(1, Step(doubles, 3).Executions);  // dependent NOT suppressed
        Assert.Equal(1, Step(doubles, 4).Executions);  // transitive dependent NOT suppressed
        Assert.Equal(SuccessExitCode, exit);

        // Positive control: with EMPTY ArtifactFailures nothing is persisted as a critical failure …
        Assert.Empty(CriticalFiles(repoRoot, "*-step-02-*.json"));
        // … but the very same shape WITH one artifact failure is a root (T35) — so the emptiness of the
        // step-02 assertion above reflects the C2 contract, not a broken recorder.
    }

    // ────────────────────────────── helpers ──────────────────────────────

    // Readability forwarders to the production exit-code constants (ADDENDUM B tests).
    private const int SuccessExitCode = global::PipelineRunner.PipelineRunner.SuccessExitCode;
    private const int FatalExitCode = global::PipelineRunner.PipelineRunner.FatalExitCode;
    private const int HumanReviewExitCode = global::PipelineRunner.PipelineRunner.HumanReviewExitCode;

    // The CANONICAL step workspace directory (mirrors PipelineRunner.GetStepWorkspaceDirectory:
    // Path.Combine(OutputPath, GetStepIdentifierSlug(step))). Distinct from ObservabilityDir — T38
    // asserts the suppressed envelope lands HERE, overwriting a prior run's success (AD-029 §A4/D4).
    private static string CanonicalDir(string outputPath, IPipelineStep step)
        => Path.Combine(outputPath, global::PipelineRunner.PipelineRunner.GetStepIdentifierSlug(step));

    // Critical-failure JSON files matching a glob, tolerant of the directory not existing (no critical
    // failures were recorded), so "absent" assertions are exact rather than throwing.
    private static string[] CriticalFiles(string repoRoot, string pattern)
    {
        var dir = Path.Combine(OutputPathOf(repoRoot), "critical-failures");
        return Directory.Exists(dir) ? Directory.GetFiles(dir, pattern) : Array.Empty<string>();
    }

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
