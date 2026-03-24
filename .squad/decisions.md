# Squad Decisions

## Active Decisions

### AD-001: Team Charter — Content Correctness as Primary Goal
**Date:** 2026-03-20  
**Author:** Avery (Lead)  
**Status:** Active

The team's primary mission is **consistently correct content across all 52 namespaces at every pipeline stage**. This means:
- Every stage (Steps 0-6) must have defined input/output contracts
- Quality failures must be caught at the stage that produces them, not downstream
- AI-generated content (Steps 2, 3b, 4, 6) requires validation gates that block bad output
- "It works for storage" is not sufficient — all 52 namespaces must pass

### AD-002: Known Quality Risk Areas (Initial Assessment)
**Date:** 2026-03-20  
**Author:** Avery (Lead)  
**Status:** Active

Pipeline exploration identified these content correctness risks:

| Risk | Stages Affected | Severity | Owner |
|------|----------------|----------|-------|
| Token truncation causes AI to produce incomplete content | Steps 2, 3b, 4, 6 | High | Sage + Morgan |
| Leaked template tokens (`<<<TPL_LABEL_N>>>`) in final output | Step 3 | Medium | Morgan |
| AI JSON parse failures block horizontal articles | Step 6 | High | Sage |
| Step dependencies silently degrade (Step 3 fail → Step 4 skip) | Steps 3-4 | High | Quinn + Avery |
| GitHub API rate limiting degrades Step 5 | Step 5 | Low | Quinn |
| No end-to-end integration tests for full pipeline | All | High | Parker |
| Inconsistent parameter placeholder substitution | Steps 2-3 | Medium | Morgan |

### AD-003: Issue Triage — 18 Open Issues Routed to Team
**Date:** 2026-03-20  
**Author:** Avery (Lead)  
**Status:** Active

18 open GitHub issues triaged and assigned to team members. See issue routing below.

**Critical (blocks releases):**
- #158 → Avery + Sage + Morgan: Step 4/6 validation failures for 8 namespaces
- #148 → Morgan: Cosmos missing entire tool
- #147 → Morgan: Parameter filtering too aggressive (resource-group)

**High (content quality):**
- #155 → Morgan: Missing required frontmatter fields
- #154 → Sage: Generic H2 headings
- #153 → Sage: Duplicate Examples block
- #140 → Sage: Phantom Examples H2 section
- #149 → Sage: Cosmos SEO violations
- #139 → Parker + Morgan: ParameterCoverageChecker false positive

**Medium (style/polish):**
- #152 → Sage: Missing backticks on example values
- #151 → Morgan: Missing blank line in annotation rendering
- #141 → Morgan: CosmosDB branding normalization
- #150 → Avery: Multi-namespace article support (monitor + workbooks)

**Low (Acrolinx compliance):**
- #146 → Sage: Commas after introductory phrases
- #145 → Sage: Present tense and contractions
- #144 → Morgan: Demonstrative pronoun fix (static replacement)
- #143 → Sage: Split complex overview sentence
- #142 → Sage: Define MCP acronym on first use

### AD-006: Issue #158 Resolution — 5/8 Namespaces Fixed
**Date:** 2026-03-21  
**Author:** Avery (Lead)  
**Status:** Active

Five commits resolved 5 of the 8 failing namespaces in #158:
- **Phantom H2 stripping** (compute): Step 3 AI generates `## Examples` inside tool content; now stripped during Phase 1.5 heading replacement
- **ParameterCoverageChecker** (deploy): 4 algorithm defects fixed (word boundary, JSON array, sentence-end, abbreviation)
- **Token budget** (wellarchitectedframework): Step 6 min 8K/max 24K
- **CLI switch prefix** (fileshare-delete): Strip `--` before validation

Remaining 5 failures tracked as:
- #160: Step 4 generation produces no output (search, postgres, resourcehealth)
- #161: Step 2 checker too strict for `message-array` and `name` params (foundryextensions, fileshares)

