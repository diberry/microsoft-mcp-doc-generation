# Squad Decisions

## Active Decisions

### AD-001: Team Charter ΓÇö Content Correctness as Primary Goal
**Date:** 2026-03-20  
**Author:** Avery (Lead)  
**Status:** Active

The team's primary mission is **consistently correct content across all 52 namespaces at every pipeline stage**. This means:
- Every stage (Steps 0-6) must have defined input/output contracts
- Quality failures must be caught at the stage that produces them, not downstream
- AI-generated content (Steps 2, 3b, 4, 6) requires validation gates that block bad output
- "It works for storage" is not sufficient ΓÇö all 52 namespaces must pass

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
| Step dependencies silently degrade (Step 3 fail ΓåÆ Step 4 skip) | Steps 3-4 | High | Quinn + Avery |
| GitHub API rate limiting degrades Step 5 | Step 5 | Low | Quinn |
| No end-to-end integration tests for full pipeline | All | High | Parker |
| Inconsistent parameter placeholder substitution | Steps 2-3 | Medium | Morgan |

### AD-003: Issue Triage ΓÇö 18 Open Issues Routed to Team
**Date:** 2026-03-20  
**Author:** Avery (Lead)  
**Status:** Active

18 open GitHub issues triaged and assigned to team members. See issue routing below.

**Critical (blocks releases):**
- #158 ΓåÆ Avery + Sage + Morgan: Step 4/6 validation failures for 8 namespaces
- #148 ΓåÆ Morgan: Cosmos missing entire tool
- #147 ΓåÆ Morgan: Parameter filtering too aggressive (resource-group)

**High (content quality):**
- #155 ΓåÆ Morgan: Missing required frontmatter fields
- #154 ΓåÆ Sage: Generic H2 headings
- #153 ΓåÆ Sage: Duplicate Examples block
- #140 ΓåÆ Sage: Phantom Examples H2 section
- #149 ΓåÆ Sage: Cosmos SEO violations
- #139 ΓåÆ Parker + Morgan: ParameterCoverageChecker false positive

**Medium (style/polish):**
- #152 ΓåÆ Sage: Missing backticks on example values
- #151 ΓåÆ Morgan: Missing blank line in annotation rendering
- #141 ΓåÆ Morgan: CosmosDB branding normalization
- #150 ΓåÆ Avery: Multi-namespace article support (monitor + workbooks)

**Low (Acrolinx compliance):**
- #146 ΓåÆ Sage: Commas after introductory phrases
- #145 ΓåÆ Sage: Present tense and contractions
- #144 ΓåÆ Morgan: Demonstrative pronoun fix (static replacement)
- #143 ΓåÆ Sage: Split complex overview sentence
- #142 ΓåÆ Sage: Define MCP acronym on first use

### AD-006: Issue #158 Resolution ΓÇö 5/8 Namespaces Fixed
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

### AD-007: TDD ΓÇö Tests First, Then Code
**Date:** 2026-03-21 (updated 2026-03-22)  
**Author:** Avery (Lead)  
**Status:** Active  
**Supersedes:** Previous version of AD-007

**All development follows Test-Driven Development: write failing tests FIRST, then implement the fix.**

Workflow for every change:
1. **Write failing tests** that define the expected behavior (the contract)
2. **Verify they fail** against the current code (proves the tests are meaningful)
3. **Implement the fix** to make the failing tests pass
4. **Verify all tests pass** ΓÇö new tests + existing tests (no regressions)
5. **Commit tests and code together** ΓÇö never commit code without its tests

Rules:
- Bug fix ΓåÆ write tests that reproduce the bug BEFORE writing the fix
- New feature ΓåÆ write tests that define the feature contract BEFORE implementing
- Prompt changes ΓåÆ write tests for the validation rules BEFORE changing the prompt
- Config changes ΓåÆ write tests that verify config loading BEFORE changing config
- If the code under test is `private`, make it `internal` with `[InternalsVisibleTo]`
- **PRs that contain code changes without corresponding test changes are blocked**

### AD-010: Test Coverage Depth ΓÇö Tests Must Catch the Bug on Regression
**Date:** 2026-03-22  
**Author:** Avery (Lead)  
**Status:** Active  
**Extends:** AD-007

**Tests must be behavioral, not just structural.** A test that only checks a method signature or return type is insufficient. Every fix must include tests that would **definitively catch the bug if it regressed**.

**Coverage depth requirements:**

| Change Type | Minimum Test Coverage |
|-------------|----------------------|
| Bug fix (code path) | ΓëÑ1 test that reproduces the exact failure scenario with realistic inputs |
| Bug fix (error handling) | ΓëÑ1 test for the error path + ΓëÑ1 test for the happy path to guard regressions |
| Bug fix (AI-dependent) | ΓëÑ1 test simulating AI failure (mock/stub), ΓëÑ1 test simulating AI success |
| New feature | ΓëÑ1 test per public method, ΓëÑ1 edge case, ΓëÑ1 null/empty input |
| Config/data change | ΓëÑ1 test proving the new config value is loaded AND applied |
| Post-processing step | ΓëÑ1 test with input containing the problem pattern, ΓëÑ1 test with clean input (no false positives) |

