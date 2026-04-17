# Project Context

- **Owner:** diberry
- **Project:** Azure MCP Documentation Generator — automated pipeline producing 800+ markdown docs for 52 Azure MCP namespaces
- **Stack:** .NET 9, C#, Handlebars.Net, PowerShell 7, bash, Docker, Azure OpenAI
- **Created:** 2026-03-20

## Learnings

### 2026-03-30: .NET Consolidation Plan Documentation Impact Review — APPROVED

**Final Verdict:** APPROVED (Minimal documentation impact; 3 tech docs need targeted updates)

**Documentation Impact Summary:**
- **Risk Level:** 🟢 LOW
- **User-facing impact:** Minimal (no config/behavior changes; internal refactoring only)
- **Developer experience:** POSITIVE (cleaner mental model, fewer projects)
- **Action items for Reeve:** 4 (1 before start, 3 during/after)

**Pre-Implementation Task (PRIORITY 1):**
- [ ] Write `mcp-tools/DocGeneration.Steps.ToolFamilyCleanup.Validation.Tests/README.md` (30 min)
  - Explain PowerShell integration test design (why zero C# project references)
  - Document requirements and running instructions

**Post-Implementation Tasks (PRIORITY 2-3):**

**Task 1: Update `docs/ci-integration.md` (Test Projects Inventory)**
- Remove row: `DocGeneration.Core.NaturalLanguage.Tests` (merged)
- Update framework status for 3 projects: Now all xUnit (was NUnit)
- Update header: "All **19 test projects**" (was 20)
- New summary: "Framework unified: all 19 use **xUnit on .NET 9**"
- **Owner:** Reeve (post-migration)

**Task 2: Update `docs/test-strategy.md` (Multiple Sections)**
- Section 1.2: Remove Core.NaturalLanguage.Tests row (count 17; was 18)
- Section 1.3: Update TextCleanup coverage (tests moved from Core.NaturalLanguage.Tests to Core.Shared.Tests)
- **Owner:** Reeve (post-consolidation)

**Task 3: Optional Update `docs/ARCHITECTURE.md`**
- Add version date: "Updated 2026-Q2: Consolidated single-file projects into core libraries"
- Update project count reference (42 → 38/40)
- Note: Dependency diagram simplification (Core.NaturalLanguage merge removes edge)
- **Owner:** Reeve (optional, not blocking)

**Documentation Not Affected:**
- ✅ `README.md` — Doesn't list removed projects by name
- ✅ `GENERATION-SCRIPTS.md` — Script orchestration is project-agnostic
- ✅ `QUICK-START.md` — Entry points remain valid
- ✅ `PROJECT-GUIDE.md` — High-level, no project details

**CHANGELOG Entry (Template Provided):**
- Document Actions 1-6 consolidations
- Note: Breaking change for PostProcessVerifier callers (must use ToolFamilyCleanup `--verify-only`)
- Include links to consolidation PR

**Developer Onboarding Impact:**
- **Before consolidation (42 projects):** "Why so many projects?" — confusing mental model
- **After consolidation (38 projects):** Cleaner "core libraries, pipeline steps, utilities, tests" model
- **Impact:** ✅ POSITIVE — Easier project comprehension

**Plan Quality Assessment (Reeve's Observations):**
- ✅ Exceptionally well-written (comprehensive, risk-aware, execution roadmap clear)
- ✅ Data-driven (full dependency analysis, concrete metrics)
- ✅ Mitigations documented (namespace preservation, script audit, test verification)

**Decisions filed:** AD-027 (main), AD-028 (quality gates)

**Next:** Write Validation.Tests README before consolidation starts (Action 6 prerequisite).

---

### 2026-03-24: Multi-Agent PR Review — Documentation Completeness

**PR #200 and PR #201 documentation completeness — BOTH APPROVED:**

**Key Assessment:** Both PRs involve user-visible output changes (generated markdown structure) but require no user-facing documentation updates because:
1. These are internal code generation improvements, not configuration changes
2. Commit messages clearly explain the *why* and *what*
3. Inline code documentation is excellent (XML comments with detailed explanations)
4. Test coverage is comprehensive (AD-007/AD-010 compliant)

**Internal Improvement Documentation Principle:**
Internal code generation improvements (bug fixes affecting only generated markdown) don't require user-facing documentation if they have:
- Clear, comprehensive commit messages explaining the *why*
- Excellent inline code documentation (XML comments, test class descriptions)
- Comprehensive tests meeting AD-007 (TDD) and AD-010 (test depth) standards

The tests themselves become the documentation of the fix's contract. No separate user guide needed when the change is transparent to end users (configuration-wise) and well-documented in code.

**Recommendation:** Both PRs exemplify best practices. Code formatting/standardization changes should include comprehensive test coverage (11+ test cases minimum) to guard against regressions, even for internal pipelines. The documentation gates pass; defer to Parker's test validation for final approval.

### 2026-03-24: Round 2 Documentation Re-Review — PRs #200 and #201 (APPROVED)

**Outcome:** Both PRs APPROVED after Round 1 review cycle completed.

**Round 2 Assessment:**
- **PR #200:** Test documentation excellent. New `AnnotationTemplateRegressionTests` class includes 5 comprehensive test descriptions explaining template edge cases. `StripFrontmatter` test method names and descriptions document realistic YAML patterns. Commit message clear and thorough.
- **PR #201:** Test documentation excellent. New `ToolFamilyPageTemplateRegressionTests` class includes 8 test descriptions covering marker placement, idempotency, and edge cases. Regex behavior tests have clear method names documenting expected input/output patterns. Commit message thoroughly explains both the `@mcpcli` marker standardization and the `WrapExampleValues` fix.
- **No user-facing docs needed** — internal generation improvements with configuration-transparent changes.
- **Knowledge transfer:** Test comments and method names provide excellent self-documenting code for future maintainers.

**Assessment:** Both PRs meet documentation standards. Tests exemplify expected AD-019 pattern (template-level regression tests with clear descriptions). Ready for merge.

---

### 2026-03-26: .NET Project Consolidation Impact Review

**Recommendation:** ✅ **Plan APPROVED for documentation requirements. LOW RISK.**

**Full review:** `.squad/decisions/inbox/reeve-consolidation-review.md`

**Key Findings:**

1. **Existing docs are architecture-agnostic** — No user-facing docs reference removed projects (CliAnalyzer, PostProcessVerifier, Core.NaturalLanguage are internal implementation details). README.md, ARCHITECTURE.md, PROJECT-GUIDE.md stay valid.

2. **Documentation updates needed (MINOR):**
   - CREATE: `mcp-tools/DocGeneration.Steps.ToolFamilyCleanup.Validation.Tests/README.md` — explains PowerShell integration test design (Action 6, assigned to Reeve)
   - UPDATE: `docs/ci-integration.md` Section 3 — remove NaturalLanguage.Tests row, update framework status for 3 projects (xUnit)
   - UPDATE: `docs/test-strategy.md` Section 1.2 + 1.3 — same changes (test count, framework, TextCleanup location)

3. **Consolidation is NET POSITIVE for onboarding:** 42 → 38 projects is clearer, removes orphaned confusion, makes dependency story simpler.

4. **CHANGELOG entry guidance:** Provided in review doc (PR author should include both description and migration notes).

5. **Plan quality: EXCELLENT** — Comprehensive inventory, clear problem statement, testable success criteria, risk-aware, assigns responsibility per team member. Gaps are minor cross-functional handoffs (expected since Avery is architecture lead, not doc owner).

**Action Items for Reeve:**
- Write README for Validation.Tests BEFORE consolidation starts (Action 6 blocker)
- Review each consolidation PR for doc completeness
- Post-consolidation: update ci-integration.md and test-strategy.md with new project counts

---

- Internal consolidation (project merges) can have ZERO user-facing impact if scoped to implementation details (project boundaries don't affect CLI behavior, config, or architecture). Document carefully which changes are user-visible (flags, namespace usage, config) vs. internal refactoring (compile structure).
- Cross-functional documentation impact analysis is critical for code restructuring. Create a documentation impact assessment BEFORE implementation starts to catch downstream doc updates early (not as afterthoughts).
