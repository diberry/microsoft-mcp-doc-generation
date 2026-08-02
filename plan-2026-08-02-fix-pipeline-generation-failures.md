---
title: Fix Pipeline Generation Failures from 2026-08-02 Run
status: in-progress
created: 2026-08-02T11:38:00
reviewed: 2026-08-02T11:49:00
tags: [pipeline, tdd, generation-failures, step-2, step-4, step-5]
---

# Fix Pipeline Generation Failures from 2026-08-02 Run

## Context

Full catalog generation run on 2026-08-02 produced 23 critical failures across 15 namespaces (out of 63 total). Failures come from `generated-20260802T094145/critical-failures/`. Three distinct failure categories need fixes in the pipeline source code — NOT in generated output.

## Failure Summary

### Category A: Step 2 — Example Prompt Generation (8 failures)
AI generates 5 example prompts per tool but fails to include required parameters even after 2 automatic retry attempts with validation feedback.

| Namespace | Tool | Missing Params |
|-----------|------|----------------|
| appconfig | kv-get | `account` |
| azurebackup | governance-soft-delete | `soft-delete` |
| azureterraform | aztfexport-query | `query` |
| azureterraform | aztfexport-resourcegroup | (retry exhausted) |
| datadog | monitoredresources-list | `datadog-resource` |
| foundryextensions | openai-chat-completions-create | `deployment` |
| foundryextensions | openai-embeddings-create | (retry exhausted) |
| group | resource-list | `resource-group` |

**Root cause (corrected after team review)**: The AI system prompt already contains maximum-strength language ("ABSOLUTE RULE", "ZERO EXCEPTIONS", "FAILURE CONDITION"). The problem is in `DeterministicPromptRepairer.cs` — the retry feedback template does NOT name the specific missing params, does NOT identify which prompt indices could reference them, and does NOT provide a concrete rewrite example. The AI has no actionable guidance on HOW to fix its response.

### Category B: Step 4 — Source CLI JSON Parameter Mismatch (4 namespaces, ~12 tools)
The ToolFamilyPostAssemblyValidator cross-checks documented parameters against source CLI JSON metadata and finds mismatches.

| Namespace | Issue Type | Details |
|-----------|-----------|---------|
| cosmos (7 tools) | Phantom param documented | `authentication-method` in article but NOT in CLI source |
| monitor | Phantom param documented | `web-test-locations` in article but not in CLI source |
| monitor | Required params missing from article | `resource-id`, `table` missing from log-query tools |
| compute | Missing param in prompt | `Virtual machine scale set (VMSS) name` |
| eventhubs | Missing param in prompt | `eventhub` |
| loadtesting | Missing param in prompt | `testrun-id` |

**Root cause (phantom params)**: Step 3 (AI tool-description improvement) hallucinated parameters that sound plausible (e.g., `authentication-method` for Cosmos DB) but don't exist in source CLI JSON. The post-assembly validator correctly catches this, but as a blocking failure after the article is already written — too late to auto-correct.

**Root cause (missing required params)**: Likely incorrect classification as "common parameters" in `ParameterFilterHelper.cs` / `common-parameters.json`. Must verify before assuming Step 1 extraction bug.

### Category C: Step 5 — Skills Relevance Missing (4 failures)
Extension namespaces (`extension_azqr`, `extension_cli_generate`, `extension_cli_install`, `get_azure_bestpractices`) produce no skills relevance output.

**Root cause (corrected after team review)**: `FailurePolicy` is already `Warn` in `SkillsRelevanceStep.cs` constructor. The actual failure mechanism is one of:
1. **Filename sanitization mismatch** (most likely): `SkillsMarkdownWriter.SanitizeFileName` strips non-`[a-z0-9\-]` characters, so `extension_azqr` becomes `extensionazqr-skills-relevance.md`. But `SkillsRelevanceStep.ExecuteAsync` constructs `reportPath` using the raw namespace key with underscores → expected file never found → `BuildResult(..., false, ...)`
2. **Zero-skills exit**: Process exits 0 but no skills matched; step interprets absent output as failure

**Required first action**: Inspect `generated-20260802T094145/` output to confirm which mismatch applies.

## Phases