**Test quality checklist (reviewer must verify):**
- [ ] Test would FAIL if the fix were reverted (not just a type check or reflection test)
- [ ] Test uses realistic inputs (not trivial "hello world" data)
- [ ] Test asserts on the observable behavior, not implementation details
- [ ] Error-path tests verify the error message/warning content, not just success/failure boolean
- [ ] If testing a pipeline step, test uses the existing `CallbackProcessRunner`/mock pattern

**Anti-patterns (blocked in review):**
- Γ¥î Reflection-only tests (checking method exists, return type, attribute presence) as sole coverage
- Γ¥î Tests that pass regardless of whether the fix is present
- Γ¥î Tests that only assert `Assert.True(result.Success)` without checking warnings/output
- Γ¥î "Smoke tests" that call the method but don't assert meaningful behavior

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

**Always check:** `ls generated-{ns}/tools/` ΓÇö if empty, you need Step 3. `ls generated-{ns}/example-prompts/` ΓÇö if empty, you need Step 2. Work backwards from the failing step.

### AD-004: PR Documentation Requirement
**Date:** 2026-03-20  
**Author:** Reeve (Documentation Engineer)  
**Status:** Active

**Every PR must include documentation or a documented exemption.** This is a merge-blocking requirement.

**PR Documentation Gate (enforced by Reeve):**

| PR Contains | Docs Required | What To Include |
|-------------|---------------|-----------------|
| New feature or pipeline step | Γ£à Required | User guide in `docs/`, updated README if public-facing |
| Bug fix that changes behavior | Γ£à Required | Update any docs that described the old behavior |
| Architecture change | Γ£à Required | Engineering docs, updated pipeline stage docs |
| Prompt changes | Γ£à Required | Document what changed and why in prompt file comments + `docs/` |
| Config file changes | Γ£à Required | Update config reference in `docs/` |
| Internal refactor (no behavior change) | ≡ƒô¥ Exemption OK | PR comment: "Internal refactor, no behavior change" |
| Test-only changes | ≡ƒô¥ Exemption OK | PR comment: "Test coverage addition, no user-facing change" |
| Large feature (docs too big for same PR) | ≡ƒöù Follow-up OK | Link to follow-up docs issue in PR description |

### AD-005: All Work Must Go Through PRs
**Date:** 2026-03-20  
**Author:** Avery (Lead)  
**Status:** Active

**No direct commits to main.** All changes ΓÇö code, prompts, configs, scripts, docs ΓÇö must go through pull requests with:
1. At least one team member review (domain specialist)
2. Reeve's documentation review (blocks if docs missing)
3. Parker's test validation (for code changes)
4. Passing CI build (`dotnet build && dotnet test`)

### AD-017: Link Format Convention
**Date:** 2026-03-20  
**Author:** Sage  
**Status:** Active

Generated content must NEVER use `~/` (DocFX repo-root) paths. Use only: absolute URLs, site-root-relative (`/azure/...`), or file-relative (`../includes/...`) paths.

### AD-011: Multi-Namespace Merge ΓÇö Post-Assembly Design
**Date:** 2026-03-22  
**Author:** Avery (Lead)  
**Status:** Active

Some Azure services span multiple MCP namespaces but publish as a single article (e.g., `monitor` + `workbooks` ΓåÆ `azure-monitor.md`). Rather than threading multi-namespace awareness through all 6 pipeline steps (high risk), we use a **post-assembly merge** pattern:

1. Each namespace generates independently through Steps 1-6 as before
2. A merge step runs AFTER all namespaces complete
3. Grouped namespaces are defined in `brand-to-server-mapping.json` via optional fields:
   - `mergeGroup`: group identifier (e.g., `"azure-monitor"`)
   - `mergeOrder`: position within group (1 = primary)
   - `mergeRole`: `"primary"` (owns frontmatter/overview/related-content) or `"secondary"` (contributes tool H2 sections only)
4. Namespaces WITHOUT `mergeGroup` are standalone ΓÇö fully backward compatible
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
1. `ToolReader.StripFrontmatter()` ΓÇö regex-based
2. `ComposedToolGeneratorService.StripFrontmatter()` ΓÇö line-by-line parsing
3. `PageGenerator.StripFrontmatter()` ΓÇö line-by-line parsing

**Decision:** When module boundaries are next refactored, consolidate into `DocGeneration.Core` or `DocGeneration.Core.NaturalLanguage`. Until then, duplication is acceptable tech debt (implementations in different project boundaries).

**Priority:** Low ΓÇö tracked as tech debt, not blocking.

### AD-020: User Workflow Directive ΓÇö Mandatory Development Process
**Date:** 2026-03-24 (updated 2026-03-29)
**Author:** Dina Berry (via Copilot)
**Status:** Active

**All development work must follow this process in strict order:**
1. Create a plan (design, test strategy, edge cases)
2. Write failing tests (define the contract before implementation)
3. Write implementation code (make the tests pass)
4. Run tests (all must pass, no regressions)
5. Create PR ΓÇö **STOP. Do NOT merge.**
6. Full team review (all 9 reviewers: Avery, Riley, Morgan, Quinn, Sage, Cameron, Parker, Reeve, Copilot)
7. User (Dina) reviews and merges

