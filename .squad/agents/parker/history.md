# Project Context

- **Owner:** diberry
- **Project:** Azure MCP Documentation Generator — automated pipeline producing 800+ markdown docs for 52 Azure MCP namespaces
- **Stack:** .NET 9, C#, Handlebars.Net, PowerShell 7, bash, Docker, Azure OpenAI
- **Created:** 2026-03-20

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### 2026-03-24: Multi-Agent PR Review — Test Coverage Assessment

**PR #200 and PR #201 test coverage review — BOTH REJECTED:**

**Core Issue:** Both PRs modify `.hbs` templates but have zero template-level regression tests. Per AD-010, method-level tests are insufficient for template fixes. If template changes were reverted, all tests would pass — unacceptable.

**Detailed Findings:**

**PR #200 (annotation inline rendering):**
- ✅ `StripFrontmatter()` helper tests: 77+ cases with realistic YAML examples — comprehensive and solid
- ❌ Missing: No test rendering `tool-family-page.hbs` with new inline annotation pattern using `HandlebarsTemplateEngine.ProcessTemplateString()`
- If template change reverted, test suite still passes — AD-010 violation

**PR #201 (mcpcli markers + example backticks):**
- ✅ `WrapExampleValues()` helper tests: 11 comprehensive cases covering single values, comma-separated, idempotency, null/empty, edge cases
- ❌ Missing: No test rendering template with backtick-wrapped examples in parameter descriptions
- If template change reverted, test suite still passes — AD-010 violation

**Untracked Pattern Exists:**
- `ToolFamilyPageTemplateRegressionTests.cs` exists in codebase but not committed in either PR
- File demonstrates the expected approach: load template from `docs-generation/templates/`, render with controlled data, assert on output

**Regex Bug Found:**
- **PR #201:** `WrapExampleValues` regex incorrectly backticks mixed value/explanation patterns
- Example: Input `(for example, PT1H for 1 hour, PT5M for 5 minutes)` → Current output `(for example, `PT1H for 1 hour, PT5M for 5 minutes`)`
- Expected: Only wrap actual values (`PT1H`, `PT5M`), not explanations
- This edge case would be caught by template-level tests on real parameter data

**New Decision Issued (AD-019):** Template-level regression tests now required for all `.hbs` file changes. Tests must:
1. Load actual template from `docs-generation/templates/`
2. Render using `HandlebarsTemplateEngine.ProcessTemplateString()`
3. Assert on specific output change
4. Would FAIL if template change reverted

**Test Suite Baseline:**
- PR #200 branch: 1023 tests (all passing)
- PR #201 branch: 1028 tests (all passing)

**Resubmission Requirements:**
- Add template-level regression tests to both PRs
- Fix comma-split edge case in `WrapExampleValues` regex
- Resubmit both PRs together

**Learning:** PRs that fix template rendering bugs tend to only test the helper methods, not the template rendering itself. Template-level regression tests (rendering actual `.hbs` files) are the missing coverage gap.

---

### 2026-03-23: PR #201 and PR #200 Test Quality Review

**Both PRs REJECTED** for the same structural problem: method-level tests are solid, but template-level changes (the actual bug fixes) have zero test coverage. Per AD-010, if the template changes were reverted, no test would fail.

**Key findings:**
1. **Untracked regression test file exists** (`ToolFamilyPageTemplateRegressionTests.cs`) that covers both PRs' template changes, but it's not committed in either PR. It should be included.
2. **PR #201 regex bug:** `WrapExampleValues` incorrectly backticks mixed value/explanation patterns like `(for example, PT1H for 1 hour, PT5M for 5 minutes)` — wraps explanation text inside backticks.
3. **PR #200 test data gap:** Real annotation files have `# [!INCLUDE` and `# azmcp` comment lines in frontmatter, but tests use simplified inputs.
4. Test suite baseline: 1028 tests (PR #201 branch), 1023 tests (PR #200 branch), all passing.

**Pattern identified:** PRs that fix template rendering bugs tend to only test the helper methods, not the template rendering itself. Template-level regression tests (rendering actual `.hbs` files with `HandlebarsTemplateEngine.ProcessTemplateString()`) are the gap.