- [ ] Phase 1: Diagnosis & test scaffolding — Write failing tests that reproduce each failure category
- [ ] Phase 2: Fix Category C (Step 5) — Fix filename mismatch or zero-skills handling
- [ ] Phase 3: Fix Category B (Step 4 phantom params) — Add pre-assembly param cross-check
- [ ] Phase 4: Fix Category A (Step 2 prompt quality) — Fix retry feedback template + add deterministic fallback
- [ ] Phase 5: Regression run & validation — Re-run affected namespaces, confirm 0 critical failures

## Phase 1: Diagnosis & Test Scaffolding

**Owner**: Cameron (test strategy) + Parker (implementation)
**TDD requirement**: Write tests FIRST that reproduce each failure mode.

### Diagnostic Tasks (Before Tests)
- [ ] **Parker**: Inspect `generated-20260802T094145/critical-failures/` for `extension_azqr` — confirm whether output file is absent entirely or named differently (underscore vs sanitized)
- [ ] **Parker**: Check `ParameterFilterHelper.cs` and `common-parameters.json` for `resource-id`, `table` being incorrectly listed as common params (Category B sub-issue 3b) DINA-STATEMENT - i want all params in the param tables now - I'll cut out what I don't want after generation - what needs to change in this plan

### Test Specifications
- [ ] Write test: Step 5 returns `Succeeded = true` with warning when namespace has zero skills mapped (not `false`)
- [ ] Write test: Step 5 `reportPath` construction uses `SanitizeFileName` consistently with writer (if filename mismatch confirmed)
- [ ] Write test: `ParameterCrossCheckService` strips phantom param present in tool markdown but absent from parameter manifest JSON
- [ ] Write test: `ParameterCrossCheckService` with zero required params in manifest → must not throw (edge case)
- [ ] Write test: Step 4 validator blocks when required CLI param missing from assembled article
- [ ] Write test: `DeterministicPromptRepairer` feedback explicitly names missing required params by name with prompt indices
- [ ] Write test: Step 2 validation — after max retries exhausted with missing param → failure correctly recorded
- [ ] Write test: `authentication-method` is NOT in cosmos CLI parameter manifest but appears in generated tool file → proves hallucination path
- [ ] Write test: 5 identical prompts after AI repair → `DuplicateExampleStripper` handles gracefully (edge case)
- [ ] Run all new tests, confirm they FAIL against current code (RED phase)

## Phase 2: Fix Category C — Step 5 Skills Relevance Graceful Handling

