# VERDICT: FAIL

**Gate:** Issue #813, tracker item 2 — *"Nondeterministic evaluation: Eval Reviewer examines real representative orchestration and accounting for missed cascades."*
**Reviewer:** Ellis (standing Evaluation Reviewer, nondeterministic). Independent; authored none of the change.
**PR:** #815 — HEAD `1da15b5ca5db0a15af2d27e7dbdf4a1b0ed77b2f`, baseline `8d41a9bad920335b875ebbd4d73b03881a954e18`.
**Date:** 2026-08-14. Environment: dotnet 10.0.303, pwsh 7.6.4, Windows. Working tree clean at HEAD before and after review.

Rejection lockout applies: on this FAIL, the original author of the affected work (Morgan, C# runtime) is barred from the remediation, per charter.

---

## Bottom line

The suppression runtime is architecturally sound and the accounting is arithmetically honest, but the feature is **inert against the real corpus it was built to handle**. Walking the **real production registry graph** against **every historical cascade** in the frozen beta.34 baseline, the new runtime would have suppressed **0 of 10** cascades. The trigger for suppression is `stepOutcome.ExitCode != SuccessExitCode` (`PipelineRunner.cs:256`), but **16 of the 17** real Step‑2 failures — which are exactly the 16 upstream links behind **all 10** cascades — return `Success=true` and therefore **exit 0**. They are never recorded as roots and suppress nothing. The one Step‑2 failure that would trigger suppression (`monitor.02`, `Success=false`) has **no** dependent cascade.

This is not "insufficient evidence." The primary sources **affirmatively** show the missed cascades. That is a substantive FAIL.

The scope defense (AD‑029 §8 lists a "typed Step 2 generation‑failure result" as a non‑goal) is real and I weigh it below — but it does not rescue the gate, because **this specific gate evaluates real orchestration for missed cascades**, and the real result is 0/10 suppressed. A feature that ships a Class‑D‑cascade‑elimination mechanism which fires on none of the historical Class‑D cascades cannot pass a gate whose sole purpose is to check exactly that.

---

## What I re-derived myself vs. accepted from others' evidence

**Re-derived independently from primary sources (code, fixtures, live runs):**
- The full production dependency graph (`StepRegistry.CreateDefault` + each step's `DependsOn`) and its reverse adjacency and transitive closures.
- Every accounting count in `beta34-baseline-manifest.json`, by parsing the fixtures fresh (read‑only Python): 34 logical / 68 physical, 17 Step‑2 / 17 Step‑4, 10 dependent records / 16 dependency links, chainRole root 24 / cascade 10, classification 21/9/3/1, errorClass 29/7/3/1.
- The failure‑mode categorization of all 17 Step‑2 records (16 "validation after retries" `Success=true`; 1 "generation failed" `Success=false`) directly from `critical-failures/*.json`.
- The cascade→upstream linkage (10 cascades, 16 upstream links, **all** upstream `stepId==02`) from the manifest `records[]`.
- The suppression trigger and exit‑code decoupling, by reading the exact lines: `PipelineRunner.cs` 236–306, 754–766, 808–907; `ExamplePromptsStep.cs` 80–142; `NamespaceStepBase.cs` 91–112; `ToolFamilyCleanupStep.cs` 43–46, 318–354; `CriticalFailureRecorder.Persist`; `RunPostValidatorsAsync` 768–806; `RunValidationGatesAsync` 677–721.
- Empirical GREEN: built `DocGeneration.PipelineRunner.Tests` (Release, 0 warn/0 err) and ran the Step‑2 suite → **31/31 pass** at HEAD.
- Fixtures byte‑unchanged: `git diff --stat 8d41a9b..HEAD -- .../Beta34.Tests/Fixtures/` is **empty**.
- Disjointness spot‑check: reproduced the pre‑existing xUnit failure `FamilyMetadataGeneratorTests.GenerateAsync_WhenAiResponseIsTruncated_UsesFallbackDescription` on HEAD (a project Step 2 does not touch).
- Scope‑discipline: full changed‑file list; grep for `Validator|generated|Validation/`.

**Accepted from others' evidence (corroborated, not fully independently re‑executed) — with the reason:**
- The **per‑row RED observations** in `813-step2-mutation-proof.txt` for rows I did not transiently mutate. I did **not** perform transient production‑file mutations, because my charter forbids modifying any production/test/script/doc file and the gate's value depends on that independence. Instead I **analytically re‑derived the RED mechanism** for rows M8, M20, M26 against the actual test + production code (shown below), and confirmed the suite is GREEN at HEAD empirically. This is a genuine spot‑check from primary sources; it is not a blind trust of Parker's table.
- The **Pester P1–P8** results (I did not run Pester in this review).
- The **pristine‑worktree side** of the disjointness proof (I reproduced only the feature‑branch side; the changed‑file list independently confirms Step 2 touches neither the failing project nor the coverage scanner).

---

## The production dependency graph (re-derived)

| Step | Name | Scope | Policy | DependsOn | MaxRetries |
|---|---|---|---|---|---|
| 0 | Bootstrap | Global | Fatal | — | 0 |
| 1 | Annotations/params/raw | Namespace | Fatal | — | 0 |
| 2 | Example prompts | Namespace | **Fatal** | [1] | — |
| 3 | Tool generation | Namespace | Fatal | [1,2] | — |
| 4 | Tool‑family cleanup | Namespace | **Fatal** | [3] | **2** |
| 5 | Skills relevance | Namespace | Warn | [0] | — |
| 6 | Horizontal article | Namespace | Fatal | [0] | — |
| 7 | Article health | Namespace | Warn | [4] | — |
| 8 | Coverage audit | Namespace | Warn | [0,4,7] | — |

Reverse adjacency (dependents): `0→{5,6,8}`, `1→{2,3}`, `2→{3}`, `3→{4}`, `4→{7,8}`, `7→{8}`, else `{}`.
Transitive closures used below: **closure(2) = {3,4,7,8}**, closure(1) = {2,3,4,7,8}, closure(4) = {7,8}.
Independent‑step check: Steps 5 and 6 depend only on global Step 0, appear in **no** namespace‑root closure. ✔ (see NON‑BLOCKING over‑suppression note).

---

## BLOCKING findings

### BLOCKING‑1 — All 10 historical cascades are MISSED (0/10 suppressed). This is the core gate failure.

**Claim.** For every cascade in the frozen baseline, the new runtime would **not** have suppressed the dependent work, because the Step‑2 root that causes the cascade returns `Success=true` → exit 0 → is never recorded as a root → triggers no suppression closure.

**Proof chain (exact lines):**
1. `ExamplePromptsStep.cs:126–138` runs `ValidateWithRetriesAsync` and, on failure after retries, appends to `artifactFailures` (line 138) **without an early return**. Control falls through to `ExamplePromptsStep.cs:141`: `return BuildResult(context, processResults, true, warnings, validatorResults, artifactFailures);` — **`success:true` with a non‑empty `artifactFailures`.** (`Success=false` occurs only at lines 92, 104, 122 — generation crash / per‑tool generation failure / incomplete outputs.)
2. `NamespaceStepBase.cs:91–111` `BuildResult` passes the `success` argument **straight through** to `new StepResult(success, …, resolvedFailures)` (line 111). `Success` is **not** recomputed from `artifactFailures`. So `Success=true` + non‑empty `ArtifactFailures` is a valid, reachable `StepResult`.
3. `PipelineRunner.cs:878` `CriticalFailureRecorder.Persist(context, step, result)` persists a critical‑failure JSON whenever `ArtifactFailures.Count > 0`, **regardless of `Success`**. → the failure is recorded on disk.
4. `PipelineRunner.cs:896` `var stepExitCode = MapStepFailureExitCode(step.FailurePolicy, result.Success, result.ExitCodeOverride);`. `MapStepFailureExitCode` (754–766): `if (stepSucceeded || failurePolicy == Warn) return SuccessExitCode;` → with `Success=true`, returns **exit 0** even for a Fatal step.
5. `PipelineRunner.cs:256` `if (stepOutcome.ExitCode == SuccessExitCode) { … continue; }`. The root‑recording + suppression block (268–281) is the **else**. So an exit‑0 Step‑2 record: **no root recorded, no `catalogHadFatalRoot`, no `SelectedTransitiveDependents`, nothing suppressed.** Step 3 and Step 4 then execute normally.

**Corpus intersection (re‑derived):** Of 17 Step‑2 records, **16** have summary *"example prompt validation failed for this tool after automatic retries"* with `validatorResults[].success=false` — i.e. exactly the path 1→5 above (`Success=true`, exit 0). The **1** exception, `monitor.02.webtests-get.01` (*"generation failed"*, `Success=false`), is the only one the runtime would act on — and `monitor` has **no** Step‑4 cascade. Every one of the **16 upstream links** behind the 10 cascades is in the `Success=true` set.

**Consequence, verified downstream:** In each cascade namespace, Step 4 (`ToolFamilyCleanupStep`) then runs. Its post‑assembly validator is a registered **post‑validator** (`ToolFamilyCleanupStep.cs:45`), so `RunPostValidatorsAsync` (`PipelineRunner.cs:797`, `success &= validatorResult.Success`) flips Step 4 to `Success=false`, it retries twice (`MaxRetries=2`), and finally exits 1 — becoming **its own Step‑4 root**. Net effect under the new runtime: the 10 historical cascades **re‑occur live as 10 independent Step‑4 roots**, each having burned Step‑3 + three Step‑4 attempts, with **no** suppression and **no** recorded link to the Step‑2 upstream. This is precisely the Class‑D waste #813 set out to eliminate, and it is not eliminated.

**This directly falsifies:** AD‑029 §6 category 5 ("the new runtime turns [these cascades] into suppressed work") and issue #813's Class‑D goal, **for 10 of 10** historical cascades.

**Scope note (weighed, does not rescue the gate):** `ExamplePromptsStep.cs` is **not** modified by this PR, and AD‑029 §8 lists the typed Step‑2 generation‑failure result as a non‑goal (item 4). So the defect is a *design‑boundary* choice, not a coding error in the diff. But the gate under evaluation is not "did the diff compile against its own design"; it is "examine real representative orchestration and accounting for missed cascades." The real orchestration misses 10/10. A mechanism that cannot fire on the actual failure shape of the corpus does not satisfy this gate.

**What evidence would resolve BLOCKING‑1 (either is sufficient):**
- (a) Make a Fatal‑policy step that has produced artifact failures after retries surface a non‑success outcome (e.g., `Success=false`, or an `ExitCodeOverride`), so the namespace loop records it as a root — **and** a test/real run demonstrating ≥1 real cascade namespace (e.g. `appconfig`: Step‑2 validation‑after‑retries failure) actually suppresses Step 3/4; **or**
- (b) If (a) is genuinely out of scope for #813 Step 2, then the change and AD‑029 must **stop claiming to eliminate Class‑D cascades**, and the tracker item 2 must be re‑scoped — because as written the gate cannot pass while 10/10 cascades go unsuppressed.

---

### BLOCKING‑2 — No evidence exercises the real failure shape; the gate's "real representative orchestration" is absent.

**Claim.** Every suppression/accounting test injects a Step‑2 root as `Success=false` (a shape the runtime *can* suppress). **None** injects the real shape (`Fatal` + `Success=true` + non‑empty `ArtifactFailures`). No real or representative pipeline run exists in the evidence. Therefore the GREEN suite and the 34/34 mutation proof demonstrate the *synthetic mechanism*, not the *real corpus behavior*, and cannot detect BLOCKING‑1.

**Proof (exact artifacts):**
- Test failure injectors, `DependencySuppressionTests.cs:495–506`: `Success()` = `StepResult.DryRun(...)` (Success=true, no failures); `Failure()` = `new(false, {"boom"}, …, Array.Empty<ArtifactFailure>())` (**Success=false, empty failures**); `HumanReview()` = Success=false + override 2. `RunAccountingTests.cs:459` repeats the same `Failure()` pattern.
- **The decisive artifact:** T9 = `RunAsync_FatalStep2_SuppressesDependents_RunsIndependentStep6_AndContinues` (`DependencySuppressionTests.cs:153–168`) — the one test that names the exact cascade scenario — injects the root via `Step(doubles, 2).Outcome = _ => Failure();` (line 157). That is `Success=false`, **disjoint from all 16 real cascade roots** (`Success=true`). It is GREEN precisely because it tests a failure mode the runtime suppresses, while the real mode it does not. A test titled "FatalStep2 suppresses dependents" validates a Step‑2 shape that does not occur for any cascade‑causing root in the corpus.
- No real orchestration: `813-step2-green-run.txt` shows only `dotnet test` on synthetic `RecordingNamespaceStep` doubles (`TestDoubles.cs`, `MirroredRegistry.CreateDoublesMatchingDefault`). The beta.34 manifest is consumed **only** as static constants for accounting categories 5/6 (`BuildBaselineReconciliation`, `PipelineRunner.cs:1775`); it never flows through the suppression logic. The feature has never been exercised against a single real cascade.

**What evidence would resolve BLOCKING‑2:** a test (or real run) that drives a Fatal step to return `Success=true` with a non‑empty `ArtifactFailures` (the real Step‑2 mode) through the namespace loop and asserts the resulting behavior of its dependents. Per the gate's own rule ("insufficient evidence is a FAIL"), the absence of any such representative evidence is independently disqualifying, separate from BLOCKING‑1.

---

## Per-cascade missed-cascade analysis (graph walk shown)

For every cascade, the graph walk is identical, so I show it once and tabulate the corpus intersection. **Walk:** a Step‑2 failure *should* suppress `closure(2) = {3,4,7,8}` via reverse adjacency (`2→{3}→{4}→{7,8}`). Suppression fires only if Step‑2's outcome has `ExitCode≠0` (`PipelineRunner.cs:256/268`). All 16 cascade‑upstream Step‑2 records return `Success=true` → exit 0 → the walk **never starts**; `{3,4,7,8}` all execute; Step 4 then fails as its own root.

| # | Cascade Step‑4 record | Upstream Step‑2 root(s) (all `Success=true` "validation after retries") | Step‑2 exit | Suppression fires? | Real‑runtime outcome |
|---|---|---|---|---|---|
| 1 | `appconfig.04.appconfig.01` | `appconfig.02.kv-get.01` | 0 | **No** | Step 3/4 run; Step 4 re‑fails as own root |
| 2 | `azurebackup.04.azurebackup.01` | `azurebackup.02.governance-soft-delete.01` | 0 | **No** | " |
| 3 | `azureterraform.04.azureterraform.01` | `aztfexport-query.01`, `aztfexport-resourcegroup.02` | 0 | **No** | " |
| 4 | `datadog.04.datadog.01` | `datadog.02.monitoredresources-list.01` | 0 | **No** | " |
| 5 | `foundryextensions.04.foundryextensions.01` | `openai-chat-completions-create.01`, `openai-embeddings-create.02` | 0 | **No** | " |
| 6 | `group.04.group.01` | `group.02.resource-list.01` | 0 | **No** | " |
| 7 | `search.04.search.01` | `search.02.knowledge-base-retrieve.01` | 0 | **No** | " |
| 8 | `sreagent.04.sreagent.01` | `docs-memories-add.01`, `docs-memories-search.02`, `threads-send-message.03` | 0 | **No** | " |
| 9 | `storage.04.storage.01` | `account-create.01`, `blob-container-create.02` | 0 | **No** | " |
| 10 | `storagesync.04.storagesync.01` | `cloudendpoint-changedetection.01`, `cloudendpoint-create.02` | 0 | **No** | " |

**Missed cascades: 10 of 10. Suppressed cascades: 0.** (16 upstream links, all `stepId==02`, all in the `Success=true` mode.)

**Negative/positive control on the same graph:**
- The only Step‑2 record that *would* trigger suppression, `monitor.02.webtests-get.01` (`Success=false`), has **no** Step‑4 cascade in the corpus → even the one live trigger has nothing to suppress.
- If a Step‑2 root *did* return `Success=false` (as the synthetic tests inject), the walk correctly suppresses `{3,4,7,8}` and leaves independent Steps 5,6 running — the mechanism is sound; it is simply never reached by the real data.

---

## Over-suppression, attribution, accounting, exit-code, lost-information (items 2–6)

**Over‑suppression (item 2): PASS.** Independent steps depending only on global Step 0 survive a namespace‑root fatal. Graph: 5,6 ∉ any namespace‑root closure. Test T9 asserts `Step(doubles,6).Executions == 1` after a fatal Step‑2. No over‑suppression. ✔

**Attribution (item 3): NON‑BLOCKING.** First‑match attribution (`PipelineRunner.cs:276–278`) and `rootFailureId = {slug}.{stepId:D2}.root` (line 269) are collision‑free within a namespace (one id per stepId) and stable. For the real corpus this path is **moot** — the Step‑2 roots are never detected, so nothing is attributed to them (the failures surface as Step‑4 roots instead). Where multiple roots exist in one namespace, ascending step order attributes a shared dependent to the earliest/most‑upstream root, whose closure is a superset — defensible. No blocking loss.

**Accounting truthfulness (item 4): arithmetically correct, but not a live measure of suppression — NON‑BLOCKING with caveat.** I recomputed the six‑category reconciliation. The live partition (cats 1–4) is a genuine first‑match partition (T26) and reconciles `24 + 10 = 34` logical (T29); cascade is sourced from `chainRoleCounts.cascade` (10), **not** `classificationCounts.cascade` (9), honoring L‑004 (T27, `RunAccountingTests.cs:177–201`); baseline constants are read once and **not** summed across namespaces (P7 / mutation M32); 34 logical vs 68 physical is handled. All counts match the manifest exactly (table below). **Caveat:** cats 5/6 are **static constants** from the frozen manifest, not live measurements. Because of BLOCKING‑1, a real run of these 10 namespaces would show **live Suppressed = 0** while the summary still prints *"Cascades imported from historical fixtures (10)"* — a truthful historical restatement that a reader could misread as live suppression. See NON‑BLOCKING‑2.

**Nothing quietly lost (item 5): PASS on mechanism.** A suppressed step emits a durable `WriteSuppressedEnvelope` (persisted under `OutputPath/observability`, mutation M11) carrying `Suppressed=true`, `status=failure`, `validationStatus=skipped`, and `BlockedByDependency{namespace, failedRootStepId, rootFailureId}` (`StepResultFile.cs:221–275`). `schemaVersion` stays `"1.0"` (M3) and integer `Version` bumps 3→4 (M4). A consumer that ignores the new `suppressed` field still sees `status=failure`, so a suppressed step cannot be mistaken for success (M5). ✔ **However**, this is only reachable when suppression fires — which, per BLOCKING‑1, it never does for the real corpus. The information there is not "lost" but is instead expressed as a fresh Step‑4 root with **no upstream‑Step‑2 linkage**, degrading the causal chain the feature promised to preserve.

**Exit‑code honesty (item 6): holds — NON‑BLOCKING.** In a default run, exit is `catalogHadFatalRoot ? worstRootExit : SuccessExitCode` (`PipelineRunner.cs:305`); `RunValidationGatesAsync` (677–721) does **not** consult `criticalFailures` and returns success unless an opt‑in gate is enabled. For the real corpus, `catalogHadFatalRoot` becomes true via **Step 4's own fatal root** (post‑validator flips Success=false → exit 1), so the catalog exits non‑zero. I confirmed no path where a cascade namespace exits 0 with critical JSON on disk, because Step 4's post‑assembly validator is a registered post‑validator (`ToolFamilyCleanupStep.cs:45`) that fails fatally. `Worse(fatal=1, human‑review=2)=1` preserves fatal dominance across namespaces (T17). ✔ (This "honesty via Step‑4 root" is exactly what makes BLOCKING‑1 observable in production: the cascades come back as live roots.)

---

## Recomputed counts vs. claimed (all match)

| Quantity | Manifest `accounting` (claimed) | My independent recompute | Agree? |
|---|---|---|---|
| logicalRecords | 34 | 34 | ✔ |
| physicalCopies | 68 | 68 | ✔ |
| step2Records | 17 | 17 | ✔ |
| step4Records | 17 | 17 | ✔ |
| dependentRecords | 10 | 10 (cascade Step‑4 records) | ✔ |
| dependencyLinks | 16 | 16 (all `stepId==02`) | ✔ |
| chainRoleCounts.root / cascade | 24 / 10 | 24 / 10 | ✔ |
| classificationCounts root/cascade/mixed/diagnostic | 21 / 9 / 3 / 1 | 21 / 9 / 3 / 1 | ✔ |
| errorClassCounts A/B/AB/C | 29 / 7 / 3 / 1 | 29 / 7 / 3 / 1 | ✔ |
| Step‑2 mode split | (not in manifest) | **16** validation‑after‑retries (`Success=true`) + **1** generation‑failed (`Success=false`) | — |

No disagreement on any published count. The only number that *matters for the gate* and is **not** published is the live‑suppression rate against the corpus, which I compute as **0 / 10**.

---

## Evidence integrity (item 7)

**RED is RED for the right reason.** The RED addendum drives T30/T31/P7/P8 by the absence of the six‑category console block and injects Step‑2 roots via `Success=false`. The RED is caused by the missing production emitter, not by an unrelated defect. ✔ (Accepted from `813-step2-red-addendum.txt` + confirmed the corresponding GREEN passes at HEAD.)

**Mutation proof 34/34 — credible as a code‑coverage/revert claim; orthogonal to BLOCKING‑1.** I analytically re‑derived three rows against the actual code + tests:
- **M8** (reverse vs forward adjacency → T1,T9): forcing `BuildDependentsOf` to forward edges makes `dependentsOf[0]` empty, so `T1` (`Assert.Equal([5,6,8], …)`) and `T9` (`Assert.Equal(0, Step 3 Executions)`, since `closure(2)` no longer contains 3) both flip RED. Matches the recorded "T1 Expected [5,6,8] Actual []; T9 Expected 0 Actual 1." ✔
- **M20** (cascade from `chainRoleCounts` not `classificationCounts` → T27): sourcing from `classificationCounts.cascade` (9) makes `CascadeImported=9`, so `T27`'s `Assert.Equal(10, …)`/`Assert.NotEqual(9, …)` flip RED (`RunAccountingTests.cs:196–200`). ✔
- **M26** (AD‑027 case‑insensitive param/local collision → P5 + 6 collateral): renaming the local to a case‑variant of the param genuinely clobbers it. Consistent with the recorded 6 collateral RED. ✔ (Pester side accepted, not re‑run.)

The 34/34 is a valid statement that **every written line is guarded by a test**. It is **not** a statement that the code is fit for the real corpus, and it structurally cannot surface BLOCKING‑1: the mutation set is closed over the code‑as‑implemented, and no mutation (and no test) targets the "Fatal step returns `Success=true` + artifactFailures" trigger, because that path does not exist and no double injects that shape.

**Parker's finding F1 (M6/T13 non‑discriminating): confirmed real, and it is a symptom of BLOCKING‑2 — NON‑BLOCKING for M6, but corroborating.** Under M6 (execute suppressed dependents), T9 flips RED (Step 3 `Executions 0→1`) so M6 is genuinely guarded; T13 stays GREEN because the `RecordingNamespaceStep` dependents default to DryRun **success**, emitting no step‑03/04 critical JSON whether suppressed or executed. Parker rated this LOW and self‑reported it honestly; I agree M6's revert proof holds via T9. But the **root cause of F1 is exactly the harness gap in BLOCKING‑2**: the doubles never model a *failing* dependent (the real Step‑4 cascade shape), so T13 cannot distinguish "suppressed" from "executed‑and‑succeeded." F1 is a second, independent footprint of the same blind spot.

**Disjointness holds (spot‑checked).** I reproduced on HEAD the one pre‑existing xUnit failure (`FamilyMetadataGeneratorTests.GenerateAsync_WhenAiResponseIsTruncated_UsesFallbackDescription`, `Assert.Contains` sub‑string not found) in `DocGeneration.Steps.ToolFamilyCleanup.Tests` — a project **not** in the PR's changed‑file set. Step 2 neither fixes, hides, nor weakens it. ✔

---

## Scope discipline (item 8): clean

- **Changed files (23):** production limited to `PipelineRunner.cs`, `shared/…/StepResultFile.cs`, `start-with-logs.ps1`; tests `TestDoubles.cs`, `DependencySuppressionTests.cs`, `RunAccountingTests.cs`, `validation/tests/StartWithLogs.Tests.ps1`; 7 evidence files; docs (CHANGELOG, README, ARCHITECTURE, START‑SCRIPTS, test‑strategy); squad metadata (cameron matrix, ellis charter/history, casting‑registry, team). No items from issue #813 items 3–10 leaked in.
- **No validator weakened/disabled:** grep `Validator|generated|Validation/` over the changed set returns only `mcp-tools/validation/tests/StartWithLogs.Tests.ps1` (a Pester **test**, not a production validator). No `Validation/*.cs` production validator touched.
- **No `generated*/` output edited.** ✔
- **No new pipeline phase / no new `IPipelineStep`.** `StepRegistry.CreateDefault` is not in the changed‑file list; changes live inside the existing `RunAsync` namespace loop and helpers.
- **`StepResultFile.cs` changes are additive/nullable** (`Suppressed`, `BlockedByDependency`); `schemaVersion` unchanged at `"1.0"`.
- **Frozen beta.34 fixtures byte‑unchanged:** `git diff --stat 8d41a9b..HEAD -- mcp-tools/DocGeneration.Baseline.Beta34.Tests/Fixtures/` is **empty** (manifest, 34 critical‑failure JSONs, source‑inventory all untouched). No service‑specific or hardcoded‑service logic added.

---

## NON-BLOCKING findings (for the remediation, not gating)

- **NON‑BLOCKING‑1 (F1).** M6's second guard (T13) is non‑discriminating because dependents default to DryRun success. Seed a dependent with a failing/critical outcome so "executed vs suppressed" changes emitted JSON — this simultaneously closes the BLOCKING‑2 representativeness gap.
- **NON‑BLOCKING‑2 (accounting readability).** The console/JSON line *"Cascades imported from historical fixtures (10)"* (`RunAccountingTests.cs:294`; `WriteRunAccountingSummary`) is a frozen constant. Given BLOCKING‑1, a live run would show Suppressed = 0 for those namespaces. Recommend the summary visibly separate "historical (imported)" from "this‑run (live)" suppression so cat 5 cannot be read as live evidence the runtime suppressed 10 cascades.

---

## Findings index

| # | Finding | Severity | Primary proof |
|---|---|---|---|
| 1 | 10/10 real cascades unsuppressed (Step‑2 `Success=true` → exit 0 → no root) | **BLOCKING** | `ExamplePromptsStep.cs:138,141` → `NamespaceStepBase.cs:111` → `PipelineRunner.cs:878,896`, `MapStepFailureExitCode:756`, `PipelineRunner.cs:256`; 16/17 fixture mode split; 16 cascade links all `stepId==02` |
| 2 | No evidence exercises the real failure shape; no representative orchestration | **BLOCKING** | `DependencySuppressionTests.cs:157,495–506`; T9 title vs `Failure()`; `813-step2-green-run.txt` (synthetic doubles only); manifest used only as constants (`PipelineRunner.cs:1775`) |
| 3 | Over‑suppression | PASS | graph 5,6 ∉ closures; T9 Step‑6 Executions==1 |
| 4 | Attribution first‑match / rootFailureId stability | NON‑BLOCKING | `PipelineRunner.cs:269,276–278` |
| 5 | Accounting six‑category partition correctness (cats 5/6 static) | NON‑BLOCKING | T26/T27/T29; recompute table |
| 6 | Exit‑code honesty | NON‑BLOCKING (holds) | `PipelineRunner.cs:305`, `RunValidationGatesAsync:677–721`, `ToolFamilyCleanupStep.cs:45`, `RunPostValidatorsAsync:797` |
| 7 | Mutation proof 34/34 credible as coverage, orthogonal to #1; F1 real | NON‑BLOCKING | M8/M20/M26 re‑derivation; `813-step2-mutation-proof.txt:343–356` |
| 8 | Scope discipline; frozen fixtures byte‑unchanged | PASS | `git diff --name-status`; empty `Fixtures/` diff |

---

## Verdict

**FAIL.** Two independent blocking findings: (1) the runtime would have suppressed **0 of 10** real historical cascades because the real Step‑2 failure mode returns `Success=true`/exit 0 and never triggers suppression; and (2) no test or run in the evidence exercises that real failure shape, so the gate's required examination of *real representative orchestration for missed cascades* has not been performed. The accounting, envelope, over‑suppression avoidance, exit‑code honesty, mutation coverage, disjointness, and scope discipline are all sound in isolation — but the feature is inert against the corpus it exists to handle. Per this gate's standard, that is a FAIL, and the lockout bars the original runtime author from the remediation.

↩︎ Responding to: "You are **Ellis** … Your gate — issue #813, tracker item 2 … issue **PASS** or **FAIL** … MISSED CASCADES is the core question … Write `.squad/agents/ellis/813-step2-evaluation.md` … Report back your verdict and the findings list."
