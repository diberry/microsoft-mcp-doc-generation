# Project Context

- **Owner:** diberry
- **Project:** Azure MCP Documentation Generator — automated pipeline producing 800+ markdown docs for 52 Azure MCP namespaces
- **Stack:** .NET 9, C#, Handlebars.Net, PowerShell 7, bash, Docker, Azure OpenAI
- **Created:** 2026-03-20

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### 2026-04-17: Morgan History Summarization (Archive Phase 1)

**Scope:** Consolidated early-stage PR review work (2026-03-23 to 2026-03-25) and Acrolinx implementation into core principles. This is a HARD GATE summarization (file was 19.7 KB, now being reduced).

**Archived Work (Full details in git commits):**
- **PR #200/#201 reviews** — Template regression tests, annotation inline rendering, WrapExampleValues regex idempotency
- **Acrolinx P0+P1 implementation** — AcronymExpander, PresentTenseFixer, IntroductoryCommaFixer, StaticTextReplacementJson entries (62 new tests)
- **Fix #219 + #220** — ms.date frontmatter, LearnUrlRelativizer for URL normalization
- **Multi-agent PR reviews** — Implementation assessments pre-consolidation

**Permanently Retained Principles:**
1. **Template-level regression tests** must render actual `.hbs` files via `HandlebarsTemplateEngine.ProcessTemplateString()` (AD-019)
2. **Windows cross-platform pattern** — template-rendering tests require `Normalize()` (replace `\r\n` → `\n`)
3. **Regex idempotency pattern** — `WrapExampleValues` uses `[^)\x60]` character class to prevent double-wrapping
4. **Post-processing order** — Must stay identical across tool merges (10-processor chain: AcronymExpander → JsonSchemaCollapser)
5. **Frontmatter generation** — `ms.date` should emit at source (generator), not solely at enricher stage
6. **AI URL normalization** — Belt-and-suspenders: deterministic post-processor (LearnUrlRelativizer) as backstop for full URLs
7. **Acrolinx integration** — Add compliance sections to all AI system prompts; word-boundary regex prevents trailing-space key matching

**Core Decisions (3-month summary):**
- AD-007: TDD (tests first, then implement)
- AD-019: Template-level regression tests required for template fixes
- AD-022: Acrolinx compliance strategy
- AD-024: LearnUrlRelativizer decision
- AD-027 through AD-034: Consolidation safeguards

---

### 2026-03-30: .NET Project Consolidation Implementation Review — APPROVED WITH CHANGES

**Verdict:** APPROVE WITH CHANGES (Realistic 7-hour effort estimate for Actions 2-5)

**Key Implementation Findings:**

| Action | Difficulty | Effort | Notes |
|--------|-----------|--------|-------|
| 1. Remove CliAnalyzer | 🟢 LOW | 15 min | Straightforward git rm + solution edit |
| 2. Merge PostProcessVerifier | 🟡 MEDIUM | 90 min | Port Program.cs `.after` suffix logic; exit code preservation critical |
| 3. Merge Core.NaturalLanguage | 🟡 MEDIUM | 120 min | Data file path verification required; namespace preservation mandatory |
| 4. NUnit → xUnit | 🟡 MEDIUM | 180 min | 155 tests across 3 projects; assertion rewrites require careful review |
| 5. Consolidate StripFrontmatter | 🟢 LOW | 5-45 min | **BEHAVIORAL CAVEAT:** Fingerprint has `.TrimStart()` that canonical doesn't. RECOMMEND: Keep Fingerprint local (Option A, 5 min) not add parameter (Option B, 45 min) |
| 6. Document Validation.Tests | 🟢 LOW | 30 min | README explaining PowerShell integration test design |

**Critical Discoveries:**
- **Post-processor order (Action 2):** Both tools use identical 10-processor chain (AcronymExpander → JsonSchemaCollapser). Merge is safe if order preserved.
- **Data file paths (Action 3):** TextCleanup.LoadFiles() expects caller-provided paths. Current callers use `../../../data/` relative paths. After merge, must verify paths still resolve in bin/net9.0/.
- **NUnit edge cases (Action 4):** No `[TestContext]`, no complex `[SetUp]/[TearDown]` — all 146 tests are mechanical `[Test]` → `[Fact]` refactors. Assertion rewrites require human review (argument order reversal for `Assert.Equal`).
- **StripFrontmatter behavior (Action 5):** Core.Shared removes frontmatter only; Fingerprint adds `.TrimStart()` removing all leading whitespace. This is intentional for fingerprinting use case — should NOT be merged.

**Team Coordination:**
- Morgan implements Actions 2-5
- Riley oversees namespace preservation (Action 3)
- Parker validates file discovery tests + test count matching (Actions 3, 4)
- Quinn audits scripts for PostProcessVerifier references (Action 2)

**Decisions filed:** AD-027 (main), AD-029 (data file discovery), AD-030 (exit codes), AD-031 (namespace), AD-032 (test baseline), AD-033 (post-processor order)

---

