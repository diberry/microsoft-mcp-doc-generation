# QA Review — Step 3 Canonical Parameter Contract (PR #816)

**Reviewer:** Parker (QA/Tester)  
**Date:** 2026-08-15  
**Branch:** `squad/813-step3-canonical-parameter-contract` @ `ee3ca02`  
**Base:** `b58431f`

## Environment

| Tool | Version |
|------|---------|
| .NET SDK | 10.0.303 |
| PowerShell | 7.6.5 |
| Git | 2.55.0.vfs.0.3 |
| OS | Windows_NT |

## Commands Executed

### 1. Build (branch)
```
dotnet build mcp-doc-generation.sln --configuration Release
→ Exit code: 0 | 0 Warnings, 0 Errors | 20.88s
```

### 2. xUnit Tests (branch) — 3 consecutive runs
| Run | Passed | Failed | Skipped | Exit |
|-----|--------|--------|---------|------|
| 1   | 3,801  | 1      | 1       | 1    |
| 2   | 3,801  | 1      | 1       | 1    |
| 3   | 3,801  | 1      | 1       | 1    |

**Single failure (all 3 runs):** `DocGeneration.Steps.ToolFamilyCleanup.Tests.FamilyMetadataGeneratorTests.GenerateAsync_WhenAiResponseIsTruncated_UsesFallbackDescription`  
**Single skip:** `DocGeneration.Baseline.Beta34.Tests.ImmutabilityTests.T4b_DeepVerify_LiveSourceRun_Hashes_Match_Inventory`

### 3. Pester Tests (branch)
```
pwsh -NoProfile -Command "Invoke-Pester -Path ./mcp-tools/validation/tests -Output Detailed -CI"
→ Exit code: 8 | Passed: 118, Failed: 8, Skipped: 1
```
All 8 failures in `Scan-McpToolCoverage.Tests.ps1`:
1. finds only one compute tool (compute vm list) in azure-compute.md
2. returns empty for an undocumented tool (no marker in content)
3. script detects annotation mismatches for documented tools
4. annotation mismatch is recorded for compute vm list (Open World)
5. undocumented tool (compute vm start) has no annotations checked
6. returns empty hashtable for tool marker absent (Get-DocumentedAnnotations unit)
7. compute namespace has one missing tool (compute vm start)
8. annotation mismatch detected for compute vm list (Open World)

### 4. Build & Test (base `b58431f`)
```
dotnet build mcp-doc-generation.sln --configuration Release → Exit code: 0
dotnet test mcp-doc-generation.sln --no-build --configuration Release → Exit code: 1
```
**Base totals:** 3,682 passed, 1 failed, 1 skipped (same single failure + skip as branch)

**Base Pester:** 118 passed, 8 failed, 1 skipped (identical 8 failures)

## Regression Disjointness

| Metric | Base | Branch | Delta |
|--------|------|--------|-------|
| xUnit Passed | 3,682 | 3,801 | +119 |
| xUnit Failed | 1 | 1 | 0 |
| Pester Passed | 118 | 118 | 0 |
| Pester Failed | 8 | 8 | 0 |

**No new failures introduced.** The single xUnit failure (`GenerateAsync_WhenAiResponseIsTruncated_UsesFallbackDescription`) and 8 Pester failures are pre-existing at base. ✅

## Total-Count Discrepancy Explanation

The true number on branch is **3,801 passed** (3,803 total including 1 failed + 1 skipped).

The implementation evidence claim of "~3,803" counts total tests. The "~3,604" figure from another reviewer likely excludes the `skills-generation/` solution (which is a separate `.slnx` file NOT included in `mcp-doc-generation.sln`). There is no skills-generation suite in these runs.

The +119 new tests vs base break down by assembly:
- Core.Shared.Tests: 554 vs 470 (+84)
- ExamplePrompts.Generation.Tests: 332 vs 320 (+12)
- AnnotationsParametersRaw.Annotations.Tests: 379 vs 369 (+10)
- PipelineRunner.Tests: 680 vs 675 (+5)
- ExamplePrompts.Validation.Tests: 16 vs 11 (+5)
- ToolFamilyCleanup.Tests: 977 vs 974 (+3)

## Determinism (3 consecutive runs)

All three runs produced **identical** pass/fail sets: 3,801 passed, 1 failed (same test), 1 skipped (same test). **No flakes detected.** ✅

## Behavioral Spot-Checks

All performed independently via a throwaway `parker-spot-check` console project referencing production types directly.

| # | Check | Result |
|---|-------|--------|
| SC1a | `<account>` → AuthorizedPlaceholder | PASS ✅ |
| SC1b | `<account_name>` → AuthorizedPlaceholder | PASS ✅ |
| SC1c | `App Configuration store <app_config_store_name>` → Missing | PASS ✅ |
| SC2 | Legacy bare-array rejected with `PARAM_MANIFEST_LEGACY_FORMAT` | PASS ✅ |
| SC3 | Missing manifest rejected with `PARAM_MANIFEST_NOT_FOUND` | PASS ✅ |
| SC4a | Already-covered prompt is byte-identical after repair | PASS ✅ |
| SC4b | Second repair pass is byte-identical (idempotent) | PASS ✅ |

**SC3 validator exit code:** Confirmed via code inspection — `Program.cs:267` returns `invalid > 0 ? 1 : 0`, and `ParameterManifestException` increments `invalid` (line 192). Nonzero exit guaranteed.

## Anti-Gaming Search

### Search: `Skip =`, `[Ignore]`, `Assert.True(true`, empty test bodies

**Results from `git diff b58431f..ee3ca02`:**

| File | Line | Finding | Assessment |
|------|------|---------|------------|
| `shared/DocGeneration.Core.Shared.Tests/Round3CanonicalContractTests.cs` | 153 | `Assert.True(true, "Structural check deferred to integration test below");` | **Tautological assertion.** The preceding code (lines 145-148) computes `referencesManifestException` via reflection but never asserts on it. The test always passes regardless of implementation. |
| `mcp-tools/DocGeneration.E2E.Tests/GeneratedOutputTests.cs` | 181 | `Assert.True(true, ...)` | Pre-existing at base — NOT new. |
| `mcp-tools/DocGeneration.PipelineRunner.SmokeTests/PipelineSmokeTests.cs` | 174 | `Assert.True(true);` | Pre-existing at base — NOT new. |

**No tests deleted, quarantined, or `[Skip]`-ed on this branch.** No empty test bodies added. One tautological test confirmed (previously reported by another reviewer).

## Findings

| # | Severity | Finding |
|---|----------|---------|
| 1 | **Low** | `Round3CanonicalContractTests.cs:153` — tautological `Assert.True(true, ...)`. The reflection variable `referencesManifestException` is computed but never asserted on. Test passes unconditionally. This is not blocking because the behavioral coverage for manifest error handling is verified by other tests (`CanonicalParameterManifestLoaderTests`, `ExamplePromptsStep_LoadRequiredOptions_MissingManifest_MustThrow`) which DO have real assertions. |

## Verdict

**APPROVE WITH NOTES**

The branch adds 119 well-structured tests, introduces no regressions, is deterministic across 3 runs, and all behavioral spot-checks pass independently. The single tautological assertion (Low severity) does not weaken overall coverage since the behavior it purports to test is covered by other tests with real assertions.

---
↩︎ Responding to: "You are **Parker**, QA / Tester. You hold the **independent execution / regression approval seat** for Step 3 of issue diberry/microsoft-mcp-doc-generation#813..."
