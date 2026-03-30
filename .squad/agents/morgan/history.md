# Project Context

- **Owner:** diberry
- **Project:** Azure MCP Documentation Generator — automated pipeline producing 800+ markdown docs for 52 Azure MCP namespaces
- **Stack:** .NET 9, C#, Handlebars.Net, PowerShell 7, bash, Docker, Azure OpenAI
- **Created:** 2026-03-20

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

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

### 2026-03-24: Multi-Agent PR Review — Implementation Assessment

**PR #200 and PR #201 implementation review:**
- Both PRs compile cleanly; all tests pass (1028 total, 0 build errors).
- PR #200: `StripFrontmatter()` implementation is clean and performant. APPROVED.
- PR #201: `WrapExampleValues()` regex achieves idempotency through `[^)\x60]` character class (backtick in exclusion prevents double-wrapping). APPROVED.
- Minor observation: Comma-split edge case in PR #201 regex may incorrectly backtick explanation text in patterns like `(for example, PT1H for 1 hour, PT5M for 5 minutes)`. Explanation text gets wrapped instead of just values. This should be caught by template-level regression tests.
- No merge conflicts expected between PRs.

### 2026-03-24: PR #201 Review Feedback Fix

**Parker rejected PR #201 — fixed all 4 findings:**
1. **Regex bug**: `WrapExampleValues` replacement logic now splits each comma-separated part on spaces. First token = value (backticked), rest = explanation (left as-is). Fixes `(for example, PT1H for 1 hour)` → `(for example, \`PT1H\` for 1 hour)`.
2. **Template regression tests**: Added 8 tests rendering actual .hbs templates via `HandlebarsTemplateEngine.ProcessTemplateString()`. Tests assert `@mcpcli` prefix, no plain markers, no blank line between H2 and marker, and no `@mcpcli` in example-prompts. All would FAIL on revert.
3. **Untracked test file**: `ToolFamilyPageTemplateRegressionTests.cs` committed with corrected namespace (`DocGeneration.Steps.AnnotationsParametersRaw.Annotations.Tests`) and added `DocGeneration.Core.TemplateEngine` project reference to csproj.
4. **Regex fix tests**: 4 new test methods covering single value+explanation, multiple values+explanations, and mixed plain+explanation patterns.
- **Key learning**: Template-rendering tests need `Normalize()` (replace `\r\n` → `\n`) for cross-platform reliability on Windows.

---

### 2026-03-23: Reviewed PR #200 (annotation inline rendering) and PR #201 (mcpcli markers + example backticks). Both APPROVED.
  - TextCleanup description chain order: `ReplaceStaticText` → `EnsureEndsPeriod` → `WrapExampleValues` (outermost)
  - WrapExampleValues regex uses `[^)\x60]` char class to achieve idempotency — backtick in char exclusion prevents double-wrapping
  - StripFrontmatter lives on PageGenerator (internal static), exposed to tests via AssemblyInfo.cs `[InternalsVisibleTo]`
  - Template triple-mustache `{{{AnnotationContent}}}` needed because annotation content has emoji/pipes
  - `AnnotationFileName` still assigned in PageGenerator even though template now uses `AnnotationContent` — candidate for cleanup
  - Both PRs modify tool-family-page.hbs but in non-overlapping sections (marker vs. annotation) — no merge conflict

### 2026-03-24: PR #200 Review Feedback Fix — Template Regression Tests + Realistic Test Data

**Parker rejected PR #200 — fixed both findings:**
1. **AD-010 template test coverage**: Created `AnnotationTemplateRegressionTests.cs` with 5 tests using `HandlebarsTemplateEngine.ProcessTemplateString()` to render the annotation template section. Tests verify: inline rendering when AnnotationContent is present, fallback comment when absent, triple-mustache unescaped emoji/pipes, condition on AnnotationContent (not AnnotationFileName), and actual template file pattern. All 5 would FAIL if the template change were reverted.
2. **Realistic StripFrontmatter test data**: Updated existing tests with frontmatter matching real pipeline output — includes `generated:` timestamp, `# [!INCLUDE [...]]` comment, `# azmcp <command>` comment, and full semver+build metadata version string. Added new `StripFrontmatter_DestructiveAnnotation_ReturnsCorrectFlags` test sourced from real appservice annotation files.
- **Key pattern**: Added TemplateEngine ProjectReference and linked `tool-family-page.hbs` as TestData in annotations test csproj for template file assertions.
- Total: 12 annotation/frontmatter tests pass (5 new template + 1 new StripFrontmatter + 6 existing).

