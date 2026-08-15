# Step 2 (#813) RED-Test Matrix — Runtime Dependency Suppression, Accounting & Orchestration

**Author:** Cameron (Test Lead) · **Status:** Authoritative · **Contract:** `.squad/decisions/inbox/riley-ad-029-runtime-dependency-suppression.md` (AD-029 §1–§9)
**Implementer:** Parker. Follow this literally. **Cameron is reviewer-locked out of writing these tests.**

> This document is a test *strategy*. It contains **no test code**. Parker writes every test named below. Where a value is pinned (e.g. `"compute.02.root"`, `Executions == 0`, exit `1`), Parker MUST assert that exact value — never a set membership, never a range.

---

## 0. Scope, files, harness, and required production seams

### 0.1 Files Parker will touch (ONLY these)
| File | Kind | Action |
|---|---|---|
| `mcp-tools/DocGeneration.PipelineRunner.Tests/Unit/DependencySuppressionTests.cs` | xUnit | **NEW** |
| `mcp-tools/DocGeneration.PipelineRunner.Tests/Unit/RunAccountingTests.cs` | xUnit | **NEW** |
| `mcp-tools/DocGeneration.PipelineRunner.Tests/Fixtures/TestDoubles.cs` | xUnit fixtures | **EXTEND** (shared doubles only — no parallel harness) |
| `mcp-tools/validation/tests/StartWithLogs.Tests.ps1` | Pester | **EXTEND + UPDATE one existing It** |

No `.cs`/`.ps1` production file is edited by Parker in the RED phase. The RED phase compiles the two new xUnit files against the **new production API that does not yet exist** (see §0.4); those tests fail to compile until GREEN — that is the intended RED for the API-shape tests (called out per-test in §4).

### 0.2 Reuse existing harness (do NOT invent a parallel one)
- Construct the runner exactly as `PipelineRunnerGlobalScopeTests` / `SkipDependencyValidationTests` do:
  `new PipelineContextFactory(RecordingProcessRunner, WorkspaceManager, StaticCliMetadataLoader, TargetMatcher, StubFilteredCliWriter, StubBuildCoordinator, StubAiCapabilityProbe, BufferedReportWriter, repoRoot)` then `new PipelineRunner(new StepRegistry([...doubles...]), contextFactory, changelogGate: null)`.
- `repoRoot` = a per-test temp dir containing `mcp-tools/scripts/` and an empty `mcp-doc-generation.sln` (copy the existing helper).
- `StaticCliMetadataLoader` returns namespaces `["compute","storage"]` in order — this is the two-namespace fixture every continuation test relies on.
- `PipelineRequest` signature (already present): `(string? Namespace, IReadOnlyList<int> Steps, string OutputPath, bool SkipBuild, bool SkipValidation, bool DryRun, bool SkipEnvValidation=false, bool SkipDependencyValidation=false, …)`.
- A **failing** namespace step outcome = `new StepResult(Success:false, Warnings:["boom"], TimeSpan.Zero, [], [], [], [])`. A **human-review** outcome = success with `ExitCodeOverride = 2`.
- `InternalsVisibleTo("DocGeneration.PipelineRunner.Tests")` is already on the runner project — closure/adjacency seams are asserted directly.

### 0.3 Shared doubles Parker adds to `Fixtures/TestDoubles.cs` (used by BOTH new files)
1. **`RecordingNamespaceStep`** — a configurable `StepDefinition`/`IPipelineStep` double exposing:
   - ctor params: `id, name, scope, failurePolicy, dependsOn, maxRetries`.
   - `public int Executions` (incremented on every `ExecuteAsync`).
   - `public List<string> ExecutedNamespaces` (records the namespace slug on each execution, for leak/continuation proofs).
   - a per-namespace outcome selector `Func<string?, StepResult> Outcome` (default → `StepResult.DryRun`/success), so one double can fail for `compute` yet succeed for `storage`.
2. **`MirroredRegistry.CreateDoublesMatchingDefault(scriptsRoot)`** — returns nine `RecordingNamespaceStep` whose `(Id, Scope, FailurePolicy, DependsOn, MaxRetries)` **exactly equal** `StepRegistry.CreateDefault(scriptsRoot).GetAllSteps()`. Test `StepDoubleRegistry_MirrorsRealStepRegistryGraph` (T8) proves this mirror is faithful, so behavioral tests built on doubles are trustworthy.
3. Reuse the existing `StaticCliMetadataLoader` shape already duplicated in the test project (centralize into `TestDoubles.cs` if convenient; do not change its `["compute","storage"]` output).

### 0.4 Production seams AD-029 requires (tests bind to these; absence = RED)
| Seam | Kind | Used by |
|---|---|---|
| `StepResultFile.Suppressed` (`bool?`) | NEW envelope field (§2) | T12, T14, T25 (compile-RED) |
| `StepResultFile.BlockedByDependency` (nested: `Namespace`, `FailedRootStepId`, `FailedRootStepName`, `RootFailureId`) | NEW envelope type (§2) | T12, T25 (compile-RED) |
| `internal` reverse-adjacency builder over `IEnumerable<IPipelineStep>` (e.g. `PipelineRunner.BuildDependentsOf`) | NEW seam (§3) | T1 |
| `internal` transitive-suppression closure `SelectedTransitiveDependents(rootId, selectedIds, dependentsOf)` | NEW seam (§3) | T2–T7 |
| `run-accounting.json` emitted at `CompleteRun` under `OutputPath` | NEW output (§6) | T21–T29 |
| Suppressed envelope persisted under `OutputPath/observability/{id}-{slug}/step-result.json` | NEW code path (§3) | T12, T13, T14, T20 |

> **Testability requirement Parker must flag to Riley/Quinn if missing:** the reverse-adjacency and closure logic MUST be reachable as `internal static` methods that accept the **real** `StepRegistry.CreateDefault(...)` steps, so T1–T7 assert against production edges rather than a hand-rolled graph. If the implementation buries this in a private local function, T1–T7 cannot bind and Parker escalates rather than hand-rolling a duplicate graph.

### 0.5 Observation points after `RunAsync` (persist across `WorkspaceManager.DeleteAll()`)
`OutputPath` resolves to `Path.Combine(repoRoot, <outputPathLeaf>)` (relative → `Path.GetFullPath(Path.Combine(repoRoot, normalized))` in `PipelineContextFactory`). Pass a known leaf (e.g. `".\generated"`) and read:
- Suppressed/normal envelope: `{repoRoot}\generated\observability\{step.Id}-{slug}\step-result.json` → `StepResultReader.TryRead(dir, out env)`.
- Critical-failure JSON: `{repoRoot}\generated\critical-failures\*.json` (filenames `…-step-{id:D2}-….json`).
- Run accounting: `{repoRoot}\generated\run-accounting.json`.
`DeleteAll()` removes only tracked temp workspaces, NOT `OutputPath`, so these reads are stable.

### 0.6 Naming conventions (match existing repo style)
- xUnit behavioral: `RunAsync_<Scenario>_<Expectation>`; pure-function: `<Method>_<Case>_<Expectation>` (mirrors `ExitCodeMappingTests`, `SkipDependencyValidationTests`).
- Pester: `Describe "start-with-logs.ps1"` → `Context "<behavior>"` → `It "<lower-case behavioral sentence>"` (mirrors `StartWithLogs.Tests.ps1`).

---

## 1. Gate → Test mapping

Every contractual gate from #813/AD-029 maps to ≥1 exact test identifier. `[C]`=compile-RED (needs new API), `[R]`=runtime-RED (fails on current behavior), `[M]`=guard proven RED by mutation (§2), `[G]`=green regression guard.

### 1.A `DependencySuppressionTests.cs` (xUnit)

| # | Gate (contractual) | `TestClass.Test_Name` | RED |
|---|---|---|---|
| T1 | Full **real**-registry reverse adjacency | `DependencySuppressionTests.Registry_ReverseAdjacency_MatchesRealStepRegistryEdges` | `[C]` |
| T2 | Transitive closure — fatal Step 1 | `DependencySuppressionTests.SelectedTransitiveDependents_FatalStep1_Suppresses_2_3_4_7_8` | `[C]` |
| T3 | Transitive closure — fatal Step 2 | `DependencySuppressionTests.SelectedTransitiveDependents_FatalStep2_Suppresses_3_4_7_8` | `[C]` |
| T4 | Transitive closure — fatal Step 3 | `DependencySuppressionTests.SelectedTransitiveDependents_FatalStep3_Suppresses_4_7_8` | `[C]` |
| T5 | Transitive closure — fatal Step 4 | `DependencySuppressionTests.SelectedTransitiveDependents_FatalStep4_Suppresses_7_8` | `[C]` |
| T6 | Closure **∩ selection only** (AD-029 §3 ex.) | `DependencySuppressionTests.SelectedTransitiveDependents_Step2Root_Selection_1_2_3_4_7_Suppresses_3_4_7` | `[C]` |
| T7 | Independent step survives (AD-029 §3 ex.) | `DependencySuppressionTests.SelectedTransitiveDependents_Step4Root_Selection_1_2_3_4_6_SuppressesNothing` | `[C]` |
| T8 | Doubles faithfully mirror real graph | `DependencySuppressionTests.StepDoubleRegistry_MirrorsRealStepRegistryGraph` | `[R]` |
| T9 | **No dependent side effects** + independent runs + continues | `DependencySuppressionTests.RunAsync_FatalStep2_SuppressesDependents_RunsIndependentStep6_AndContinues` | `[R]` |
| T10 | **Later independent namespace still runs** | `DependencySuppressionTests.RunAsync_FatalNamespaceStep_LaterNamespaceStillExecutes` | `[R]` |
| T11 | State resets — suppression does not leak | `DependencySuppressionTests.RunAsync_StateResetsBetweenNamespaces_SuppressionDoesNotLeak` | `[R]` |
| T12 | Suppressed envelope + `blockedByDependency` | `DependencySuppressionTests.RunAsync_SuppressedStep_WritesSuppressedEnvelope_WithBlockedByDependency` | `[C]` |
| T13 | **No critical-failure JSON for suppressed step** | `DependencySuppressionTests.RunAsync_SuppressedStep_EmitsNoCriticalFailureJson_ButRootDoes` | `[M]` |
| T14 | Envelope schema pins (§2) | `DependencySuppressionTests.RunAsync_SuppressedEnvelope_KeepsSchemaVersion1_0_AndVersion4_StatusFailure_ValidationSkipped` | `[C]` |
| T15 | Warning failure does **not** suppress dependents | `DependencySuppressionTests.RunAsync_WarnStepFails_DoesNotSuppressDependents_AndCatalogSucceeds` | `[M]` |
| T16 | Global fatal aborts whole catalog | `DependencySuppressionTests.RunAsync_GlobalFatalStep0_AbortsCatalog_NoNamespaceRuns` | `[G]` |
| T17 | Worst-exit-code preserved across roots | `DependencySuppressionTests.RunAsync_TwoFatalRootsAcrossNamespaces_WorstExitCodeIsOne` | `[R]` |
| T18 | `--skip-deps` never rescues a suppressed dependent | `DependencySuppressionTests.RunAsync_SkipDeps_SelectedStep3Fails_Step4StillSuppressed` | `[R]` |
| T19 | Cancel stops remaining namespaces | `DependencySuppressionTests.RunAsync_CancellationRequested_StopsRemainingNamespaces` | `[R]` |
| T20 | Same-workspace rerun overwrites suppressed envelope | `DependencySuppressionTests.RunAsync_RerunSameOutputPath_SuppressedEnvelopeOverwrittenOnCleanRerun` | `[C]` |
| T18a | (guard) planning error unless `--skip-deps` | *(reference existing)* `SkipDependencyValidationTests.RunAsync_Step4WithoutStep3_WithoutSkipDeps_ReturnsInvalidArguments` | `[G]` |
| T18b | (guard) `--skip-deps` skips **planning** check only | *(reference existing)* `SkipDependencyValidationTests.RunAsync_Step4WithoutStep3_WithSkipDeps_DoesNotReturnInvalidArguments` | `[G]` |

> Parker: confirm the two referenced existing `SkipDependencyValidationTests` names against the file and use the actual identifiers; they are cited as **guards**, not rewritten.

### 1.B `RunAccountingTests.cs` (xUnit) — separate success/root/warning/suppressed/cascade/unclassified summaries

