# Project Context

- **Owner:** diberry
- **Project:** Azure MCP Documentation Generator — automated pipeline producing 800+ markdown docs for 52 Azure MCP namespaces
- **Stack:** .NET 9, C#, Handlebars.Net, PowerShell 7, bash, Docker, Azure OpenAI
- **Created:** 2026-03-20

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### 2026-03-30: .NET Consolidation QA Review — FINAL (APPROVED WITH CONTINGENCIES)

**Final Verdict:** APPROVED WITH CONTINGENCIES (9 acceptance criteria required before each phase)

**Complete Assessment Summary:**
- **~1,100+ tests affected** across 7 actions
- **0 tests will be lost** (all moved/consolidated, none deleted)
- **All test coverage preserved** across all consolidation actions
- **Test framework migration (NUnit→xUnit) has mechanical low risk, verification high importance**

**9 Acceptance Criteria (per reviewer Parker):**
1. ✅ CliAnalyzer orphan verification (zero references expected)
2. ⚠️ PostProcessVerifier baseline (old output vs. new `--verify-only` byte-identical on 5+ namespaces)
3. ⚠️ PostProcessVerifier script audit (zero script/CI references)
4. ⚠️ NUnit→xUnit baseline (test count match before/after for 3 projects)
5. ⚠️ Core.NaturalLanguage data file discovery (TextCleanup JSON files load after merge)
6. ⚠️ HorizontalArticles output consistency (Step 6 output identical before/after Action 3)
7. ⚠️ Core.Shared.Tests integration (file discovery tests pass; test count increased correctly)
8. ⚠️ File system cleanup (Fingerprint temp dirs cleaned up properly on Windows)
9. ⚠️ Baseline path resilience (PromptRegression.Tests paths remain valid)

**File System Test Risks (HIGH priority):**
- **R1 (TextCleanup JSON discovery):** HIGH — runtime crash if paths break
- **R2 (Fingerprint temp cleanup):** MEDIUM — Windows temp dir locking issues
- **R3 (PromptRegression baseline paths):** HIGH — hardcoded relative paths may fail

**Validation Before Each Phase:**
- Pre-Phase 1: Baseline test count + build time
- Pre-Phase 2: PostProcessVerifier script audit complete
- Pre-Phase 3: Core.Shared data file validation + integration tests written

**Test Coverage Preservation (Final Tally):**
- CliAnalyzer: 0 tests (removed; no test project)
- PostProcessVerifier: 0 tests (merged; logic tested by ToolFamilyCleanup.Tests)
- Core.NaturalLanguage: ~25 tests (moved to Core.Shared.Tests; preserved)
- NUnit→xUnit: ~155 tests (framework migration; logic preserved)
- StripFrontmatter: ~98 tests (refactored; logic preserved)
- **Total:** ~1,100+ tests executable after consolidation (invariant: test count unaffected by consolidation)

**Decisions filed:** AD-027 (main), AD-028 (quality gates), AD-029 (data file discovery), AD-032 (test baseline), AD-034 (AI output consistency)

**Next:** Execute quality gates per AD-028 before proceeding to next phase.

---

### 2026-03-26: .NET Consolidation Plan QA Review (Actions 1-7)

**Context:** Avery proposed 7 consolidation actions to reduce 42 projects → 38 (or 32 with future work). Reviewed for test coverage, edge cases, and regression risk.

**Key Findings:**
- **~700+ tests affected** across 7 actions; **0 tests will be lost** (all moved, none removed)
- **No orphaned test data:** Test fixtures are either in-memory strings or properly tracked in .csproj files
- **3 NUnit projects need framework migration** to xUnit (82+ tests total); mechanical conversion, low risk
- **File system state critical for 4 test projects:** Temp cleanup, baseline paths, JSON data loading must be validated post-merge
- **CliAnalyzer removal safe:** Zero tests, zero callers, only dead code
- **PostProcessVerifier consolidation requires script audit:** Quinn must verify all callers before Morgan implements

