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

### AD-020: User Workflow Directive — Mandatory Development Process
**Date:** 2026-03-24 (updated 2026-03-29)
**Author:** Dina Berry (via Copilot)
**Status:** Active

**All development work must follow this process in strict order:**
1. Create a plan (design, test strategy, edge cases)
2. Write failing tests (define the contract before implementation)
3. Write implementation code (make the tests pass)
4. Run tests (all must pass, no regressions)
5. Create PR — **STOP. Do NOT merge.**
6. Full team review (all 9 reviewers: Avery, Riley, Morgan, Quinn, Sage, Cameron, Parker, Reeve, Copilot)
7. User (Dina) reviews and merges

**NEVER merge a PR. Only Dina merges.** Sub-agents must NEVER call `gh pr merge`. Their job ends at step 5 (create PR). This applies to all agents — general-purpose, task, and squad members alike.

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

### AD-021: Step 3 and Step 6 Must Have Post-Validators Before Next Release
**Date:** 2026-03-25  
**Author:** Parker (QA/Tester)  
**Status:** Active  
**Extends:** AD-002, AD-010  
**Triggered by:** Test strategy audit (docs/test-strategy.md)

**Decision:** Steps 3 (ToolGeneration) and 6 (HorizontalArticles) must implement `IPostValidator` before the next release. These are the only AI-dependent steps with **no output validation at all**, creating the two highest-risk gaps identified in AD-002:

1. **Step 3:** Template token leakage (`<<<TPL_LABEL_N>>>`) in final output goes undetected
2. **Step 6:** AI JSON parse failures producing incomplete horizontal articles go undetected

**Rationale:** The test strategy audit found that only Step 4 has a post-validator. Steps 3 and 6 produce AI-generated content that feeds directly into the final documentation corpus — if they fail silently, bad content ships. Both risks are already documented in AD-002 but have no mitigation.

**Impact:** Morgan or Sage should implement these validators. Parker will write tests for the validators once implemented. Blocks: any release claiming "full pipeline validation".

---

### AD-022: Acrolinx Compliance Strategy for Tool-Family Articles
**Date:** 2026-03-25
**Author:** Sage (AI / Prompt Engineer)
**Status:** Active
**Triggered by:** Acrolinx gate requirement; 30% pass rate on tool-family articles; issues #142-#146; PR review feedback from azure-dev-docs-pr

**Context:** Our generated tool-family articles currently pass Acrolinx quality gate (80+) at only 30% rate (3/10). Worst performers: Deploy (61), Postgres (64), Cloud Architect (67). Acrolinx is mandatory for merging to production docs.

**Decision:** Implement **6-priority remediation plan** combining prompt changes (P0) and deterministic post-processors (P1-P4):

1. **P0 — System prompt update:** Add explicit Acrolinx compliance rules to `tool-family-cleanup-system-prompt.txt` (sentence length ≤25 words, present tense, active voice, contractions, introductory commas, wordy phrase avoidance).
2. **P1 — JsonSchemaCollapser:** New post-processor to collapse inline JSON schema parameter descriptions into human-readable summaries. Expected +15-20 pts for Deploy.
3. **P1 — ContractionFixer extension:** Add positive contractions ("it is"→"it's", "you are"→"you're", etc.) to existing ContractionFixer.
4. **P2 — WordyPhraseFixer + static replacements:** Deterministic removal of "in order to", "due to the fact that", deprecated Microsoft terms ("Azure AD"→"Microsoft Entra ID"), and ableist language ("simply", "just").
5. **P3 — TenseFixer + AcronymExpander:** Present tense enforcement ("will list"→"lists") and multi-acronym first-use expansion.
6. **P4 — SentenceLengthWarner:** Diagnostic logging for sentences exceeding 25 words (inform, not auto-fix).

**Rationale:** Post-processing is preferred over prompt-only fixes because it's **deterministic** — a regex that converts "it is" to "it's" always works, while an AI prompt instruction may be ignored 20% of the time. The prompt changes (P0) remain valuable as first-line defense.

**Quick win:** Expanding `static-text-replacement.json` with 15 wordy phrases, 8 deprecated terms, and 5 ableist language removals yields +5-10 pts with zero code changes.

**Impact:**
- **Sage:** Owns prompt change (P0) and all post-processor implementations (P1-P3).
- **Morgan:** May need to adjust FamilyFileStitcher call order if new post-processors are added.
- **Parker:** Must write tests for each new post-processor per AD-007 and AD-010.
- **All namespaces:** Changes apply universally across all 52 namespaces — no service-specific logic.

---

### AD-023: Work Prioritization Framework — Post-Review Issue Set
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
| #203 | Fix ResetOutputDirectory — destroys partial progress | P0 | Riley |
| #204 | Add Step 3 post-validator — template token leakage | P0 | Morgan |
| #205 | Add Step 6 post-validator — incomplete horizontal articles | P0 | Sage |
| #206 | Add TextCleanup unit tests — high-risk regex chain | P1 | Parker |
| #207 | Add failure path tests for all pipeline steps | P1 | Parker |
| #208 | Add Bootstrap contract tests — step I/O validation | P1 | Avery |
| #209 | Implement baseline fingerprinting for generated output | P1 | Avery + Quinn |
| #210 | Replace regex error detection with structured step-result.json | P1 | Riley |
| #211 | Add prompt versioning system | P2 | Sage |
| #212 | Add token usage tracking and observability | P2 | Sage |
| #213 | Create CI integration documentation | P2 | Quinn |
| #214 | Build prompt regression testing framework | P2 | Sage |
| #215 | Acrolinx compliance — automated style fixes | P3 | Sage |
| #216 | Consolidate StripFrontmatter implementations | P3 | Morgan |