**NEVER merge a PR. Only Dina merges.** Sub-agents must NEVER call `gh pr merge`. Their job ends at step 5 (create PR). This applies to all agents ΓÇö general-purpose, task, and squad members alike.

**Never skip steps or present work before the full team review is complete.** This is a user-mandated workflow to ensure consistency, quality, and accountability across all squad work.

**Reference:** Captured in `.squad/decisions/inbox/copilot-directive-2026-03-24T19-38-25Z.md`

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

Template-level tests must supplement method-level tests for helper functions ΓÇö method tests are necessary but not sufficient for template fix PRs.

**Rationale:** PRs #200 and #201 both fix template bugs but only test helper methods. If template changes were reverted, all tests would pass ΓÇö an AD-010 violation. The pattern already exists in `ToolFamilyPageTemplateRegressionTests.cs`.

**Impact:** Affects all contributors modifying `.hbs` files.

---

### AD-020: Pipeline Architecture Assessment
**Date:** 2026-03-24  
**Author:** Riley (Pipeline Architect)  
**Status:** Informational

Comprehensive architecture review of PipelineRunner, Steps 0-6, cross-step data flow, merge infrastructure, and workspace isolation. **Overall assessment: production-worthy with manageable risks.** The architecture is sound and typed contracts provide good guardrails.

**Key Findings:**

