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
**Date:** 2026-03-21  
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

**Rationale:** Our #158 work shipped 6 commits but only 5 tests. Retrofitting 45 tests after the fact (AD-007 v1) worked but is wasteful — TDD catches design issues earlier and produces better-targeted fixes.

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

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