### 2026-03-24: Round 2 Implementation Re-Review — PRs #200 and #201 (APPROVED + FIXES COMPLETED)

**Review Outcome:** Both PRs APPROVED after Round 1 fixes.

**Round 2 Implementation Assessment:**
- **PR #200:** Template regression tests added. `StripFrontmatter()` implementation remains clean and performant. Realistic test data added from pipeline. APPROVED.
- **PR #201:** Regex bug fixed. 8 template regression tests cover all edge cases. 4 regex behavior tests validate the split-on-spaces fix. APPROVED.
- **Regex validation:** Validated against 12 real Azure CLI parameter patterns. No issues detected.
- **Test suite:** 1,061 tests passing, 0 regressions.
- **Key pattern:** Template-rendering tests using `Normalize()` are essential for cross-platform Windows reliability.

**Technical Assessment:** Both implementations exemplify the expected pattern from AD-019 (template-level regression tests required). Code quality solid. Ready for merge.

---

### 2026-03-24: PR #201 and PR #200 Final Resubmission Work — All Rejection Findings Resolved

**Completed:** Both PRs now ready for final Parker review with all findings addressed.

**PR #201 Fixes (mcpcli markers + example backticks):**
1. Fixed regex bug in `WrapExampleValues` — now splits each comma-separated part on spaces; first token = value (backticked), rest = explanation (left as-is). Handles edge case: `(for example, PT1H for 1 hour, PT5M for 5 minutes)` → `(for example, \`PT1H\` for 1 hour, \`PT5M\` for 5 minutes)`
2. Added 8 template regression tests via `ToolFamilyPageTemplateRegressionTests.cs` using `HandlebarsTemplateEngine.ProcessTemplateString()` to render actual `.hbs` templates. Tests assert `@mcpcli` prefix, no plain markers, no blank line between H2 and marker, and no `@mcpcli` in example-prompts. All 8 would FAIL on revert.
3. Committed untracked test file with corrected namespace (`DocGeneration.Steps.AnnotationsParametersRaw.Annotations.Tests`) and added `DocGeneration.Core.TemplateEngine` project reference to csproj.
4. Added 4 regex fix tests: single value+explanation, multiple values+explanations, mixed plain+explanation, comma-in-pattern edge case.
- Key learning: Template-rendering tests need `Normalize()` (replace `\r\n` → `\n`) for cross-platform reliability on Windows.
- Test suite: 1034+ total tests, all passing, 0 new regressions.

**PR #200 Fixes (annotation inline rendering):**
- Already completed (see entry above)

**Decision Issued:** AD-019 (Template-Level Regression Tests Required for Template Fixes) — both PRs now exemplify the expected pattern.

### 2026-03-25: Acrolinx Compliance Gap Analysis — Implementation Audit

**Task:** Dina requested analysis of post-processing chain and templates to identify Acrolinx compliance gaps. Goal: generated tool-family files scoring well above 80.