**Owner**: Morgan (C# code) + Quinn (pipeline config verification)

### Diagnostic Gate (Must Complete First)
- [ ] Confirm root cause from Parker's Phase 1 diagnostic (filename mismatch vs zero-skills exit)

### If Filename Mismatch (Most Likely)
- [ ] Fix `SkillsRelevanceStep.ExecuteAsync`: use `SanitizeFileName` when constructing `reportPath` so it matches what `SkillsMarkdownWriter` actually writes
- [ ] Verify no other steps have the same pattern (grep for raw namespace in path construction)

### If Zero-Skills Exit
- [ ] Detect zero-skills outcome from child process stdout (`"Relevant skills: 0"` or similar)
- [ ] Have `SkillsRelevanceStep` itself write a `skills-relevance-skipped.md` placeholder file directly
- [ ] Return `BuildResult(context, processResults, true, warnings)` — success with warning, NOT failure

### Both Cases
- [ ] Do NOT change `FailurePolicy` — it is already `Warn`
- [ ] Add `skills-relevance-skipped.md` as an acceptable alternative output in `expectedOutputs` contract (`WorkspaceManager` artifact sweep)
- [ ] Verify Phase 1 tests now PASS (GREEN phase)

## Phase 3: Fix Category B — Phantom Parameter Hallucination Guard

**Owner**: Morgan (C# code) + Sage (AI prompt instruction)

### Sub-issue 3a: Phantom Params (Pre-Assembly Guard)

**Architecture decision (from Riley)**: Implement `ParameterCrossCheckService` inside `FamilyStructureBuilder.BuildAsync` — BEFORE the stitcher writes final markdown. Do NOT use `IPostValidator` interface (that runs after article is written, causing unnecessary retry loops).

- [ ] Create `ParameterCrossCheckService` class
- [ ] Source of truth: parameter manifest JSONs at `context.OutputPath/parameters/` (written by Step 1)
- [ ] For each param in the tool's parameter table: verify it exists in the manifest; strip if absent
- [ ] Log warning: `"⚠️ Phantom param stripped: {param} from {tool} — not in CLI source manifest"`
- [ ] Reuse `ParameterFilterHelper` for common-param logic — do NOT duplicate filtering logic
- [ ] In Step 3 system prompt: Add instruction "Do not introduce parameter names not present in the source tool data provided below" (one-line addition)
- [ ] Verify Phase 1 tests now PASS (GREEN phase)

### Sub-issue 3b: Missing Required Params from Article

- [ ] **Check first**: Are `resource-id`, `table` incorrectly listed in `common-parameters.json`? If yes → remove them
- [ ] If not common-param issue: check if Step 1 parameter extraction is missing them from the manifest
- [ ] Fix the root cause (likely `ParameterFilterHelper` over-aggressively filtering)
- [ ] Verify Phase 1 tests now PASS (GREEN phase)

## Phase 4: Fix Category A — Example Prompt Required-Parameter Coverage

**Owner**: Sage (retry feedback) + Morgan (deterministic fallback)

### Fix Target: `DeterministicPromptRepairer.cs` (NOT system prompt)

The system prompt already has maximum-strength language. Adding more emphasis will not help.

- [ ] Modify `DeterministicPromptRepairer.cs` feedback template to include:
  - (a) Explicit list of missing required params BY NAME
  - (b) Which specific prompts (#1–#5) could naturally reference each missing param
  - (c) A concrete rewritten example of one failing prompt showing how to incorporate the param
- [ ] Add `DeterministicPromptRepairerTests` verifying feedback output contains param names and indices

### Deterministic Last-Resort Fallback

- [ ] In `DeterministicExamplePromptGenerator`: after final AI response, if a required param is STILL absent, inject a corrected prompt using a template string WITHOUT an additional AI call
- [ ] Template: `"Use {tool-name} to {action} with {missing-param-name} set to [value]"`
- [ ] This guarantees 100% param coverage even when AI fails all retries

### Retry Count Adjustment (ONLY After Feedback Fix Is Verified)
- [ ] Increase `MaxValidationRetries` constant in `ExamplePromptsStep.cs` from 2→3
- [ ] Only do this AFTER the improved feedback template is working — do not increase retries with the broken feedback

### Verify
- [ ] Phase 1 tests now PASS (GREEN phase)

## Phase 5: Regression Run & Validation

**Owner**: Quinn (run orchestration) + Parker (validation)

### Gate Order (Strict Sequence)
- [ ] Run `dotnet test mcp-doc-generation.sln` — all tests pass (new + existing) ← **FIRST GATE**
- [ ] Run `dotnet build mcp-doc-generation.sln --configuration Release` — 0 warnings
- [ ] Run `./start.sh appconfig 2 --skip-deps` — verify Step 2 passes
- [ ] Run `./start.sh cosmos 4 --skip-deps` — verify Step 4 passes (no phantom params)
- [ ] Run `./start.sh extension_azqr 5 --skip-deps` — verify Step 5 skips gracefully
- [ ] Run `./start.sh monitor 4 --skip-deps` — verify monitor Step 4 passes
- [ ] If all pass: run full catalog generation for the 15 affected namespaces
- [ ] Confirm `critical-failures/` directory is empty for all re-run namespaces

## Acceptance Criteria

1. All 23 critical failures are resolved (0 fatal failures for these namespaces)
2. No regressions in previously-passing namespaces
3. Every fix has corresponding unit test coverage (TDD — tests written FIRST, confirmed RED, then GREEN)
4. `dotnet test mcp-doc-generation.sln` passes with 0 failures
5. `dotnet build mcp-doc-generation.sln --configuration Release` has 0 warnings
6. CHANGELOG.md updated under `## [Unreleased]` with one entry per failure category

## Team Assignments Summary

| Phase | Primary | Secondary | Deliverable |
|-------|---------|-----------|-------------|
| 1 | Cameron + Parker | — | Failing tests + diagnostic findings |
| 2 | Morgan | Quinn (verify artifact contract) | Step 5 fix |
| 3 | Morgan + Sage | Riley (architecture review) | Pre-assembly param guard |
| 4 | Sage + Morgan | Cameron (test review) | Retry feedback fix + deterministic fallback |
| 5 | Quinn + Parker | All (validation) | Regression confirmation |
| Docs | Reeve | — | CHANGELOG, ARCHITECTURE.md, PROJECT-GUIDE.md |

---

## Team Review Feedback (2026-08-02)

**Verdict: Conditional Approve — 8/8 reviewers approved with improvements**

All 22 improvements below have been incorporated into the phase details above.

### Reviewer Verdicts

| Reviewer | Verdict | Key Contribution |
|----------|---------|-----------------|
| Avery | APPROVE | Priority order confirmed correct; added "diagnose before code" gate |
| Riley | CONDITIONAL APPROVE | Corrected `ParameterCrossCheckService` hook point (pre-assembly, not post-validator) |
| Morgan | APPROVE | Confirmed Step 5 policy already Warn; proposed step-writes-placeholder pattern |
| Quinn | APPROVE | Added `--skip-deps` to regression commands; flagged artifact contract check |
| Sage | CONDITIONAL APPROVE | Redirected Phase 4 from system prompt to `DeterministicPromptRepairer.cs`; added deterministic fallback |
| Cameron | APPROVE | Expanded test specs with edge cases; added `DeterministicPromptRepairerTests` requirement |
| Parker | APPROVE | Identified filename sanitization mismatch as likely Category C root cause |
| Reeve | APPROVE | Specified 4 doc update targets: CHANGELOG, ARCHITECTURE, PROJECT-GUIDE, copilot-instructions |

### Improvements Registry

| # | Source | Incorporated Into |
|---|--------|-------------------|
| 1 | Avery | Phase 2 diagnostic gate |
| 2 | Avery | Phase 5 test-first gate order |
| 3 | Riley | Phase 3a architecture (FamilyStructureBuilder.BuildAsync) |
| 4 | Riley | Phase 3a reuse ParameterFilterHelper |
| 5 | Riley | Phase 4 retry count conditional on feedback fix |
| 6 | Morgan | Phase 2 step-writes-placeholder pattern |
| 7 | Morgan | Phase 3b check common-parameters.json first |
| 8 | Quinn | Phase 5 `--skip-deps` flag |
| 9 | Quinn | Phase 2 artifact contract verification |
| 10 | Sage | Phase 4 target DeterministicPromptRepairer.cs |
| 11 | Sage | Phase 4 deterministic last-resort injection |
| 12 | Sage | Phase 3a Step 3 prompt one-liner |
| 13 | Cameron | Phase 1 Category C test: assert Succeeded=true + warning |
| 14 | Cameron | Phase 1 Category B test: realistic fixture |
| 15 | Cameron | Phase 1 DeterministicPromptRepairerTests |
| 16 | Parker | Phase 1 diagnostic: filename mismatch investigation |
| 17 | Parker | Phase 3 edge case: zero required params |
| 18 | Parker | Phase 4 edge case: duplicate prompts after repair |
| 19 | Reeve | CHANGELOG entry (3 bullets, one per category) |
| 20 | Reeve | ARCHITECTURE.md Step 5 section update |
| 21 | Reeve | PROJECT-GUIDE.md troubleshooting entry |
| 22 | Reeve | copilot-instructions.md Common Issues update |

### Risk Flags

1. **Phase 2 regression risk**: If we change `reportPath` construction, verify ALL namespaces still resolve correctly — not just the 4 failing ones
2. **Phase 3 over-stripping risk**: `ParameterCrossCheckService` must NOT strip params that are legitimately added by Step 3 AI enrichment and absent from Step 1 manifest due to manifest incompleteness. Add an allowlist escape hatch.
3. **Phase 4 deterministic fallback quality**: Template-generated prompts will be less natural than AI-generated ones. Acceptable as a last resort but should not trigger for >10% of tools.

DINA-STATEMENT: make sure architecture is up to date after these changes