| # | Gate | `TestClass.Test_Name` | RED |
|---|---|---|---|
| T21 | `run-accounting.json` emitted at CompleteRun | `RunAccountingTests.RunAccounting_IsEmittedAsJson_AtCompleteRun` | `[R]` |
| T22 | Success bucket = all-steps-succeeded, zero roots | `RunAccountingTests.RunAccounting_SuccessfulNamespaces_AllStepsSucceeded_ZeroRoots` | `[R]` |
| T23 | Root bucket = each root, stable id | `RunAccountingTests.RunAccounting_RootFailedNamespaces_ReportEachRoot_WithStableRootFailureId` | `[R]` |
| T24 | Warning-only bucket separate, not root | `RunAccountingTests.RunAccounting_WarningOnlyFailures_ReportedSeparately_NotAsRoot` | `[R]` |
| T25 | Suppressed bucket carries step id + root id | `RunAccountingTests.RunAccounting_SuppressedSteps_ReportStepIdAndRootFailureId` | `[C]` |
| T26 | Buckets mutually exclusive (first-match order) | `RunAccountingTests.RunAccounting_Partition_RecordAssignedByFirstMatchOrder_MutuallyExclusive` | `[R]` |
| T27 | Cascade category uses **chainRole** not classification (L-004) | `RunAccountingTests.RunAccounting_CascadeCategory_UsesChainRoleCount10_NotClassification9` | `[R]` |
| T28 | Unclassified/diagnostic category | `RunAccountingTests.RunAccounting_UnclassifiedCategory_Beta34DiagnosticIsExactlyOne` | `[R]` |
| T29 | Six-category partition reconciles to baseline | `RunAccountingTests.RunAccounting_SixCategoryPartition_ReconcilesTo34Logical68Physical` | `[R]` |

**Pinned `run-accounting.json` contract these tests assert** (Parker + Quinn/Riley must agree the field names before GREEN; tests pin whatever is agreed — proposed):
```json
{
  "schemaVersion": "1.0",
  "successfulNamespaces": ["storage"],
  "rootFailedNamespaces": [
    { "namespace": "compute", "rootStepId": 2, "rootStepName": "Generate example prompts",
      "rootFailureId": "compute.02.root", "exitCode": 1 }
  ],
  "warningOnlyFailures": [
    { "namespace": "compute", "stepId": 7, "stepName": "Validate article health" }
  ],
  "suppressedSteps": [
    { "namespace": "compute", "stepId": 3, "rootFailureId": "compute.02.root" },
    { "namespace": "compute", "stepId": 4, "rootFailureId": "compute.02.root" }
  ]
}
```
For T27–T29 the six-category reconciliation overlays a **live** run-accounting partition onto the **frozen** beta34 manifest (`mcp-tools/DocGeneration.Baseline.Beta34.Tests/Fixtures/beta34-baseline-manifest.json`, read-only): `chainRoleCounts` `{root:24, cascade:10}`, `classificationCounts` `{root:21, cascade:9, mixed:3, diagnostic:1}`, `34` logical / `68` physical. Locate the manifest by walking up for `mcp-doc-generation.sln` (replicate `BaselineContext.FindRepoRoot`; **do not** add a project reference or edit the fixture).

### 1.C `StartWithLogs.Tests.ps1` (Pester) — script orchestration §7

| # | Gate | `Describe / Context / It` | RED |
|---|---|---|---|
| P1 | `--skip-build` gating (build until confirmed) — **RED driver** | `It "continues past a namespace failure and reports it in the summary"` **(UPDATE expected invocations)** | `[R]` |
| P2 | Conservative rebuild when prior built but exited nonzero | `It "rebuilds the next namespace when the prior namespace built but exited nonzero"` | `[R]` |
| P3 | Immediate `$LASTEXITCODE` capture (preserved nonzero exit) | `It "captures the namespace exit code immediately after start.sh before any output-move cmdlet"` | `[R]` (structural) |
| P4 | Failure decision uses captured var not `$LASTEXITCODE` | `It "uses the captured namespace exit code rather than \$LASTEXITCODE for the failure decision"` | `[R]` (structural) |
| P5 | AD-027 case-insensitive collision check | `It "declares no param or local variable whose name collides case-insensitively (AD-027)"` | `[R]` (structural) |
| P6 | (guard) confirmed-build-then-skip happy path | `It "dispatches every mapped namespace to start.sh with steps 1 through 5"` *(existing — stays green)* | `[G]` |

**P1 updated expected invocation array** (fake `start.sh` logs `"$*"`; `GENERATION_FAIL_NAMESPACE=keyvault`; input `[storage, keyvault, monitor]` sorts to `keyvault, monitor, storage`):
```
"keyvault 1,2,3,4,5"                                  # idx0 builds (unconfirmed) → FAILS (exit 17)
"monitor 1,2,3,4,5"                                   # idx1 still unconfirmed → REBUILDS → succeeds → confirmed
"storage 1,2,3,4,5 --skip-build --skip-npm-update"   # idx2 confirmed → skips
```
The current script (`if ($index -gt 0)`) would instead emit `--skip-build --skip-npm-update` on `monitor` — so this updated assertion is RED until the `$sharedBuildConfirmed` rule ships. Also assert the summary still reports `keyvault` failed AND the cmdlet's final exit is nonzero (preserved-nonzero-exit gate lives here).

**P2 minimal scenario:** two namespaces `[aaa, bbb]`, `GENERATION_FAIL_NAMESPACE=aaa`. Expected exactly `@("aaa 1,2,3,4,5","bbb 1,2,3,4,5")` — crux assertion: `bbb` has **no** `--skip-build`/`--skip-npm-update` (prior built but exited nonzero ⇒ not confirmed ⇒ rebuild).