**Real Acrolinx scores from MicrosoftDocs/azure-dev-docs-pr PRs:**
- `azure-deploy.md` (PR #8750): **61** (Terminology: 81, Spelling/Grammar: 45, Clarity: 67) — CRITICAL
- `azure-virtual-machines.md` (PR #8668): **74-77** (Terminology: 75-78, S&G: 76-77, Clarity: 73-77) — BELOW THRESHOLD
- Annotation includes: **100** — no issues
- `supported-azure-services.md`: **96** — no issues

**Key finding:** Tool-family files are 20-40 points below the 80 threshold. Spelling/Grammar is the worst category (45 on deploy). This is the #1 priority.

**Gaps identified (8 categories) and corresponding code changes needed — see full analysis delivered in task response.**

### 2026-03-25: Acrolinx Compliance P0+P1 Implementation — 4 Services + 9 Static Entries

**Task:** Implement Acrolinx P0+P1 fix plan using strict TDD (AD-007: tests first, then implement).

**Work completed:**

1. **P0: static-text-replacement.json** — Added 9 wordy-phrase entries:
   - `etc.` → `and more`, `in order to` → `to`, `make sure` → `ensure`, `a number of` → `several`, `utilize` → `use`, `functionality` → `feature`, `via` → `through`, `leverage` → `use`, `prior to` → `before`
   - Dropped `please ` — word-boundary regex prevents matching trailing-space keys before next word. Needs a dedicated service.
   - 20 new tests in StaticTextReplacementTests.cs

2. **P1: IntroductoryCommaFixer** — New service inserts commas after: For example, In addition, By default, In this case, If not. Skips code blocks/backticks. Dropped "For each" and "When using" — these need comma after the whole dependent clause, not just the two-word phrase.
   - 20 tests in IntroductoryCommaFixerTests.cs

3. **P1: PresentTenseFixer** — New service converts "will be <verb>ed" → "is/are <verb>ed", "will <verb>" → "<verb>s", "will not be" → "is not". Whitelist of 16 common verbs avoids false positives. Skips code blocks/backticks.
   - 24 tests in PresentTenseFixerTests.cs

4. **P1: AcronymExpander** — Generalized PostProcessor.ExpandMcpAcronym() to config-driven multi-acronym expander. Handles VM, VMSS, AKS, RBAC, IaC, WAF, NSG, VNet, ACR + MCP context pattern. Skips frontmatter/headings/backticks.
   - Config: `data/acronym-definitions.json`
   - 11 tests in AcronymExpanderTests.cs

5. **FamilyFileStitcher wiring** — Stage 4: AcronymExpander (replaces MCP-only), Stage 8: PresentTenseFixer (before ContractionFixer), Stage 10: IntroductoryCommaFixer (after ContractionFixer). Order ensures "will not be" → "is not" → "isn't" flows correctly.
   - 7 integration tests in StitcherAcrolinxIntegrationTests.cs

**Test totals:** 62 new behavioral tests. All 202 ToolFamilyCleanup tests pass. Full solution: 0 build warnings, 0 regressions.

**Key learning:** The static-text-replacement engine's word-boundary regex `(?![A-Za-z0-9_-])` prevents matching keys with trailing spaces when followed by a word character. Entries ending in spaces (like "please ") silently fail. Always use word-boundary-compatible keys.

### 2026-03-25: Fix #219 — ms.date missing in generated tool-family frontmatter

**Bug:** `DeterministicFrontmatterGenerator.Generate()` produced frontmatter YAML without `ms.date`. While `FrontmatterEnricher.Enrich()` correctly injects it during the stitcher pipeline, the intermediate metadata file (saved before stitching in CleanupGenerator line 496) lacked the field.

**Fix:** Added `ms.date: {DateTime.UtcNow:MM/dd/yyyy}` directly to `DeterministicFrontmatterGenerator.Generate()` — 1-line change. Placed after `description:` and before `ms.service:` per Microsoft Learn conventions. The enricher remains as an idempotent safety net (skips ms.date when already present).

**Tests (TDD per AD-007):** 11 integration tests in `FrontmatterPipelineIntegrationTests.cs` — 2 generator-level tests (one was the failing red-phase test), 9 pipeline-level tests verifying all enriched fields survive the full Generate→Stitch flow.

**Key learning:** Required frontmatter fields should be emitted at the source (generator), not solely injected by downstream enrichers. Intermediate files saved between pipeline stages bypass post-processing. Defense-in-depth: generate correct output AND enrich as a safety net.

### 2026-03-25: Fix #220 — Full learn.microsoft.com URLs → site-root-relative paths

**Task:** Generated tool-family files contained full `https://learn.microsoft.com/azure/...` URLs in AI-generated intro paragraphs (line 14 across all namespaces). AD-017 requires site-root-relative paths (`/azure/...`).

**Root cause:** AI-generated content (Step 4 intro paragraphs) produces full URLs despite prompt instructions. Templates were clean — no hardcoded full URLs. HorizontalArticles already had `StripLearnPrefix` but only for its own pipeline.

**Implementation:**
- Created `LearnUrlRelativizer.cs` — regex-based post-processor using `[GeneratedRegex]` source generator for performance. Handles locale stripping, backtick/code-block protection, query params, and anchors.
- Wired as Stage 12 in `FamilyFileStitcher.Stitch()` (after ExampleValueBackticker).
- 17 TDD tests (AD-007) covering all edge cases.
- Full solution: 0 build warnings, 0 test failures (226 ToolFamilyCleanup tests, 1100+ total).

**Key learning:** AI-generated content is the primary source of full learn.microsoft.com URLs — prompts instruct relative paths but AI doesn't always comply. Belt-and-suspenders: always have a deterministic post-processor as backstop for URL normalization, don't rely on prompt instructions alone.

**PR:** #221 (MERGED)

**Decision issued:** AD-024 (LearnUrlRelativizer Decision)

### 2026-03-25: ms.date Frontmatter Assignment

**Task:** Fix #219 — Generated tool-family files missing or containing placeholder `ms.date` values in frontmatter. Requires actual generation timestamp.

**Implementation:** PageGenerator now assigns `ms.date: [datetime]` to all generated files at template rendering.

**Tests:** 11 new tests; all passing.

**PR:** #222 (MERGED)

---

### 2026-03-30: .NET Project Consolidation Plan — C# Implementation Review

**Task:** Review Avery's 7-action .NET consolidation plan from C# implementation perspective. Assess difficulty, code coupling, build order risks, NuGet conflicts, namespace impacts.

**Key Findings:**
- **All 7 actions are technically achievable** — no architectural blockers detected
- **Effort estimates realistic:** 430 minutes total (7 hours) for Actions 1-6
- **No circular dependencies:** All merges are downward in dependency tree
- **No NuGet conflicts:** All versions centrally managed in Directory.Packages.props

**Action difficulty reassessment (vs plan):**
1. CliAnalyzer removal — 🟢 LOW (15 min) — verified zero refs
2. PostProcessVerifier → ToolFamilyCleanup — 🟡 MEDIUM (90 min) — dry-run logic straightforward
3. Core.NaturalLanguage → Core.Shared — 🟡 MEDIUM (120 min) — data file paths require runtime audit
4. NUnit → xUnit — 🟡 MEDIUM (180 min) — 146 tests, mechanical refactor
5. StripFrontmatter consolidation — 🟢 LOW (5 min) — RECOMMENDATION: keep Fingerprint's local impl (has .TrimStart() behavior)
6. Validation.Tests documentation — 🟢 LOW (20 min) — documentation-only task

**Hidden Risks Identified:**
- **Data file path handling (Action 3):** TextCleanup.LoadFiles() expects caller to pass RequiredFiles paths. After merge, must verify all 3 projects (RawTools, Annotations, HorizontalArticles) still find data files at runtime. Runtime-dependent; test against 3+ namespaces.
- **Namespace mismatch (Action 3):** Plan recommends keeping `DocGeneration.Core.NaturalLanguage` namespace post-merge. Creates semantic confusion (namespace doesn't match project location). Should rename to `DocGeneration.Core.Shared.NaturalLanguage` instead (15 min additional work).
- **NUnit assertion argument order (Action 4):** xUnit reverses assertion argument order vs NUnit. Easy to introduce silent test failures. Mitigation: convert one project fully, validate, replicate pattern.
- **StripFrontmatter behavioral difference (Action 5):** Core.Shared.StripFrontmatter() uses regex replacement; Fingerprint uses regex + `.TrimStart()`. Fingerprint output cleaner. DECISION: keep Fingerprint's local implementation (Option A). Document design choice as "intentional optimization for fingerprinting use case."

**Build Order Verification:**
- All merges downward in dependency tree — zero circular risks
- After merges: Core.Shared remains leaf node (no outbound deps)
- Verified project reference chains: PostProcessVerifier → ToolFamilyCleanup → Core.Shared (acyclic)
- After Core.NaturalLanguage merge: RawTools/Annotations/HorizontalArticles → Core.Shared (simplified, no back-refs)

**Missing: TransitiveDependencyFrameworkVersion**
- Directory.Packages.props has ManagePackageVersionsCentrally=true but no TransitiveDependencyFrameworkVersion
- Should add `<TransitiveDependencyFrameworkVersion>net9.0</TransitiveDependencyFrameworkVersion>` for tight alignment
- Action: Include in Phase 1 (10 min fix)

**Verdict: APPROVE WITH CHANGES**
- Conditions: (1) Quinn verify no CI refs to CliAnalyzer/PostProcessVerifier, (2) Morgan audit data file paths pre-merge, (3) Parker validate test count before/after NUnit conversion (146→146)
- Recommendation: Rename Core.NaturalLanguage namespace to Core.Shared.NaturalLanguage (avoid semantic confusion)
- Recommendation: Keep Fingerprint's StripFrontmatter local (document .TrimStart() as intentional optimization)
- Recommendation: Execute Action 4 (NUnit → xUnit) before Action 3 (consolidation) — cleaner ecosystem baseline

**Full review:** `.squad/decisions/inbox/morgan-consolidation-review.md`

---

### 2026-03-25: Fix #220 — Full learn.microsoft.com URLs → site-root-relative paths