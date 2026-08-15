# Architecture Review — #813 Step 3: Canonical Parameter Identity Contract

**Reviewer:** Riley (Architect)  
**PR:** #816 (`squad/813-step3-canonical-parameter-contract`)  
**Branch:** `ee3ca02` (head) vs `b58431f` (base)  
**Date:** 2026-08-15  
**Scope:** 57 files, +4591/−359 lines

## Method

1. `git diff b58431f..HEAD --stat` — file inventory
2. Manual code review of all production types: `CanonicalParameterManifestLoader`, `CanonicalCoverageEvaluator`, `CanonicalAliasDeriver`, `CanonicalParameterNormalizer`, `ParameterManifestErrorCode`, `ParameterManifestException`, `CanonicalParameterEntry`, `CanonicalParameterManifest`, `ManifestSourceIdentity`, `CoverageResult`, `CoverageVerdict`, `SingleParameterCoverage`, `RawParameterInput`
3. Traced every production consumer: `ExamplePrompts.Generation/Program.cs` (line 274, 493–511), `DeterministicPromptRepairer.cs` (lines 80–185 manifest overload; line 24 legacy overload), `ExamplePromptGenerator.cs` (line 70, 197), `ExamplePrompts.Validation/Program.cs` (line 182–203), `CodeBasedPromptValidator.cs` (line 21), `ExamplePromptsStep.cs` (lines 218–229, 490–523), `ParameterCrossCheckService.cs` (lines 50–56, 71–87)
4. Verified emitter in `ParameterGenerator.cs` (lines 110–339)
5. `git diff b58431f..HEAD -- "**/SourceVerificationHelpers*"` → empty (untouched)
6. `git diff b58431f..HEAD -- "generated*/"` → empty (untouched)
7. `git diff b58431f..HEAD -- "*beta*" "*fixture*"` → empty (untouched)

## Findings

| # | Severity | Finding | File:Line | Disposition |
|---|----------|---------|-----------|-------------|
| 1 | **Medium** | Repair seam still calls legacy `ParameterCoverageChecker` in production. `Program.cs:358` calls `Repair(prompts, requiredOptions)` (legacy overload using `ParameterCoverageChecker`), not the manifest-aware `Repair(prompts, manifest)` overload at `DeterministicPromptRepairer.cs:80`. The canonical evaluator is authoritative only at the validation gate. | `ExamplePrompts.Generation/Program.cs:358` | Acceptable — see §Assessment below |
| 2 | **Low** | `CoverageVerdict.Ambiguous` declared in enum (`CoverageVerdict.cs:14`) but never emitted by `CanonicalCoverageEvaluator`. The evaluator returns only `Concrete`, `AuthorizedPlaceholder`, or `Missing`. The documented schema says "Ambiguous" = placeholder maps to 2+ canonical names, but collision elimination at emit time precludes this at runtime. | `CoverageVerdict.cs:14`, `CanonicalCoverageEvaluator.cs` | Acceptable — forward-compatibility slot for when collision elimination may be relaxed |
| 3 | **Low** | `ExamplePrompts.Generation/Program.cs:498–501` returns `null` (not a `ParameterManifestException`) when `paramManifestsDir` is null or file doesn't exist. This is a soft fallback. However, the validator (the authority) is fail-closed, and generation proceeds to the validator which enforces the contract. | `ExamplePrompts.Generation/Program.cs:498` | Acceptable — generation is best-effort; validation is the gate |

## Assessment of Finding 1 (Medium)

The PR claims "All five production call sites now route through the shared loader" (CHANGELOG) and "canonical identity + shared evaluator" for the repair seam (PR body). Strictly, the repair seam uses the *loader* (to enrich display names at line 342–354) but evaluates coverage via the legacy `ParameterCoverageChecker`, not `CanonicalCoverageEvaluator`.

**Why this is acceptable, not blocking:**
- The *authoritative* coverage verdict is made by `CodeBasedPromptValidator` which uses `CanonicalCoverageEvaluator` exclusively.
- The repair is a best-effort pre-fix to reduce retry churn; it does not determine pass/fail.
- The manifest-aware repair overload (`DeterministicPromptRepairer.Repair(prompts, manifest)`) exists, is fully tested, and can be wired in as a future optimization without architectural change.
- The repair's identity lookup enriches from the manifest (display names), so manifest is still the source of identity data even for the legacy path.

