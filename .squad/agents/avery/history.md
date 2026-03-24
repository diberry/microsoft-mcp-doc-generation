# Project Context

- **Owner:** diberry
- **Project:** Azure MCP Documentation Generator — automated pipeline producing 800+ markdown docs for 52 Azure MCP namespaces
- **Stack:** .NET 9, C#, Handlebars.Net, PowerShell 7, bash, Docker, Azure OpenAI
- **Created:** 2026-03-20

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### 2026-03-24: Round 2 Architecture Re-Review — PRs #200 and #201

**Session:** Re-review after Morgan addressed Parker's Round 1 rejection findings.

**Avery's Round 2 Assessment: APPROVED both PRs.**

**PR #201 — Regex fragility analysis:**
The `WrapExampleValues` space-detection heuristic (`IndexOf(' ')` to split value from explanation) introduces mild fragility but is well-scoped for the domain. Azure CLI parameter values are single-token identifiers (e.g., `PT1H`, `Standard_DS1_v2`, `mydb`) — multi-word bare values are effectively nonexistent in this corpus. The 4 new regex tests (`ValueWithExplanation`, `MultipleValuesWithExplanations`, `MixedPlainAndExplanationValues`) cover the realistic patterns. The outer regex char class `[^)\x60]` maintains idempotency. **Verdict: acceptable trade-off, not fragile for this domain.**

**PR #201 — Template regression tests (239 lines, 8 tests):**
`ToolFamilyPageTemplateRegressionTests.cs` reads actual `.hbs` templates via directory-walking from `AppContext.BaseDirectory` upward. This is a standard integration test pattern. Tests render with `HandlebarsTemplateEngine.ProcessTemplateString()` and assert on marker format, placement, and count. All 8 tests would FAIL if template changes were reverted. **Satisfies AD-019.**

**PR #200 — Template regression tests (152 lines, 5 tests):**
`AnnotationTemplateRegressionTests.cs` uses inline template section for 4/5 tests (fast, focused) and reads actual template file for 1 verification test. Tests cover inline rendering, fallback comment, triple-mustache unescaped output, condition field correctness, and actual file verification. `StripFrontmatter` tests now use realistic annotation file content (semver+build metadata, `[!INCLUDE]` comments, generated timestamps). **Satisfies AD-019.**

**Cross-stage concerns:** None. Both PRs are localized to Step 1 (annotation/parameter generation). The 635 total new test lines are all in `DocGeneration.Steps.AnnotationsParametersRaw.Annotations.Tests` — no new test projects, no cross-step coupling. All tests pass on both branches (214 tests in Annotations.Tests, full suite green).

**Merge order recommendation:** PR #200 first (same as Round 1 — simpler change, reduces cognitive load on template state).

---

### 2026-03-24: Multi-Agent PR Review Session — PRs #200 and #201

**Session:** Architecture, implementation, test coverage, and documentation reviews

**Summary:** Comprehensive team review of PR #200 (annotation inline rendering) and PR #201 (template format fixes). Outcomes diverged: Avery, Morgan, and Reeve approved; Parker rejected for AD-010 violations.

**Avery's Architecture Assessment (APPROVED both):**
- PR #200: Introduces third `StripFrontmatter` — acceptable duplication across module boundaries. File as tech debt (AD-018).
- PR #201: Idempotent regex design prevents double-wrapping.
- No merge conflicts; recommend merging #200 first for reduced cognitive load on template state.
- Cross-stage risk: Low, localized to Step 4.

**Morgan's Implementation Review (APPROVED both):**
- Both PRs build clean; all 1028 tests pass.
- PR #200: `StripFrontmatter()` implementation is clean and performant.
- PR #201: `WrapExampleValues()` regex uses `[^)\x60]` char class for idempotency.
- Minor note: Comma-split edge case (mixed value/explanation patterns like `(for example, PT1H for 1 hour)`) may incorrectly wrap explanation text.

**Parker's Test Coverage Review (REJECTED both):**
- **AD-010 violation:** Both PRs modify `.hbs` templates but have zero template-level regression tests.
- PR #200: 77+ helper tests, no template rendering tests.
- PR #201: 11 helper tests, no template rendering tests.
- An untracked `ToolFamilyPageTemplateRegressionTests.cs` exists but not committed in either PR.
- Regex bug found: `WrapExampleValues` incorrectly backticks explanation text in comma-separated patterns.
- **New decision issued (AD-019):** Template-level tests now required for `.hbs` file changes.

**Reeve's Documentation Review (APPROVED both):**
- No user-facing docs needed — internal generation improvements.
- Excellent commit messages, inline code comments, and comprehensive tests (meeting AD-007/AD-010 standards).
- Tests themselves serve as documentation of the fix's contract.

**Key Decisions Issued:**
- **AD-018:** Consolidate 3× `StripFrontmatter` as future tech debt.
- **AD-019:** Template-level regression tests required for `.hbs` file changes (new requirement).
- **AD-020:** Full pipeline architecture assessment by Riley (informational).

**Action Items for Authors:**
1. Add template-level tests using `HandlebarsTemplateEngine.ProcessTemplateString()`
2. Fix `WrapExampleValues` regex for comma-split edge case
3. Resubmit both PRs

---

### 2026-03-24: Round 2 Architecture Re-Review — PRs #200 and #201 (APPROVED)

**Outcome:** Both PRs approved after all Round 1 rejection findings resolved by Morgan.

**Round 2 Assessment:**
- **PR #200:** Template-level regression tests added. `StripFrontmatter()` implementation remains clean and performant. No architectural concerns.
- **PR #201:** Regex bug fixed. 8 template-level regression tests confirm idempotency. `WrapExampleValues` char class design remains sound.
- **Cross-stage risk:** Remains low. Both PRs localized to Step 1. No new architectural concerns introduced.
- **Merge sequence:** PR #200 first (no change to recommendation).

**Verified:** All 1,061 tests passing. Template-level tests validate actual `.hbs` rendering. Architecture clean.

---

### 2026-03-23: Architecture Review of PRs #200 and #201

**PR #201 (fix/template-format-bugs):** APPROVED. Standardizes `@mcpcli` markers and adds `WrapExampleValues()` to the TextCleanup chain. Low cross-stage risk — idempotent regex applied consistently in PageGenerator and ParameterGenerator. 11 tests.

**PR #200 (fix/annotation-inline-rendering):** APPROVED with advisory. Changes annotation rendering from `[!INCLUDE]` to inline `{{{AnnotationContent}}}` in tool-family-page.hbs. Introduces third `StripFrontmatter` implementation (also in ToolReader and ComposedToolGeneratorService) — filed as tech debt for future consolidation.

**Key findings:**
- Both PRs modify `PageGenerator.cs` and `tool-family-page.hbs` in different sections — should auto-merge but recommend sequential merge.
- `tool-complete-template.hbs` still uses `[!INCLUDE]` with different path pattern — separate rendering context, not affected.
- CI `build-and-test` failures on both PRs are infrastructure-related (0 errors, all 204 tests pass, exit code 1 from orphan dotnet cleanup).
- Three separate `StripFrontmatter` implementations exist across assemblies — tech debt to consolidate.