**Verdict:** APPROVED WITH CONTINGENCIES.
- **Phase 1 (Actions 1, 5, 6):** Low-risk; execute immediately
- **Phase 2 (Actions 2, 4):** Gate on baseline test execution + PostProcessVerifier script audit
- **Phase 3 (Action 3):** Gate on Phase 2 completion + Core.Shared data file validation
- **All 1,100+ tests must pass after each action** (invariant enforcement)

**Test Execution Plan:**
- Pre-consolidation baseline: `dotnet test docs-generation.sln` → record count
- Post-each-action: Repeat test execution; fail if count decreases or failures occur
- Edge case focus: 52-namespace validation (currently only 3-4 tested); temp file cleanup on Windows; baseline path resilience
- Regression detection: Fingerprint diff-report + PromptRegression baseline comparison

**Action-Specific Contingencies:**
- **Action 3 (Core.NaturalLanguage merge):** If data files don't copy, switch from `<CopyToOutputDirectory>` to `<EmbeddedResource>` pattern
- **Action 4 (xUnit migration):** If async tests break, implement IAsyncLifetime pattern; if reflection tests fail, verify xUnit reflection support
- **Action 2 (PostProcessVerifier merge):** If scripts break, keep PostProcessVerifier as thin wrapper instead of full consolidation

**Test Coverage Status After Phase 3:**
- 1,100+ tests (invariant)
- All frameworks unified to xUnit
- 0 StripFrontmatter implementations outside FrontmatterUtility
- File system state properly isolated (temp cleanup, baseline paths)
- Pipeline passes 52-namespace end-to-end validation

### 2026-03-24: PR #200 and PR #201 Final Review — Both APPROVED After Morgan Fixes

**Context:** Initial multi-agent review rejected both PRs for template-level test coverage gaps (AD-010 violation). Morgan completed comprehensive fixes; both PRs now meet all requirements.

**PR #200 — annotation inline rendering (APPROVED):**
- ✅ `StripFrontmatter()` helper tests: 77+ cases with realistic YAML examples — comprehensive and solid
- ✅ New `AnnotationTemplateRegressionTests.cs`: 5 tests rendering actual `tool-family-page.hbs` via `HandlebarsTemplateEngine.ProcessTemplateString()`. All would FAIL if template reverted.
- ✅ Realistic test data: Frontmatter updated with pipeline-sourced patterns (`generated:` timestamp, `# [!INCLUDE]` comment, `# azmcp` comment, full semver+metadata)
- ✅ Edge case coverage: New test sourced from real appservice annotation files
- Implementation: Clean, performant `StripFrontmatter()` with proper triple-mustache handling for unescaped emoji/pipes

**PR #201 — mcpcli markers + example backticks (APPROVED):**
- ✅ `WrapExampleValues()` helper tests: 11 comprehensive cases covering single values, comma-separated, idempotency, null/empty, edge cases
- ✅ Regex bug fixed: Now splits each comma-separated part on spaces; first token = value (backticked), rest = explanation (left as-is). Handles `(for example, PT1H for 1 hour, PT5M for 5 minutes)` → `(for example, \`PT1H\` for 1 hour, \`PT5M\` for 5 minutes)`
- ✅ New `ToolFamilyPageTemplateRegressionTests.cs`: 8 tests rendering actual `.hbs` templates. Tests assert `@mcpcli` prefix, no plain markers, no blank line between H2 and marker, no `@mcpcli` in example-prompts. All would FAIL if template reverted.
- ✅ Untracked test file committed with corrected namespace and csproj reference
- ✅ 4 regex fix tests: behavioral coverage for single value+explanation, multiple values+explanations, mixed patterns, comma-in-pattern edge cases
- Key learning: Template tests need `Normalize()` (`\r\n` → `\n`) for cross-platform Windows reliability
- Implementation: Idempotent regex using `[^)\x60]` character class (backtick in exclusion prevents double-wrapping)