### AD-007: TDD — Tests First, Then Code
**Date:** 2026-03-21 (updated 2026-03-22)  
**Author:** Avery (Lead)  
**Status:** Active  
**Supersedes:** Previous version of AD-007

**All development follows Test-Driven Development: write failing tests FIRST, then implement the fix.**

Workflow for every change:
1. **Write failing tests** that define the expected behavior (the contract)
2. **Verify they fail** against the current code (proves the tests are meaningful)
3. **Implement the fix** to make the failing tests pass
4. **Verify all tests pass** — new tests + existing tests (no regressions)
5. **Commit tests and code together** — never commit code without its tests

Rules:
- Bug fix → write tests that reproduce the bug BEFORE writing the fix
- New feature → write tests that define the feature contract BEFORE implementing
- Prompt changes → write tests for the validation rules BEFORE changing the prompt
- Config changes → write tests that verify config loading BEFORE changing config
- If the code under test is `private`, make it `internal` with `[InternalsVisibleTo]`
- **PRs that contain code changes without corresponding test changes are blocked**

### AD-010: Test Coverage Depth — Tests Must Catch the Bug on Regression
**Date:** 2026-03-22  
**Author:** Avery (Lead)  
**Status:** Active  
**Extends:** AD-007

**Tests must be behavioral, not just structural.** A test that only checks a method signature or return type is insufficient. Every fix must include tests that would **definitively catch the bug if it regressed**.

**Coverage depth requirements:**

| Change Type | Minimum Test Coverage |
|-------------|----------------------|
| Bug fix (code path) | ≥1 test that reproduces the exact failure scenario with realistic inputs |
| Bug fix (error handling) | ≥1 test for the error path + ≥1 test for the happy path to guard regressions |
| Bug fix (AI-dependent) | ≥1 test simulating AI failure (mock/stub), ≥1 test simulating AI success |
| New feature | ≥1 test per public method, ≥1 edge case, ≥1 null/empty input |
| Config/data change | ≥1 test proving the new config value is loaded AND applied |
| Post-processing step | ≥1 test with input containing the problem pattern, ≥1 test with clean input (no false positives) |

**Test quality checklist (reviewer must verify):**
- [ ] Test would FAIL if the fix were reverted (not just a type check or reflection test)
- [ ] Test uses realistic inputs (not trivial "hello world" data)
- [ ] Test asserts on the observable behavior, not implementation details
- [ ] Error-path tests verify the error message/warning content, not just success/failure boolean
- [ ] If testing a pipeline step, test uses the existing `CallbackProcessRunner`/mock pattern

**Anti-patterns (blocked in review):**
- ❌ Reflection-only tests (checking method exists, return type, attribute presence) as sole coverage
- ❌ Tests that pass regardless of whether the fix is present
- ❌ Tests that only assert `Assert.True(result.Success)` without checking warnings/output
- ❌ "Smoke tests" that call the method but don't assert meaningful behavior

**Rationale:** PR #172 initially had only 1 reflection test for a 3-file fix. The test would pass even if the exit code logic were reverted. Adding behavioral tests (simulating "exit 0 but no output files") caught the actual scenario. Tests must be written to catch the bug, not just prove the code compiles.

### AD-008: Plan Step Runs Before Executing
**Date:** 2026-03-21  
**Author:** Avery (Lead)  
**Status:** Active

**Before running any namespace validation, map which steps ACTUALLY need to run based on what changed and what's on disk.**

Step dependency chain (what each step reads from disk):

| Step | Reads From | Writes To |
|------|------------|-----------|
| 1 | `cli/` | `annotations/`, `parameters/`, `tools-raw/` |
| 2 | `cli/`, `annotations/`, `parameters/` | `example-prompts/` |
| 3 | `tools-raw/`, `annotations/`, `parameters/`, `example-prompts/` | `tools-composed/`, `tools/` |
| 4 | `tools/` | `tool-family/`, `tool-family-metadata/`, `tool-family-related/` |
| 5 | `tools/` | `skills-relevance/` |
| 6 | `tools/` | `horizontal-articles/` |

