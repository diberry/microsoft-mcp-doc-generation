# Project Context

- **Owner:** diberry
- **Project:** Azure MCP Documentation Generator — automated pipeline producing 800+ markdown docs for 52 Azure MCP namespaces
- **Stack:** .NET 9, C#, Handlebars.Net, PowerShell 7, bash, Docker, Azure OpenAI
- **Created:** 2026-03-20

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### 2026-03-30: .NET Project Consolidation Plan — Team Review Complete (AD-027 through AD-034)

**Session:** Orchestrated asynchronous review of consolidation plan across 8-member team.

**Decision:** Plan APPROVED WITH CONDITIONS across all 8 reviewers.

**Verdicts:**
- ✅ Riley (Architect): APPROVE WITH CHANGES
- ✅ Morgan (C# Dev): APPROVE WITH CHANGES  
- ✅ Cameron (Test Lead): APPROVE WITH CHANGES
- ✅ Quinn (DevOps): APPROVED
- ✅ Parker (QA): APPROVE WITH CONTINGENCIES
- ✅ Sage (AI/Prompt): RECOMMEND APPROVAL
- ✅ Reeve (Docs): APPROVED

**Consolidation scope:** 42 → 40 projects (Actions 1-6 approved; Action 7 deferred).

**Actions approved:**
1. Remove CliAnalyzer (orphaned, 15 min)
2. Merge PostProcessVerifier → ToolFamilyCleanup (90 min)
3. Merge Core.NaturalLanguage → Core.Shared (120 min, file discovery validation required)
4. NUnit → xUnit migration (180 min, test count verification required)
5. Consolidate StripFrontmatter (5-45 min, behavioral option decision required)
6. Document Validation.Tests (30 min)

**Action deferred:**
- Action 7 (Bootstrap sub-step consolidation): Riley rejected — violates subprocess isolation contract (resilience requirement)

**Execution:** 6-8 hours across 2 sprints (Phase 1-3). Quality gates mandatory per AD-028.

**Key decisions filed:**
- AD-027: Consolidation plan approval
- AD-028: Quality gate strategy (3 gates)
- AD-029: Data file discovery requirement
- AD-030: Exit code preservation requirement
- AD-031: Namespace preservation for Action 3
- AD-032: Test baseline verification for Action 4
- AD-033: Post-processor order verification (Action 2)
- AD-034: AI output consistency test (Action 3)

**Orchestration log:** 9 files created in `.squad/orchestration-log/2026-03-30T00-45-consolidation-review/` (one per agent + session summary)

**Session log:** `.squad/log/2026-03-30T00-45-consolidation-review.md`

**Decision inbox:** 8 files merged into `decisions.md` as AD-027–AD-034; inbox now empty.

**Next step:** Morgan begins Phase 1 implementation (Actions 1, 6, 5) pending team sign-off.

---

### 2026-03-26: .NET Project Consolidation Investigation

**Session:** Full audit of 42 .csproj files across the repo, mapping dependencies, usage, and consolidation opportunities.

**Key architectural findings:**
- **Pipeline architecture is subprocess-based:** PipelineRunner invokes each step project via `dotnet run --project`. Step projects MUST remain independent Exe projects. This is the #1 constraint against aggressive consolidation.
- **42 projects:** 19 Exe (pipeline steps + tools), 5 core libraries, 19 test projects. ~427 .cs files total.
- **CliAnalyzer is orphaned:** `DocGeneration.Steps.Bootstrap.CliAnalyzer` (8 .cs files) is not referenced by PipelineRunner, not invoked by any script. Dead code with dual JSON library dependency (only project using Newtonsoft.Json + System.Text.Json).
- **PostProcessVerifier is a 1-file shim:** Reapplies ToolFamilyCleanup's post-processors in dry-run mode. Should be a CLI flag, not a separate project.
- **Core.NaturalLanguage is 1 file:** Single TextCleanup.cs (472 lines) already depends on Core.Shared. Belongs in Core.Shared.
- **Core.GenerativeAI MUST stay separate:** Azure.AI.OpenAI/Azure.Identity dependencies should not bleed into Core.Shared (would pollute 24+ downstream projects).
- **Mixed test frameworks:** 3 NUnit projects among 16 xUnit projects (TextTransformation.Tests, HorizontalArticles.Tests, SkillsRelevance.Tests).
- **ToolFamilyCleanup.Validation.Tests is orphaned by design:** Tests PowerShell script via Process.Start(), intentionally has zero project references. Needs documentation.
- **StripFrontmatter (AD-018):** Mostly consolidated — 4 projects correctly delegate to FrontmatterUtility. 2 remaining duplicates in Fingerprint.MarkdownAnalyzer and PromptRegression.Tests.QualityMetrics.

**Consolidation plan:** `docs/proposals/dotnet-consolidation-plan.md` — 7 actions, 42→34 projects (or 32 with future Bootstrap merge). Decision filed in inbox.

**Key file paths:**
- Solution: `mcp-doc-generation.sln`
- Pipeline orchestrator: `mcp-tools/DocGeneration.PipelineRunner/` (44 .cs files, largest project)
- Step registry: `DocGeneration.PipelineRunner/StepRegistry.cs`
- Orphaned project: `mcp-tools/DocGeneration.Steps.Bootstrap.CliAnalyzer/`
- Consolidation plan: `docs/proposals/dotnet-consolidation-plan.md`
- Decision: `.squad/decisions/inbox/avery-dotnet-consolidation.md`

---

### 2026-03-25:Work Prioritization — 14 Issues Created from Team Reviews (UPDATE: ALL MERGED)

**Session:** Synthesized requirements review (#202), test strategy reviews (6 reviewers), AD-020, AD-021, and existing issue backlog into a prioritized issue set. **Status: All 3 merged PRs complete; decision inbox consolidated; user workflow directive enforced.**

**Work distribution:**
- Morgan: P0 (#204 - Step 3 validator), P3 (#216 - StripFrontmatter consolidation)
- Sage: P0 (#205 - Step 6 validator), P2 (#211, #212, #214 - prompt versioning/tracking/regression)
- Parker: P1 (#206, #207 - TextCleanup tests, failure path tests)
- Avery: P1 (#208, #209 - Bootstrap contracts, fingerprinting)
- Riley: P0 (#203 - ResetOutputDirectory fix), P1 (#210 - structured step-result.json)

**Merged work this batch:**
- PR #221 (Morgan): LearnUrlRelativizer — full URLs → relative paths (AD-024)
- PR #222 (Morgan): ms.date frontmatter assignment
- PR #223 (Sage): Acrolinx compliance — 4 services + 62 tests (AD-022, AD-025 by Parker)

**Key decision:** AD-020 (User Workflow Directive) — all future work must follow 6-step process (plan → test → code → tests → review → notify). Never skip steps.

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