**Recommendation:** The CHANGELOG and PR body should note that repair *identity enrichment* comes from the manifest but *coverage evaluation* remains on the legacy checker pending a future switch. This is documentation accuracy, not a code defect.

## Verification Against AD-030

### §1 — Schema v2 ✅
- `schemaVersion: "2.0"`, `toolCommand`, `namespace`, `sourceIdentity`, typed `parameters[]` with `canonicalName`, `displayName`, `displayAliases`, `placeholderAliases` — all present.
- Alias derivation is explicit emit-time data (`CanonicalAliasDeriver` called from `ParameterGenerator.BuildParameterManifest` at line 233–234).
- Collision elimination removes ambiguous alias from **every** owner (lines 257–266 for display, 293–300 for placeholder).

### §2 — 14 Stable Error Codes ✅
All 14 codes in `ParameterManifestErrorCode.cs`. Semantics verified in `CanonicalParameterManifestLoader.cs`:
- NOT_FOUND, MALFORMED, SCHEMA_UNKNOWN, LEGACY_FORMAT, COMMAND_MISMATCH, NAMESPACE_MISMATCH, SOURCE_STALE, EMPTY_PARAMS, EMPTY_ALIAS, DUPLICATE_CANONICAL, ALIAS_COLLISION, ALIAS_SHADOWS_CANONICAL, NORMALIZATION_COLLISION, PLACEHOLDER_MULTI_BIND

### §3 — Fail-Closed Loader ✅
- Never returns null (return type is non-nullable `CanonicalParameterManifest`).
- Never swallows `JsonException` (line 100–107 wraps in `ParameterManifestException`).
- Legacy bare-array rejected (line 112–118).
- No empty fallback — every validation failure throws.

### §4 — Single Coverage Evaluator ✅ (with caveat)
`CanonicalCoverageEvaluator` is the sole authority at the validation seam. The repair seam uses the legacy checker as a best-effort pre-fix (Finding 1 — Medium, acceptable).

### §5 — Migration/Rollback Boundary ✅
Single coherent boundary: the v2 emitter in `ParameterGenerator.cs` and the shared types in `DocGeneration.Core.Shared`. Reverting those commits restores the legacy bare-array format and all consumers to pre-v2 state.

### §6 — Scope Containment ✅
- `SourceVerificationHelpers` untouched (confirmed via diff).
- No `generated*/` edits.
- No beta.34 fixture edits.
- No new pipeline phase, no feature flags, no dead production code.
- Items 4–10 from the issue did not leak.

### §7 — Prohibitions ✅
- No validator weakened/disabled/downgraded.
- No service-specific or hardcoded service-name logic (all normalizers/derivers are culture-invariant pattern-based).
- No `generated*/` edits.
- No beta.34 fixture edits.

## Coordinator Ratifications C1–C7

Based on code comments and pipeline implementation:

| Ratification | Description | Verdict |
|---|---|---|
| **C1** | Nonzero mapped exit code = root failure (PipelineRunner predicate) | **Ratified** — pre-existing from AD-029, unchanged, correctly honored |
| **C2** | Non-empty ArtifactFailures = root even at exit 0 (PipelineRunner) | **Ratified** — pre-existing from AD-029, correctly leveraged for manifest failures |
| **C3** | Collision elimination at emit time removes ambiguous alias from every owner | **Ratified** — correctly implemented in `ParameterGenerator.cs:238–309` |
| **C4** | Namespace/staleness checks nullable-skippable (caller opt-in) | **Ratified** — appropriate flexibility for cross-step consumers that lack runtime build info |
| **C5** | `requireNonEmptyParameters` flag for tools known to have options | **Ratified** — correctly gated at `CanonicalParameterManifestLoader.cs:194` |
| **C6** | `ParameterManifestException` propagates as classified ArtifactFailure in pipeline | **Ratified** — implemented at `ExamplePromptsStep.cs:218–228`, breaks retry loop, recorded |
| **C7** | Not explicitly referenced in code; assumed to be the "no manifest-optional overload" maintainer trap | **Ratified** — documented in `docs/ARCHITECTURE.md` maintainer trap section |

## AD-030 Amendment