**P3/P4/P5 are structural** (source-text) because the `$LASTEXITCODE`-ordering and collision fixes are latent (cmdlets between the call and the read don't clobber `$LASTEXITCODE`, so a pure behavioral test would pass on current code = vacuous). Precedent already in the file: `It "does not call the legacy Generate-ToolFamily script"` matches source text. Assert via `Get-Content $ProductionScript -Raw`:
- **P3:** regex requires the line immediately after the `& $bashPath @startArguments` invocation to be an assignment capturing `$LASTEXITCODE` into a namespace-exit local, with **no** cmdlet between them. Anti-vacuity: the regex must fail if any token appears between the bash call and the capture.
- **P4:** the failure branch condition references the captured local (e.g. `$namespaceExitCode`), and the raw script contains **no** `if ($LASTEXITCODE` / `$LASTEXITCODE -ne 0` in the per-namespace failure decision.
- **P5:** enumerate `param()` names + all `$assignments`; assert the count of names that are equal under `ToLowerInvariant()` but represent distinct declarations is **0**. Must explicitly include the new locals `$namespaceExitCode`, `$sharedBuildConfirmed` and prove they do not fold onto `NamespaceList`/`NamespaceFile`/`PreflightOnly`/`$namespace`/`$namespaces`.

**Gate coverage roll-up:** full-registry suppression T1–T9; fatal/warning/global T9/T15/T16; no-dependent-side-effects (exec/output/retry/**no critical JSON**) T9/T13; later namespace runs T10; retry/resume/cancel/rerun T19/T20 (+existing replay guards in `PipelineRunnerGlobalScopeTests`); mutation/revert §2; AD-027 P5; preserved nonzero exit + immediate capture P1/P3/P4; `--skip-deps` semantics T18/T18a/T18b; build/preflight gating for `--skip-build` P1/P2; separate summaries T21–T29.

---

## 2. Mutation / revert matrix

Each row: **one** production change from AD-029 → the named test that goes **RED** when that change **alone** is reverted → exact mutation instruction. Reviewer rule (charter): *if the test would still pass with the change reverted, it is not a real test.* Every AD-029 element in §2/§3/§4/§5/§6/§7 is covered.

| AD-029 ref | Production change | Test that goes RED on revert | Exact mutation instruction |
|---|---|---|---|
| §2 | Add `Suppressed` field to `StepResultFile` | T12, T14 | Delete the `Suppressed` property from `StepResultFile.cs` → T12/T14 fail to compile. |
| §2 | Add `BlockedByDependency` type + property | T12, T25 | Remove the `BlockedByDependency` nested type/property → T12/T25 fail to compile. |
| §2 | `schemaVersion` stays `"1.0"` (no bump) | T14 | Change writer to emit `schemaVersion="2.0"` → T14 RED (`Assert.Equal("1.0", …)`), and reader-round-trip throws. |
| §2 | Integer `Version` bumped 3→4 | T14 | Leave `Version=3` → T14 RED (`Assert.Equal(4, env.Version)`). |
| §2 | Suppressed step writes `status="failure"`,`validationStatus="skipped"`, `suppressed=true` | T14, T13 | Change suppressed writer to `status="success"` → T14 RED; drop `suppressed=true` (leave null) → T13's authoritative-signal assert RED. |
| §3 | Suppressed dependents are **not executed** (`WriteSuppressedEnvelope` + `continue`, no `ExecuteStepAsync`) | T9, T13 | Replace suppression branch with a normal `ExecuteStepAsync` call → suppressed Step 3/4 execute (T9 `Executions==0` RED) and emit their own critical JSON (T13 RED). |
| §3 | Early-abort `return CompleteRun(...)` replaced by **record-root + continue** | T10, T11, T17 | Restore `return CompleteRun(...)` on nonzero namespace step → later namespace/independent steps never run (T10/T11 RED; T17 sees only one root, exit path differs). |
| §3 | Suppression uses **reverse** adjacency (dependents-of), not forward | T1, T9 | Swap `BuildDependentsOf` to return forward `DependsOn` edges → T1 RED (edges reversed) and T9 suppresses the wrong steps. |
| §3 | Closure intersected with **selected** step ids | T6, T7, T9(Step6) | Remove `∩ selectedIds` (suppress all transitive dependents regardless of selection) → T7 RED (Step 6 wrongly suppressed / or unselected 8 counted), T6 RED. |
| §3 | Transitive (not just direct) dependents suppressed | T2, T3 | Suppress only **direct** dependents → T3 RED (Step 4/7/8 not all suppressed from root 2). |
| §3 | Suppressed envelope persisted under `OutputPath/observability` | T12, T20 | Write suppressed envelope only to the step workspace (deleted by `DeleteAll`) → T12/T20 RED (`TryRead` returns false). |
| §3 | Fresh `NamespaceRuntimeState` per namespace | T11 | Hoist runtime state above the namespace loop (shared) → T11 RED (compute's suppression set leaks; storage Step 3/4 `Executions==0`). |
| §4 | Global-scope loop stays **abort-on-fatal** (not routed through suppression) | T16 | Route Step 0 fatal through the new suppression/continue path → T16 RED (namespaces execute after a global fatal). |
| §4 | Worst-exit-code = `Worse(1,2)=1` (hard fatal dominates human-review) | T17 | Change `Worse` to return `2` (human-review) when both present → T17 RED (`Assert.Equal(1, exit)`). |
| §5 | Suppression is independent of `SkipDependencyValidation` | T18 | Gate the runtime suppression branch behind `!request.SkipDependencyValidation` → T18 RED (Step 4 executes under `--skip-deps`). |
| §5 | `--skip-deps` alters **planning** only, never converts a failed selected dep to success | T18, T18a | Make `--skip-deps` skip the runtime root-record for the selected failed Step 3 → T18 RED (Step 4 runs / no root recorded). |
| §6 | `run-accounting.json` written at `CompleteRun` | T21 | Comment out the accounting emitter → T21 RED (file absent). |
| §6 | Suppressed steps counted in **suppressed** bucket, not success | T25, T26 | Fold suppressed steps into `successfulNamespaces` → T25 RED (bucket empty) and T26 RED (record in two buckets). |
| §6 | Warning-only failures reported separately, not as roots | T24 | Emit warning-only namespace into `rootFailedNamespaces` → T24 RED. |
| §6 | Cascade category derived from **chainRole** | T27 | Compute cascade from `classificationCounts.cascade` (9) → T27 RED (`Assert.Equal(10, …)` and `Assert.NotEqual(9, …)`). |
| §6 | First-match partition is mutually exclusive | T26 | Change partition to allow a record in multiple buckets → T26 RED. |
| §7 | `$sharedBuildConfirmed` flag replaces `$index -gt 0` | P1, P2 | Restore `if ($index -gt 0)` skip-flag logic → P1 RED (monitor gets skip flags) and P2 RED (bbb gets skip flags). |
| §7 | `$sharedBuildConfirmed` set only when built AND `$namespaceExitCode -eq 0` | P2 | Set `$sharedBuildConfirmed=$true` unconditionally after first namespace → P2 RED (bbb skips despite aaa failing). |
| §7 | Immediate `$LASTEXITCODE` capture on the line after the bash call | P3 | Move the capture below an output-move cmdlet (insert a `Move-Item`/`Write-Host` between) → P3 regex RED. |
| §7 | Failure decision uses `$namespaceExitCode` | P4 | Change failure `if` back to `$LASTEXITCODE -ne 0` → P4 RED. |
| §7 | No case-insensitive param/local collision (AD-027) | P5 | Rename a new local to `$namespacelist` (collides with `$NamespaceList`) → P5 RED. |

**Matrix size: 26 mutation rows** covering AD-029 §2 (5), §3 (7), §4 (2), §5 (2), §6 (5), §7 (5).

---

## 3. Negative / anti-vacuity rules

Global bans (charter + BLOCKING-1 finding):
- **BANNED:** `Should -BeIn @(…)`, `Assert.Contains(value, set)` as the *only* assertion, `Should -Match '(warn|fail)'`, "not null / not empty" as sole assertion, reflection-only "member exists" tests, and any assertion that passes regardless of the fix. **PIN exact values.**
- Every count is an **exact equality** (`==`), never `>=`/`<=`/range.
- Every "did not happen" assertion is paired with a **positive control** proving the observation channel works.

Per-risky-test rules:
| Test | Anti-vacuity rule |
|---|---|
| T1 | Assert the **full** reverse-adjacency dictionary equals the exact expected map `{0:[5,6,8],1:[2,3],2:[3],3:[4],4:[7,8],5:[],6:[],7:[8],8:[]}` (order-insensitive but element-exact). Do **not** assert only "contains edge 1→2". |
| T2–T5 | Assert the returned suppressed set **equals** the exact int set (e.g. `{3,4,7,8}`), not "contains 4". Also assert the root id itself is **not** in the set. |
| T6 | Assert `== {3,4,7}` AND assert `8 ∉ set` (8 unselected) AND `2 ∉ set` (root). Three pins, or the ∩-selection revert passes vacuously. |
| T7 | Assert `set.Count == 0` AND separately assert Step 6 executes in the behavioral counterpart (T9), so "empty" isn't confused with "independent step wrongly skipped". |
| T9 | Pin **all four**: `step3.Executions==0`, `step4.Executions==0`, `step6.Executions==1`, `exit==1`. Do not assert only Step 3. On current code Step 3/4 are 0 only because of abort — so T9 is **RED-valid only in combination with the `step6.Executions==1` + `exit==1 with storage continuing` facts**; keep Step 6 and continuation asserts in the same test to avoid a vacuous green. |
| T10 | Positive control: assert `storageStep2/3/4.ExecutedNamespaces` contains `"storage"` with `Executions==1` each, AND assert compute's suppressed steps are 0 — proving continuation, not global no-op. |
| T11 | Assert storage Step 3/4 `Executions==1` (leak would drive them to 0). Do not assert only "no exception". |
| T13 | **Paired positive control (mandatory):** assert the **root** critical JSON `…-step-02-….json` **exists** (records are being written) AND `…-step-03-….json` / `…-step-04-….json` **do not exist**. An "absent" assertion alone is vacuous if recording is globally broken. |
| T14 | Pin every field literally: `schemaVersion=="1.0"`, `Version==4`, `status=="failure"`, `validationStatus=="skipped"`, `suppressed==true`. No set membership. |
| T15 | Pin `step8.Executions==1` AND `exit==0`. (Warn-fail must neither suppress Step 8 nor fail the catalog.) Assert Step 8 ran with the warn root present, not merely "no throw". |
| T17 | Pin `exit==1` exactly; add a control asserting a human-review-only run (override 2, no fatal) yields `exit==2`, so the `Worse` mutation is caught in both directions. |
| T18 | Pin `step4.Executions==0` under `SkipDependencyValidation:true` with Step 3 failing. Add control: with Step 3 **succeeding** under `--skip-deps`, `step4.Executions==1` (proves suppression is failure-driven, not a blanket skip). |
| T19 | Pin: the second namespace's steps have `Executions==0` AND the call surfaces cancellation (`OperationCanceledException`/canceled result) — not merely "fewer executions". |
| T20 | After two runs on the same `OutputPath`, assert the suppressed envelope's `BlockedByDependency.RootFailureId=="compute.02.root"` from the **second** run (single, overwritten file) — not two stale files. |
| T23 | Pin `rootFailureId=="compute.02.root"`, `rootStepId==2`, `rootStepName=="Generate example prompts"`, and `rootFailedNamespaces.Count==1`. |
| T27 | Pin `cascade==10` AND `Assert.NotEqual(9, cascade)` AND `Assert.NotEqual(3, mixed-as-cascade)` — proves the model reads `chainRoleCounts` not `classificationCounts` (L-004). |
| T28 | Pin unclassified/diagnostic `==1` exactly (beta34 `diagnostic:1`); not `>=1`. |
| T29 | Pin `logical==34` AND `physical==68` AND assert the six live buckets **sum** to the logical total (`Assert.Equal(34, sumOfBuckets)`) — reconciliation, not coincidental single-number match. |
| P1 | Assert the **entire ordered** invocation array equals the 3-element expected array (element-exact strings), not "contains monitor". Plus summary-failure + nonzero-exit asserts. |
| P2 | Assert `bbb`'s logged args are **exactly** `"bbb 1,2,3,4,5"` (no trailing skip flags). A `-notmatch '--skip-build'` alone is weaker — assert full-string equality. |
| P3/P4 | Regex must anchor to the **immediate** next line after the bash call (no intervening tokens) / must assert **absence** of `$LASTEXITCODE` in the failure branch. Pair each with a positive control asserting the captured local name **is** present, so a typo'd regex can't pass vacuously. |
| P5 | Assert the collision count is **exactly 0** after explicitly injecting the known names into the comparison set; do not assert "no error thrown". |

---

## 4. RED expectations (compile vs runtime) per test

Preference (charter): **runtime** failure on missing behavior over compile failure, EXCEPT where a genuinely new API member is required. The compile-RED tests below legitimately require the new `Suppressed` / `BlockedByDependency` envelope API or the new `internal` closure seams and therefore will not compile until GREEN — that is expected and acceptable for those specific tests.

| Test | RED mode | Exact expected RED failure |
|---|---|---|
| T1 | **Compile** | `PipelineRunner.BuildDependentsOf` (internal seam) does not exist → CS0117/CS0103. After it exists, dictionary-equality assertion is the guard. |
| T2–T7 | **Compile** | `SelectedTransitiveDependents` seam absent → CS0117/CS0103 until GREEN; then set-equality guards. |
| T8 | **Runtime** | Assertion that doubles' `(Id,Scope,FailurePolicy,DependsOn,MaxRetries)` equal `StepRegistry.CreateDefault` — fails only if Parker's mirror drifts; green once mirror is faithful (bridge test). |
| T9 | **Runtime** | On current abort code: `step6.Executions==1` FAILS (`Actual 0`) — Step 6 never runs because the catalog aborts after compute Step 2. This is the RED discriminator. |
| T10 | **Runtime** | `Assert.Equal(1, storageStep2.Executions)` fails (`Actual 0`) — storage never runs on current abort. |
| T11 | **Runtime** | `Assert.Equal(1, storageStep3.Executions)` fails (`Actual 0`). |
| T12 | **Compile** | `env.Suppressed` / `env.BlockedByDependency` members absent → CS1061 until GREEN; then value asserts. |
| T13 | **Runtime (mutation-proven)** | On current code, suppressed steps don't exist, so step-03/04 JSON is absent **vacuously** (abort). RED validity is established by the §3 mutation (revert suppression-skip → step-03/04 JSON appears). Positive control (root step-02 JSON present) fails RED only under the recording-broken mutation. |
| T14 | **Compile** | `env.Suppressed`/`env.Version` new usages → CS1061 until GREEN; then literal-field asserts. |
| T15 | **Runtime (mutation-proven)** | On current code a warn step failing does not abort, so Step 8 already runs — the guard's RED is proven by the §6 mutation (treat warn as suppressing/root) and by pinning `exit==0`. |
| T16 | **Runtime** (guard) | Passes on current code (global fatal already aborts). RED only under the §4 mutation (route global through suppression) → namespaces execute → `Assert.Equal(0, computeStep.Executions)` fails. Documented as guard. |
| T17 | **Runtime** | On current abort, only the first root is seen; `Assert.Equal(1, exit)` with two roots across namespaces fails because the second namespace never runs (`Actual` reflects single-namespace abort path). |
| T18 | **Runtime** | `Assert.Equal(0, step4.Executions)` under `--skip-deps` with Step 3 failing — on current code the whole run aborts at Step 3, so Step 4 is 0 vacuously; RED validity via the §5 mutation (gate suppression behind `SkipDependencyValidation`) → Step 4 executes. Pair with the success control. |
| T19 | **Runtime** | Second namespace `Executions==0` and cancellation surfaced — on current code cancellation handling may differ; assert exact canceled outcome. |
| T20 | **Compile** then **Runtime** | `BlockedByDependency` absent → compile-RED; post-GREEN, single overwritten file with second-run root id. |
| T21 | **Runtime** | `File.Exists(run-accounting.json)` → `false` on current code. |
| T22–T24, T26 | **Runtime** | `run-accounting.json` absent → deserialization/file-read fails; post-GREEN, bucket-content asserts. |
| T25 | **Compile** | Depends on `Suppressed`/`BlockedByDependency` to produce suppressed entries → compile-RED until GREEN; then bucket asserts. |
| T27–T29 | **Runtime** | `run-accounting.json` absent on current code; post-GREEN, chainRole/diagnostic/reconciliation asserts against frozen manifest. |
| P1 | **Runtime** | Current script emits `--skip-build --skip-npm-update` on `monitor`; updated ordered-array assert fails (`monitor 1,2,3,4,5` expected, `…--skip-build…` actual). |
| P2 | **Runtime** | Current script emits skip flags on `bbb`; full-string equality fails. |
| P3 | **Runtime (structural)** | Current source has cmdlets between the bash call and the `$LASTEXITCODE` read (or reads it later); anchored regex `Should -Match` fails. |
| P4 | **Runtime (structural)** | Current failure branch references `$LASTEXITCODE`; `Should -Not -Match '\$LASTEXITCODE'` in the failure decision fails / captured-local `Should -Match` fails. |
| P5 | **Runtime (structural)** | Passes now (no collisions yet); becomes a live guard once new locals are added. Its RED is proven by the §7 collision mutation. Documented as guard. |

---

## 5. Exact RED and GREEN commands

Working dir: `C:\my-squad-projects\worktrees\microsoft-mcp-doc-generation\813-step2-runtime-orchestration`. All C# in **Release** (CI treats warnings as errors).

**Per-project filtered (precise Step-2 evidence) — xUnit:**
```powershell
dotnet test .\mcp-tools\DocGeneration.PipelineRunner.Tests\DocGeneration.PipelineRunner.Tests.csproj -c Release `
  --filter "FullyQualifiedName~DependencySuppressionTests|FullyQualifiedName~RunAccountingTests" --nologo
```
Single-class drill-downs:
```powershell
dotnet test .\mcp-tools\DocGeneration.PipelineRunner.Tests\DocGeneration.PipelineRunner.Tests.csproj -c Release --filter "FullyQualifiedName~DependencySuppressionTests" --nologo
dotnet test .\mcp-tools\DocGeneration.PipelineRunner.Tests\DocGeneration.PipelineRunner.Tests.csproj -c Release --filter "FullyQualifiedName~RunAccountingTests" --nologo
```

**Per-file (precise Step-2 evidence) — Pester:**
```powershell
Invoke-Pester -Path ./mcp-tools/validation/tests/StartWithLogs.Tests.ps1 -Output Detailed -CI
```

**RED expectation:** the two `--filter` commands **fail to build** (compile-RED tests T1–T7, T12, T14, T20, T25) — this is the correct RED for the new-API/seam tests; runtime-RED tests report `[FAIL]` with the pinned-value assertion messages above once the file compiles (e.g. after Parker stubs just enough to compile, the behavioral tests must still be RED against current runtime). Pester RED: P1/P2 assertion diffs on the invocation array, P3/P4 regex non-matches.

**GREEN expectation:** after Parker's + implementers' GREEN, all commands exit `0`; every pinned value matches.

**Full-suite (run before AND after; used for the disjointness delta in §5.1):**
```powershell
dotnet test .\mcp-doc-generation.sln -c Release --nologo
Invoke-Pester -Path ./mcp-tools/validation/tests -Output Detailed -CI
```

### 5.1 Distinguishing Step-2 results from the known pre-existing unrelated failures (mandatory)
Two failure sets exist on `main` unrelated to #813. Parker must **prove disjointness**, never hide/weaken them:

1. **xUnit — `FamilyMetadataGeneratorTests` (different project).** The failing test is `DocGeneration.Steps.ToolFamilyCleanup.Tests.FamilyMetadataGeneratorTests.GenerateAsync_WhenAiResponseIsTruncated_UsesFallbackDescription` (verbatim assertion: `Assert.Contains() Failure: Sub-string not found` — the fallback description substring). Parker's Step-2 tests live in **`DocGeneration.PipelineRunner.Tests`**; the same-named class *inside* PipelineRunner.Tests passes **9/9**. Proof command (must be all-green, proving Parker's project has no FamilyMetadata failure):
   ```powershell
   dotnet test .\mcp-tools\DocGeneration.PipelineRunner.Tests\DocGeneration.PipelineRunner.Tests.csproj -c Release --filter "FullyQualifiedName~FamilyMetadataGeneratorTests" --nologo
   ```
   Confirm the known failure is confined to the other project (unchanged pre/post):
   ```powershell
   dotnet test .\mcp-tools\DocGeneration.Steps.ToolFamilyCleanup.Tests\DocGeneration.Steps.ToolFamilyCleanup.Tests.csproj -c Release --filter "FullyQualifiedName~FamilyMetadataGeneratorTests" --nologo
   ```

2. **Pester — `Scan-McpToolCoverage.Tests.ps1` (different file).** Eight `It` blocks fail there on `main`, independent of `StartWithLogs.Tests.ps1`. Parker adds nothing to that file. Proof:
   ```powershell
   Invoke-Pester -Path ./mcp-tools/validation/tests/Scan-McpToolCoverage.Tests.ps1 -Output Detailed -CI   # 8 fail, unchanged pre/post
   Invoke-Pester -Path ./mcp-tools/validation/tests/StartWithLogs.Tests.ps1 -Output Detailed -CI            # Step-2 file: 100% green at GREEN
   ```

3. **Delta proof:** capture full-suite results **before** any Step-2 work and **after** GREEN. The only differences must be (a) the new/updated Step-2 tests flipping to pass and (b) the unchanged known-failing set. Any *new* red outside the Step-2 identifiers = a regression Parker must fix, not mask. Record the two failing-set identifiers explicitly in the PR so reviewers can confirm the Step-2 set is disjoint.

---

## 6. Out-of-scope for Step 2 (Parker must NOT test these)

These belong to #813 items 3–10 / AD-029 §8 non-goals. No Step-2 test may assert them:
1. **Canonical parameter-identity model / coverage evaluator / Step 1 manifest schema** (item 3). No parameter-canonicalization or coverage-scoring tests.
2. **Step 4 `SourceVerificationHelpers` → manifest-binding rewrite** (item 4). No source-verification/manifest-binding tests.
3. **Typed Step 2 generation-failure result, secret redaction, stdout/stderr streaming** (item 5). No redaction or streaming assertions.
4. **Class A/B/C production bug fixes** (the beta34 known-defect classes) — Step 2 only guarantees *Class D* (no duplicate/critical side effects after suppression) via T13; do not test A/B/C remediation.
5. **Parity / nondeterminism / replay-stub / intended-diff allowlist** machinery beyond the existing replay guards already in `PipelineRunnerGlobalScopeTests`.
6. **Do not** edit or assert new expectations against the **frozen** beta34 fixtures (`beta34-baseline-manifest.json`, `BaselineContext`, `ChainAndAccountingTests`) — T27–T29 **read** them only.
7. **Do not** weaken, disable, or re-scope any existing validator, the `Scan-McpToolCoverage` tests, or the `FamilyMetadataGeneratorTests` in the other project.
8. **Do not** add tests that touch `generated/` or `generated-*` output, real Azure OpenAI calls, or the real `start.sh`/bash pipeline end-to-end (Pester uses the existing fake `start.sh`).

---

## 7. Roster summary (for the PR checklist)
- **`DependencySuppressionTests.cs`:** 20 tests (T1–T20) + 2 referenced existing guards (T18a/T18b).
- **`RunAccountingTests.cs`:** 9 tests (T21–T29).
- **`StartWithLogs.Tests.ps1`:** 5 new/updated `It` (P1 update + P2–P5) + 1 existing guard (P6).
- **Shared doubles** added to `Fixtures/TestDoubles.cs`: `RecordingNamespaceStep`, `MirroredRegistry.CreateDoublesMatchingDefault`.
- **Mutation matrix:** 26 rows across AD-029 §2/§3/§4/§5/§6/§7.

---

## ADDENDUM A — Six-category summary surfaces (coordinator gap closure)

**Author:** Cameron (Test Lead) · **Status:** Authoritative addendum · **Trigger:** coordinator review found the AD-029 §6 accounting model is emitted to the `run-accounting.json` **artifact** (T21–T29) but is **not** surfaced in either **human-visible summary**. **Strategy only — no test code.** Parker implements. Append-only; §0–§7 above are unchanged and still binding.

### A.0 The gap (verbatim requirement + current state)

Approved issue requirement (verbatim):
> The summary must separately report successful namespaces, root-failed namespaces, warning-only failures, suppressed steps, cascades imported from historical fixtures, and unclassified records.

Verified current state (read the files myself):
- `PipelineRunner.WriteRunAccounting` (`PipelineRunner.cs` ~L1629) already writes the six categories to `run-accounting.json` (`successfulNamespaces`, `rootFailedNamespaces`, `warningOnlyFailures`, `suppressedSteps`, `reconciliation.categoryCounts.cascadeImported`, `reconciliation.categoryCounts.unclassifiedDiagnostic`). **T21–T29 already cover this artifact — no change.**
- `PipelineRunner.CompleteRun` (`PipelineRunner.cs` ~L1216–L1234) prints only the legacy lines (`Pipeline completed with N warning(s)…` / `Pipeline stopped with N critical failure(s) recorded.`). **The six categories are NOT surfaced to the console.** ← gap.
- `start-with-logs.ps1` (~L463–L485) prints only `Generation Summary: X/Y succeeded, Z failed` + a failed-namespace list. **The six categories are NOT surfaced in the catalog summary.** ← gap. (Quinn correctly declined to ship this untested; these tests force it.)

### A.1 Surface decisions (which surfaces must report the six categories, and why)

| # | Surface | Decision | Justification |
|---|---|---|---|
| S1 | `run-accounting.json` artifact | **Already done — no new test.** | Machine-readable summary; T21–T29 pin all six fields + the frozen-baseline reconciliation. The two human surfaces below **read the same fields**; they never recompute the model. |
| S2 | **PipelineRunner console** summary (`CompleteRun`) | **Add tests T30–T31.** Six labeled lines emitted via `context.Reports.Info`, gated by the **same** `namespaceReports is not null` guard as `WriteRunAccounting` (pre-namespace global aborts have no namespace data → no six-category block; that path is unchanged). | This is the human-visible summary for a **single** pipeline invocation — i.e. every `start.sh <ns>` worker call **and** a direct all-namespaces run. Categories 1–4 come from the live `namespaceReports`; categories 5–6 come from `BuildBaselineReconciliation()` (the same value `WriteRunAccounting` already computes). |
| S3 | **`start-with-logs.ps1` catalog** console summary | **Add tests P7–P8.** A six-labeled catalog block that **aggregates** each namespace's `run-accounting.json`. | This is the human-visible summary for a **full catalog** run (N worker invocations). It must aggregate categories 1–4 across namespaces and surface catalog-constant categories 5–6 **once**. |

**Why both human surfaces (not just one):** "the summary" a human reads depends on entry point — the catalog orchestrator (`start-with-logs.ps1`) for a full run, the PipelineRunner console for a single-namespace/worker run. Closing only one leaves the requirement unmet for the other entry point.

### A.2 Script-surface fixture-supply mechanism (implementable with no live pipeline)

**Production reality I verified (so the contract is faithful, not invented):**
- `PipelineRequest.GetDefaultOutputPath` (`Cli/PipelineRequest.cs` L66–74) returns `.\generated-<namespace>-<timestamp>` for a namespaced run. So `start.sh <ns>` writes `run-accounting.json` to `generated-<ns>-<timestamp>/run-accounting.json`.
- `start-with-logs.ps1`'s **existing** move block (L417–L425) already globs `generated-<ns>-*` and relocates the whole directory to `$consolidatedDir/<ns>/`. Therefore `run-accounting.json` lands at `$consolidatedDir/<ns>/run-accounting.json` in a real run.

**Mechanism the P7/P8 tests use — PRE-STAGE, do NOT modify the fake `start.sh`:**
1. Before `Invoke-Generator`, the test writes one stub `run-accounting.json` per namespace into `<repository>/generated-<ns>-fixture/run-accounting.json` (suffix arbitrary; it only has to match the `generated-<ns>-*` glob). The stub uses the **exact field names** `WriteRunAccounting` emits (see A.5 stubs) so the script's `ConvertFrom-Json` reads the same shape.
2. The fake `start.sh` stays **byte-for-byte unchanged** (all namespaces exit 0 in these tests). The script's **real** move block relocates each `generated-<ns>-fixture` → `$consolidatedDir/<ns>/`, exactly as it does in production.
3. The new catalog summary reads each processed namespace's `$consolidatedDir/<ns>/run-accounting.json`, aggregates, and prints the six-labeled block before the final exit.

**Why pre-stage instead of having the fake write it:** it exercises the **real** relocation path (production code) and the new read/aggregate/print path with zero risk to the shared fake `start.sh` (so P1–P6 remain byte-identical), and it avoids Git-Bash Windows-path issues with `cp`/heredoc inside the fake. It is temporally indistinguishable from "start.sh produced it" from the move block's perspective (the file merely has to exist when the move runs). No live pipeline, no AI, no real `start.sh` pipeline, no reading of any real `generated-*/` run.

**Aggregation contract the script must satisfy (pinned by the tests):**
- Categories **1–4** (`successfulNamespaces`, `rootFailedNamespaces`, `warningOnlyFailures`, `suppressedSteps`): **concatenate across all** per-namespace files; the printed count is the total number of entries.
- Categories **5–6** (`cascadeImported`, `unclassifiedDiagnostic`): read from `reconciliation.categoryCounts` and report **once** — they are catalog-constant (a pure function of the frozen beta34 baseline, identical in every namespace's file). **Never summed.**

### A.3 Canonical six labels (shared by S2 and S3)

Both surfaces MUST use this fixed, service-agnostic label set (the six verbatim requirement phrases), each followed by ` (<count>)`:

| Cat | Canonical label (exact) | Count source |
|---|---|---|
| 1 | `Successful namespaces` | live — `successfulNamespaces.Count` |
| 2 | `Root-failed namespaces` | live — `rootFailedNamespaces.Count` |
| 3 | `Warning-only failures` | live — `warningOnlyFailures.Count` |
| 4 | `Suppressed steps` | live — `suppressedSteps.Count` |
| 5 | `Cascades imported from historical fixtures` | baseline — `reconciliation.categoryCounts.cascadeImported` |
| 6 | `Unclassified records` | baseline — `reconciliation.categoryCounts.unclassifiedDiagnostic` |

**All six labels ALWAYS print, even at count 0** (that is what "separately report" requires). The trailing `)` in the pinned `(N)` token disambiguates counts (`(1)` never matches `(10)`).

Recommended console rendering (implementer may choose punctuation; the **tokens** in A.4 are what the test pins):
```
Run accounting — six categories:
  Successful namespaces (1): storage
  Root-failed namespaces (1): compute [step 2 root=compute.02.root]
  Warning-only failures (1): compute [step 5]
  Suppressed steps (2): compute [step 3 root=compute.02.root], compute [step 4 root=compute.02.root]
  Cascades imported from historical fixtures (10)
  Unclassified records (1)
```

### A.4 New xUnit tests — PipelineRunner console (S2)

**File:** `mcp-tools/DocGeneration.PipelineRunner.Tests/Unit/RunAccountingTests.cs` — **EXTEND** (reuse its private `BuildRunner` → `(runner, reports)`, `MirroredRegistry.CreateDoublesMatchingDefault`, `Step(doubles,id)`, `Request(...)`; the `reports` channel is already returned and currently discarded — these two tests are its first consumers). No new file (avoids duplicating the private harness; conforms to §0.2 "reuse existing harness").

Real step facts these scenarios rely on (from `StepRegistry.CreateDefault` / mirrored doubles): reverse-adjacency `dependentsOf[2]` transitively = `{3,4,7,8}`; Step 5 = `SkillsRelevanceStep` **Warn**, `DependsOn [0]` (global) → **not** in Step 2's closure → still runs after a Step 2 root. Names: Step 2 = `Generate example prompts`, Step 5 = `Generate skills relevance`. `BuildBaselineReconciliation` resolves the **real** frozen manifest by walking up from `AppContext.BaseDirectory` (the test bin dir), so reconciliation is non-null in tests (cascade=10, diagnostic=1) — identical to T27/T28.

**T30 — `RunAccountingTests.RunAccounting_ConsoleSummary_ReportsAllSixCategories_WithLiveIdentifiersAndBaselineConstants`**
Scenario (one run, both metadata namespaces `compute`,`storage`; `Request(namespaceName:null, steps [1,2,3,4,5])`; if planning rejects `[1,2,3,4,5]` — it should not, Step 5 depends only on global Step 0, cf. T24's `[1,2,3,4,7]` — pass `skipDeps:true`, which per §5 does not affect suppression):
- `Step(doubles,2).Outcome = ns => ns=="compute" ? Failure() : Success();` (fatal root in compute → suppresses `{3,4}`).
- `Step(doubles,5).Outcome = ns => ns=="compute" ? Failure() : Success();` (Warn step warn-fails in compute → warning-only; not suppressed).
- Result: compute = root(2)+suppressed(3,4)+warningOnly(5); storage = fully successful.

Assertions on `reports.Messages` (use `Assert.Single(msgs, m => m.Contains(<token>))` — exactly-one, which also catches a duplicated/merged label; NOT `Assert.Contains` set-membership):

| Category | Pinned token (exact, incl. `)`) | Same-line identifier pins | Distinguishability (paired positive control) |
|---|---|---|---|
| 1 | `Successful namespaces (1)` | line `Contains "storage"` | line `DoesNotContain "compute"` (control: it **does** contain `storage`) |
| 2 | `Root-failed namespaces (1)` | line `Contains "compute"` **and** `Contains "compute.02.root"` | line `DoesNotContain "storage"` (control: it **does** contain `compute`) |
| 3 | `Warning-only failures (1)` | line `Contains "compute"` and `Contains "5"` | distinct label+count from cats 2 & 4 |
| 4 | `Suppressed steps (2)` | line `Contains "compute.02.root"` (steps 3 & 4) | count `(2)` distinct from the `(1)` categories |
| 5 | `Cascades imported from historical fixtures (10)` | — | **absent-pair:** `Assert.DoesNotContain(msgs, m => m.Contains("Cascades imported from historical fixtures (0)"))` (positive control = the `(10)` `Assert.Single` above) |
| 6 | `Unclassified records (1)` | — | **absent-pair:** `Assert.DoesNotContain(msgs, m => m.Contains("Unclassified records (0)"))` (positive control = the `(1)` `Assert.Single`) |

Plus positive control that the run reached `CompleteRun`: `Assert.Equal(1, exit)` (fatal root ⇒ exit 1).

**T31 — `RunAccountingTests.RunAccounting_ConsoleSummary_CleanCatalogRun_StillPrintsAllSixLabels_LiveCategoriesZero`**
Scenario: `Request(namespaceName:null, steps [1,2,3,4,5])`, **no** Outcome overrides → both namespaces fully succeed.
Assertions on `reports.Messages`:
- `Assert.Single(m => m.Contains("Successful namespaces (2)"))`; that line `Contains "compute"` **and** `Contains "storage"` (positive control: aggregation of two namespaces).
- **Labels-always-print (absent-value) trio**, each `Assert.Single`: `Root-failed namespaces (0)`, `Warning-only failures (0)`, `Suppressed steps (0)`.
- **Baseline constants still non-zero in a clean run** (this is the positive control that pairs with the `(0)` trio and proves the channel is live, not dead): `Assert.Single(m => m.Contains("Cascades imported from historical fixtures (10)"))`, `Assert.Single(m => m.Contains("Unclassified records (1)"))`.
- Negative control that the zeros are real (not a mislabeled non-zero): `Assert.DoesNotContain(m => m.Contains("Root-failed namespaces (1)"))`.
- Positive control the run completed: `Assert.Equal(0, exit)`.

### A.5 New Pester tests — `start-with-logs.ps1` catalog summary (S3)

**File:** `mcp-tools/validation/tests/StartWithLogs.Tests.ps1` — **EXTEND** with two `It` blocks in the existing `Describe "start-with-logs.ps1"`. Use the existing `New-TestRepository` / `New-MetadataVersion` / `Invoke-Generator`. **Do not modify the fake `start.sh`.** `Write-Host` output is captured in `$result.Output` (existing tests already assert `Generation Summary` this way).

Stub `run-accounting.json` shapes the test pre-stages (exact field names = `WriteRunAccounting` output):
```json
// generated-aaa-fixture/run-accounting.json  (successful)
{ "schemaVersion":"1.0","successfulNamespaces":["aaa"],"rootFailedNamespaces":[],"warningOnlyFailures":[],"suppressedSteps":[],
  "reconciliation":{"logicalRecordTotal":34,"physicalCopyTotal":68,
    "categoryCounts":{"successful":0,"rootFailed":23,"warningOnly":0,"suppressed":0,"cascadeImported":10,"unclassifiedDiagnostic":1}} }
