# Reeve — Documentation Review: #813 Step 3 Canonical Parameter Contract

**PR:** https://github.com/diberry/microsoft-mcp-doc-generation/pull/816  
**Branch:** `squad/813-step3-canonical-parameter-contract`  
**Head:** `ee3ca02` | **Base:** `b58431f`  
**Reviewer:** Reeve (Documentation Engineer)  
**Date:** 2026-08-15

---

## AD-026 Compliance Checklist

| Requirement | Status |
|---|---|
| Entry under `## [Unreleased]` | ✅ Present in `### Changed` |
| Describes behavior change | ✅ Full description of manifest schema, loader, evaluator, consumers |
| Breaking change stated | ✅ "Breaking: regenerate Step 1 output" clearly stated |
| Correct section (Changed) | ✅ |
| PR/issue numbers | ✅ `#813, Step 3` |

**Verdict:** AD-026 fully satisfied.

---

## File-by-File Assessment

### `CHANGELOG.md` (lines 10–23)

**Assessment: PASS**

Comprehensive entry covering: schema v2, alias derivation, fail-closed loader with 14 error codes, coverage evaluator with four verdicts, bounded repair, consumer migration (five seams), `--parameter-manifests-dir` argument, and the breaking regeneration requirement. Correctly placed under `### Changed` in `## [Unreleased]`.

### `docs/ARCHITECTURE.md` (lines 59, 650–732)

**Assessment: PASS**

New section "Canonical Parameter Identity and Manifest Contract (AD-030)" at line 650 covers:
- ✅ Schema v2 with realistic JSON example
- ✅ Emit-time alias derivation and collision elimination
- ✅ Strict fail-closed loader with all 14 error codes in validation order
- ✅ Coverage evaluator with four verdicts table
- ✅ Consumer seam map (5 seams)
- ✅ Bounded repair contract (at most one clause per missing param)
- ✅ Rollback boundary (single commit revert)
- ✅ **Maintainer trap** explicitly stated: manifest-optional overload or `catch (JsonException) { return empty; }` re-opens fail-open
- ✅ **SourceVerificationHelpers note** — explicitly states heuristic is deliberately retained and replaced in next step

Step 1 data flow (line 59) updated to show `parameters/{tool}-params.json (v2 canonical manifests)`.

### `mcp-tools/DocGeneration.Steps.AnnotationsParametersRaw.Annotations/README.md`

**Assessment: PASS**

- Added manifest output in architecture diagram
- Added "V2 canonical parameter manifests" bullet explaining `BuildParameterManifest`, alias derivation, and downstream consumption via `CanonicalParameterManifestLoader`

### `mcp-tools/DocGeneration.Steps.ExamplePrompts.Generation/README.md`

**Assessment: PASS**

Updated repair section to reference `CanonicalCoverageEvaluator` and manifest-authorized `placeholderAliases`. Correctly removes mention of "display-name-based last-resort fallback" and replaces with the bounded-clause contract.

### `mcp-tools/DocGeneration.Steps.ExamplePrompts.Validation/README.md`

**Assessment: PASS**

- Purpose updated: now loads v2 manifest via loader, uses evaluator for verdicts
- CLI arguments rewritten with `--parameter-manifests-dir` documented
- Nonzero exit on manifest failure documented
- Old positional arguments removed

### `mcp-tools/DocGeneration.Steps.ToolFamilyCleanup/README.md`

**Assessment: PASS**

Updated Phase 1.5 bullet: `ParameterCrossCheckService` now loads via `CanonicalParameterManifestLoader`; `ParameterManifestException` propagates — no silent fallback.

### `shared/DocGeneration.Core.Shared/README.md`

**Assessment: N/A** — File does not exist (confirmed `Test-Path` = False). This is a pre-existing state; the project has never had a README. The contract is thoroughly documented in `docs/ARCHITECTURE.md`. See Medium finding below.

### `mcp-tools/DocGeneration.PipelineRunner/README.md`

**Assessment: N/A** — File does not exist (confirmed). Pre-existing state. Change to `ExamplePromptsStep.cs` is internal wiring (loader swap); behavior is documented in ARCHITECTURE.md consumer seam map.

### `docs/START-SCRIPTS.md` and `README.md`

**Assessment: PASS** — Correctly unchanged. `git diff b58431f..HEAD` shows zero changes to either file. No script-visible behavior, prerequisite, navigation path, or output structure changed: the v2 manifest is a new file emitted alongside existing `parameters/*.md`, consumed internally by later steps. No new CLI flags on `start.sh` or `start-only.sh`. The `--parameter-manifests-dir` flag is on the validator CLI, already documented in its own README.

---

## Command and Path Verification Table