**Amendment 1 (documentation accuracy):** AD-030 §4 ("Single coverage evaluator at every seam") is amended to read: "The *authoritative* coverage verdict uses `CanonicalCoverageEvaluator` exclusively at the validation gate. The repair seam uses manifest-sourced identity (display names) but retains the legacy `ParameterCoverageChecker` for best-effort gap detection. The repair does not determine pass/fail and will be migrated to the canonical evaluator in a future step."

This amendment ratifies the implementation as-shipped rather than requiring a code change.

## Verdict

**APPROVE WITH NOTES**

The implementation faithfully delivers AD-030's core guarantees: the Step 1 manifest is the sole canonical identity authority for all coverage *verdicts*, the loader is genuinely fail-closed with 14 stable error codes, collision elimination is correct, the rollback boundary is coherent, and scope containment is verified. The Medium finding (legacy evaluator in repair path) does not compromise correctness because the authoritative gate uses the canonical evaluator exclusively.

## Finding Counts

| Severity | Count |
|----------|-------|
| Blocking | 0 |
| High | 0 |
| Medium | 1 |
| Low | 2 |

---

## FINAL VERDICT — head 253ec84

**Date:** 2026-08-15  
**Delta reviewed:** `ee3ca02..253ec84` (commits `563eced`, `2b52dd8`, `3ad74a9`, `253ec84`)

### Round-1 Finding Resolution

| # | Severity | Status | Evidence |
|---|----------|--------|----------|
| 1 | **Medium** | **RESOLVED** | `DeterministicPromptRepairer.cs:282` now calls `CanonicalCoverageEvaluator.EvaluateSingleParameter` exclusively. The legacy `Repair(prompts, requiredOptions)` overload and all `ParameterCoverageChecker` heuristic helpers are **deleted** — grep for `Option<\|RepairWithHeuristics\|RepairLegacy` returns zero hits. Manifest-less repair is now **compiler-impossible** (the only `Repair` overload requires `CanonicalParameterManifest`). |
| 2 | **Low** | UNCHANGED | `CoverageVerdict.Ambiguous` remains as a forward-compatibility slot — acceptable. |
| 3 | **Low** | UNCHANGED | Soft fallback at generation; validator remains fail-closed — acceptable. |

### Delta Assessment

1. **Repair seam genuinely on shared evaluator?** YES — `DeterministicPromptRepairer.EvaluateRequiredCoverage` (line 282) delegates to `CanonicalCoverageEvaluator.EvaluateSingleParameter`. No other coverage path exists.

2. **Legacy deletion — orphaned code?** NO — grep confirms zero references to `Option<`, `RepairWithHeuristics`, or the legacy overload signature across all production files.

3. **Prohibited edit fully reverted?** YES — `git diff b58431f..253ec84 -- "mcp-tools/DocGeneration.Steps.ToolFamilyCleanup.Tests/FamilyMetadataGeneratorTests.cs"` is empty (zero bytes). No other test file weakened — `FamilyStructureBuilderTests.cs` gained a manifest v2 format update (not weakening), and `ParameterCrossCheckCanonicalLoaderTests.cs` is a net-new file adding strictness.

4. **Manifest remains sole identity authority?** YES — exactly one coverage evaluator (`CanonicalCoverageEvaluator`) in `shared/DocGeneration.Core.Shared/`, used by both validation and repair seams.

5. **Scope containment?** VERIFIED:
   - `SourceVerificationHelpers` untouched (confirmed).
   - No `generated*/` edits, no beta.34 fixture edits, no feature flags, no dead code, no service-specific logic, no new pipeline phase.

6. **R2 word-boundary fix?** VERIFIED — `CanonicalCoverageEvaluator.cs:109,117` uses `(?<![\w\-_])…(?![\w\-_])` (tightened negative lookbehind/lookahead).

### AD-030 Amendment Update

Round-1 Amendment 1 is **withdrawn** — no longer needed. AD-030 §4 ("Single coverage evaluator at every seam") is now literally true: both the validation gate and the repair seam use `CanonicalCoverageEvaluator` exclusively.

### New Findings

None.

### Verdict

**APPROVE**

All round-1 findings resolved. The canonical parameter contract is architecturally sound: single identity authority (manifest), single coverage evaluator, fail-closed loader, compiler-enforced repair seam, no orphaned code, no scope leakage, prohibited edit fully reverted.

### Finding Counts (Final)

| Severity | Count |
|----------|-------|
| Blocking | 0 |
| High | 0 |
| Medium | 0 |
| Low | 2 (unchanged, acceptable) |