**Risks (prioritized):**
1. ≡ƒö┤ **Risk 1 (Critical):** Bootstrap `ResetOutputDirectory` destroys partial progress. Running `./start.sh advisor 4` after fixing a failed step wipes Steps 1-3 output. Mitigation: Add incremental mode that preserves outputs when `--skip-deps` is active.
2. ≡ƒƒá **Risk 2 (High):** Subprocess error detection via regex is fragile. If a generator changes output format, failures go undetected. Mitigation: Structured error contracts (`step-result.json`).
3. ≡ƒƒá **Risk 3 (Medium):** Implicit dependencies not captured. Steps 5 and 6 depend on Bootstrap metadata but don't declare `dependsOn`. Mitigation: Add explicit `DependsOn` declarations.
4. ≡ƒƒí **Risk 4 (Medium):** Dual merge implementations (shell + C#). Consolidate to single implementation.
5. ≡ƒƒí **Risk 5 (Medium):** Step 4 file matching could cross namespaces if `@mcpcli` annotations are corrupted. Add cross-validation after matching.

**Opportunities (by impact):**
1. Incremental Bootstrap mode (save 10-15 min per retry)
2. Structured error contracts between runner and generators
3. Add retries to Step 3 (fragile AI step)
4. Consolidate merge implementations
5. Add content integrity validation to Step 1

**Component Ratings:**
- ≡ƒƒó Solid: PipelineRunner core, step contracts, StepRegistry, WorkspaceManager, deterministic generators, FamilyFileStitcher, parallel safety
- ≡ƒƒí Acceptable: Bootstrap (destructive reset), Steps 1-3 (regex detection), Steps 5-6 (implicit dependencies), post-assembly merge
- ≡ƒƒá Needs attention: Step 4 (complex file matching + validator complexity)

See `.squad/orchestration-log/2026-03-24T15-06-32Z-riley.md` for full detailed review.

---

### AD-021: Step 3 and Step 6 Must Have Post-Validators Before Next Release
**Date:** 2026-03-25  
**Author:** Parker (QA/Tester)  
**Status:** Active  
**Extends:** AD-002, AD-010  
**Triggered by:** Test strategy audit (docs/test-strategy.md)

**Decision:** Steps 3 (ToolGeneration) and 6 (HorizontalArticles) must implement `IPostValidator` before the next release. These are the only AI-dependent steps with **no output validation at all**, creating the two highest-risk gaps identified in AD-002:

1. **Step 3:** Template token leakage (`<<<TPL_LABEL_N>>>`) in final output goes undetected
2. **Step 6:** AI JSON parse failures producing incomplete horizontal articles go undetected

**Rationale:** The test strategy audit found that only Step 4 has a post-validator. Steps 3 and 6 produce AI-generated content that feeds directly into the final documentation corpus ΓÇö if they fail silently, bad content ships. Both risks are already documented in AD-002 but have no mitigation.

**Impact:** Morgan or Sage should implement these validators. Parker will write tests for the validators once implemented. Blocks: any release claiming "full pipeline validation".

---

### AD-022: Acrolinx Compliance Strategy for Tool-Family Articles
**Date:** 2026-03-25
**Author:** Sage (AI / Prompt Engineer)
**Status:** Active
**Triggered by:** Acrolinx gate requirement; 30% pass rate on tool-family articles; issues #142-#146; PR review feedback from azure-dev-docs-pr

**Context:** Our generated tool-family articles currently pass Acrolinx quality gate (80+) at only 30% rate (3/10). Worst performers: Deploy (61), Postgres (64), Cloud Architect (67). Acrolinx is mandatory for merging to production docs.

**Decision:** Implement **6-priority remediation plan** combining prompt changes (P0) and deterministic post-processors (P1-P4):

1. **P0 ΓÇö System prompt update:** Add explicit Acrolinx compliance rules to `tool-family-cleanup-system-prompt.txt` (sentence length Γëñ25 words, present tense, active voice, contractions, introductory commas, wordy phrase avoidance).
2. **P1 ΓÇö JsonSchemaCollapser:** New post-processor to collapse inline JSON schema parameter descriptions into human-readable summaries. Expected +15-20 pts for Deploy.
3. **P1 ΓÇö ContractionFixer extension:** Add positive contractions ("it is"ΓåÆ"it's", "you are"ΓåÆ"you're", etc.) to existing ContractionFixer.
4. **P2 ΓÇö WordyPhraseFixer + static replacements:** Deterministic removal of "in order to", "due to the fact that", deprecated Microsoft terms ("Azure AD"ΓåÆ"Microsoft Entra ID"), and ableist language ("simply", "just").
5. **P3 ΓÇö TenseFixer + AcronymExpander:** Present tense enforcement ("will list"ΓåÆ"lists") and multi-acronym first-use expansion.
6. **P4 ΓÇö SentenceLengthWarner:** Diagnostic logging for sentences exceeding 25 words (inform, not auto-fix).

**Rationale:** Post-processing is preferred over prompt-only fixes because it's **deterministic** ΓÇö a regex that converts "it is" to "it's" always works, while an AI prompt instruction may be ignored 20% of the time. The prompt changes (P0) remain valuable as first-line defense.

**Quick win:** Expanding `static-text-replacement.json` with 15 wordy phrases, 8 deprecated terms, and 5 ableist language removals yields +5-10 pts with zero code changes.

**Impact:**
- **Sage:** Owns prompt change (P0) and all post-processor implementations (P1-P3).
- **Morgan:** May need to adjust FamilyFileStitcher call order if new post-processors are added.
- **Parker:** Must write tests for each new post-processor per AD-007 and AD-010.
- **All namespaces:** Changes apply universally across all 52 namespaces ΓÇö no service-specific logic.

---

### AD-023: Work Prioritization Framework ΓÇö Post-Review Issue Set
**Date:** 2026-03-25
**Author:** Avery (Lead / Architect)
**Status:** Active
**Requested by:** Dina Berry

**Context:** After merging PRs #200 and #201, creating the requirements doc (#202), completing the test strategy, and receiving 6 team reviews, the backlog needed consolidation and prioritization.

**Decision:** 14 GitHub issues created across 4 priority tiers, synthesized from requirements review (#202), test strategy reviews (6 reviewers), AD-020 (pipeline architecture assessment), AD-021 (Step 3/6 validator requirement), and existing backlog.

**Priority Framework:**

| Tier | Criteria | Count | Issues |
|------|----------|-------|--------|
| **P0** | Data loss or silent bad content shipping | 3 | #203, #204, #205 |
| **P1** | Catches bugs before production | 5 | #206, #207, #208, #209, #210 |
| **P2** | Improves developer experience / observability | 4 | #211, #212, #213, #214 |
| **P3** | Improves quality over time | 2 | #215, #216 |

**Issue Assignments:**

| # | Title | Priority | Owner |
|---|-------|----------|-------|
| #203 | Fix ResetOutputDirectory ΓÇö destroys partial progress | P0 | Riley |
| #204 | Add Step 3 post-validator ΓÇö template token leakage | P0 | Morgan |
| #205 | Add Step 6 post-validator ΓÇö incomplete horizontal articles | P0 | Sage |
| #206 | Add TextCleanup unit tests ΓÇö high-risk regex chain | P1 | Parker |
| #207 | Add failure path tests for all pipeline steps | P1 | Parker |
| #208 | Add Bootstrap contract tests ΓÇö step I/O validation | P1 | Avery |
| #209 | Implement baseline fingerprinting for generated output | P1 | Avery + Quinn |
| #210 | Replace regex error detection with structured step-result.json | P1 | Riley |
| #211 | Add prompt versioning system | P2 | Sage |
| #212 | Add token usage tracking and observability | P2 | Sage |
| #213 | Create CI integration documentation | P2 | Quinn |
| #214 | Build prompt regression testing framework | P2 | Sage |
| #215 | Acrolinx compliance ΓÇö automated style fixes | P3 | Sage |
| #216 | Consolidate StripFrontmatter implementations | P3 | Morgan |

**Execution Order:**
1. **Immediate:** P0 issues (#203, #204, #205) ΓÇö all three can run in parallel
2. **Next sprint:** P1 issues ΓÇö #206 and #210 first (highest test/infra leverage), then #207, #208, #209
3. **Following sprint:** P2 issues ΓÇö #211 and #214 first (prompt quality loop), then #212, #213
4. **Backlog:** P3 issues ΓÇö pick up opportunistically

**Rationale:** Prioritization follows a single principle: **prevent harm before adding value.** P0 prevents data loss and silent failures. P1 builds the safety net. P2 makes the team faster. P3 polishes quality. Each tier's value compounds ΓÇö fingerprinting (P1) enables prompt regression testing (P2), which enables prompt versioning (P2) to be meaningful.

**Impact:** All team members have assigned work. The `squad` label on all issues enables triage routing. Individual `squad:{member}` labels enable filtered views per team member.

---

### AD-024: LearnUrlRelativizer ΓÇö Deterministic Post-Processing Backstop for Full URLs
**Date:** 2026-03-25
**Author:** Morgan (C# Generator Developer)
**Status:** Active
**Related:** PR #221, Issue #220

**Context:** Generated tool-family files contained full `https://learn.microsoft.com/azure/...` URLs violating AD-017 (Link Format Convention). Root cause: AI-generated content (Step 4 intro paragraphs) produces full URLs despite prompt instructions telling it to use relative paths.

**Decision:** Added `LearnUrlRelativizer` as a deterministic post-processing stage (Stage 12 in FamilyFileStitcher) that converts all full learn.microsoft.com URLs to site-root-relative paths. This is a **belt-and-suspenders** approach: prompts already request relative URLs, but the post-processor enforces it deterministically.

**Key Design Choices:**
1. **Regex with `[GeneratedRegex]`** ΓÇö source-generated for performance, handles locale stripping (`/en-us`), query params, and anchors.
2. **Code-block protection** ΓÇö skips URLs inside backticks and fenced code blocks (consistent with ContractionFixer, PresentTenseFixer pattern).
3. **Placed last in pipeline** ΓÇö Stage 12, after all other text transformations, so it catches any URLs introduced by earlier stages.
4. **Applies to all learn.microsoft.com paths** ΓÇö not just `/azure/...` but also `/cli/`, `/dotnet/`, etc.

**Rationale:** AI content generation is inherently non-deterministic ΓÇö prompt instructions alone cannot guarantee URL format compliance. Every post-processing service in the pipeline follows this pattern: deterministic fix as backstop for AI behavior. The HorizontalArticles pipeline already had `StripLearnPrefix` for its own content; this extends the same principle to tool-family files.

**Impact:** All generated tool-family files will have site-root-relative paths for learn.microsoft.com links. Future AI-generated content that includes full learn URLs will be automatically corrected. Minimal performance impact ΓÇö regex runs once per file at the end of the pipeline.

**Test Coverage:** 17 TDD tests (AD-007) covering all edge cases ΓÇö locale stripping, query params, anchors, code-block protection, idempotency.

---

### AD-025: Test-Driven Quality Assurance ΓÇö PR #217 and #218 Assessment
**Date:** 2026-03-25
**Author:** Parker (QA / Tester)
**Status:** Active
**Related:** PR #217 (Generation Report), PR #218 (Acrolinx Compliance)

**Context:** Comprehensive QA review of two major PRs following team standardization on AD-007 (TDD) and AD-010 (behavioral test depth).

**Decision:** Both PRs **APPROVED** for merge based on exceptional test coverage and behavioral alignment with AD-010 standards.

**PR #217 ΓÇö Generation Report (Quinn):**
- **Verdict:** APPROVE
- **Test results:** 28/28 passing (Node.js `node:test`)
- **AD-010 compliance:** Γ£à PASS ΓÇö All 5 exported functions (`loadCommonParams`, `extractNamespaces`, `computeToolStats`, `computeNamespaceSummary`, `generateReport`) have tests asserting on specific output values. Reverting any function logic would break tests.
- **Edge cases:** Γ£à Comprehensive ΓÇö empty results, missing option arrays, determinism checks, alphabetical sort verification
- **Test data:** Γ£à Realistic ΓÇö real Azure namespace patterns (acr, cosmos) with production parameter names (`--tenant`, `--retry-*`, etc.) from `common-parameters.json` schema
- **Coverage gaps (non-blocking):** `readJson()` (npm output parser) and `parseArgs()` untested; integration test for CLI `main()` missing ΓÇö acceptable for report script

**PR #218 ΓÇö Acrolinx Compliance (Morgan):**
- **Verdict:** APPROVE
- **Test results:** 202/202 ToolFamilyCleanup tests + 240/240 Annotations tests = 442/442 passing; full solution 1,149 tests, 0 failures
- **AD-010 compliance:** Γ£à PASS ΓÇö All services tested behaviorally:
  - AcronymExpander: 12 tests asserting exact output ("virtual machine (VM)" appears)
  - IntroductoryCommaFixer: 14 tests asserting comma insertion
  - PresentTenseFixer: 16 tests asserting tense conversion ("will return" ΓåÆ "returns")
  - StaticTextReplacement: 20 tests asserting phrase replacements
  - StitcherAcrolinxIntegrationTests: 7 integration tests verifying full pipeline wiring and ordering
- **Critical strength:** 7 integration tests call `FamilyFileStitcher.Stitch()` end-to-end, verifying correct order and combined behavior
- **Edge cases:** Γ£à Comprehensive ΓÇö null/empty inputs, idempotency, code-block protection, heading/frontmatter protection, sentence boundaries, mid-sentence exclusion, proper noun handling, plural subject detection
- **Test data:** Γ£à Realistic ΓÇö Azure-specific content (VM management, AKS, RBAC, storage, Cosmos DB)
- **Coverage gaps (non-blocking):** Acronym definitions in JSON have no dedicated tests; verb whitelist only 6/16 verbs individually tested; file-loading fallback untested ΓÇö all use same code paths, low risk

**Key Assessment:** Both PRs exemplify expected TDD pattern. Template-level regression tests in both PRs satisfy AD-019. Zero regressions across full solution. Both ready for merge.

---

### AD-026: PR Documentation Skill ΓÇö CHANGELOG + Docs Update Required
**Date:** 2026-03-29
**Author:** Dina Berry (via Copilot)
**Status:** Active
**Extends:** AD-004

**Every PR must run the `pr-docs` skill before team review.** This means:

1. **CHANGELOG.md** must be updated with a user-facing entry under `## [Unreleased]`
2. **User-facing documentation** must be updated based on the documentation routing table in `.squad/skills/pr-docs/SKILL.md`
3. **README navigation** must be updated if new documentation files are created

**Exemptions** (must be stated in PR comment):
- Test-only changes: "Test coverage addition, no user-facing change"
- Internal refactors: "Internal refactor, no behavior change"
- Docs-only PRs: The PR itself IS the docs update (still needs CHANGELOG entry)

**Enforcement:** The `pr-docs` skill is called before the `pr-review` skill. Reeve validates completeness during team review.

---

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction

# Decision: Acrolinx Compliance Sections in All AI System Prompts

**Date:** 2026-03-25
**Author:** Sage (AI / Prompt Engineer)
**Status:** Implemented (PR #223)

## Decision

Every AI system prompt that generates prose for published articles must include a dedicated **Acrolinx Compliance Guidelines** section with 10 standardized rules.

## Rules Added to All Prompts

1. Present tense (no "will return" ΓÇö use "returns")
2. Contractions ("doesn't" not "does not")
3. Active voice ("The tool lists" not "Resources are listed")
4. Introductory commas ("For example, ..." "By default, ...")
5. No first person (never "we", "our", "us")
6. Acronym expansion on first use ("role-based access control (RBAC)")
7. Site-root-relative URLs ("/azure/..." not full learn.microsoft.com)
8. Sentence length under 35 words
9. No wordy phrases ("to" not "in order to")
10. Brand compliance (official Azure service names)

## Affected Prompts

- Step 2: `ExamplePrompts.Generation/prompts/system-prompt-example-prompt.txt`
- Step 3: `ToolGeneration.Improvements/Prompts/system-prompt.txt`
- Step 4: `ToolFamilyCleanup/prompts/tool-family-cleanup-system-prompt.txt`
- Step 6: `HorizontalArticles/prompts/horizontal-article-system-prompt.txt`
- Shared: `prompts/system-prompt.txt`, `prompts/tool-family-cleanup-system-prompt.txt`

## Rationale

This is the P0 (highest-leverage) item from `docs/acrolinx-compliance-strategy.md`. Adding style rules directly to prompts is more effective than post-processors alone because the AI generates compliant text from the start, reducing the number of violations that downstream fixers need to catch.

## Impact on Team

- **Morgan:** If you modify any system prompt, preserve the Acrolinx section. 42 tests in `AcrolinxComplianceSectionTests.cs` will fail if the section is removed.
- **Parker:** The new tests are in `DocGeneration.Steps.ToolFamilyCleanup.Tests/AcrolinxComplianceSectionTests.cs`. They read prompt files from disk and assert required keywords.
- **Quinn:** No pipeline changes needed ΓÇö prompts are loaded at runtime.

---

### AD-027: .NET Project Consolidation Plan ΓÇö Team Review and Approval
**Date:** 2026-03-30  
**Author:** Avery (Team Lead)  
**Reviewed by:** Riley, Morgan, Cameron, Quinn, Parker, Sage, Reeve  
**Status:** APPROVED WITH CONDITIONS  
**Related:** docs/proposals/dotnet-consolidation-plan.md

**Context:** Investigation identified 42 .NET projects with optimization opportunities: orphaned code (CliAnalyzer), thin shims (PostProcessVerifier), single-file libraries (Core.NaturalLanguage), and mixed test frameworks (NUnit + xUnit).

**Decision:** Consolidate 42 ΓåÆ 40 projects through 6 approved actions + 1 deferred architectural review.

**Actions Approved (1-6):**
1. Remove CliAnalyzer ΓÇö orphaned Bootstrap utility, 8 files
2. Merge PostProcessVerifier ΓåÆ ToolFamilyCleanup ΓÇö add `--verify-only` flag
3. Merge Core.NaturalLanguage ΓåÆ Core.Shared ΓÇö preserve namespace for zero-churn refactor
4. Standardize NUnit ΓåÆ xUnit ΓÇö 3 test projects, 155 tests
5. Consolidate StripFrontmatter duplication ΓÇö note: Fingerprint has `.TrimStart()` behavior; recommend keeping local implementation (Option A)
6. Document ToolFamilyCleanup.Validation.Tests ΓÇö explain PowerShell integration test design

**Action Deferred (7):**
- Bootstrap sub-step consolidation ΓÇö Riley rejected (violates subprocess isolation contract; resilience requirement)

**Conditions & Requirements by Reviewer:**

| Reviewer | Verdict | Key Conditions |
|----------|---------|----------------|
| Riley (Architect) | APPROVE WITH CHANGES | Namespace preservation (Action 3), exit code preservation (Action 2), Action 7 rejection documented |
| Morgan (C# Dev) | APPROVE WITH CHANGES | 7-hour implementation estimate; behavioral caveat on Action 5 (Fingerprint StripFrontmatter); test before/after for Actions 4 |
| Cameron (Test Lead) | APPROVE WITH CHANGES | 3 quality gates: full test pass, pipeline dry-run (3 namespaces), fingerprint regression check |
| Quinn (DevOps) | APPROVED | No CI/CD changes needed; script audit required (Actions 1-2); deprecation message for CliAnalyzer wrapper |
| Parker (QA) | APPROVE WITH CONTINGENCIES | 9 acceptance criteria: orphan verification, baseline comparisons, file discovery tests, test count matching |
| Sage (AI/Prompt) | RECOMMEND APPROVAL | 3 safeguards: post-processor order test (Action 2), output consistency test (Action 3), assertion equivalence (Action 4) |
| Reeve (Docs) | APPROVED | 3 tech docs need updates (ci-integration.md, test-strategy.md), 1 new README (Validation.Tests) |

**Impact Assessment:**
- **Build time:** ~5% faster (fewer projects)
- **Test coverage:** ~1,100+ tests preserved (zero deletions, consolidations only)
- **CI/CD:** No workflow changes required (auto-discovery continues)
- **Developers:** Cleaner mental model (fewer projects, unified test framework)

**Execution Timeline:**
- Phase 1 (Immediate): Actions 1, 6, 5 (~1 hour, low risk)
- Phase 2 (Same sprint): Actions 2, 4 (~4-5 hours, medium risk)
- Phase 3 (Next sprint): Action 3 (~2-3 hours, medium-high risk)
- **Total:** 6-8 hours across 1-2 sprints

**Team Assignments:**
- **Morgan:** Implement Actions 2-5
- **Quinn:** Script audit (Actions 1-2), CI validation
- **Parker:** Run before/after test baselines, file discovery validation
- **Riley:** Namespace preservation oversight, subprocess isolation protection
- **Reeve:** Write Validation.Tests README, update documentation
- **Sage:** Validate post-processor order, AI output consistency
- **Cameron:** Design quality gates and regression tests

**Rationale:** Consolidation improves code maintainability without sacrificing resilience. Subprocess isolation (ProcessRunner architecture) preserved. Clean dependency graph maintained. All 1,100+ tests remain executable. Conservative approach: don't break what works.

---

### AD-028: Quality Gate Strategy for .NET Consolidation
**Date:** 2026-03-30  
**Author:** Cameron (Test Lead)  
**Status:** Active  
**Related:** AD-027

**Three mandatory quality gates must pass before consolidation completes:**

**Gate 1: Full Test Pass**
- Command: `dotnet test docs-generation.sln`
- Criteria: 0 failures, 0 skipped (unless pre-existing), test count matches baseline
- Timing: After each consolidation phase

**Gate 2: Pipeline Dry-Run on Representative Namespaces**
- Scope: 3 namespaces (advisor, storage, compute)
- Command: `./start.sh --namespace {name} --verify-only` for each
- Criteria: All 7 steps execute, output directories exist, no runtime file errors
- Timing: After all Actions 1-5 complete

**Gate 3: Regression Baseline Fingerprinting**
- Before: `./prompt-regression.sh --baseline`
- After: `./prompt-regression.sh --verify`
- Criteria: Fingerprints match or improve (no hallucinations)
- Timing: Final validation before merge

**Responsibility:** Parker executes all gates; team reviews results before proceeding to next phase.

---

### AD-029: Critical Data File Discovery Requirement (Action 3)
**Date:** 2026-03-30  
**Author:** Morgan (C# Developer), Parker (QA)  
**Status:** Active  
**Related:** AD-027 (Action 3)

**Before Core.NaturalLanguage merge, data file runtime discovery MUST be verified:**

**Files Affected:**
- nl-parameters.json
- static-text-replacement.json
- nl-parameter-identifiers.json

**Validation Required:**
1. Audit TextCleanup.cs file loading mechanism (embedded resources vs. relative paths vs. BaseDirectory)
2. Verify Core.Shared.csproj has `<CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>`
3. Write integration test: `Core.Shared.Tests/TextCleanupRuntimeFileDiscoveryTests.cs`
4. Run HorizontalArticles (Step 6) on 5 diverse namespaces end-to-end
5. Verify TextCleanup processes without file-not-found errors

**Risk if missed:** Runtime crash in TextCleanup after merge (data files not found at new location).

**Ownership:** Morgan (implementation), Parker (validation).

---

### AD-030: Exit Code Preservation Requirement (Action 2)
**Date:** 2026-03-30  
**Author:** Riley (Architect)  
**Status:** Active  
**Related:** AD-027 (Action 2)

**When merging PostProcessVerifier into ToolFamilyCleanup's `--verify-only` flag, exit codes MUST remain unchanged:**

**Exit Code Contract:**
- Success: exit 0
- Validation failure: exit 1
- File not found: exit 2 (or appropriate error code)

**Why:** Scripts and CI workflows may parse exit codes for error handling. Changing exit codes breaks backward compatibility.

**Validation:** Before deletion of PostProcessVerifier, run ToolFamilyCleanup with `--verify-only` on 5+ namespaces and verify exit codes match old tool.

**Ownership:** Morgan (implementation), Quinn (script verification).

---

### AD-031: Namespace Preservation for Core.NaturalLanguage (Action 3)
**Date:** 2026-03-30  
**Author:** Riley (Architect), Morgan (C# Developer)  
**Status:** Active  
**Related:** AD-027 (Action 3)

**After merging Core.NaturalLanguage ΓåÆ Core.Shared, KEEP namespace as `DocGeneration.Core.NaturalLanguage`:**

**Rationale:** Three downstream projects (Annotations, RawTools, HorizontalArticles) import `using DocGeneration.Core.NaturalLanguage;`. Changing namespace would require updating all 3 projects + 6 test files (churn). Namespace preservation makes this a zero-change refactor.

**Implementation:**
```csharp
// File: Core.Shared/NaturalLanguage/TextCleanup.cs
namespace DocGeneration.Core.NaturalLanguage  // ΓåÉ KEEP THIS
{
    public static class TextCleanup { ... }
}
```

**Project reference changes needed:** Update 3 step projects' .csproj to remove `<ProjectReference>` to Core.NaturalLanguage (they already reference Core.Shared).

**Ownership:** Morgan (implementation), Riley (verification).

---

### AD-032: Test Baseline Verification for Framework Migration (Action 4)
**Date:** 2026-03-30  
**Author:** Cameron (Test Lead), Morgan (C# Developer)  
**Status:** Active  
**Related:** AD-027 (Action 4)

**NUnit ΓåÆ xUnit migration for 3 test projects (155 tests) requires before/after test count matching:**

**Process:**
1. Before migration: `dotnet test Core.TextTransformation.Tests Core.HorizontalArticles.Tests Core.SkillsRelevance.Tests > baseline.txt` (record test count and results)
2. Execute migration: Replace NUnit attributes ΓåÆ xUnit, rewrite assertions
3. After migration: `dotnet test Core.TextTransformation.Tests Core.HorizontalArticles.Tests Core.SkillsRelevance.Tests > migration.txt`
4. Verification: `diff baseline.txt migration.txt` ΓåÆ must be identical in test count and results

**Why:** NUnit and xUnit have subtle assertion semantic differences (argument order, message formatting). Before/after comparison catches silent assertion failures.

**Risk if skipped:** Silent test regressions due to incorrect assertion rewrites (e.g., `Assert.Equal(expected, actual)` vs. `Assert.Equal(actual, expected)` order matters).

**Ownership:** Cameron (audit), Morgan (migration), Parker (validation).

---

### AD-033: Post-Processor Order Verification (Action 2, AI Safeguard)
**Date:** 2026-03-30  
**Author:** Sage (AI/Prompt Engineer), Morgan (C# Developer)  
**Status:** Active  
**Related:** AD-027 (Action 2)

**When merging PostProcessVerifier into ToolFamilyCleanup, post-processor order MUST remain identical:**

**Critical Chain (10 post-processors in order):**
1. AcronymExpander
2. FrontmatterEnricher
3. DuplicateExampleStripper
4. AnnotationSpaceFixer
5. PresentTenseFixer
6. ContractionFixer
7. IntroductoryCommaFixer
8. ExampleValueBackticker
9. LearnUrlRelativizer
10. JsonSchemaCollapser

**Validation:** Run ToolFamilyCleanup with `--verify-only` on 5 diverse namespaces (StorageAccount, KeyVault, CosmosDB, EventGrid, Compute) and verify `.after` files are byte-identical to old PostProcessVerifier output.

**Risk:** Reordering processors changes AI output quality gates and content correctness validation.

**Ownership:** Morgan (implementation), Sage (validation).

---

### AD-034: AI Output Consistency Test for Core.NaturalLanguage Merge (Action 3, AI Safeguard)
**Date:** 2026-03-30  
**Author:** Sage (AI/Prompt Engineer), Parker (QA)  
**Status:** Active  
**Related:** AD-027 (Action 3)

**After merging Core.NaturalLanguage ΓåÆ Core.Shared, HorizontalArticles (Step 6) AI output MUST remain identical:**

**Validation Process:**
1. Generate baseline with old Core.NaturalLanguage: Run Step 6 on 5 diverse namespaces, capture output
2. Execute consolidation (Action 3)
3. Generate new output with merged Core.Shared: Run Step 6 on same 5 namespaces
4. Compare: TextCleanup output must be byte-identical (no parameter normalization regressions)

**Namespace Validation:** Run PromptRegression.Tests to verify `.after` files used by regression detection remain compatible.

**Risk:** Data file discovery failure or namespace change breaks TextCleanup runtime, causing Step 6 output degradation.

**Ownership:** Parker (test execution), Sage (output validation).