```
```json
// generated-bbb-fixture/run-accounting.json  (root + 2 suppressed)
{ "schemaVersion":"1.0","successfulNamespaces":[],
  "rootFailedNamespaces":[{"namespace":"bbb","rootStepId":2,"rootStepName":"Generate example prompts","rootFailureId":"bbb.02.root","exitCode":1}],
  "warningOnlyFailures":[],
  "suppressedSteps":[{"namespace":"bbb","stepId":3,"rootFailureId":"bbb.02.root"},{"namespace":"bbb","stepId":4,"rootFailureId":"bbb.02.root"}],
  "reconciliation":{"logicalRecordTotal":34,"physicalCopyTotal":68,
    "categoryCounts":{"successful":0,"rootFailed":23,"warningOnly":0,"suppressed":0,"cascadeImported":10,"unclassifiedDiagnostic":1}} }
```
```json
// generated-ccc-fixture/run-accounting.json  (warning-only)
{ "schemaVersion":"1.0","successfulNamespaces":[],"rootFailedNamespaces":[],
  "warningOnlyFailures":[{"namespace":"ccc","stepId":7,"stepName":"Validate article health"}],"suppressedSteps":[],
  "reconciliation":{"logicalRecordTotal":34,"physicalCopyTotal":68,
    "categoryCounts":{"successful":0,"rootFailed":23,"warningOnly":0,"suppressed":0,"cascadeImported":10,"unclassifiedDiagnostic":1}} }
