# Project Context

- **Owner:** diberry
- **Project:** Azure MCP Documentation Generator — automated pipeline producing 800+ markdown docs for 52 Azure MCP namespaces
- **Stack:** .NET 9, C#, Handlebars.Net, PowerShell 7, bash, Docker, Azure OpenAI
- **Created:** 2026-03-20

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

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
