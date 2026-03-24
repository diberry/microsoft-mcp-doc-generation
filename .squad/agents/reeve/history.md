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

---

- Internal code generation improvements (bug fixes that only affect generated markdown structure/formatting) don't require user-facing docs if they have clear commit messages, excellent inline code comments, and comprehensive tests that meet AD-007/AD-010 standards. The tests themselves become the "documentation" of the fix's contract.
- Code formatting/standardization changes should include comprehensive test coverage (11+ test cases) to guard against regressions, even for internal pipelines.
