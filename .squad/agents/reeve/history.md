# Project Context

- **Owner:** diberry
- **Project:** Azure MCP Documentation Generator — automated pipeline producing 800+ markdown docs for 52 Azure MCP namespaces
- **Stack:** .NET 9, C#, Handlebars.Net, PowerShell 7, bash, Docker, Azure OpenAI
- **Created:** 2026-03-20

## Learnings

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

- Internal code generation improvements (bug fixes that only affect generated markdown structure/formatting) don't require user-facing docs if they have clear commit messages, excellent inline code comments, and comprehensive tests that meet AD-007/AD-010 standards. The tests themselves become the "documentation" of the fix's contract.
- Code formatting/standardization changes should include comprehensive test coverage (11+ test cases) to guard against regressions, even for internal pipelines.
