# VERDICT: APPROVED — 2026-08-14T23:55:02-07:00

**Gate:** Issue #813, live-tracker **item 2** — *Runtime dependency suppression, accounting, and orchestration.*
**Named approver:** Avery (Team Lead) — scope containment + final acceptance.
**PR:** #815 (`squad/813-step2-runtime-orchestration` → `main`), state **OPEN**, `mergedAt: null`.
**HEAD reviewed:** `65791f6a682463425c083613a187e8180756ef1f`. Baseline: `8d41a9b` (Step 1 freeze, PR #814 squash).
**Environment:** dotnet 10.0.303 build/test, pwsh 7.6.4, Windows. Worktree clean before/after; the only file I changed is this acceptance record.

This is a verification sign-off, not a rubber stamp. I re-ran all three gates myself, re-derived scope containment from the diff, read Ellis's evaluation in full (both rounds), and checked every required behavior against the code rather than the claims. Held to the same bar as my Step 1 acceptance.

---

## 1. Subordinate-gate status (both must pass before I sign)

Per the tracker update protocol — *"Never check a parent until both subordinate gates pass and the named approver signs off."*

| Subordinate gate | Owner | Result | Basis |
|---|---|---|---|
| **Deterministic tests** (full registry graph; fatal/warning/global; no dependent side effects; later namespaces; retry/resume/cancel/rerun; mutation/revert; Pester AD-027 collision) | Cameron (strategy) + Parker (impl) | **PASS** | 40 named xUnit tests GREEN at HEAD; 42/42 mutation/revert proof; 9 evidence files; Pester AD-027 check present. Re-run by me below. |
| **Nondeterministic evaluation** (real representative orchestration + accounting for missed cascades) | Ellis (standing Evaluation Reviewer) | **PASS** (round 2) | Round 1 **FAIL** (0/10 cascades) → Rowan remediation → round 2 **PASS** (10/10 rooted), independently reproduced by Ellis via transient mutation revert (`Expected 17 Actual 1`). |

Both gates pass. Reviewer independence honored (§5). Documentation gate (Reeve) PASS twice (§6). → I sign off.

---

## 2. Scope containment — the highest-risk failure mode

**Finding: CLEAN. Nothing from items 2–10 of the approved plan leaked in.** Step 2 is architecture **item 1 only** (runtime dependency suppression), and the change is confined to it.

Production files changed across `8d41a9b..HEAD` — **exactly three**:

| File | Δ | In scope for item 1? |
|---|---|---|
| `mcp-tools/DocGeneration.PipelineRunner/PipelineRunner.cs` | +515 / −6 | ✅ suppression runtime, `IsFatalRoot`, traversal, envelope, accounting |
| `shared/DocGeneration.Core.Shared/StepResultFile.cs` | +46 / −0 | ✅ additive/nullable `suppressed` + `blockedByDependency` only |
| `start-with-logs.ps1` | +127 / −3 | ✅ `$LASTEXITCODE` capture, `--skip-build` gating, six-category summary |

Confirmed **absent** (each is a distinct plan item, all fenced off by AD-029 §8):

- **Item 2** — canonical parameter-identity model / coverage evaluator / Step 1 manifest schema in `DocGeneration.Core.Shared`: **not touched** (only `StepResultFile.cs` changed there, and only with additive suppression fields).
- **Item 3** — Step 4 `SourceVerificationHelpers` → manifest binding: **not touched.**
- **Item 4** — typed/discriminated Step 2 generation-failure result, secret-redaction contract, stdout/stderr streaming: **not touched** (`ExamplePromptsStep.cs`/`ExamplePromptGenerator` unchanged).
- **Class A/B/C fixes** and **Monitor diagnosis/fix** (items 6/7): **not present.**
- **Parity/nondeterminism controls, replay stubs, intended-diff allowlist** (item 8): **not present.**
- **Full 63-namespace catalog run** (item 10): **not present.**

Independent corroboration: `git diff 8d41a9b..HEAD --name-only -- "*.cs" "*.ps1"` excluding tests returns only the three files above; Ellis reached the same conclusion independently (evaluation §8).

## 3. Prohibitions — all respected

- **No weakened/downgraded/disabled validation.** Diff grep for `Skip=`/`[Ignore]`/`Fact(Skip` added lines → **empty**. No `Validation/*.cs` production validator touched. The one authorized test edit (`PipelineRunnerPostValidatorTests.cs:335`, `SuccessExitCode`→`FatalExitCode`) is a **tightening** with an inline rationale — the old assertion *encoded the D1 bug*; it re-REDs under mutation M35 as a second C2 guard.
- **No service-specific / hardcoded-service logic.** The suppression runtime is pure graph/exit-code/envelope logic. No service names.
- **No edits under `generated/` or `generated-*/`.** Confirmed empty diff.
- **No new pipeline phase / no new `IPipelineStep`.** `StepRegistry.CreateDefault` is not in the changed set; `RecordingNamespaceStep : StepDefinition` is a **test double**, not a production step. Changes live inside the existing `RunAsync` loop.
- **Frozen Step-1 beta.34 fixtures byte-unchanged.** `git diff 8d41a9b..HEAD -- "*Beta34*" "generated/" "generated-*/"` → **empty**.

## 4. Required approved behavior — verified against code (PipelineRunner.cs / start-with-logs.ps1)

| Required behavior | Verified at | Note |
|---|---|---|
| Root-failure suppression that fires on the **real** shape | `IsFatalRoot` (:797–802) = `Fatal && (mappedExit != 0 \|\| artifactFailures.Count > 0)` | C2 clause is the round-2 fix; catches `Success=true` + exit 0 + `ArtifactFailures`. |
| Correct per-namespace state reset | `new NamespaceRuntimeState()` inside the `foreach namespace` loop (:236) | State cannot leak across namespaces (test `…StateResetsBetweenNamespaces…`). |
| Continue independent work / later namespaces | namespace `state` local; independent steps stay eligible (:262–272); loop continues (:208) | Tests `…RunsIndependentStep6…`, `…LaterNamespaceStillExecutes`. |
| Preserved nonzero exit + forced C2 exit | `rootExit` recompute (:278–280); `finalExitCode = catalogHadFatalRoot ? worstRootExit : 0` (:318); `Worse` keeps fatal(1) > human-review(2) | Artifact-failure-only root can't leave catalog at 0. |
| Immediate `$LASTEXITCODE` capture | `start-with-logs.ps1`: `$namespaceExitCode = $LASTEXITCODE` immediately after `& $bashPath` **before** the output-move block; failure decision uses `$namespaceExitCode` | Fixes the clobber-by-move-block bug. |
| `--skip-build` gated on confirmed build (not loop position) | `$sharedBuildConfirmed` set true only when a namespace built (omitted `--skip-build`) **and** exited 0; `--skip-build` added `if ($sharedBuildConfirmed)` | Replaces `if ($index -gt 0)`. |
| Explicit `--skip-deps`; never converts a failed selected dep to success | Suppression only over `SelectedTransitiveDependents` (∩ `selectedIds`); `--skip-deps` planning path unchanged (`PipelineCli.cs` not in diff) | Tests T18, T37; mutations M15/M16. |
| Separate success / root / warning / suppressed / cascade / unclassified summaries | `WriteRunAccounting` + `WriteRunAccountingSummary` (six labeled lines; cats 5–6 read once, never summed); PS catalog aggregation mirrors it | Live cats 1–4; baseline cats 5–6 from frozen manifest. |

## 5. Gate-to-evidence mapping (every named gate → real test + captured evidence)

| Deterministic gate (tracker item 2) | Named test(s) | Evidence |
|---|---|---|
| Full registry graph | `Registry_ReverseAdjacency_MatchesRealStepRegistryEdges`, `StepDoubleRegistry_MirrorsRealStepRegistryGraph`, `SelectedTransitiveDependents_FatalStep{1,2,3,4}_Suppresses_*` | `green-run.txt`, `green-addendum-b.txt` |
| Fatal / warning / global behavior | `RunAsync_GlobalFatalStep0_AbortsCatalog_NoNamespaceRuns`, `RunAsync_WarnStepFails_DoesNotSuppressDependents_AndCatalogSucceeds`, `RunAsync_WarnStep_RealShapeArtifactFailure_DoesNotSuppressDependents`, `RunAsync_FatalStep2_SuppressesDependents_…` | `green-run.txt` |
| No dependent side effects after suppression | `RunAsync_SuppressedStep_EmitsNoCriticalFailureJson_ButRootDoes`, `RunAsync_RealShapeRoot_SuppressedFailingDependent_EmitsNoDependentJson` | `green-addendum-b.txt` |
| Later independent namespaces | `RunAsync_FatalNamespaceStep_LaterNamespaceStillExecutes`, `RunAsync_FatalStep2_SuppressesDependents_RunsIndependentStep6_AndContinues` | `green-run.txt` |
| Retry / resume / cancel / same-workspace rerun | `RunAsync_CancellationRequested_StopsRemainingNamespaces`, `RunAsync_RerunSameOutputPath_SuppressedEnvelopeOverwrittenOnCleanRerun`, `RunAsync_SuppressedStep_WritesCanonicalEnvelope_OverwritingStaleSuccess` (suppressed = no retries) | `green-addendum-b.txt` |
| Mutation / revert proof | 42 rows M1–M42 (incl. M35 D1, M39 D3, M40 D4, M6 F1) | `mutation-proof.txt` (Ellis physically re-ran 4) |
| Real-shape resolution (BLOCKING-1/-2) | `Beta34Corpus_EveryStep2Failure_IsFatalRoot_EvenWhenSuccessTrue`, `RunAsync_RealShapeFatalRoot_…`, `IsFatalRoot_TruthTable_…`, `RunAsync_PreAiNonFatalSkip_EmptyArtifactFailures_IsNotARoot` | `red-addendum-b.txt` → `green-addendum-b.txt` |
| Pester case-insensitive param/local collision (AD-027) | `It "declares no param or local variable whose name collides case-insensitively (AD-027)"` + `It "captures the namespace exit code immediately after start.sh…"` | `green-pester.txt` |

All 9 evidence files present and substantial (6.5–72 KB), covering RED→GREEN, mutation, Pester, and baseline-disjointness.

## 6. Reviewer independence & lockout — honored

- **Design → strategy → tests → impl → scripts → docs** each by a distinct agent: Riley (AD-029 + AMENDMENT 1), Cameron (strategy; matrix header explicitly states *"Cameron is reviewer-locked out of writing these tests. Implementer: Parker."*), Parker (tests), Morgan (round-1 runtime `668318d`), Quinn (`start-with-logs.ps1`), Reeve (docs `df9e2fd`/`982b331`).
- **Ellis** returned **FAIL** round 1 and **PASS** round 2, re-derived from primary sources (code, fixtures, live runs). Independence invariant recorded in `casting-registry.json`: authors no implementation/tests/fixtures/scripts/docs for anything Ellis evaluates. Read in full.
- **Morgan locked out** of remediating his own rejected runtime; guest **Rowan** shipped the fix (`4dea086`). `casting-registry.json` records Rowan authored *none* of the Step 2 design/tests/impl/scripts/docs. The fix commit body itself states *"No validation weakened, no test edited, no new pipeline phase."*
- **No one approved their own work.** Ellis (evaluator) authored no implementation; Rowan (fixer) does not hold the evaluation gate; I (approver) authored none of the change.

## 7. AD-026 (CHANGELOG) + documentation gate — describes the SHIPPED behavior

The `## [Unreleased] → ### Changed` entry describes the **round-2 shipped** contract, not the rejected exit-code-only first attempt. It explicitly states a step is a fatal root when *"its mapped exit code is nonzero **or** it recorded one or more per-artifact failures — crucially, artifact failures make a step a root even when the step itself reports `success` and maps to exit `0`."* It also documents the full-reverse-graph traversal through unselected intermediates (D3) and the canonical+observability envelope overwrite (D4). README + ARCHITECTURE.md + START-SCRIPTS.md updated consistently. Reeve PASS (round 2 after the behavior changed). ✔

## 8. Independently re-run gate numbers (not taken on faith)

```
dotnet build mcp-doc-generation.sln --configuration Release
  → Build succeeded. 0 Warning(s) / 0 Error(s)

dotnet test  mcp-doc-generation.sln --no-build --configuration Release
  → Failed: 1, Passed: 3682, Skipped: 1  (Total 3684 across 23 assemblies)
  → the ONE failure is the known pre-existing:
    DocGeneration.Steps.ToolFamilyCleanup.Tests.FamilyMetadataGeneratorTests
      .GenerateAsync_WhenAiResponseIsTruncated_UsesFallbackDescription
    (Assert.Contains() Failure: Sub-string not found) — in a project Step 2 does not touch.

Invoke-Pester -Path ./mcp-tools/validation/tests -Output None -PassThru
  → Passed: 118, Failed: 8, Skipped: 1  (Total 127)
  → all 8 failures in the pre-existing Scan-McpToolCoverage.Tests.ps1.
```

Every number matches the expected outcome and Ellis's round-2 recompute exactly. **No unexpected failure. No blocker.**

## 9. Ruling on Ellis's two NEW round-2 findings

**NEW-1 (un-gated Step-3 reducer-fallback path becomes a C2 fatal root) — DEFERRED, does NOT block item 2.**
Rationale: (a) it is **fail-closed** — the opposite direction from this gate's fail-open subject; it cannot reintroduce a missed cascade; (b) it is **corpus-absent** — beta.34 has **zero** Step-3 records, so it does not affect the 10/10 cascade resolution or any beta.34 acceptance criterion; (c) it is **defensible** and consistent with C2's intent (a `Fatal` step that persisted an artifact-failure record did not cleanly succeed); (d) the entangled behavior — Step-3 typed generation-failure semantics — is explicitly **item 4** territory (AD-029 §8 non-goal), so fixing it here would itself be scope creep. It is nonetheless a real, **untested, undocumented** behavioral change and must not be lost. → **Tracked as a follow-up under #813** (single-tracking-issue rule), to be resolved when **item 4** (typed Step 2/3 generation-failure result) is implemented, and in any case **before item 10's full-catalog run** if that run can produce Step-3 records. Ellis's menu is the right one: either route Step 3's pre-AI failure through the empty-`ArtifactFailures` gate shape like Steps 4/6, or add a test + doc line affirming the intentional root.

**NEW-2 (ARCHITECTURE.md "pre-AI skip = empty AF" dichotomy incomplete for in-step reducer paths) — DEFERRED, does NOT block item 2.**
The shipped ARCHITECTURE.md is accurate for the external `TryRunPreAiGateAsync` gate and for every path the beta.34 corpus/tests exercise; the incompleteness only concerns the same un-gated in-step reducer paths as NEW-1. → **Tracked under #813 alongside NEW-1**; Reeve adds the clarifying sentence when NEW-1 is addressed.

Neither finding undermines this gate's subject (missed cascades against real orchestration), which is affirmatively resolved 10/10.

## 10. Approval is NOT merge authority

This APPROVED verdict authorizes marking tracker **item 2** `VERIFIED`/`[x]`. It is **not** authority to merge. **Only the repository owner (Dina) merges PRs.** **PR #815 must remain OPEN and UNMERGED.** I have not merged it and will not; verified state OPEN / `mergedAt: null` at sign-off.

## 11. Conditions / follow-ups attached to this approval

1. **PR #815 stays open** until the owner merges after review — non-negotiable.
2. **NEW-1** tracked under #813, resolved with **item 4** (or before any full-catalog run producing Step-3 records). Do **not** fix it under item 2.
3. **NEW-2** doc precision tracked with NEW-1 (Reeve).
4. **NON-BLOCKING-2 (accounting readability)** carried forward: ARCHITECTURE.md now separates live cat-4 from historical cat-5; the console constant *"Cascades imported from historical fixtures (N)"* remains — keep the live/historical distinction visible so cat-5 is never misread as live suppression. Non-gating.
5. The one pre-existing xUnit failure (`FamilyMetadataGeneratorTests…UsesFallbackDescription`) and the 8 pre-existing `Scan-McpToolCoverage` Pester failures are **out of scope** for item 2 but remain open debt on their owning projects.

---

**Bottom line:** Item 2 is scoped exactly to plan item 1, honors every prohibition, keeps the frozen fixtures byte-identical, satisfies both subordinate gates with real named tests and captured evidence, was built through a properly locked-out multi-agent process with an independent two-round evaluation, documents the shipped behavior, and reproduces its build/test/Pester numbers under my own hand. The two new findings are fail-closed, corpus-absent, and correctly deferred. **APPROVED.**

↩︎ Responding to: "You are **Avery, the Team Lead** … Your gate: scope + final acceptance for issue #813, tracker item 2 … Write `.squad/agents/avery/813-step2-acceptance.md` … Then commit and push it, referencing #813."
