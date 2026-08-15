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