**Decision matrix:**

| What Changed | Minimum Steps to Run |
|-------------|---------------------|
| ParameterCoverageChecker (validation only) | Step 2 only (re-validate existing prompts) if prompts on disk; Steps 2,3,4 if prompts need regeneration |
| Step 2 prompt generation code | Steps 2,3,4 (regenerated prompts feed into tools) |
| Step 4 prompt/validator | Step 4 only (if `tools/` exists on disk) |
| Step 3 template/improvement | Steps 3,4 (if `annotations/`, `parameters/`, `example-prompts/` exist) |
| Token budget (Step 6) | Step 6 only (if `tools/` exists) |

**Always check:** `ls generated-{ns}/tools/` — if empty, you need Step 3. `ls generated-{ns}/example-prompts/` — if empty, you need Step 2. Work backwards from the failing step.

### AD-004: PR Documentation Requirement
**Date:** 2026-03-20  
**Author:** Reeve (Documentation Engineer)  
**Status:** Active

**Every PR must include documentation or a documented exemption.** This is a merge-blocking requirement.

**PR Documentation Gate (enforced by Reeve):**

| PR Contains | Docs Required | What To Include |
|-------------|---------------|-----------------|
| New feature or pipeline step | ✅ Required | User guide in `docs/`, updated README if public-facing |
| Bug fix that changes behavior | ✅ Required | Update any docs that described the old behavior |
| Architecture change | ✅ Required | Engineering docs, updated pipeline stage docs |
| Prompt changes | ✅ Required | Document what changed and why in prompt file comments + `docs/` |
| Config file changes | ✅ Required | Update config reference in `docs/` |
| Internal refactor (no behavior change) | 📝 Exemption OK | PR comment: "Internal refactor, no behavior change" |
| Test-only changes | 📝 Exemption OK | PR comment: "Test coverage addition, no user-facing change" |
| Large feature (docs too big for same PR) | 🔗 Follow-up OK | Link to follow-up docs issue in PR description |

### AD-005: All Work Must Go Through PRs
**Date:** 2026-03-20  
**Author:** Avery (Lead)  
**Status:** Active

**No direct commits to main.** All changes — code, prompts, configs, scripts, docs — must go through pull requests with:
1. At least one team member review (domain specialist)
2. Reeve's documentation review (blocks if docs missing)
3. Parker's test validation (for code changes)
4. Passing CI build (`dotnet build && dotnet test`)

### AD-017: Link Format Convention
**Date:** 2026-03-20  
**Author:** Sage  
**Status:** Active

Generated content must NEVER use `~/` (DocFX repo-root) paths. Use only: absolute URLs, site-root-relative (`/azure/...`), or file-relative (`../includes/...`) paths.

### AD-011: Multi-Namespace Merge — Post-Assembly Design
**Date:** 2026-03-22  
**Author:** Avery (Lead)  
**Status:** Active

Some Azure services span multiple MCP namespaces but publish as a single article (e.g., `monitor` + `workbooks` → `azure-monitor.md`). Rather than threading multi-namespace awareness through all 6 pipeline steps (high risk), we use a **post-assembly merge** pattern:

1. Each namespace generates independently through Steps 1-6 as before
2. A merge step runs AFTER all namespaces complete
3. Grouped namespaces are defined in `brand-to-server-mapping.json` via optional fields:
   - `mergeGroup`: group identifier (e.g., `"azure-monitor"`)
   - `mergeOrder`: position within group (1 = primary)
   - `mergeRole`: `"primary"` (owns frontmatter/overview/related-content) or `"secondary"` (contributes tool H2 sections only)
4. Namespaces WITHOUT `mergeGroup` are standalone — fully backward compatible
5. Validation rules:
   - Each group must have exactly one `"primary"` namespace
   - `mergeOrder` values must be unique and sequential within a group
   - `mergeGroup` value should match the primary namespace's `fileName`