```

**P7 — `It "reports all six accounting categories in the catalog summary and never sums the baseline constants"`**
Setup: `New-MetadataVersion -Namespaces @("aaa","bbb","ccc")`; pre-stage the three stubs above (into `<repo>/generated-aaa-fixture/…`, `…-bbb-fixture/…`, `…-ccc-fixture/…`); `Invoke-Generator` (all exit 0). Aggregate expectation: successful **1** (aaa), root **1** (bbb), warning **1** (ccc), suppressed **2** (bbb 3,4), cascades **10** (once), unclassified **1** (once).
Assertions on `($result.Output -join "`n")` unless noted (each `(N)` token uses `\(N\)` so it cannot match a wider number):
- `Should -Match 'Successful namespaces \(1\)'`; the successful line `Should -Match 'aaa'` and `Should -Not -Match 'bbb'` (per-line: `$result.Output | Where-Object { $_ -match 'Successful namespaces' }`).
- `Should -Match 'Root-failed namespaces \(1\)'`; root line `Should -Match 'bbb'`, `Should -Match 'bbb\.02\.root'`, `Should -Not -Match 'aaa'`.
- `Should -Match 'Warning-only failures \(1\)'`; warning line `Should -Match 'ccc'`.
- `Should -Match 'Suppressed steps \(2\)'`; suppressed line `Should -Match 'bbb\.02\.root'`.
- **Anti-sum crux (constant-once), each an absent-assertion paired with its positive control:** `Should -Match 'Cascades imported from historical fixtures \(10\)'` **and** `Should -Not -Match 'Cascades imported from historical fixtures \(30\)'`; `Should -Match 'Unclassified records \(1\)'` **and** `Should -Not -Match 'Unclassified records \(3\)'`.

**P8 — `It "prints every accounting category label even when all namespaces succeed"`**
Setup: `New-MetadataVersion -Namespaces @("aaa","bbb")`; pre-stage **two successful** stubs (`successfulNamespaces:["aaa"]` and `["bbb"]`, all other live lists empty, reconciliation 10/1); `Invoke-Generator` (exit 0).
Assertions:
- `Should -Match 'Successful namespaces \(2\)'`; that line `Should -Match 'aaa'` **and** `Should -Match 'bbb'` (positive control: catalog union of two files).
- **Labels-always-print trio (absent-value):** `Should -Match 'Root-failed namespaces \(0\)'`, `Should -Match 'Warning-only failures \(0\)'`, `Should -Match 'Suppressed steps \(0\)'`.
- **Paired positive controls (channel is live):** `Should -Match 'Cascades imported from historical fixtures \(10\)'`, `Should -Match 'Unclassified records \(1\)'`.
- Negative control the zeros are genuine: `Should -Not -Match 'Root-failed namespaces \(1\)'`.

### A.6 Mutation / revert rows (continuing §2 — 26 → **34** total)

| # | AD-029 ref | Production change | Test RED on revert | Exact single-change mutation |
|---|---|---|---|---|
| M27 | §6 (console S2) | `CompleteRun` emits the six-category console block | T30, T31 | Delete the six-category emission in `CompleteRun`, leaving only the legacy `Pipeline completed…/stopped…` lines → every `Assert.Single(label)` finds 0. |
| M28 | §6 (console S2) | Suppressed steps printed under their own label with the **live** count | T30 | Drop the `Suppressed steps` line (or fold suppressed into the successful line) → `Suppressed steps (2)` absent. |
| M29 | §6 (console S2) | Warning-only printed as a **separate** line, distinct from root | T30 | Emit the warning-only step under the `Root-failed namespaces` line → `Warning-only failures (1)` absent and the root line's identifier/count wrong. |
| M30 | §6 (console S2) | Cats 5/6 sourced from reconciliation; all six always print incl. 0 | T30, T31 | Hardcode the cascades/unclassified lines to `(0)` or omit zero-count lines → T30 loses `(10)`/`(1)`; T31 loses the `(0)` trio. |
| M31 | §7 (script S3) | `start-with-logs.ps1` prints the six-category catalog block | P7, P8 | Remove the six-category block → labels absent from `$result.Output`. |
| M32 | §7 (script S3) | Cats 5/6 taken **once** (catalog-constant), never summed | P7 | Sum `cascadeImported`/`unclassifiedDiagnostic` across the N files → prints `(30)`/`(3)`; `\(10\)`/`\(1\)` no longer match. |
| M33 | §7 (script S3) | Cats 1–4 aggregated across **all** namespaces | P7 | Read only one namespace's file (e.g. `$namespaces[-1]`) instead of aggregating → counts collapse (e.g. successful `(0)`, root `(0)` or warning `(1)`-only). |
| M34 | §7 (script S3) | All six labels always print, even at 0 | P8 | Skip categories whose count is 0 → `Root-failed namespaces (0)` / `Warning-only failures (0)` / `Suppressed steps (0)` absent. |

**New matrix size: 34 mutation rows** (26 prior + 8 here: §6 console ×4, §7 script ×4).

### A.7 RED expectations (all four are **runtime-RED**, no new API)

Unlike the compile-RED envelope tests (T12/T14/T25), T30/T31/P7/P8 need **no** new API — `context.Reports`, `namespaceReports`, `run-accounting.json`, `BuildBaselineReconciliation`, and the script move block all already exist. So all four **compile/parse clean against current code and fail at runtime** on the missing surface (charter preference: runtime-RED over compile-RED).

| Test | RED mode | Exact expected RED failure |
|---|---|---|
| T30 | Runtime | `Assert.Single() Failure … the collection contained 0 matching elements` for `m => m.Contains("Successful namespaces (1)")` — current `CompleteRun` prints only legacy lines. |
| T31 | Runtime | Same, for `m => m.Contains("Root-failed namespaces (0)")`. |
| P7 | Runtime | `Expected regex 'Successful namespaces \(1\)' to match …, but it did not` — current script has no six-category block. |
| P8 | Runtime | Same, for `Root-failed namespaces \(0\)`. |

### A.8 Exact RED/GREEN commands (Release, filtered)

xUnit console tests (isolated by method-name substring so T21–T29 are excluded):
```powershell
dotnet test .\mcp-tools\DocGeneration.PipelineRunner.Tests\DocGeneration.PipelineRunner.Tests.csproj -c Release `
  --filter "FullyQualifiedName~RunAccounting_ConsoleSummary" --nologo
```
Pester script tests (whole file; P7/P8 are the new red until GREEN):
```powershell
Invoke-Pester -Path ./mcp-tools/validation/tests/StartWithLogs.Tests.ps1 -Output Detailed -CI
```
**RED:** the xUnit command reports the two `Assert.Single` failures above; Pester reports P7/P8 `Should -Match` failures (P1–P6 stay green). **GREEN:** after Morgan adds the `CompleteRun` block and Quinn adds the `start-with-logs.ps1` block, all exit 0 and every pinned `(N)` token matches. Disjointness from the known-unrelated failures is unchanged — §5.1 applies verbatim (these live in `DocGeneration.PipelineRunner.Tests` and `StartWithLogs.Tests.ps1`; `FamilyMetadataGeneratorTests` and `Scan-McpToolCoverage.Tests.ps1` are untouched).