| Doc reference | Claimed path/command | Verified against source | Status |
|---|---|---|---|
| ARCHITECTURE.md:59 | `parameters/{tool}-params.json` | `ParameterGenerator.cs` emits to parameters dir | ✅ |
| ARCHITECTURE.md:656–677 | JSON schema example | Matches `CanonicalParameterManifest.cs` + `CanonicalAliasDeriver.cs` output | ✅ |
| ARCHITECTURE.md:680 | `ParameterGenerator.BuildParameterManifest` | Method exists in `ParameterGenerator.cs` | ✅ |
| ARCHITECTURE.md:684 | `CanonicalParameterManifestLoader` in `DocGeneration.Core.Shared` | File at `shared/DocGeneration.Core.Shared/CanonicalParameterManifestLoader.cs` | ✅ |
| ARCHITECTURE.md:693 | 14 error codes (8 structural + 6 named + implicit split) | `ParameterManifestErrorCode.cs` has exactly 14 `public const` fields | ✅ |
| ARCHITECTURE.md:699 | `CanonicalCoverageEvaluator` in `DocGeneration.Core.Shared` | File at `shared/DocGeneration.Core.Shared/CanonicalCoverageEvaluator.cs` | ✅ |
| ARCHITECTURE.md:714 | `ExamplePrompts.Generation/Program.cs` uses `LoadAsync` | Confirmed via grep | ✅ |
| ARCHITECTURE.md:715 | `DeterministicPromptRepairer` uses `EvaluateParameterCoverage` | File at `ExamplePrompts.Generation/Generators/DeterministicPromptRepairer.cs` | ✅ |
| ARCHITECTURE.md:717 | `CodeBasedPromptValidator` uses evaluator | Line 21 calls `CanonicalCoverageEvaluator.EvaluateParameterCoverage` | ✅ |
| ARCHITECTURE.md:718 | `ParameterCrossCheckService` uses `LoadAsync` | File uses `CanonicalParameterManifestLoader` | ✅ |
| ARCHITECTURE.md:732 | `SourceVerificationHelpers` still uses heuristic | File exists at `PipelineRunner/Validation/SourceVerificationHelpers.cs` | ✅ |
| Validation README:83–85 | `--parameter-manifests-dir ../generated/parameters` | `Program.cs:49` parses `--parameter-manifests-dir` | ✅ |
| Annotations README:62 | `CanonicalAliasDeriver` | File at `shared/DocGeneration.Core.Shared/CanonicalAliasDeriver.cs` | ✅ |

---

## Consistency Check

| Source | Claim | Docs | Match? |
|---|---|---|---|
| AD-030 §7.1 | References `RepairSinglePrompt` method | Docs do NOT reference this non-existent method | ✅ (docs correct, AD-030 has aspirational name) |
| AD-030 §1.1 | `displayAliases: ["account-name", "account name"]` | ARCHITECTURE.md shows `["account-name", "account"]` | ✅ (docs match actual code — `Normalize()` converts spaces to hyphens) |
| AD-030 §6.3 | 6 call sites listed | Docs list 5 consumer seams (excludes SourceVerificationHelpers "seam only") | ✅ (correctly reflects implementation; SourceVerificationHelpers is noted separately) |
| PR description | States fail-closed, no empty fallback | Docs state same | ✅ |

---

## Findings

### Medium: `DocGeneration.Core.Shared` lacks a README

**File:** `shared/DocGeneration.Core.Shared/` (no README.md)  
**Severity:** Medium  
**Rationale:** This project gained 12 new public files (loader, evaluator, normalizer, deriver, models, error codes, exception). The copilot-instructions state every project should have a README. However, this is a **pre-existing omission** (the project never had one) and the full contract is documented in `docs/ARCHITECTURE.md`. Not blocking for this PR, but should be addressed as follow-up.

### Low: ARCHITECTURE.md "14 codes" — count is implicit, could be explicit

**File:** `docs/ARCHITECTURE.md:693`  
**Text:** `Structural checks: PARAM_MANIFEST_EMPTY_PARAMS, PARAM_MANIFEST_EMPTY_ALIAS, ...`  
**Severity:** Low  
**Rationale:** The CHANGELOG says "14 codes" but ARCHITECTURE.md lists them split across items 1–7 (7 individual) + item 8 (7 grouped). A maintainer must count manually. Consider adding "(14 codes total)" parenthetical. Non-blocking.

---

## Contract Clarity Assessment

**Could someone who has never seen this work correctly:**

1. **Regenerate manifests?** — Yes. ARCHITECTURE.md states Step 1 emits them; the breaking-change note in CHANGELOG says "rerun Step 1"; `start.sh` with step 1 is documented in START-SCRIPTS.md.

2. **Diagnose a `PARAM_MANIFEST_*` failure?** — Yes. Error codes are listed in validation order with clear triggering conditions and the action "Rerun Step 1" is noted for legacy format. The exception type and propagation path are documented.

3. **Understand why a placeholder was or was not authorized?** — Yes. The coverage evaluator section explains that only `placeholderAliases` entries (after Normalize) count; generic similarity is explicitly excluded. The four verdicts are defined with clear criteria.

---

## Verdict

**APPROVE WITH NOTES**

| Severity | Count |
|---|---|
| Blocking | 0 |
| High | 0 |
| Medium | 1 |
| Low | 1 |

All required documentation is present, accurate, and consistent with shipped code. The CHANGELOG correctly describes the breaking change. The ARCHITECTURE.md section covers the full contract comprehensively. No contradictions with AD-030 or the source code. The two notes are non-blocking quality improvements.