### AD-018: Consolidate StripFrontmatter Implementations
**Date:** 2026-03-24  
**Author:** Avery (Lead)  
**Status:** Proposed  
**Triggered by:** Architecture review of PR #200

Three separate `StripFrontmatter` implementations exist:
1. `ToolReader.StripFrontmatter()` — regex-based
2. `ComposedToolGeneratorService.StripFrontmatter()` — line-by-line parsing
3. `PageGenerator.StripFrontmatter()` — line-by-line parsing

**Decision:** When module boundaries are next refactored, consolidate into `DocGeneration.Core` or `DocGeneration.Core.NaturalLanguage`. Until then, duplication is acceptable tech debt (implementations in different project boundaries).

**Priority:** Low — tracked as tech debt, not blocking.

---

### AD-019: Template-Level Regression Tests Required for Template Fixes
**Date:** 2026-03-24  
**Author:** Parker (QA/Tester)  
**Status:** Active  
**Extends:** AD-010

**Decision:** Any PR that modifies a Handlebars template (`.hbs` file) must include at least one test that:
1. Loads the actual template from `docs-generation/templates/`
2. Renders it using `HandlebarsTemplateEngine.ProcessTemplateString()`
3. Asserts on the specific output change
4. Would FAIL if the template change were reverted

Template-level tests must supplement method-level tests for helper functions — method tests are necessary but not sufficient for template fix PRs.

**Rationale:** PRs #200 and #201 both fix template bugs but only test helper methods. If template changes were reverted, all tests would pass — an AD-010 violation. The pattern already exists in `ToolFamilyPageTemplateRegressionTests.cs`.

**Impact:** Affects all contributors modifying `.hbs` files.

---

### AD-020: Pipeline Architecture Assessment
**Date:** 2026-03-24  
**Author:** Riley (Pipeline Architect)  
**Status:** Informational

Comprehensive architecture review of PipelineRunner, Steps 0-6, cross-step data flow, merge infrastructure, and workspace isolation. **Overall assessment: production-worthy with manageable risks.** The architecture is sound and typed contracts provide good guardrails.

**Key Findings:**

**Risks (prioritized):**
1. 🔴 **Risk 1 (Critical):** Bootstrap `ResetOutputDirectory` destroys partial progress. Running `./start.sh advisor 4` after fixing a failed step wipes Steps 1-3 output. Mitigation: Add incremental mode that preserves outputs when `--skip-deps` is active.
2. 🟠 **Risk 2 (High):** Subprocess error detection via regex is fragile. If a generator changes output format, failures go undetected. Mitigation: Structured error contracts (`step-result.json`).
3. 🟠 **Risk 3 (Medium):** Implicit dependencies not captured. Steps 5 and 6 depend on Bootstrap metadata but don't declare `dependsOn`. Mitigation: Add explicit `DependsOn` declarations.
4. 🟡 **Risk 4 (Medium):** Dual merge implementations (shell + C#). Consolidate to single implementation.
5. 🟡 **Risk 5 (Medium):** Step 4 file matching could cross namespaces if `@mcpcli` annotations are corrupted. Add cross-validation after matching.

**Opportunities (by impact):**
1. Incremental Bootstrap mode (save 10-15 min per retry)
2. Structured error contracts between runner and generators
3. Add retries to Step 3 (fragile AI step)
4. Consolidate merge implementations
5. Add content integrity validation to Step 1

**Component Ratings:**
- 🟢 Solid: PipelineRunner core, step contracts, StepRegistry, WorkspaceManager, deterministic generators, FamilyFileStitcher, parallel safety
- 🟡 Acceptable: Bootstrap (destructive reset), Steps 1-3 (regex detection), Steps 5-6 (implicit dependencies), post-assembly merge
- 🟠 Needs attention: Step 4 (complex file matching + validator complexity)

See `.squad/orchestration-log/2026-03-24T15-06-32Z-riley.md` for full detailed review.

---

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
