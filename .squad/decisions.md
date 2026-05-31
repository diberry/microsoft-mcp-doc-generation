# Squad Decisions

## Active Decisions

### 2026-05-30: Per-tool AI call refactor
**By:** Morgan
**What:** GenerateAIContent now calls AI once per tool + once for namespace summary, instead of once per namespace with all tools. Eliminates token overflow on large namespaces like storage.
**Why:** Storage namespace had 18k token input, exceeding limits. Per-tool calls are bounded to ~1 tool's data.

### 2026-05-30: Per-tool AI prompts created
**By:** Sage
**What:** Created horizontal-article-tool-system-prompt.txt, horizontal-article-tool-user-prompt.txt, horizontal-article-namespace-user-prompt.txt for per-tool AI calls.
**Why:** Namespace-level calls caused 18k token overflow on storage. Per-tool calls bound input to one tool.

### 2026-05-30: Empty namespace summary causes article failure
**By:** Morgan
**What:** Added validation check after AggregateAIData — if ServiceShortDescription or ServiceOverview is empty, fail the article with a clear error message rather than silently generating broken output.
**Why:** Rubber-duck review caught that the empty-fallback path produced valid-looking but content-corrupted articles that passed all validation gates.

### 2026-05-29: npm-to-dotnet CLI metadata migration completed (#627)
**By:** Quinn (DevOps) & Reeve (Documentation)
**What:** Removed all Node.js npm scripts from `mcp-cli-metadata/` (package.json, generate-report.js, validate-cli-output.js, etc.). The .NET `McpCliMetadata` tool is now the sole CLI metadata extractor. Updated CI workflows (update-azure-mcp.yml, test-azure-mcp-update.yml) to use Python for JSON validation instead of Node.js. Updated all documentation (README, ARCHITECTURE, CHANGELOG, copilot-instructions).
**Why:** The .NET replacement (PR #628, #631) is complete and integrated. Keeping npm scripts alongside created ambiguity and false security surface (npm audit on unused code). CI still installs `npm install -g @azure/mcp` to get the binary on PATH for the .NET tool to invoke.

### 2026-05-26: PRD #574 formalized and approved
**By:** Avery
**What:** Formalized issue #574 into a 17-dimension PRD artifact at `projects/azure-ai-tools/prds/2026-05-26T11-57-prd-574-validation-pipeline-integration.md` and completed a 6-round, 8-reviewer approval cycle to 8/8 approval.
**Why:** The integrated validation pipeline is a high-blast-radius feature. The final PRD now pins gate ownership to the pipeline, defines versioned validation contracts, rollout criteria, waiver rules, and test expectations tightly enough to guide implementation without reopening architecture questions.

### 2026-05-26: PRD #574 Phase 1 boundary and exit criteria
**By:** Avery
**What:** Phase 1 implements repo-local relocation of the existing validation scripts, fixtures, and Pester suites into `mcp-tools/validation/`, plus the minimum support surface needed to keep them usable and enforced (README, runbook, changelog, and CI execution of the relocated Pester suite). Phase 1 does **not** add pipeline wrapper code, PRD JSON-contract fields, placeholder-token detection, waiver logic, or gate verdict computation; those stay in Phases 2-4.
**Why:** The first week needs a clean, low-blast-radius landing of the already-tested deterministic validators before we start changing their runtime contracts. Moving the assets and making the suite runnable in CI gives Morgan, Quinn, and Cameron a stable base for wrapper work without mixing relocation risk with new validation semantics.

### 2026-05-26: Normalize PipelineRunner output paths before resolution
**By:** Avery
**What:** PipelineRunner now normalizes both `\\` and `/` in `PipelineRequest.OutputPath` before resolving it against the repo root.
**Why:** CI runs on Linux while many scripts and tests still pass Windows-style relative paths like `.\\generated`. Normalizing separators makes output and trace artifacts land in the intended directories on every platform instead of creating literal backslash path segments on Unix.

### 2026-05-26: PRD #574 Phase 1 review — BLOCKED (files not committed)
**By:** Morgan
**What:** BLOCKED. The branch `diberry/validation-pipeline-integration` is not mergeable. The entire Phase 1 deliverable — `mcp-tools/validation/` (scripts, tests, fixtures, README) and `docs/VALIDATION-RUNBOOK.md` — is untracked in the working directory and never committed. Only a Scribe housekeeping commit is on the branch. If merged, the `pester-tests` CI job would fail (path does not exist).
**Why:** `git status` confirms both paths are untracked. `git diff origin/main --name-only` shows only workflow/doc changes and `.squad/` files — no validation tree. Fix: `git add mcp-tools/validation/ docs/VALIDATION-RUNBOOK.md && git commit` before PR review.

### 2026-05-26: PRD #574 Phase 1 Doc Review — CONDITIONAL PASS
**By:** Reeve
**What:** Reviewed Phase 1 docs (diberry/validation-pipeline-integration). Verdict: CONDITIONAL PASS. Docs are substantively accurate; no wrong commands or overpromised behavior. Two non-blocking content issues must be fixed before commit; one process issue (nothing committed) blocks PR submission.
**Why:** Docs review passed. Content issues correctable; process blocker (untracked deliverables) is the same one Morgan identified.
**Notes:** Process blocker: nothing committed except Scribe housekeeping. Content issues: (1) RUNBOOK pre-creation instruction orphaned from command blocks — move mkdir note into each command example; (2) validation README uses Windows-only backslashes in Invoke-Pester path — change to forward slashes for cross-platform.

### 2026-05-27: Phase 1 #574 test review — REJECT (2 blocking findings)
**By:** Cameron
**What:** Reviewed validation test suite (Test-ArticleHealth.Tests.ps1, Scan-McpToolCoverage.Tests.ps1, fixtures/) on diberry/validation-pipeline-integration. Verdict: REJECT. Two AD-010 violations (vacuous test, zero regression coverage).
**Why:** Test review identified vacuous assertion and missing fixture. Per reviewer protocol, original author is locked out; fixes owned by another agent (Parker).
**Blocking findings:**
- BLOCKING-1 (ms.reviewer test): `$r | Should -BeIn @("warn", "fail")` is vacuous — test passes regardless of outcome. Fix: Pin to `$r | Should -Be "warn"`.
- BLOCKING-2 (markers.well-formed): Zero regression coverage — no bad-markers.md fixture, no test ever triggers the warn path. Fix: Add bad-markers.md fixture with malformed HTML comment and test that asserts `markers.well-formed` returns `"warn"`.

---

## Bug Fixes & Merges

**Author:** Morgan (C# Generator Developer)  
**Date:** 2026-05-19  
**Branch:** `squad/603-604-602-namespace-resolution-fixes`

---

## Summary

Fixed three interconnected bugs that caused the pipeline to fail for decomposed namespaces (e.g., `extension_azqr`, `extension_ghissues`) and when Step 3 is skipped.

---

## Bug #603 — ResolveFamilyName uses CLI prefix instead of raw namespace key

**Root cause:** `ResolveFamilyName()` in `ToolFamilyCleanupStep.cs` always took `tokens[0]` from the first CLI command (e.g., `"extension"` for `extension azqr scan`). The brand mapping keys use underscores (`extension_azqr`), so the lookup always missed.

**Files changed:**
- `mcp-tools/DocGeneration.PipelineRunner/Steps/Namespace/ToolFamilyCleanupStep.cs`  
  `ResolveFamilyName()` now checks `currentNamespace` against brand mappings first; falls back to CLI prefix only if no direct match.
- `shared/DocGeneration.Core.Shared/ToolFileNameBuilder.cs`  
  `ResolveFamilyFileName()` now tries `familyName.Replace(' ', '_')` as a secondary key when direct lookup fails.

**Tests added:** `ToolFamilyCleanupStepTests.Step4_UsesDecomposedNamespace_AsFamilyName_Bug603`,  
`ToolFileNameBuilderTests.ResolveFamilyFileName_SpaceInFamilyName_TriesUnderscoreKey_Bug603`,  
`ToolFileNameBuilderTests.ResolveFamilyFileName_SpaceKey_NoUnderscoreMapping_FallsBackToFamilyName_Bug603`

---

## Bug #604 — BrandMappingValidator rejects prefix-covered namespaces

**Root cause:** `Program.cs` in `DocGeneration.Steps.Bootstrap.BrandMappings` extracted the first token of each CLI command as the namespace (e.g., `"extension"`) and required an exact match in brand mappings. Decomposed entries like `extension_azqr` were never checked.

**Files changed:**
- `mcp-tools/DocGeneration.Steps.Bootstrap.BrandMappings/Program.cs`  
  After exact-match fails, checks if any brand mapping key starts with `ns + "_"`. If yes, the namespace is considered covered and excluded from unmapped list.

**Tests added:** `BrandMapperValidatorTests.Validator_ConsidersNamespaceCovered_WhenDecomposedEntriesExist_Bug604`,  
`BrandMapperValidatorTests.Validator_ReportsUnmapped_WhenNamespaceHasNoExactOrPrefixMatch_Bug604`

---

## Bug #602 — Step 4 fails when Step 3 is skipped (tools/ empty)

**Root cause:** Step 4 (`ToolFamilyCleanupStep`) hard-failed if `tools/` directory didn't exist or was empty, with no fallback. When Step 3 is skipped (no AI steps), `tools/` is never populated.

**Files changed:**
- `mcp-tools/DocGeneration.PipelineRunner/Steps/Namespace/ToolFamilyCleanupStep.cs`  
  Before failing, checks if `tools-raw/` exists and is non-empty; if so, uses it as the input directory. Logs `"INFO: Using tools-raw/ as fallback (tools/ not available)."`.

**Tests added:** `ToolFamilyCleanupStepTests.Step4_FallsBackToToolsRaw_WhenToolsDirectoryAbsent_Bug602`,  
`ToolFamilyCleanupStepTests.Step4_FallsBackToToolsRaw_WhenToolsDirectoryEmpty_Bug602`,  
`ToolFamilyCleanupStepTests.Step4_Fails_WhenBothToolsAndToolsRawAbsent_Bug602`

---

## Test Results

- `DocGeneration.PipelineRunner.Tests` — 12/12 ToolFamilyCleanup tests pass ✅
- `DocGeneration.Core.Shared.Tests` — 6/6 ResolveFamilyFileName tests pass ✅  
- `DocGeneration.Steps.Bootstrap.BrandMappings.Tests` — 17/17 pass ✅  
- `DocGeneration.Steps.ToolFamilyCleanup.Tests` — 880/881 pass (1 pre-existing `R_CG2` failure unrelated to these changes) ✅