### A.9 Out-of-scope for this gap closure (must NOT be added)

1. **No new pipeline phase/step/gate.** The console block lives inside the existing `CompleteRun` (behind the existing `namespaceReports is not null` guard); the script block lives in the existing post-loop summary section.
2. **No service-specific logic.** Labels and aggregation are service-agnostic; scenarios use generic `compute`/`storage` and `aaa`/`bbb`/`ccc`. No hardcoded real service names.
3. **No weakened/downgraded validation.** Purely additive reporting — no validator, gate, exit-code mapping, or existing test is changed. Keep the legacy `CompleteRun` lines and the `Generation Summary:` line (both stay; P1/P6 remain green).
4. **No live AI / no real pipeline / no real `start.sh` pipeline.** Console tests use the mirrored doubles; script tests pre-stage stub `run-accounting.json` and keep the fake `start.sh` byte-identical.
5. **No reading of real `generated-*/` output as test input.** The script tests read only the **pre-staged stub** relocated by the real move block; they never consume a real run. The only frozen-fixture read is the **read-only** reconciliation the product itself performs (as in T27–T29) — do **not** edit the beta34 fixtures.
6. **No redefinition of the accounting model.** Category 6 surfaces the artifact's existing `unclassifiedDiagnostic` value; do **not** expand it here to disk-scan orphan critical JSON (that clause of §6 is a separate feature, not part of surfacing).
7. **No new `param()` / no AD-027 regression.** Any new `start-with-logs.ps1` variables are **locals** (auto-enumerated by P5's AST scan, which continues to guard them); they must not fold case-insensitively onto existing names. Do not add a script parameter.
8. **No change to `run-accounting.json` schema** or to T21–T29 — the two human surfaces **read** the same fields the artifact already emits.

**Addendum summary:** +4 tests (**T30, T31** in `RunAccountingTests.cs`; **P7, P8** in `StartWithLogs.Tests.ps1`) · +8 mutation rows (**M27–M34**, new total **34**) · surfaces forced: PipelineRunner **console** (`CompleteRun`) + `start-with-logs.ps1` **catalog** summary, both reading the existing `run-accounting.json`/reconciliation fields.

---

## ADDENDUM B — post-evaluation gap closure (corpus-grounded root detection, failing-dependent harness, full-graph traversal, canonical envelope)

**Trigger:** Ellis's **VERDICT: FAIL** on the #813 Step-2 build (`.squad/agents/ellis/813-step2-evaluation.md`) and Riley's `## AMENDMENT 1 (post-evaluation)` in `.squad/decisions/inbox/riley-ad-029-runtime-dependency-suppression.md`. Ellis's BLOCKING-2 is a **direct indictment of this matrix**: every suppression/accounting test in §1–§4 + Addendum A injects a Step-2 root as `Success=false` (via `Failure()` / `HumanReview()`), a shape the runtime *already* suppresses. **None** injects the real corpus shape — `FailurePolicy.Fatal` + `Success=true` + non-empty `ArtifactFailures` + a failed `ValidatorResult` — so the GREEN suite and the 34/34 mutation proof demonstrate the *synthetic mechanism*, not the *real corpus behavior*, and structurally **cannot** detect BLOCKING-1 (0/10 real cascades suppressed). Parker's self-reported **F1** (non-discriminating T13) has the same root cause: the dependent doubles never *fail*, so "suppressed steps emit no critical JSON" cannot distinguish "suppressed" from "executed-and-succeeded." Both findings are correct. ADDENDUM B closes the gap.

**Scope of this addendum:** strategy only. **No test code, no `.cs`/`.ps1` edits** here — this appends to this one markdown file only. §0–§7 and Addendum A are unchanged and remain in force. All numbering continues: xUnit **T32–T40**, Pester **none** (P-series unchanged), mutations **M35–M42** (new total **42**). Line references below were re-verified against HEAD `1da15b5`.

### B.0 — Inputs pinned (verified against production at HEAD, not paraphrased)

**The four defects Ellis + Riley require this matrix to guard (contracts from AMENDMENT 1 §A2–A5):**

- **D1 — real-shape root detection (A2).** The Step-2 corpus mode is **not** `Success=false`. `ExamplePromptsStep.cs:141` returns `StepResult` with `Success=true`, a **failed** `ValidatorResult` (`example-prompt-validation`, `Success=false`, added at `:137–138`), **and** ≥1 `ArtifactFailure`. `NamespaceStepBase.cs:111` passes `Success` through untouched, so the runtime sees `Success=true`. The current root predicate keys only on exit code (`PipelineRunner.cs:256` region), so this shape exits `0` and is **never** a root. AMENDMENT 1's predicate:
  `IsFatalRoot := step.FailurePolicy == Fatal AND ( mappedExitCode != SuccessExitCode  [C1]  OR  result.ArtifactFailures.Count > 0  [C2] )`.
  **C2 keys on `ArtifactFailures`, NOT on a failed `ValidatorResult`** — because the pre-AI gate returns `Success=true` + a failed `pre-ai-validation` validator + **empty** `ArtifactFailures` and must stay non-fatal (`PreAiValidationGateTests` :49/:79/:116). Root exit recompute (load-bearing): `rootExit = exit != 0 ? exit : MapStepFailureExitCode(Fatal, false, override)` so a C2-only root still exits nonzero. Warn steps are never roots (policy guard short-circuits).
- **D2 — failing-dependent harness (A5).** The doubles must model (a) the real Step-2 root shape and (b) a **failing dependent** that *would* emit critical JSON if executed, so "no critical JSON from suppressed steps" becomes **discriminating** (retires F1).
- **D3 — full-graph traversal through unselected intermediates (A3).** `SelectedTransitiveDependents` (`PipelineRunner.cs:1526–1556`) currently intersects with `selectedIds` **at enqueue** (`:1545`), so the walk stops at an unselected node. With selected `{2,4}` and unselected `3`, the real edge chain `2 → 3 → 4` means a fatal Step 2 MUST suppress Step 4; the buggy walk returns `{}`. Fix: traverse the **full** reverse graph with its own visited set, intersect `∩ selectedIds` **only at collection**. Signature unchanged.
- **D4 — canonical envelope + stale overwrite (A4).** `WriteSuppressedEnvelope` (`:1586–1620`) writes the suppressed envelope **only** to `GetObservabilityDirectory` (`:1617`). The authoritative read path — `StepResultReader.TryRead`, `UpstreamArtifactResolver.TryReadUpstream` (`UpstreamArtifactResolver.cs:21`, dir `step-{stepId}-{stepSlug}`), and replay (`PipelineRunner.cs:1356`) — reads the **canonical** step workspace `{outputPath}/{GetStepIdentifierSlug(step)}/step-result.json` (`GetStepWorkspaceDirectory` :1143, `GetStepIdentifierSlug` :1140 `internal static`). The envelope must be written to **both** canonical and observability, must **overwrite a stale success envelope** on a same-workspace rerun, and must carry `OutputFileCount=0` with no `OutputArtifacts`.

**Envelope field names pinned** (`shared/DocGeneration.Core.Shared/StepResultFile.cs`, for T38): `Version` (:114, current `4`), `SchemaVersion` (:166, `"1.0"`), `Status` (:118, `StepResultStatus.Failure`), `ValidationStatus` (:190, `ValidationStatus.Skipped`), `OutputFileCount` (:130, `0`), `OutputArtifacts` (:184, null/empty), `Suppressed` (:221, `true`), `BlockedByDependency` (:228 → `Namespace`, `FailedRootStepId`, `FailedRootStepName`, `RootFailureId`).

**Corpus numbers pinned** (Ellis-rederived; must equal `mcp-tools/DocGeneration.Baseline.Beta34.Tests/Fixtures/beta34-baseline-manifest.json` accounting + records — read-only): **17** Step-2 critical-failure records; **16** in the `Success=true` validation-after-retries mode; **1** in the `Success=false` generation-failed mode (`monitor.02.webtests-get.01`); **10** cascades (`chainRole=="cascade"`); **16** upstream dependency links (every one `stepId==02`). Cross-checks: 34 logical / 68 physical; 17 Step-4 records (10 cascade + 7 Step-4 roots); 24 roots total (17 Step-2 + 7 Step-4); `chainRoleCounts` root 24 / cascade 10. 16-link namespace split (for the linkage assertion): appconfig 1, azurebackup 1, azureterraform 2, datadog 1, foundryextensions 2, group 1, search 1, sreagent 3, storage 2, storagesync 2 = **16**.

**Seam visibility pinned (testability — verified):** `SuccessExitCode=0`, `FatalExitCode=1`, `HumanReviewExitCode=2`, and `MapStepFailureExitCode(FailurePolicy, bool, int?)` are `public static` (test-reachable). `BuildDependentsOf` (:1503) and `SelectedTransitiveDependents` (:1526) are `internal static` (reachable via existing `InternalsVisibleTo`). `GetStepIdentifierSlug` (:1140) is `internal static` (reachable — T38 uses it to compute the canonical dir; **do not hand-roll a slug**). `StepExecutionOutcome` is `private` (:1885) and **cannot** be constructed by tests.

> **§0.4 testability-seam mandate (restated, binding on the implementer).** Because `StepExecutionOutcome` is private, the C1∪C2 predicate MUST be extracted as an `internal static bool PipelineRunner.IsFatalRoot(FailurePolicy policy, int mappedExitCode, IReadOnlyList<ArtifactFailure> artifactFailures)` reachable from `DocGeneration.PipelineRunner.Tests`. This is the same seam-extraction rule §0.4 already applies. If the predicate is left buried inline inside the private outcome-handling block, **Parker escalates to Riley — Parker does NOT hand-roll a duplicate predicate in the test** (a duplicate would pass against the bug and re-introduce BLOCKING-2). T32 and T33 depend on this seam and are **compile-RED** until it exists.

### B.1 — Harness upgrades required in `Fixtures/TestDoubles.cs` (Deliverable 1)

All additions are **purely additive**. Do **not** modify or fork `RecordingNamespaceStep`, `MirroredRegistry.CreateDoublesMatchingDefault`, or the existing `Success()/Failure()/HumanReview()` factories (`DependencySuppressionTests.cs:495–506) — the T8 execution-mirror invariant and every existing consumer (T2–T31, Addendum A) must keep compiling and passing unchanged. The new outcomes are injected through the **existing** `RecordingNamespaceStep.Outcome` delegate (`Func<string, StepResult>` keyed by namespace); **no new double type** is introduced.

Add one new `internal static class StepOutcomes` to `Fixtures/TestDoubles.cs` with exactly these factory methods (names are binding — tests reference them verbatim):

1. **`StepOutcomes.ValidationAfterRetriesFailure(string toolCommand = "storage account create")`** → returns a `StepResult` with **`Success = true`**, exactly **one** `ArtifactFailure` (non-empty; use `ArtifactFailure.Create(...)` with an `example-prompts` artifact type and a per-call `toolCommand`-derived name), and **one failed `ValidatorResult`** (`validatorId: "example-prompt-validation"`, `Success = false`). This is the **real Step-2 corpus mode** and mirrors `ExamplePromptsStep.cs:137–138,141`. Callers vary `toolCommand` across Azure services (Storage, Key Vault, Cosmos DB, Speech, Monitor) per the Universal Design Principle — the value is cosmetic to the predicate but keeps the suite service-agnostic.
2. **`StepOutcomes.FailingDependentWithArtifacts(string artifactName = "storage")`** → returns a `StepResult` with **`Success = false`** and ≥1 `ArtifactFailure`. If this outcome is ever **executed**, `CriticalFailureRecorder.Persist` (:30, writes when `ArtifactFailures.Count > 0`) emits a `*-step-{id}-*.json`. This is the **failing dependent** (D2) that makes "no critical JSON" discriminating. Used as the Step-4 outcome so a suppressed Step 4 leaves **zero** step-04 JSON while an executed Step 4 leaves ≥1.
3. **`StepOutcomes.PreAiSkipNonFatalOutcome(string validatorId = "pre-ai-validation")`** → returns a `StepResult` with **`Success = true`**, **EMPTY `ArtifactFailures`**, and one failed `ValidatorResult` (`Success = false`). This is the pre-AI-gate shape that must **never** become a root (the C2 discriminator: it has a failed validator but no artifact failure). Used by T40 and by the M36 mutation guard.

**Corpus reader helper (T33), additive, read-only.** Add an `internal static class Beta34Corpus` to the PipelineRunner test project (or reuse the existing baseline-locate helper the `DocGeneration.Baseline.Beta34.Tests` fixtures already expose) that walks up to the `mcp-doc-generation.sln` root (same `FindRepoRoot` pattern T27–T29 use) and returns the parsed manifest records + the per-record `validatorResults` from `critical-failures/*.json`. **No project reference to the baseline test project; no fixture edit; open every file read-only.** If the baseline project already exposes a public locate/parse seam, reuse it rather than duplicating.

### B.2 — New tests T32–T40 (Deliverables 2, 3, 5)

All in `mcp-tools/DocGeneration.PipelineRunner.Tests/Unit/DependencySuppressionTests.cs` unless noted. `[C]` = compile-RED (seam absent until A2), `[R]` = runtime-RED (compiles now, fails against current behavior). Every test names varied Azure services in its data.

#### T32 — `DependencySuppressionTests.IsFatalRoot_TruthTable_FiresOnFatalArtifactFailuresEvenWhenSuccessTrue`  `[C]`
Pure-function truth table over the new `IsFatalRoot(policy, mappedExitCode, artifactFailures)` seam. Rows (each an exact `Assert.True/False`, ArtifactFailure names drawn from **different** services):
| policy | mappedExitCode | artifactFailures | expected | pins |
|---|---|---|---|---|
| Fatal | `SuccessExitCode` | 1 (Storage) | **true** | C2 — the D1 real shape |
| Fatal | `FatalExitCode` | 0 | **true** | C1 — Success=false |
| Fatal | `HumanReviewExitCode` | 0 | **true** | C1 — override exit 2 |
| Fatal | `SuccessExitCode` | 0 | **false** | clean success / pre-AI skip (empty AF) |
| Warn | `SuccessExitCode` | 1 (Key Vault) | **false** | policy guard — warn never root |
| Warn | `FatalExitCode` | 1 (Cosmos DB) | **false** | policy guard |
**Anti-vacuity:** true-rows and false-rows asserted in the **same** test (a predicate hardwired `true` fails the false-rows; hardwired `false` fails the true-rows). Exact booleans — no "truthy." The `Fatal/Success/1AF → true` row paired with `Fatal/Success/0AF → false` pins "keys on ArtifactFailures **presence**, not on policy alone." The two Warn rows pin the policy guard independent of exit and AF. **RED:** compile-RED until the `IsFatalRoot` seam exists (A2), then truth-table-RED against any predicate missing C2 or the policy guard.

#### T33 — `DependencySuppressionTests.RootPredicate_ClassifiesAllSeventeenBeta34Step2Records_AsRoots_SixteenViaSuccessTrueArtifactFailures`  `[C]` — **the BLOCKING-1 catcher**
Reads the **frozen** beta.34 baseline read-only (via `Beta34Corpus`). Procedure and pinned counts:
1. `records` where `stepId == 2` → **`Assert.Equal(17, step2.Count)`**; cross-check `accounting.step2Records == 17`.
2. Per Step-2 record, read its `sanitizedRelativePath` JSON's `validatorResults`. Derive `reconstructedSuccess = validatorResults.Any()` (validation-after-retries path adds validators → `Success=true`; generation-failure path returns before validation → `[]` → `Success=false` — mirrors `ExamplePromptsStep`). Assert **`Assert.Equal(16, step2.Count(r => r.HasValidatorResults))`** and **`Assert.Equal(1, step2.Count(r => !r.HasValidatorResults))`**, and that the single empty-validator record is **`monitor.02.webtests-get.01`** (pin the identity, not just the count).
3. Per record: `mappedExit = PipelineRunner.MapStepFailureExitCode(FailurePolicy.Fatal, reconstructedSuccess, null)` (16 → `SuccessExitCode`; 1 → `FatalExitCode`). Reconstruct `artifactFailures` = one `ArtifactFailure` from the record. `isRoot = PipelineRunner.IsFatalRoot(FailurePolicy.Fatal, mappedExit, artifactFailures)`.
4. **`Assert.Equal(17, step2.Count(isRoot))`** ← the crux; under the exit-only predicate this is **1**.
5. **`Assert.Equal(16, step2.Count(r => r.mappedExit == SuccessExitCode && isRoot(r)))`** ← the 16 `Success=true` cascade-upstream roots the OLD predicate misses (the anti-BLOCKING-1 pin).
6. **Negative control (documents the gap):** **`Assert.Equal(1, step2.Count(r => r.mappedExit != SuccessExitCode))`** — an exit-only rule would find only this 1; fails loudly if the mode split drifts.
7. **Cascade linkage:** `records.Count(chainRole=="cascade")` → **`Assert.Equal(10, …)`** (cross-check `chainRoleCounts.cascade == 10`); gather all `upstreamStableIds` → **`Assert.Equal(16, links.Count)`** (cross-check `dependencyLinks == 16`); assert **every** upstream stableId has `stepId == 02` **and** resolves to one of the 17 predicate-classified roots (enumerate + count `16`, never set-membership). Optional cross-checks: Step-4 records `== 17`, Step-4 roots `== 7`, total roots `== 24`.
**Anti-vacuity:** every count is `==` (never `>=`); the 16-with-`mappedExit==0`-and-root count is the positive control that the predicate fires on the `Success=true` mode; the exit-only `== 1` is the paired negative control; identity pin on `monitor.02.webtests-get.01`; linkage enumerated then counted. **RED:** compile-RED until `IsFatalRoot` exists; then, against the buggy exit-only predicate, step 4 yields `1 != 17` and step 5 yields `0 != 16` → RED. This is the single test that would have caught BLOCKING-1.

#### T34 — `DependencySuppressionTests.RunAsync_RealShapeFatalStep2_SuccessTrueWithArtifactFailures_RecordsRoot_SuppressesTransitiveDependents`  `[R]`
Mirrored doubles (all 9), two namespaces `compute` then `storage`; `Request` selects steps `{1,2,3,4,6,7,8}`. `compute` Step 2 `Outcome = StepOutcomes.ValidationAfterRetriesFailure("compute vm list")`; `storage` Step 2 succeeds. closure(2) ∩ selected = `{3,4,7,8}`; Step 6 (depends on global bootstrap only) is independent.
**Assertions (all pinned):** `Step(3).Executions == 0`, `Step(4).Executions == 0`, `Step(7).Executions == 0`, `Step(8).Executions == 0` (suppressed); `Step(6).Executions == 1` (independent runs); **every** storage step `Executions == 1` (later namespace fully runs — continuation control, proves suppression is scoped, not a global abort); `exitCode == FatalExitCode`; the suppressed step-3/step-4 envelopes carry `BlockedByDependency.RootFailureId == "compute.02.root"`; `rootFailedNamespaces`/accounting lists `compute` as a root namespace.
**Anti-vacuity:** the four `Executions == 0` **plus** `Step(6) == 1` **plus** storage-continuation **plus** `exit == 1` asserted together — the Step-6 and storage facts make it impossible to pass vacuously via a global abort. Positive control: the compute step-02 critical JSON **exists** (recording works). **RED:** runtime-RED — on current code the real shape exits `0`, so Steps 3/4/7/8 **execute** (`Executions == 1`) and `exit == 0`; both fail. (This scenario was *vacuously green* in the rejected build because it used synthetic `Failure()`.)

#### T35 — `DependencySuppressionTests.RunAsync_SuppressedFailingDependent_EmitsNoCriticalJson_ButRealShapeRootDoes`  `[R]` — **retires F1**
Mirrored doubles, two namespaces; select `{1,2,3,4}`. `compute` Step 2 = `ValidationAfterRetriesFailure("compute disk create")` (real root); `compute` Step 4 = `StepOutcomes.FailingDependentWithArtifacts("compute")` (would persist a step-04 JSON **if executed**); `storage` succeeds. closure(2) ∩ selected = `{3,4}`.
**Assertions:** **Positive control** — count of `critical-failures/*-step-02-*.json` `== 1` (the real-shape root persisted exactly its one ArtifactFailure); **discriminators** — count of `*-step-03-*.json` `== 0` and `*-step-04-*.json` `== 0`; `Step(3).Executions == 0`, `Step(4).Executions == 0`; `exit == FatalExitCode`.
**Why this retires F1:** because the Step-4 double *fails with artifacts*, "step-04 JSON count == 0" now genuinely distinguishes **suppressed** (0 files) from **executed** (≥1 file). Under the old T13 the dependent succeeded (no AF), so 0 files appeared either way — vacuous. **Anti-vacuity:** the step-02 `== 1` positive control guards against "recording globally broken"; the step-03/step-04 `== 0` are the paired discriminators; all exact counts. **RED:** runtime-RED — current code doesn't root the real Step-2 shape → Step 4 executes `FailingDependentWithArtifacts` → a `*-step-04-*.json` appears → `== 0` fails.

#### T36 — `DependencySuppressionTests.SelectedTransitiveDependents_Selection_2_4_TraversesThroughUnselected3_Returns_4`  `[R]` — **D3 unit**
Build the default step graph: `deps = PipelineRunner.BuildDependentsOf(StepRegistry.CreateDefault(scriptsRoot).GetAllSteps())`; call `PipelineRunner.SelectedTransitiveDependents(rootId: 2, selectedIds: {2,4}, deps)`.
**Assertions:** result set **`== {4}`** exactly; explicitly `!result.Contains(2)`, `!result.Contains(3)` (unselected intermediate, correctly excluded from the *collected* set but traversed through), `!result.Contains(7)`, `!result.Contains(8)` (unselected). **Anti-vacuity:** exact set equality to `{4}` plus the four explicit exclusions — a walk that stops at unselected `3` returns `{}` and fails `== {4}`; a walk that forgets to intersect returns `{3,4,7,8}` and fails the exclusions. **RED:** runtime-RED — current intersect-at-enqueue returns `{}` (stops at unselected `3`) → `== {4}` fails.

#### T37 — `DependencySuppressionTests.RunAsync_SkipDeps_Selected_2_4_FatalStep2_SuppressesStep4_ThroughUnselectedIntermediate`  `[R]` — **D3 integration (`--skip-deps`)**
Mirrored doubles; `Request` selects `{2,4}` with `SkipDependencyValidation = true`; `compute` Step 2 = `ValidationAfterRetriesFailure("cosmos database create")` (real root).
**Assertions:** `Step(4).Executions == 0` (suppressed through unselected `3`); `exit == FatalExitCode`; step-4 envelope `BlockedByDependency.RootFailureId == "compute.02.root"`. **Control (paired, same test or sibling):** with `compute` Step 2 **succeeding** under the identical `{2,4}` + skip-deps selection, `Step(4).Executions == 1` — proves suppression is failure-driven, not a blanket skip-deps side effect. **Anti-vacuity:** the failing-run `Executions == 0` paired with the success-run `Executions == 1` control; exact exit; envelope root id pinned. **RED:** runtime-RED — current traversal returns `{}` for `{2,4}` → Step 4 runs → `Executions == 0` fails (also gated on A2 making Step 2 a root).

#### T38 — `DependencySuppressionTests.RunAsync_SuppressedEnvelope_WrittenToCanonicalWorkspace_OverwritesStaleSuccess_OnSameWorkspaceRerun`  `[R]` — **D4**
Single `outputPath`, reused across two runs (mirrored doubles). **Run 1:** select `{3,4}` with `--skip-deps`, all succeed → Step 4 executes and writes its **canonical success** envelope. **Run 2:** SAME `outputPath`, select `{2,4}` with `--skip-deps`, `compute` Step 2 = `ValidationAfterRetriesFailure("speech transcription create")` → Step 4 suppressed. Canonical dir = `Path.Combine(outputPath, PipelineRunner.GetStepIdentifierSlug(step4))` (use the seam — do not hand-roll the slug).
**Assertions:** after Run 1, `StepResultReader.TryRead(canonicalStep4Dir, out env1) == true` and `env1.Suppressed != true`, `env1.Status == StepResultStatus.Success` (the stale success that must be overwritten). After Run 2, `StepResultReader.TryRead(canonicalStep4Dir, out env2) == true` and: `env2.Suppressed == true`; `env2.Status == StepResultStatus.Failure`; `env2.ValidationStatus == ValidationStatus.Skipped`; `env2.Version == 4`; `env2.SchemaVersion == "1.0"`; `env2.OutputFileCount == 0`; `env2.OutputArtifacts` is null/empty; `env2.BlockedByDependency.RootFailureId == "compute.02.root"`. Assert the canonical dir holds exactly **one** `step-result.json` (overwritten in place, not duplicated). **Positive control:** the observability copy also reads `Suppressed == true` (A4 writes both).
**Anti-vacuity:** the Run-1 non-suppressed `env1` is the positive control proving the canonical location genuinely held a **success** before Run 2, so "now suppressed" is a real overwrite, not a first write; every envelope field pinned literally; single-file assertion guards against write-beside-stale. **RED:** runtime-RED — current `WriteSuppressedEnvelope` writes only observability, so the canonical read returns Run-1's stale success → `env2.Suppressed == true` fails (and, pre-A2/A3, Step 4 wouldn't even suppress).

#### T39 — `DependencySuppressionTests.RunAsync_WarnStep_SuccessTrueWithArtifactFailures_IsNotRoot_DoesNotSuppress`  `[R]` — **negative control (over-firing on policy)**
Mirrored doubles; select `{1,6,7,8}`; drive a **Warn**-policy step (e.g., Step 5 `SkillsRelevance`, or Step 7/8) with an outcome that is `Success=true` + non-empty `ArtifactFailures` (reuse `ValidationAfterRetriesFailure` shape on a Warn step). **Assertions:** no namespace is recorded as a root; `exit != FatalExitCode` (warn artifact failures don't force fatal); the warn step's dependents (if any selected) all `Executions == 1` (nothing suppressed); the warn step's own `Executions == 1`. **Anti-vacuity:** paired positive/negative — the warn step DID run and DID record its artifact failure (positive), yet produced NO root and NO suppression (negative). **RED:** guard — GREEN on the correct A2 (policy guard), RED only under mutation **M38** (drop the Fatal policy guard). Prevents the predicate from over-firing on Warn steps.

#### T40 — `DependencySuppressionTests.RunAsync_FatalStep_SuccessTrue_FailedValidator_EmptyArtifactFailures_IsNotRoot`  `[R]` — **negative control (C2 discriminator / pre-AI gate)**
Mirrored doubles; select `{1,2,3,4}`; `compute` Step 2 = `StepOutcomes.PreAiSkipNonFatalOutcome()` (`Success=true`, **empty** `ArtifactFailures`, failed `pre-ai-validation` validator). **Assertions:** `compute` is **not** a root; `Step(3).Executions == 1`, `Step(4).Executions == 1` (nothing suppressed); `exit != FatalExitCode`; **no** `*-step-02-*.json` critical file (empty AF → nothing persisted — positive control that "no artifact failure" is the real condition, not a recording glitch). **Anti-vacuity:** dependents-run (`== 1`) paired with no-root/no-critical-file; distinguishes C2 (`ArtifactFailures`) from "any failed validator." **RED:** guard — GREEN on correct A2 (C2 keys on ArtifactFailures), RED under mutation **M36** (C2 rekeyed to `ValidatorResults.Any(!Success)`), which would wrongly root this pre-AI shape and suppress Steps 3/4.

**Pester (P-series):** **none required.** D1–D4 live entirely in the typed runtime (`PipelineRunner`, `TestDoubles`, `StepResultFile`); `start-with-logs.ps1` and the catalog summary (P1–P8) are untouched by AMENDMENT 1. Adding a Pester test here would be vacuous. If a future change surfaces the corrected root/exit in the PowerShell catalog banner, that is a **separate** addendum.

### B.3 — One required existing-test modification (regression tightening, not new test)

`PipelineRunnerPostValidatorTests.RunAsync_ArtifactFailuresWriteRecordsAndSummary` (`:294`) builds the **exact D1 shape** (step id `2`, Fatal, `Success=true`, artifactFailures at `:316–323`) and asserts `SuccessExitCode` at **`:335`** — that assertion **literally encoded the D1 bug**. Under the corrected predicate this exit becomes `FatalExitCode`. Parker must update **only** the `:335` assertion to `FatalExitCode` and add a root/suppression assertion. **KEEP** `Assert.Single(failureFiles)` (`:338`) and the summary asserts (`:339–341`) exactly as-is. This is the **sole** authorized edit to an existing test in this addendum and MUST be flagged in the PR body as a correct tightening (see B.6). It is **not** a new test — it is covered by mutation M35.

### B.4 — New mutation rows M35–M42 (Deliverable 4; new total **42**)

Every element of AMENDMENT 1 (A2 clauses C1/C2/rootExit/policy-guard; A3 traversal; A4 canonical-write/overwrite/OutputArtifacts) has a row whose named test goes **RED** when the production change is reverted.

| # | Production element (AMENDMENT 1) | Mutation (exact revert) | Test(s) that go RED |
|---|---|---|---|
| **M35** | A2 **C2 clause** (root on Fatal + `ArtifactFailures.Count>0`) | Change the OR to exit-only: `signalledFailure = mappedExit != SuccessExitCode` (drop `\|\| result.ArtifactFailures.Count > 0`) | **T33** (17→1, 16→0), **T34**, **T35**, **T37**, **T38**, plus the tightened `PostValidatorTests:335` |
| **M36** | A2 **C2 discriminator** (keys on `ArtifactFailures`, not validators) | Rekey C2 to `\|\| result.ValidatorResults.Any(v => !v.Success)` | **T40** + `PreAiValidationGateTests` :49/:79/:116 (over-fire on pre-AI gate) |
| **M37** | A2 **rootExit recompute** (C2-only root still exits nonzero) | `rootExit = stepOutcome.ExitCode;` (no `MapStepFailureExitCode` recompute) | **T34** (`exit == FatalExitCode`), **T37** |
| **M38** | A2 **Fatal policy guard** (warn never root) | Drop `step.FailurePolicy == Fatal` from the predicate | **T39** (warn step wrongly becomes root) |
| **M39** | A3 **full-graph traversal** | Restore intersect-at-enqueue: `if (!selectedIds.Contains(dep) \|\| !visited.Add(dep)) continue;` before enqueue | **T36** (`{2,4}` → `{}` not `{4}`), **T37** |
| **M40** | A4 **canonical write** | `WriteSuppressedEnvelope` writes only `GetObservabilityDirectory` (drop the `GetStepWorkspaceDirectory` write) | **T38** (canonical read returns stale/absent) |
| **M41** | A4 **stale overwrite** | Guard canonical write behind `if (!File.Exists(canonicalPath))` | **T38** (Run 2 canonical still reads Run-1 success) |
| **M42** | A4 **empty output on suppressed envelope** | Set `OutputFileCount = <nonzero>` / copy prior `OutputArtifacts` onto the suppressed envelope | **T38** (`OutputFileCount == 0` / `OutputArtifacts` empty) |

Each row satisfies §3's mutation contract: a real production change, a named test that flips RED, and an exact revert instruction. M35 additionally re-RED's the tightened `PostValidatorTests:335` — the reverted C2 restores the original `SuccessExitCode`, so that assertion (once Parker flips it to `FatalExitCode`) fails, giving a second independent guard on C2.

### B.5 — RED / GREEN commands (Deliverable 7)

RED phase (seam/behavior absent — expect T32/T33 compile-RED, T34–T38 runtime-RED, PostValidatorTests:335 flipped-RED):
```
dotnet test mcp-tools/DocGeneration.PipelineRunner.Tests/DocGeneration.PipelineRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~DependencySuppressionTests"
```
GREEN phase (after A2–A5 land) — new tests, then the regression-control cluster, then full project, then whole solution:
```
dotnet test mcp-tools/DocGeneration.PipelineRunner.Tests/DocGeneration.PipelineRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~DependencySuppressionTests"
dotnet test mcp-tools/DocGeneration.PipelineRunner.Tests/DocGeneration.PipelineRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~PipelineRunnerPostValidatorTests|FullyQualifiedName~PreAiValidationGateTests|FullyQualifiedName~NamespaceStepTests"
dotnet test mcp-tools/DocGeneration.PipelineRunner.Tests/DocGeneration.PipelineRunner.Tests.csproj --configuration Release
dotnet test mcp-doc-generation.sln --configuration Release
```
The corpus test (T33) needs the frozen baseline fixtures on disk; run from the repo root so `FindRepoRoot` resolves `mcp-doc-generation.sln`. Per §5.1, the known-unrelated `FamilyMetadataGeneratorTests` and `Scan-McpToolCoverage.Tests.ps1` failures remain out of scope and are **not** a gate on this work.

### B.6 — Regression watch (Deliverable 6): what shifts under the corrected predicate, and the decision rule

The corrected root predicate (C2) changes the classification of exactly one *class* of existing assertion: **a Fatal step that returns `Success=true` with non-empty `ArtifactFailures` now exits `FatalExitCode` and becomes a suppressing root.** Everything else must be byte-for-byte unchanged. Verified against HEAD:

**A. Exactly ONE existing test must flip (correct tightening — NOT a weakening):**
- `PipelineRunnerPostValidatorTests.RunAsync_ArtifactFailuresWriteRecordsAndSummary` (:294) — asserts `SuccessExitCode` at **:335** for the exact D1 shape. Flip **:335 only** to `FatalExitCode` and add a root/suppression assertion; **KEEP** `Assert.Single(failureFiles)` (:338) and summary asserts (:339–341). Flag in the PR body as "encoded the D1 bug; corrected to the post-fix contract." (See B.3.)

**B. MUST STAY GREEN — a break here means the *implementation* is wrong; fix the predicate, never the test:**
- `PreAiValidationGateTests.RunAsync_PreAiGate_SkipsStepOnValidatorFailure` (:49), `…_RecordsValidationStatusFailed` (:79), `…_Failure_IsNonFatal_PipelineContinues` (:116) — the pre-AI gate (`Success=true` + failed validator + **empty** ArtifactFailures) must stay **non-fatal**. If C2 is (mis)written against `ValidatorResults` instead of `ArtifactFailures`, this trio breaks — that is mutation M36, i.e., a wrong implementation, **not** a test to update.
- `NamespaceStepTests.Step2_ExamplePrompts_ValidatorFailureCreatesArtifactFailureWithoutBlockingStep` (:141) — asserts the **step-level** contract (Step 2 returns `Success=true` and does not self-block). The root/suppression decision is a **runtime** concern layered above the step; this test MUST NOT be "fixed" to make Step 2 fail internally.
- `PipelineRunnerPostValidatorTests` controls at :48 (FatalExitCode, C1 validator failure), :94 (FatalExitCode, retry-exhausted), :140 (SuccessExitCode, clean-on-retry), :188 (SuccessExitCode, warn+clean Step 6) — all confirmed unchanged under C2.
- `SelectedTransitiveDependents` T2–T7 (existing closure tests) and the suppression-envelope tests **T12/T14/T20** (they read the observability copy, which A4 keeps writing) — unchanged.

**C. Decision rule (binding on Parker/the implementer):** a failing existing assertion is a **correct tightening** — resolved by updating that one assertion to the post-fix contract while keeping every side-effect assertion — **only if** the failing assertion asserted pre-fix buggy behavior, namely *a Fatal step with non-empty ArtifactFailures exiting `SuccessExitCode`* **or** *dependents executing after a real-shape (`Success=true`+ArtifactFailures) Step-2 failure*. The **only** assertion known to meet this is `PostValidatorTests:335`. **Any** break in the pre-AI trio, the clean-success controls (:140/:188), the step-level `NamespaceStepTests:141`, or the warn-not-root path (T39) is a **GENUINE REGRESSION → fix the code, not the test.** No other existing assertion may be edited, relaxed, `Skip`-ped, or deleted under this work. If a break appears outside `PostValidatorTests:335` and the implementer believes it is nonetheless "correct," Parker **escalates to Riley/Cameron** rather than editing the test. **No test is weakened. Only the :335 edit is authorized.**

### B.7 — Append-only invariants (self-check)

1. Strategy only — this addendum added **no** test code and edited **no** `.cs`/`.ps1`. The `:335` change in B.3/B.6 is **specified** for Parker, not performed here.
2. Numbering continued without collision: xUnit **T32–T40** (T31 was the prior max), mutations **M35–M42** (M34 was the prior max), Pester unchanged (P8 remains max).
3. §0–§7 and Addendum A are untouched; §0.4 seam rule, §3 anti-vacuity, §5/§5.1 command + disjointness conventions are reused, not modified.
4. Every new test names its file and its RED mode; every "absent" assertion is paired with a positive control; every mutation row has an exact revert and a named RED test.
5. Universal Design Principle honored — data spans Storage, Key Vault, Cosmos DB, Speech, Monitor, compute; the one identity pin (`monitor.02.webtests-get.01`) is a **corpus fact** (the sole `Success=false` record), not service-specific test logic.

**Addendum B summary:** +9 tests (**T32–T40** in `DependencySuppressionTests.cs`; Pester **none**) · +8 mutation rows (**M35–M42**, new total **42**) · +1 authorized existing-test tightening (`PipelineRunnerPostValidatorTests:335`) · new harness `StepOutcomes` factories (`ValidationAfterRetriesFailure`, `FailingDependentWithArtifacts`, `PreAiSkipNonFatalOutcome`) + read-only `Beta34Corpus` reader, both additive. Pinned corpus: **17** Step-2 records / **16** `Success=true` / **1** `Success=false` (`monitor.02.webtests-get.01`) / **10** cascades / **16** upstream links (all `stepId==02`). The BLOCKING-1 catcher is **T33**; F1 is retired by **T35**; D3 by **T36/T37**; D4 by **T38**; over-firing guarded by **T39/T40**.
