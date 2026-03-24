# Project Context

- **Owner:** diberry
- **Project:** Azure MCP Documentation Generator — automated pipeline producing 800+ markdown docs for 52 Azure MCP namespaces
- **Stack:** .NET 9, C#, Handlebars.Net, PowerShell 7, bash, Docker, Azure OpenAI
- **Created:** 2026-03-20

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### 2026-03-24: Multi-Agent PR Review — Implementation Assessment

**PR #200 and PR #201 implementation review:**
- Both PRs compile cleanly; all tests pass (1028 total, 0 build errors).
- PR #200: `StripFrontmatter()` implementation is clean and performant. APPROVED.
- PR #201: `WrapExampleValues()` regex achieves idempotency through `[^)\x60]` character class (backtick in exclusion prevents double-wrapping). APPROVED.
- Minor observation: Comma-split edge case in PR #201 regex may incorrectly backtick explanation text in patterns like `(for example, PT1H for 1 hour, PT5M for 5 minutes)`. Explanation text gets wrapped instead of just values. This should be caught by template-level regression tests.
- No merge conflicts expected between PRs.

---

### 2026-03-23: Reviewed PR #200 (annotation inline rendering) and PR #201 (mcpcli markers + example backticks). Both APPROVED.
  - TextCleanup description chain order: `ReplaceStaticText` → `EnsureEndsPeriod` → `WrapExampleValues` (outermost)
  - WrapExampleValues regex uses `[^)\x60]` char class to achieve idempotency — backtick in char exclusion prevents double-wrapping
  - StripFrontmatter lives on PageGenerator (internal static), exposed to tests via AssemblyInfo.cs `[InternalsVisibleTo]`
  - Template triple-mustache `{{{AnnotationContent}}}` needed because annotation content has emoji/pipes
  - `AnnotationFileName` still assigned in PageGenerator even though template now uses `AnnotationContent` — candidate for cleanup
  - Both PRs modify tool-family-page.hbs but in non-overlapping sections (marker vs. annotation) — no merge conflict