**Test Suite Baseline:**
- Total: 1034+ tests (started at 1023-1028)
- Status: ✅ All passing
- Regressions: None detected
- New coverage: 13 template-level regression tests (5 PR #200 + 8 PR #201)

**No merge conflicts expected between PRs** — annotations and mcpcli changes occur in non-overlapping template sections.

**Learning captured:** PRs that fix template rendering bugs must include template-level regression tests (rendering actual `.hbs` files), not just method-level tests. This is now documented in AD-019.

**Decisions affected:** AD-019 (Template-Level Regression Tests Required for Template Fixes) issued during this review cycle. Both PRs now exemplify the expected pattern.

### 2026-03-24: Round 2 QA Re-Review — PRs #200 and #201 (APPROVED)

**Outcome:** Both PRs APPROVED after Round 1 rejection findings fully resolved.

**Round 2 Test Validation:**
- **PR #200:** All 5 Round 1 rejection findings resolved. Template regression tests cover inline rendering, fallback comments, triple-mustache emoji/pipes, condition fields, and actual file verification. `StripFrontmatter` tests updated with realistic YAML/frontmatter patterns.
- **PR #201:** All 5 Round 1 rejection findings resolved. Regex bug fixed (split-on-spaces for comma-separated patterns). 8 template regression tests validate `@mcpcli` prefix, no plain markers, marker positioning, and no `@mcpcli` in prompts. 4 regex behavior tests cover all edge cases.
- **Test Suite:** 1,061 tests passing across both branches. No new test failures. 13 template-level regression tests added (5 PR #200 + 8 PR #201).

**Quality Gate:** All AD-010 requirements met. Template-level tests demonstrate idempotency and correct rendering. No regressions detected. Ready for merge.

---

### 2026-03-23: PR #201 and PR #200 Test Quality Review

**Both PRs REJECTED** for the same structural problem: method-level tests are solid, but template-level changes (the actual bug fixes) have zero test coverage. Per AD-010, if the template changes were reverted, no test would fail.

**Key findings:**
1. **Untracked regression test file exists** (`ToolFamilyPageTemplateRegressionTests.cs`) that covers both PRs' template changes, but it's not committed in either PR. It should be included.
2. **PR #201 regex bug:** `WrapExampleValues` incorrectly backticks mixed value/explanation patterns like `(for example, PT1H for 1 hour, PT5M for 5 minutes)` — wraps explanation text inside backticks.
3. **PR #200 test data gap:** Real annotation files have `# [!INCLUDE` and `# azmcp` comment lines in frontmatter, but tests use simplified inputs.
4. Test suite baseline: 1028 tests (PR #201 branch), 1023 tests (PR #200 branch), all passing.

**Pattern identified:** PRs that fix template rendering bugs tend to only test the helper methods, not the template rendering itself. Template-level regression tests (rendering actual `.hbs` files with `HandlebarsTemplateEngine.ProcessTemplateString()`) are the gap.

### 2026-03-25: Test Strategy Document Created

**Context:** Per #202 requirements review, drafted `docs/test-strategy.md` covering the full pipeline test landscape.

**Key findings from audit:**
- **1,100+ tests** across **17 active test projects** (17 deprecated/legacy projects also exist but not in .sln)
- **3 step projects with zero test projects:** CliAnalyzer (Step 0), RawTools (Step 1), Composition (Step 3)
- **Only Step 4** has an `IPostValidator`. Steps 0, 1, 2, 3, 5, 6 have none — Steps 3 and 6 are critical gaps (template token leakage, AI JSON failures per AD-002)
- **Only 1 of 10 templates** (`tool-family-page.hbs`) has regression tests. The other 9 are untested.
- **23 of 49 namespaces** have `generated-validated-*` snapshots; 26 have zero validated output
- **Zero step contract tests** exist — no test verifies Step N output is valid Step N+1 input
- `verify-quantity/index.js` checks file existence but not content quality
- `TextCleanup` (NaturalLanguage) has zero test coverage
- `HandlebarsTemplateEngine` has only 2 basic tests despite being the core rendering engine

**Document structure:** 10 sections covering current landscape, test pyramid, test categories (6 types), test data strategy with reference namespaces, regression detection via baseline fingerprinting, AD-010/AD-019 compliance enforcement, execution/CI, gap analysis, and 8-week priority roadmap (18 tasks across 4 phases).

**Decision:** The test pyramid is bottom-heavy (strong unit tests) but hollow in the middle (no step contracts, minimal template tests). Phase 1 priority is Step 3 + Step 6 validators and template regression tests for `tool-complete-template.hbs` and `parameter-template.hbs`.