**Execution Order:**
1. **Immediate:** P0 issues (#203, #204, #205) — all three can run in parallel
2. **Next sprint:** P1 issues — #206 and #210 first (highest test/infra leverage), then #207, #208, #209
3. **Following sprint:** P2 issues — #211 and #214 first (prompt quality loop), then #212, #213
4. **Backlog:** P3 issues — pick up opportunistically

**Rationale:** Prioritization follows a single principle: **prevent harm before adding value.** P0 prevents data loss and silent failures. P1 builds the safety net. P2 makes the team faster. P3 polishes quality. Each tier's value compounds — fingerprinting (P1) enables prompt regression testing (P2), which enables prompt versioning (P2) to be meaningful.

**Impact:** All team members have assigned work. The `squad` label on all issues enables triage routing. Individual `squad:{member}` labels enable filtered views per team member.

---

### AD-024: LearnUrlRelativizer — Deterministic Post-Processing Backstop for Full URLs
**Date:** 2026-03-25
**Author:** Morgan (C# Generator Developer)
**Status:** Active
**Related:** PR #221, Issue #220

**Context:** Generated tool-family files contained full `https://learn.microsoft.com/azure/...` URLs violating AD-017 (Link Format Convention). Root cause: AI-generated content (Step 4 intro paragraphs) produces full URLs despite prompt instructions telling it to use relative paths.

**Decision:** Added `LearnUrlRelativizer` as a deterministic post-processing stage (Stage 12 in FamilyFileStitcher) that converts all full learn.microsoft.com URLs to site-root-relative paths. This is a **belt-and-suspenders** approach: prompts already request relative URLs, but the post-processor enforces it deterministically.

**Key Design Choices:**
1. **Regex with `[GeneratedRegex]`** — source-generated for performance, handles locale stripping (`/en-us`), query params, and anchors.
2. **Code-block protection** — skips URLs inside backticks and fenced code blocks (consistent with ContractionFixer, PresentTenseFixer pattern).
3. **Placed last in pipeline** — Stage 12, after all other text transformations, so it catches any URLs introduced by earlier stages.
4. **Applies to all learn.microsoft.com paths** — not just `/azure/...` but also `/cli/`, `/dotnet/`, etc.

**Rationale:** AI content generation is inherently non-deterministic — prompt instructions alone cannot guarantee URL format compliance. Every post-processing service in the pipeline follows this pattern: deterministic fix as backstop for AI behavior. The HorizontalArticles pipeline already had `StripLearnPrefix` for its own content; this extends the same principle to tool-family files.

**Impact:** All generated tool-family files will have site-root-relative paths for learn.microsoft.com links. Future AI-generated content that includes full learn URLs will be automatically corrected. Minimal performance impact — regex runs once per file at the end of the pipeline.

**Test Coverage:** 17 TDD tests (AD-007) covering all edge cases — locale stripping, query params, anchors, code-block protection, idempotency.

---

### AD-025: Test-Driven Quality Assurance — PR #217 and #218 Assessment
**Date:** 2026-03-25
**Author:** Parker (QA / Tester)
**Status:** Active
**Related:** PR #217 (Generation Report), PR #218 (Acrolinx Compliance)

**Context:** Comprehensive QA review of two major PRs following team standardization on AD-007 (TDD) and AD-010 (behavioral test depth).

**Decision:** Both PRs **APPROVED** for merge based on exceptional test coverage and behavioral alignment with AD-010 standards.

**PR #217 — Generation Report (Quinn):**
- **Verdict:** APPROVE
- **Test results:** 28/28 passing (Node.js `node:test`)
- **AD-010 compliance:** ✅ PASS — All 5 exported functions (`loadCommonParams`, `extractNamespaces`, `computeToolStats`, `computeNamespaceSummary`, `generateReport`) have tests asserting on specific output values. Reverting any function logic would break tests.
- **Edge cases:** ✅ Comprehensive — empty results, missing option arrays, determinism checks, alphabetical sort verification
- **Test data:** ✅ Realistic — real Azure namespace patterns (acr, cosmos) with production parameter names (`--tenant`, `--retry-*`, etc.) from `common-parameters.json` schema
- **Coverage gaps (non-blocking):** `readJson()` (npm output parser) and `parseArgs()` untested; integration test for CLI `main()` missing — acceptable for report script

**PR #218 — Acrolinx Compliance (Morgan):**
- **Verdict:** APPROVE
- **Test results:** 202/202 ToolFamilyCleanup tests + 240/240 Annotations tests = 442/442 passing; full solution 1,149 tests, 0 failures
- **AD-010 compliance:** ✅ PASS — All services tested behaviorally:
  - AcronymExpander: 12 tests asserting exact output ("virtual machine (VM)" appears)
  - IntroductoryCommaFixer: 14 tests asserting comma insertion
  - PresentTenseFixer: 16 tests asserting tense conversion ("will return" → "returns")
  - StaticTextReplacement: 20 tests asserting phrase replacements
  - StitcherAcrolinxIntegrationTests: 7 integration tests verifying full pipeline wiring and ordering
- **Critical strength:** 7 integration tests call `FamilyFileStitcher.Stitch()` end-to-end, verifying correct order and combined behavior
- **Edge cases:** ✅ Comprehensive — null/empty inputs, idempotency, code-block protection, heading/frontmatter protection, sentence boundaries, mid-sentence exclusion, proper noun handling, plural subject detection
- **Test data:** ✅ Realistic — Azure-specific content (VM management, AKS, RBAC, storage, Cosmos DB)
- **Coverage gaps (non-blocking):** Acronym definitions in JSON have no dedicated tests; verb whitelist only 6/16 verbs individually tested; file-loading fallback untested — all use same code paths, low risk

**Key Assessment:** Both PRs exemplify expected TDD pattern. Template-level regression tests in both PRs satisfy AD-019. Zero regressions across full solution. Both ready for merge.

---

### AD-026: PR Documentation Skill — CHANGELOG + Docs Update Required
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

1. Present tense (no "will return" — use "returns")
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
- **Quinn:** No pipeline changes needed — prompts are loaded at runtime.

---

### AD-027: .NET Project Consolidation Plan — Team Review and Approval
**Date:** 2026-03-30  
**Author:** Avery (Team Lead)  
**Reviewed by:** Riley, Morgan, Cameron, Quinn, Parker, Sage, Reeve  
**Status:** APPROVED WITH CONDITIONS  
**Related:** docs/proposals/dotnet-consolidation-plan.md

**Context:** Investigation identified 42 .NET projects with optimization opportunities: orphaned code (CliAnalyzer), thin shims (PostProcessVerifier), single-file libraries (Core.NaturalLanguage), and mixed test frameworks (NUnit + xUnit).

**Decision:** Consolidate 42 → 40 projects through 6 approved actions + 1 deferred architectural review.

**Actions Approved (1-6):**
1. Remove CliAnalyzer — orphaned Bootstrap utility, 8 files
2. Merge PostProcessVerifier → ToolFamilyCleanup — add `--verify-only` flag
3. Merge Core.NaturalLanguage → Core.Shared — preserve namespace for zero-churn refactor
4. Standardize NUnit → xUnit — 3 test projects, 155 tests
5. Consolidate StripFrontmatter duplication — note: Fingerprint has `.TrimStart()` behavior; recommend keeping local implementation (Option A)
6. Document ToolFamilyCleanup.Validation.Tests — explain PowerShell integration test design

**Action Deferred (7):**
- Bootstrap sub-step consolidation — Riley rejected (violates subprocess isolation contract; resilience requirement)

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

**After merging Core.NaturalLanguage → Core.Shared, KEEP namespace as `DocGeneration.Core.NaturalLanguage`:**

**Rationale:** Three downstream projects (Annotations, RawTools, HorizontalArticles) import `using DocGeneration.Core.NaturalLanguage;`. Changing namespace would require updating all 3 projects + 6 test files (churn). Namespace preservation makes this a zero-change refactor.

**Implementation:**
```csharp
// File: Core.Shared/NaturalLanguage/TextCleanup.cs
namespace DocGeneration.Core.NaturalLanguage  // ← KEEP THIS
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

**NUnit → xUnit migration for 3 test projects (155 tests) requires before/after test count matching:**

**Process:**
1. Before migration: `dotnet test Core.TextTransformation.Tests Core.HorizontalArticles.Tests Core.SkillsRelevance.Tests > baseline.txt` (record test count and results)
2. Execute migration: Replace NUnit attributes → xUnit, rewrite assertions
3. After migration: `dotnet test Core.TextTransformation.Tests Core.HorizontalArticles.Tests Core.SkillsRelevance.Tests > migration.txt`
4. Verification: `diff baseline.txt migration.txt` → must be identical in test count and results

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

**After merging Core.NaturalLanguage → Core.Shared, HorizontalArticles (Step 6) AI output MUST remain identical:**

**Validation Process:**
1. Generate baseline with old Core.NaturalLanguage: Run Step 6 on 5 diverse namespaces, capture output
2. Execute consolidation (Action 3)
3. Generate new output with merged Core.Shared: Run Step 6 on same 5 namespaces
4. Compare: TextCleanup output must be byte-identical (no parameter normalization regressions)

**Namespace Validation:** Run PromptRegression.Tests to verify `.after` files used by regression detection remain compatible.

**Risk:** Data file discovery failure or namespace change breaks TextCleanup runtime, causing Step 6 output degradation.

**Ownership:** Parker (test execution), Sage (output validation).


---

### 2026-04-17T06:02:15Z: User directive — Skills page content purpose
**By:** Dina (via Copilot)
**What:** Generated skill documentation pages must NOT be a duplication of the SKILL.md file. They should be a consolidated explanation of what a customer needs to know in order to use the skill successfully. The SKILL.md is an anthropic-spec agent instruction file; the generated doc page is a customer-facing reference.
**Why:** User request — this reframes the entire generation approach. The template, prompts, and extraction logic should all orient around "what does a customer need to know?" not "mirror the SKILL.md structure."


---

### 2026-04-16T17:39:33Z: User directive
**By:** Dina (via Copilot)
**What:** The only format the engineering team provides is the anthropic spec (SKILL.md files). These files are inconsistent across skills. The content generation must map whatever structure exists in the SKILL.md files to consistent documentation sections. Do not expect a standardized schema — build the mapping from what's actually there.
**Why:** User request — captured for team memory


---

# Skills Generation: Customer-Facing Page Design

**Date:** 2026-04-17  
**Author:** Riley (Pipeline Architect)  
**Status:** RECOMMENDATION  
**Priority:** P0 — reframes the entire generation approach  
**Requested by:** Dina  
**Relates to:** Sage's `sage-skills-llm-prompt-redesign.md`, directive `copilot-directive-skills-customer-purpose.md`

---

## Framing Principle

SKILL.md files are **anthropic-spec agent instruction files**. They describe how an AI agent should behave: what to detect, when to route, what to check before proceeding. They are not product documentation.

The generated doc page answers a different question: **"What does a developer need to know to successfully use this skill?"**

These are two completely different information architectures. The current pipeline treats them as the same, which is why current output reads like agent instructions.

---

## 1. Customer-Facing Section Design

The following sections belong on every generated skill page. Sections without data are omitted (not left empty).

### Section Map

| # | Section | Source in SKILL.md | Notes |
|---|---------|-------------------|-------|
| 1 | **Title + one-line description** | `display_name` + description intro | LLM rewrites to customer voice |
| 2 | **When to use this skill** | `USE FOR:` items | Direct extraction — these are customer scenarios |
| 3 | **Prerequisites** | MANDATORY checks → customer responsibilities | Parser + `BuildPrerequisites()` heuristics |
| 4 | **What Azure services does it work with?** | Services table or bullets | Table: Service + What you can do with it |
| 5 | **What can I ask it to do?** (example triggers) | Trigger test `.json` | Max 8 real prompts from `shouldTrigger` |
| 6 | **MCP tools it uses** | MCP Tools section / Quick Reference | Table: Tool + What it enables for you |
| 7 | **Related skills** | `@azure-*` cross-references | Only skills explicitly referenced |
| 8 | **Related content** | Static links | Always present |

### Section Justification

**Keep:** "When to use" — `USE FOR:` items in SKILL.md are genuinely customer scenarios. They're the best signal in the file of *why a developer would invoke this*.

**Keep:** "Prerequisites" — Translated from agent checklists to customer responsibilities. The current `BuildPrerequisites()` heuristic approach is correct but incomplete (see parser changes below).

**Keep:** "Azure services" — Developers need to know which Azure services this skill touches before they commit to using it. The current services table is good.

**Keep:** "Example triggers" — Concrete prompts are the most actionable part of any skill page. Developers copy-paste these to start.

**Keep:** "MCP tools" — Tells developers what operations the skill can execute on their behalf. Necessary for understanding scope and RBAC implications.

**REMOVE from template:** "Suggested workflow" — Workflow steps in SKILL.md describe *agent execution flow*, not customer experience. The parser is already extracting numbered steps like "Parse JSON → Validate schema → Generate report" — these are internal. A customer doesn't need to know the agent's internal sequence; they need outcomes.

**REMOVE from template:** "Decision guidance" — Decision trees in SKILL.md describe *agent routing logic* (Router archetype C, e.g., azure-compute). "Should I use VM A or VM B?" is router logic for the agent, not customer guidance. Customer decision needs are better served by the "When to use" section and example prompts.

**CONDITIONAL: "What it provides"** — The current hardcoded-string approach (`BuildWhatItProvides()` in `SkillPageGenerator.cs`) produces boilerplate: "The X skill gives GitHub Copilot specialized knowledge about X in Azure." This section has no value as currently generated. It should be **replaced by the LLM-rewritten introduction paragraph** (Sage's work), and the template section removed. The LLM intro already covers this ground better.

---

## 2. What Must Be Excluded from Generated Pages

### Hard Exclusion List (parser must skip or LLM must remove)

| Source Pattern | Reason |
|----------------|--------|
| `⛔ STOP:` conditions | Agent error handlers — not customer knowledge |
| `MANDATORY:` steps | Agent execution requirements — translate to prerequisites or omit |
| `PREFER OVER:` routing | Agent decision logic — not customer choice |
| `FORBIDDEN:` directives | Agent constraints — not relevant to customers |
| Numbered workflow execution steps | Agent procedures (parse → validate → transform) — omit entirely |
| Codebase detection patterns (regex, file discovery) | Agent implementation — omit |
| "If user says X, route to Y" patterns | Agent routing — omit |
| Agent-to-agent handoff rules | Internal plumbing — omit |
| "Works best combined with skill-X" agent routing | Agent orchestration — convert to "See also" only if there's real customer value |
| Sub-skill tables (Archetype D: microsoft-foundry) | Agent orchestration metadata — omit the routing table; keep the capability descriptions |

### Signals the Parser Should Detect and Skip

The following regex patterns indicate agent-internal content that should be stripped before the LLM sees it (or flagged so the LLM knows to exclude):

```
⛔\s*STOP
MANDATORY\s*:
PREFER\s+OVER\s*:
FORBIDDEN\s*:
PREREQUISITE\s+CHECK
DO\s+NOT\s+USE\s+WHEN
```

These blocks can span multiple lines. The parser should extract them into a separate field (`InternalDirectives`) and **not** include them in `RawBody` passed to the LLM.

---

## 3. Template Changes

### Current Template Problems

The current `skill-page-template.hbs` has these structural issues:

1. **"What it provides" section** (line 62–64) renders `{{{whatItProvides}}}` — a hardcoded string from `BuildWhatItProvides()`. This is boilerplate. Remove this section; the LLM-rewritten intro paragraph already covers it.

2. **"Suggested workflow" section** (lines 103–109) renders `workflowSteps` — agent execution flow. Remove.

3. **"Decision guidance" section** (lines 88–101) renders `decisionGuidance` — agent routing tables. Remove.

4. **Prerequisites section** (lines 17–60) is well-structured. Keep. Add a `doNotUseFor` rendering block after the "When to use" section.

5. **"When NOT to use"** is extracted by the parser (`DoNotUseFor`) but has no template section. Add it as a conditional block after "When to use."

### Proposed Template Structure

```
---
[frontmatter]
---

# Azure skill for {{{displayName}}}

{{{llmIntroduction}}}              ← LLM-rewritten introduction (replaces description + whatItProvides)

**Skill:** `{{{name}}}` | [Source code](...)

## Prerequisites
[Azure auth, subscription, RBAC, tools, resources — unchanged]

## When to use this skill
[useFor bullets — unchanged]

## When NOT to use this skill          ← NEW (conditional, from DoNotUseFor)
[doNotUseFor bullets]

## Azure services
[services table — unchanged]

## MCP tools
[mcpTools table — unchanged]

## Example triggers
[shouldTrigger bullets — unchanged, max 8]

## Related skills                       ← Move before related content
[relatedSkills — unchanged]

## Related content
[static links — unchanged]
```

### Removed Sections

- `## What it provides` — replaced by `{{{llmIntroduction}}}` at top
- `## Decision guidance` — removed (agent routing logic)
- `## Suggested workflow` — removed (agent execution steps)

### New Template Variables

| Variable | Source | Notes |
|----------|--------|-------|
| `llmIntroduction` | LLM rewrite of full skill intro | Replaces `description` + `whatItProvides` |
| `hasDoNotUseFor` | `DoNotUseFor.Count > 0` | New conditional |
| `doNotUseFor` | Parsed from `DO NOT USE FOR:` | Already parsed; needs template wiring |

---

## 4. LLM Rewrite Step Changes

Sage's `sage-skills-llm-prompt-redesign.md` covers this ground thoroughly. Riley's additions from an architecture perspective:

### Scope Expansion (Architectural Change)

The current LLM call rewrites **only the description** (line 86 of `SkillPipelineOrchestrator.cs`). This is insufficient.

**Proposed:** The LLM should receive the full skill context and produce the **entire introduction paragraph**, not just rewrite a single field.

```csharp
// Current (too narrow)
var rewrittenDescription = await _llmRewriter.RewriteIntroAsync(skillName, skillData.Description, ct);

// Proposed (full context)
var llmIntro = await _llmRewriter.RewriteIntroAsync(
    skillName: skillName,
    rawDescription: skillData.Description,
    useFor: skillData.UseFor,
    services: skillData.Services.Select(s => s.Name).ToList(),
    ct: ct
);
var updatedSkillData = skillData with { LlmIntroduction = llmIntro };
```

**Why:** The LLM needs `UseFor` to translate agent capability listings into customer value. Without it, the LLM rewrites only a description that may itself be agent-internal language.

**Boundary:** The LLM writes the intro paragraph only. It does **not** rewrite services, MCP tools, triggers, or prerequisites — those are deterministically extracted and should remain that way (reliable, predictable, testable).

### What the Prompt Must Instruct

See Sage's doc for the full prompt. Riley's requirements for the prompt from a pipeline contract perspective:

1. **Output contract:** The LLM must return only the introduction paragraph text — no markdown headers, no bullet lists, no JSON. The pipeline expects plain prose.
2. **Length contract:** 2–4 sentences. Max 80 words. (Current max 60 is too tight for complex skills like microsoft-foundry.)
3. **Fallback contract:** If source material is too agent-internal to produce customer value, return: `"This skill provides specialized {DisplayName} capabilities for GitHub Copilot in Azure."` — a safe fallback the validator can detect and flag as low-quality but not fail-block.

---

## 5. Archetype Impact

All five archetypes must normalize to the **same output template**. The sections that render are conditional on data availability. Here's how each archetype maps:

| Section | A: Service Catalog | B: Workflow | C: Router | D: Complex/Sub-skills | E: Guidance |
|---------|-------------------|-------------|-----------|----------------------|-------------|
| LLM Introduction | ✅ Describe service catalog scope | ✅ Describe workflow outcomes | ✅ Describe decision domain | ✅ Describe sub-skill landscape | ✅ Describe guidance scope |
| When to use | ✅ From USE_FOR | ✅ From USE_FOR | ✅ From USE_FOR | ✅ From USE_FOR | ✅ From USE_FOR |
| Prerequisites | ✅ Azure auth + RBAC + tools | ✅ Tools (Terraform, AZD, etc.) | ✅ Azure auth + subscription | ✅ Complex toolchain | ✅ Azure auth |
| Azure services | ✅ Rich (primary source) | ⚠️ Sparse or absent | ⚠️ Single domain | ✅ Multiple services | ⚠️ Sparse |
| MCP tools | ✅ Rich tool table | ✅ Deployment tools | ✅ Compute tools | ✅ Foundry tools | ⚠️ Sparse |
| Example triggers | ✅ | ✅ | ✅ | ✅ | ✅ |
| Decision guidance | ❌ REMOVED | ❌ REMOVED | ❌ REMOVED | ❌ REMOVED | ❌ REMOVED |
| Suggested workflow | ❌ REMOVED | ❌ REMOVED | ❌ REMOVED | ❌ REMOVED | ❌ REMOVED |

### Archetype-Specific Notes

**Archetype C (Router — azure-compute):** The decision tree content in the SKILL.md is the most tempting to preserve. However, "should I use VM or Container App?" is agent routing logic. On the customer page, this should appear only as example triggers like *"Which Azure compute option suits my API service?"* — letting the user ask the question naturally, not exposing the routing table.

**Archetype D (Complex — microsoft-foundry):** The sub-skill table lists other skills the agent orchestrates. Do not render this table. Instead, extract the sub-skills as `relatedSkills` entries so they appear in the "Related skills" section as navigation links.

**Archetype B (Workflow — azure-deploy, azure-cloud-migrate):** Numbered workflow steps from SKILL.md describe agent execution. Do NOT render as "Suggested workflow." The customer sees this through example triggers ("Deploy my Bicep template to production") and the LLM introduction ("Use this skill when you need to...").

**Archetype E (Guidance — appinsights, azure-rbac):** These skills have minimal services/tools data. The LLM introduction is the primary content. The "When to use" section is critical for these — extract it carefully from the description's USE FOR block.

---

## 6. Parser Changes

### Current Parser Weaknesses Against the New Directive

1. **`RawBody` includes agent-internal content** — sections like `## Workflow`, `## Decision Guidance`, `## Prerequisites (Agent Checks)`, MANDATORY/STOP blocks all flow into `RawBody` which is passed to the LLM. The LLM must then filter them. Better: strip them before they reach the LLM.

2. **`ExtractWorkflowSteps()` should be deprecated** — extracted steps are agent execution flow. The parser should stop populating `WorkflowSteps` or rename it `AgentWorkflowSteps` with a note that it's not for customer pages.

3. **`ExtractDecisionGuidance()` should be deprecated** — decision tables are agent routing. The parser should stop populating `DecisionGuidance` for customer page generation (or isolate it into a separate field not wired to the template).

4. **`ExtractPrerequisites()` is too shallow** — it only extracts bullet text from a `## Prerequisites` section. It doesn't handle agent-style prerequisites like:
   - `MANDATORY: Check if .bicep files exist` → should translate to `Azure CLI with Bicep extension`
   - `PREREQUISITE CHECK: Active subscription required` → should translate to `Azure subscription`
   - These patterns appear throughout the body, not just in a `## Prerequisites` section

5. **No `DoNotUseFor` template wiring** — the parser already extracts `DoNotUseFor` but the template has no section for it.

### Proposed Parser Changes

#### 1. Add `InternalDirectivesBody` field to `SkillData`

```csharp
public record SkillData
{
    // ... existing fields ...
    public string RawBody { get; init; } = "";
    public string RawBodyCleaned { get; init; } = "";  // NEW: RawBody with agent-internal stripped
    public List<string> InternalDirectiveBlocks { get; init; } = [];  // NEW: for audit/debug
}
```

#### 2. Pre-clean `RawBody` before LLM receives it

Add `StripAgentDirectives(string body)` method:

```csharp
private static string StripAgentDirectives(string body)
{
    // Strip lines starting with agent-internal markers
    var lines = body.Split('\n');
    var cleaned = lines.Where(line =>
    {
        var trimmed = line.TrimStart();
        return !trimmed.StartsWith("⛔") &&
               !Regex.IsMatch(trimmed, @"^(MANDATORY|PREFER OVER|FORBIDDEN|PREREQUISITE CHECK)\s*:", 
                   RegexOptions.IgnoreCase);
    }).ToArray();
    
    // Also strip entire sections that are agent-internal
    var result = string.Join('\n', cleaned);
    result = Regex.Replace(result, 
        @"^##\s+(?:Workflow|Decision Guidance|Internal Logic|Agent Behavior|Execution Steps)\s*$.*?(?=^##|\z)",
        "", RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.IgnoreCase);
    
    return result;
}
```

#### 3. Deprecate `WorkflowSteps` and `DecisionGuidance` for customer page generation

Mark them as `[Obsolete]` or add a `[ExcludeFromCustomerPage]` attribute. The `SkillPageGenerator.BuildContext()` should not include these in the template context.

#### 4. Improve prerequisite extraction from agent-check patterns

Extend `ExtractPrerequisites()` to scan the full body for MANDATORY patterns and translate them:

```csharp
// Pattern: MANDATORY: Check if .bicep files present
// → Add resource: "Azure CLI with Bicep extension (if using Bicep templates)"

// Pattern: ⛔ STOP if no Azure subscription
// → Add Azure.RequiresSubscription = true

// Pattern: Required: Active Azure subscription with Owner/Contributor role
// → Add RBAC requirement
```

These complement the heuristic file-extension approach in `BuildPrerequisites()`.

#### 5. Add `DoNotUseFor` to template context (already parsed, not wired)

`SkillPageGenerator.BuildContext()` already passes `doNotUseFor` but the template needs the `hasDoNotUseFor` conditional and section.

---

## 7. Pipeline Contract Changes

### `SkillData` Model Changes

| Field | Change | Reason |
|-------|--------|--------|
| `RawBody` | Keep (internal use) | Needed by parser helpers |
| `RawBodyCleaned` | **Add** | Passed to LLM instead of `RawBody` |
| `InternalDirectiveBlocks` | **Add** | Audit trail of what was stripped |
| `LlmIntroduction` | **Add** | Populated after LLM call; replaces `Description` in template |
| `WorkflowSteps` | Mark as not-for-customer-page | Agent execution steps — not rendered |
| `DecisionGuidance` | Mark as not-for-customer-page | Agent routing — not rendered |

### `ILlmRewriter` Interface Changes

```csharp
// New signature — adds UseFor and Services for richer context
Task<string> RewriteIntroAsync(
    string skillName,
    string rawDescription,
    List<string>? useFor = null,      // NEW: USE_FOR items
    List<string>? services = null,    // NEW: service names
    CancellationToken ct = default
);
```

### `SkillPipelineOrchestrator` Changes

```csharp
// After parse, clean body
var cleanedBody = SkillMarkdownParser.StripAgentDirectives(skillData.RawBody);
var cleanedSkillData = skillData with { RawBodyCleaned = cleanedBody };

// LLM call with richer context
var llmIntro = await _llmRewriter.RewriteIntroAsync(
    skillName: skillName,
    rawDescription: cleanedSkillData.Description,
    useFor: cleanedSkillData.UseFor,
    services: cleanedSkillData.Services.Select(s => s.Name).ToList(),
    ct: ct
);
var updatedSkillData = cleanedSkillData with { LlmIntroduction = llmIntro };

// Generator uses LlmIntroduction, not Description
var rendered = _pageGenerator.Generate(updatedSkillData, triggerData, tierAssessment, prerequisites, _postProcessor.ProcessText);
```

---

## 8. Sequencing and Ownership

Riley's recommendation for execution order:

| # | Task | Owner | Blocks |
|---|------|-------|--------|
| 1 | Add `StripAgentDirectives()` to parser + tests | Morgan | LLM call quality |
| 2 | Add `RawBodyCleaned` and `LlmIntroduction` to `SkillData` | Morgan | Template wiring |
| 3 | Update `ILlmRewriter.RewriteIntroAsync` signature | Sage | Prompt testing |
| 4 | Update system + user prompts (Sage's doc) | Sage | Nothing after |
| 5 | Update `skill-page-template.hbs` (remove 3 sections, add doNotUseFor, wire `llmIntroduction`) | Morgan | Generation |
| 6 | Update `SkillPageGenerator.BuildContext()` (remove deprecated fields) | Morgan | Template |
| 7 | Update `SkillPipelineOrchestrator` call sites | Avery/Morgan | E2E |
| 8 | Write tests: parse → strip → LLM → render pipeline (per AD-007) | Parker | PR gate |

**AD-020 compliance:** All work must go through PRs, no direct commits. Dina merges.

---

## 9. Summary of Net Changes

### What changes:
- Template loses 3 sections (What it provides, Suggested workflow, Decision guidance)
- Template gains 1 section (When NOT to use — already parsed, not yet wired)
- LLM intro replaces hardcoded `BuildWhatItProvides()` boilerplate
- Parser strips agent-internal content before LLM receives it
- LLM call expands context to include `UseFor` and service names

### What doesn't change:
- Services table extraction
- MCP tools extraction
- Trigger extraction (example prompts)
- Prerequisites structure (`SkillPrerequisites` model)
- Related skills extraction
- TierAssessor (tier determines which sections render — still valid)
- AcrolinxPostProcessor (downstream, no changes needed)
- Validator (checks rendered output — no changes needed)
- All existing tests (additive changes only)

### Risk: zero regressions expected
All changes are additive (new fields on `SkillData`, new LLM context) or subtractive (template sections removed). The template sections being removed produce content that Dina's directive says should not be there — removing them cannot regress customer-facing quality.

---

**Document Owner:** Riley (Pipeline Architect)  
**Last Updated:** 2026-04-17  
**AD Number:** Pending assignment by Avery


---

# Skills Generation: LLM Rewrite Prompt Redesign

**Date:** 2026-03-31  
**Requester:** Dina  
**Owner:** Sage (AI/Prompt Engineer)  
**Status:** RECOMMENDATION  
**Priority:** P1

---

## Executive Summary

The skills-generation pipeline's LLM rewrite step currently treats SKILL.md (an anthropic-spec agent instruction file) as source material for customer documentation. This creates duplication—the generated docs read like agent instructions rather than customer value propositions.

**Dina's directive:** Generated documentation should be a **consolidated explanation of what a customer needs to know to use the skill successfully**, not a rephrasing of agent-internal procedures.

**Recommendation:** Redesign the LLM rewrite prompt to explicitly translate from "agent-centric" to "customer-centric" by:
1. Stripping internal agent directives (⛔ STOP, MANDATORY, FORBIDDEN, etc.)
2. Converting "agent does X" → "this skill helps you X"
3. Extracting customer value from skill USE_FOR items
4. Removing agent routing logic and workflow execution instructions
5. Synthesizing prerequisites and resources from agent checklists

---

## Problem Analysis

### Current State

**Source Material (SKILL.md):** Anthropic-spec agent instruction files containing:
- Internal directives: ⛔ STOP conditions, MANDATORY steps, PREFER OVER routing
- Agent execution procedures: "When user says X, route to skill Y"
- Codebase detection logic: Regex patterns, file discovery procedures
- Agent-to-agent handoff rules: When to invoke other agents
- Implementation checklists: Step-by-step agent instructions

**Current Orchestrator Flow (SkillPipelineOrchestrator.cs, line 86):**
```csharp
var rewrittenDescription = await _llmRewriter.RewriteIntroAsync(skillName, skillData.Description, ct);
```

The LLM rewrite is called **only on the description**, with current prompts at:
- System: `skill-page-system-prompt.txt` (14 lines, generic documentation rules)
- User: `skill-page-user-prompt-intro.txt` (6 lines, "rewrite for customer audience")

**Current System Prompt Weaknesses:**
- No explicit instruction to REMOVE agent-speak
- No guidance on translating agent procedures to customer scenarios
- No distinction between agent-internal logic vs. customer-facing capability
- Generic "don't invent" rule but no pattern-matching guidance on what IS agent-internal

**Result:** Generated docs often retain agent patterns:
- "When you trigger this skill, it will perform X check before proceeding" (agent language)
- Routing logic: "This skill works best when combined with skill-Y" (agent handoff, not customer workflow)
- Prerequisite checklists read like agent requirements, not customer needs

### Ideal State

**Customer-Facing Skill Documentation Should Answer:**
1. **What does this skill do?** (customer value, not agent procedures)
2. **When should I use it?** (customer scenarios from USE_FOR, not agent routing)
3. **What do I need?** (prerequisites as customer responsibilities, not agent checks)
4. **What can I ask it to do?** (concrete capabilities, not internal routing rules)

**Translation Examples:**

| Agent-Speak (SKILL.md) | Customer-Speak (Generated Docs) |
|---|---|
| ⛔ STOP: If no Azure subscription detected, exit. | **Required:** An active Azure subscription. |
| MANDATORY: Check codebase for .bicep files before routing. | **Prerequisites:** If using Bicep, Azure CLI with Bicep extension installed. |
| PREFER OVER: When user mentions "deploy," route to deploy-skill if infrastructure exists. | **When to use:** Use this skill when you need to deploy infrastructure. |
| "Agent detects resource group context from local CLI state" | "Provide your resource group name or use the default." |
| Implementation step: "Parse JSON schema from tool output, validate against config/schemas.json" | (Omit entirely—this is agent implementation) |

---

## Proposed Changes

### A. LLM Rewrite Prompt Redesign

#### New System Prompt

**File:** `skills-generation/prompts/skill-page-system-prompt.txt`

```
You are a customer-facing technical documentation writer for Azure Skills in GitHub Copilot.

Your job: Transform anthropic-spec SKILL.md content (written for AI agents) into customer documentation that explains what users need to know to successfully use the skill.

## Transformation Rules

### 1. Strip Agent-Internal Directives
REMOVE entirely:
- ⛔ STOP conditions and error handlers
- MANDATORY/PREFER OVER/FORBIDDEN routing instructions
- Agent-to-agent handoff rules
- Codebase detection procedures (regex patterns, file discovery)
- Agent execution procedures ("if user says X, route to Y")

These are agent implementation details, not customer capabilities.

### 2. Translate "Agent Does X" → "This Skill Helps You X"

Agent SKILL.md language → Customer documentation language:

**Agent:** "Before proceeding, the agent checks for Azure subscription context"
**Customer:** "You need an active Azure subscription"

**Agent:** "Agent detects prerequisite tools from source file extensions"
**Customer:** "Required tools: PowerShell, Node.js, Azure CLI (depending on your use case)"

**Agent:** "When invoked, the skill validates credentials and IAM permissions"
**Customer:** "You need Azure Owner, Contributor, or appropriate RBAC role for the resources you're working with"

**Agent:** "Prefer this skill when combined with deploy-skill for infrastructure workflows"
**Customer:** (Omit—this is agent routing. Instead, mention use cases where infrastructure deployment is needed.)

### 3. Extract Customer Value from "Use For" Section

The SKILL.md USE_FOR items describe what the skill does for users. These become "When to use this skill" guidance:

**SKILL.md USE_FOR:**
- Deploy infrastructure to Azure
- Validate infrastructure configuration
- Troubleshoot deployment errors

**Generated Docs:**
"Use this skill when you need to:
- Deploy infrastructure to Azure
- Validate infrastructure configuration
- Troubleshoot deployment errors"

### 4. Convert Workflow Steps from "Agent Execution" to "What Happens"

**Agent SKILL.md:**
"Workflow: Parse tool output → Validate schema → Apply transformations → Generate report"

**Customer Docs:**
"When you use this skill, it:
- Analyzes your infrastructure
- Validates configuration against best practices
- Suggests improvements
- Generates a deployment-ready report"

Do NOT expose implementation steps. Focus on outcomes the user experiences.

### 5. Synthesize Prerequisites from Agent Checklists

**Agent SKILL.md:**
"MANDATORY: Check if .bicep files exist in codebase. If yes, require Azure CLI ≥2.60.0"

**Customer Docs:**
"Prerequisites:
- Azure CLI (v2.60.0 or higher) — if using Bicep templates
- PowerShell 7.4+ — if using PowerShell-based automation"

### 6. Style Rules (Unchanged)
- Write in present tense
- Use contractions (doesn't, isn't, can't)
- Use active voice
- Keep sentences under 35 words
- Address readers as "you" (not "we" or "one")
- NEVER invent capabilities not mentioned in SKILL.md
- If source material is agent-internal only, write: "This skill provides guidance for..."

{{ACROLINX_RULES}}
```

#### New User Prompt

**File:** `skills-generation/prompts/skill-page-user-prompt-intro.txt` (updated)

```
Write a 2–3 sentence introduction (max 60 words) for the Azure Skill "{{{skillName}}}".

Raw SKILL.md description: "{{{rawDescription}}}"

USE_FOR capabilities: {{{useFor}}}

Instructions:
1. Extract customer value from the description and USE_FOR section.
2. REMOVE agent-speak (routing logic, codebase checks, execution procedures).
3. Translate to: "What does this skill do for you?" and "Why would you use it?"
4. Do NOT copy the SKILL.md description verbatim.
5. Write as if explaining to a developer who wants to get work done, not understand agent internals.
```

---

### B. Content Transformations for LLM

The LLM rewrite should perform these transformations:

#### Pattern Recognition & Removal

| Pattern | Source | Action |
|---------|--------|--------|
| `⛔ STOP:` | Agent directives | Remove entirely |
| `MANDATORY:`, `PREFER OVER:` | Routing logic | Convert to "When to use" or remove |
| `"Agent checks for..."` | Implementation | Remove |
| `"If user says X, route to Y"` | Routing logic | Remove |
| `Regex patterns, file discovery` | Agent procedures | Remove |
| `"Parse JSON, validate schema"` | Implementation details | Translate to customer outcomes |

#### Translation Patterns

| Source Pattern | Translate To |
|---|---|
| "Agent validates prerequisites" | "You need [prerequisites]" |
| "Codebase detection for [tool]" | "If using [tool], you'll need [resource]" |
| "Workflow step: validate output" | "Validates your configuration" |
| "Combined with skill-X for scenario-Y" | "For scenario-Y, you can extend with [feature]" |

---

### C. Integration Points

#### 1. **RewriteIntroAsync** Enhancement

**Current usage (line 86 of SkillPipelineOrchestrator.cs):**
```csharp
var rewrittenDescription = await _llmRewriter.RewriteIntroAsync(skillName, skillData.Description, ct);
```

**Enhanced call (optional):**
```csharp
var rewrittenDescription = await _llmRewriter.RewriteIntroAsync(
    skillName: skillName,
    rawDescription: skillData.Description,
    useFor: skillData.UseFor,  // Add USE_FOR items to context
    ct: ct
);
```

This provides the LLM with customer value propositions, enabling better translation.

#### 2. **New Optional Method: RewriteFullIntroductionAsync**

Consider adding to `ILlmRewriter`:
```csharp
Task<string> RewriteFullIntroductionAsync(
    string skillName, 
    string rawSkillmarkdown,  // Full SKILL.md content
    SkillData skillData,       // Parsed skill (USE_FOR, Prerequisites)
    CancellationToken ct = default
);
```

This would enable **wholesale rewrite** (not just description) if needed for future updates, with full context.

---

## Implementation Checklist

- [ ] Update `skill-page-system-prompt.txt` with new transformation rules
- [ ] Update `skill-page-user-prompt-intro.txt` with new context (USE_FOR items)
- [ ] Test on 3–5 real skills:
  - [ ] Skill with heavy agent routing logic
  - [ ] Skill with complex prerequisites
  - [ ] Skill with minimal USE_FOR (edge case)
- [ ] Compare generated descriptions before/after (should remove agent-speak)
- [ ] Validate generated docs against template (`skill-page-template.hbs`)
- [ ] Run Acrolinx compliance check (ensure no style regressions)

---

## Success Criteria

✅ **Generated docs read like customer guidance, not agent instructions.**

Specific checks:
1. No ⛔ STOP, MANDATORY, PREFER OVER language in output
2. No agent routing logic ("works best combined with skill-X")
3. Prerequisites stated as customer responsibilities, not agent checks
4. USE_FOR items translated to "When to use" section
5. All sentences in present tense, active voice
6. Acrolinx score improvement (or no regression)

---

## Risks & Mitigations

| Risk | Mitigation |
|------|-----------|
| Over-removal: LLM removes important capabilities | Explicit rule: "NEVER remove or invent—only translate" |
| Incomplete translation: Agent-speak still present | Test on diverse skills before full rollout; review samples |
| Token usage increase: Larger prompts → higher costs | New prompts are modest size; likely no measurable cost impact |
| Fallback degradation: If USE_FOR unavailable | User prompt includes fallback: "If USE_FOR unavailable, rewrite description only" |

---

## Related Decisions

- **AD-022:** Acrolinx Compliance Strategy (ensures generated docs maintain style standards)
- **AD-007:** TDD requirement (tests should verify translation rules before implementation)

---

## Next Steps

1. **Review & Approve** (Dina sign-off on prompt strategy)
2. **Implement** (Update prompts, test on real skills)
3. **Monitor** (Track Acrolinx scores, customer feedback)
4. **Iterate** (Refine translation rules based on early output)

---

## Appendix: Example Before/After

### Example: Compliance Skill

**SKILL.md Excerpt:**
```
## Description
Checks Azure compliance configuration against organizational policies.

## USE_FOR
- Audit resource compliance
- Identify policy violations
- Generate compliance reports

## Internal Logic
⛔ STOP: If no Key Vault found, skip secrets audit.
MANDATORY: Validate Azure subscription context before proceeding.
Workflow: Fetch resources → Parse config → Compare against policies.json → Generate report.
```

**Current Generated Output (Problem):**
"This skill checks Azure compliance. It validates your subscription, fetches resources, and generates reports. Works best when combined with deploy-skill for infrastructure validation."

**Desired Generated Output (After Redesign):**
"This skill audits your Azure resources against organizational compliance policies, identifies violations, and generates detailed reports. Use it to maintain compliance governance and track policy adherence across your infrastructure."

**Translation applied:**
- ✅ Removed ⛔ STOP (Key Vault check is agent-internal)
- ✅ Removed MANDATORY (subscription validation is automatic)
- ✅ Removed workflow steps (implementation detail)
- ✅ Extracted USE_FOR items (audit, identify, generate reports)
- ✅ Removed "works best combined with" (agent routing)
- ✅ Reframed as customer value (compliance governance, policy adherence)

---

**Document Owner:** Sage  
**Last Updated:** 2026-03-31

