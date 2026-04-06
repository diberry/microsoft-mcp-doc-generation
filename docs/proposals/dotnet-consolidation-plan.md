# .NET Project Consolidation Plan

**Author:** Avery (Team Lead)
**Date:** 2026-03-26
**Status:** Proposal — Awaiting team review
**Reviewers:** Riley (Architecture), Morgan (C# Implementation), Cameron (Test Impact), Quinn (CI/Scripts), Parker (Test Coverage)

---

## Executive Summary

The repo contains **42 .csproj files** and **1 solution file** producing **19 executables** and **23 test/library assemblies** from approximately **427 .cs files**. Dina flagged this as too many projects. After full investigation, I'm recommending **7 consolidation actions** that would reduce the project count from **42 → 34** while improving maintainability, eliminating dead code, and standardizing test infrastructure.

The consolidation is conservative by design. The pipeline's architecture — PipelineRunner invokes each step as a separate `dotnet run` subprocess — means most step projects *must* remain as independent executables. The opportunities are in core libraries, orphaned projects, and test standardization.

---

## 1. Current State: Full Project Inventory

### 1.1 Core Libraries (5 projects)

| # | Project | Type | .cs Files | Dependencies | Purpose |
|---|---------|------|-----------|--------------|---------|
| 1 | Core.Shared | Library | 17 | None | Shared models, FrontmatterUtility, config loading |
| 2 | Core.GenerativeAI | Library | 2 | Core.Shared, Azure.AI.OpenAI, Azure.Identity | Azure OpenAI client wrapper with retry logic |
| 3 | Core.NaturalLanguage | Library | 1 | Core.Shared | TextCleanup: parameter normalization, smart text replacement |
| 4 | Core.TemplateEngine | Library | 3 | Handlebars.Net | Handlebars template rendering abstraction |
| 5 | Core.TextTransformation | Library | 8 | System.Text.Json | Text transformation utilities, JSON schema handling |

### 1.2 Pipeline Runner (1 project)

| # | Project | Type | .cs Files | Dependencies | Purpose |
|---|---------|------|-----------|--------------|---------|
| 6 | PipelineRunner | Exe | 44 | Core.Shared, System.CommandLine | Main orchestrator — dispatches Steps 0-6 via subprocess |

### 1.3 Bootstrap Step Projects (5 Exe projects)

| # | Project | Type | .cs Files | Dependencies | Purpose |
|---|---------|------|-----------|--------------|---------|
| 7 | Steps.Bootstrap.CommandParser | Exe | 4 | System.Text.Json, System.CommandLine | Parses azmcp-commands.md → structured JSON |
| 8 | Steps.Bootstrap.BrandMappings | Exe | 1 | Core.GenerativeAI, Core.Shared | Validates namespace → brand mapping, AI-suggests new ones |
| 9 | Steps.Bootstrap.ToolMetadataEnricher | Exe | 8 | System.CommandLine | Enriches tool metadata with CLI output data |
| 10 | Steps.Bootstrap.E2eTestPromptParser | Exe | 6 | System.Text.Json | Parses E2E test prompts from config |
| 11 | **Steps.Bootstrap.CliAnalyzer** | **Exe** | **8** | **System.Text.Json, System.CommandLine, Newtonsoft.Json** | **⚠️ ORPHANED — Not invoked by PipelineRunner or any script** |

### 1.4 Pipeline Step Projects (9 Exe projects)

| # | Project | Type | .cs Files | Dependencies | Purpose |
|---|---------|------|-----------|--------------|---------|
| 12 | Steps.AnnotationsParametersRaw.Annotations | Exe | 19 | Core.Shared, Core.NaturalLanguage, Core.TemplateEngine, ToolFamilyCleanup | Step 1: Generate annotation markdown |
| 13 | Steps.AnnotationsParametersRaw.RawTools | Exe | 4 | Core.Shared, Core.NaturalLanguage | Step 1: Generate raw tool files |
| 14 | Steps.ExamplePrompts.Generation | Exe | 12 | Core.GenerativeAI, Core.Shared, Core.TemplateEngine | Step 2: AI-generate example prompts |
| 15 | Steps.ExamplePrompts.Validation | Exe | 3 | Core.Shared, Core.GenerativeAI | Step 2: Validate generated prompts |
| 16 | Steps.ToolGeneration.Composition | Exe | 3 | Core.Shared | Step 3a: Compose tool documentation from parts |
| 17 | Steps.ToolGeneration.Improvements | Exe | 3 | Core.Shared, Core.GenerativeAI | Step 3b: AI-improve composed tools |
| 18 | Steps.ToolFamilyCleanup | Exe | 29 | Core.GenerativeAI, Core.Shared | Step 4: Clean/validate tool-family articles |
| 19 | Steps.SkillsRelevance | Exe | 8 | Core.Shared | Step 5: GitHub API skills relevance |
| 20 | Steps.HorizontalArticles | Exe | 7 | Core.Shared, Core.GenerativeAI, Core.NaturalLanguage, Core.TextTransformation, Core.TemplateEngine, Annotations | Step 6: Generate cross-cutting articles |

### 1.5 Utility/Tool Projects (3 projects)

| # | Project | Type | .cs Files | Dependencies | Purpose |
|---|---------|------|-----------|--------------|---------|
| 21 | Utilities.ToolMetadataExtractor | Exe | 3 | Core.Shared, Microsoft.CodeAnalysis.CSharp, Newtonsoft.Json | Roslyn-based metadata extraction from source |
| 22 | Tools.Fingerprint | Exe | 5 | System.Text.Json | Content fingerprinting for regression detection |
| 23 | **Tools.PostProcessVerifier** | **Exe** | **1** | **ToolFamilyCleanup** | **Applies ToolFamilyCleanup post-processors in dry-run mode** |

### 1.6 Test Projects (19 projects)

| # | Project | Framework | .cs Files | Tests For | Notes |
|---|---------|-----------|-----------|-----------|-------|
| 24 | Core.Shared.Tests | xUnit | 12 | Core.Shared | — |
| 25 | Core.GenerativeAI.Tests | xUnit | 4 | Core.GenerativeAI | — |
| 26 | Core.NaturalLanguage.Tests | xUnit | 1 | Core.NaturalLanguage | — |
| 27 | Core.TemplateEngine.Tests | xUnit | 1 | Core.TemplateEngine | — |
| 28 | Core.TextTransformation.Tests | **NUnit** | 3 | Core.TextTransformation | ⚠️ Mixed framework |
| 29 | Steps.Bootstrap.CommandParser.Tests | xUnit | 2 | CommandParser | — |
| 30 | Steps.Bootstrap.BrandMappings.Tests | xUnit | 3 | BrandMappings | — |
| 31 | Steps.Bootstrap.ToolMetadataEnricher.Tests | xUnit | 4 | ToolMetadataEnricher | — |
| 32 | Steps.Bootstrap.E2eTestPromptParser.Tests | xUnit | 1 | E2eTestPromptParser | — |
| 33 | Steps.AnnotationsParametersRaw.Annotations.Tests | xUnit | 16 | Annotations | — |
| 34 | Steps.ExamplePrompts.Generation.Tests | xUnit | 15 | ExamplePrompts.Generation | — |
| 35 | Steps.ExamplePrompts.Validation.Tests | xUnit | 2 | ExamplePrompts.Validation | — |
| 36 | Steps.ToolGeneration.Improvements.Tests | xUnit | 3 | ToolGeneration.Improvements | — |
| 37 | Steps.ToolFamilyCleanup.Tests | xUnit | 29 | ToolFamilyCleanup | — |
| 38 | **Steps.ToolFamilyCleanup.Validation.Tests** | **xUnit** | **2** | **PowerShell script** | **⚠️ No project ref — tests PS1 via Process.Start** |
| 39 | Steps.HorizontalArticles.Tests | **NUnit** | 5 | HorizontalArticles | ⚠️ Mixed framework |
| 40 | Steps.SkillsRelevance.Tests | **NUnit** | 2 | SkillsRelevance | ⚠️ Mixed framework |
| 41 | Tools.Fingerprint.Tests | xUnit | 3 | Tools.Fingerprint | — |
| 42 | PromptRegression.Tests | xUnit | 11 | Regression baselines | Cross-cutting regression suite |
| — | PipelineRunner.Tests | xUnit | 22 | PipelineRunner | Mocked integration tests |

---

## 2. Problems Identified

### P1: Orphaned Project — CliAnalyzer (HIGH)
**Project:** `DocGeneration.Steps.Bootstrap.CliAnalyzer`
**Evidence:** Not referenced by PipelineRunner's BootstrapStep, not invoked by any script (`start.sh`, `prompt-regression.sh`, `merge-namespaces.sh`), not in the solution's active build path for pipeline execution.
**Impact:** 8 .cs files of dead code. Uses both Newtonsoft.Json AND System.Text.Json (dual JSON library — the only project that does this).

### P2: Single-File Wrapper — PostProcessVerifier (MEDIUM)
**Project:** `DocGeneration.Tools.PostProcessVerifier`
**Evidence:** Contains 1 .cs file (Program.cs). Re-applies the exact same 10 post-processors from ToolFamilyCleanup in a dry-run mode that writes `.after` files.
**Impact:** Separate project just to run ToolFamilyCleanup logic with `--dry-run` semantics. Could be a command-line flag on ToolFamilyCleanup instead.

### P3: Single-File Core Library — NaturalLanguage (MEDIUM)
**Project:** `DocGeneration.Core.NaturalLanguage`
**Evidence:** Contains 1 .cs file (TextCleanup.cs — 472 lines). Already depends on Core.Shared. Referenced by 3 step projects.
**Impact:** Separate assembly boundary for a single utility class. Adds compile-time overhead and dependency complexity.

### P4: Mixed Test Frameworks (LOW-MEDIUM)
**Projects:** `Core.TextTransformation.Tests`, `Steps.HorizontalArticles.Tests`, `Steps.SkillsRelevance.Tests`
**Evidence:** These 3 projects use NUnit while the other 16 test projects use xUnit.
**Impact:** Two test frameworks = two sets of conventions, two assertion APIs, two packages to maintain. Confusing for developers. `dotnet test` runs both, but developers must know both frameworks.

### P5: StripFrontmatter Remaining Duplication (LOW)
**Projects:** `Tools.Fingerprint/MarkdownAnalyzer.cs`, `PromptRegression.Tests/QualityMetrics.cs`
**Evidence:** Two implementations still use their own regex patterns instead of calling the canonical `FrontmatterUtility.StripFrontmatter()` from Core.Shared (per AD-018).
**Impact:** If the frontmatter format changes, two implementations would be missed.

### P6: Orphaned-by-Design Test Project Undocumented (LOW)
**Project:** `DocGeneration.Steps.ToolFamilyCleanup.Validation.Tests`
**Evidence:** Has zero project references. Tests a PowerShell script (`Validate-ToolFamily-PostAssembly.ps1`) by spawning processes. This is intentional but undocumented — looks like a mistake to every new contributor.
**Impact:** Confusion. Any cleanup effort would incorrectly flag it for removal.

---

## 3. Dependency Diagram

```
                    ┌─────────────────────────────────────┐
                    │        PipelineRunner (Exe)          │
                    │   Orchestrates Steps 0-6 via         │
                    │   dotnet run --project <step>        │
                    └─────────────┬───────────────────────┘
                                  │ subprocess calls
    ┌─────────────────────────────┼─────────────────────────────────┐
    │                             │                                 │
    ▼                             ▼                                 ▼
┌──────────┐  ┌──────────────────────────────────┐  ┌──────────────────────┐
│ Step 0   │  │ Steps 1-4 (per namespace)        │  │ Steps 5-6            │
│ Bootstrap│  │                                  │  │ (per namespace)      │
│ (global) │  │ 1. Annotations + Raw             │  │                      │
│          │  │ 2. ExamplePrompts (Gen+Val)      │  │ 5. SkillsRelevance   │
│ Subs:    │  │ 3. ToolGeneration (Comp+Improve) │  │ 6. HorizontalArticles│
│ •Command │  │ 4. ToolFamilyCleanup             │  │                      │
│  Parser  │  └──────────────────────────────────┘  └──────────────────────┘
│ •Brand   │
│  Mappings│
│ •ToolMeta│
│  Enricher│
│ •E2eTest │
│  Parser  │
└──────────┘

Core Library Dependencies (compile-time):
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Core.Shared ◄──── Core.GenerativeAI ◄──── BrandMappings, ExamplePrompts.*, ToolFamilyCleanup,
     ▲                                     ToolGeneration.Improvements, HorizontalArticles
     │
     ├──── Core.NaturalLanguage ◄──── Annotations, RawTools, HorizontalArticles
     │
     ├──── Core.TemplateEngine ◄──── Annotations, ExamplePrompts.Generation, HorizontalArticles
     │
     ├──── Core.TextTransformation ◄──── HorizontalArticles
     │
     ├──── PipelineRunner
     ├──── ToolGeneration.Composition
     ├──── SkillsRelevance
     ├──── PromptRegression.Tests
     └──── Utilities.ToolMetadataExtractor

Standalone (no Core.Shared dependency):
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
  • CommandParser (uses System.Text.Json only)
  • ToolMetadataEnricher (uses System.CommandLine only)
  • E2eTestPromptParser (uses System.Text.Json only)
  • CliAnalyzer ← ORPHANED (Newtonsoft.Json + System.Text.Json)
  • Tools.Fingerprint (uses System.Text.Json only)
  • Core.TemplateEngine (uses Handlebars.Net only)
  • Core.TextTransformation (uses System.Text.Json only)

Cross-cutting reference:
━━━━━━━━━━━━━━━━━━━━━━━
  • Tools.PostProcessVerifier ──► ToolFamilyCleanup (reuses its post-processors)
  • Annotations ──► ToolFamilyCleanup (reuses ToolReader for StripFrontmatter)
  • HorizontalArticles ──► Annotations (reuses annotation types)
```

---

## 4. Proposed Consolidation Actions

### Action 1: Remove CliAnalyzer (ORPHANED)
| Attribute | Value |
|-----------|-------|
| **Projects affected** | `DocGeneration.Steps.Bootstrap.CliAnalyzer` |
| **Action** | Delete project directory, remove from .sln |
| **Complexity** | 🟢 Low |
| **Risk** | Low — not referenced by any code, script, or CI config |
| **Net project reduction** | -1 |

**Why:** This is dead code. CliAnalyzer is a markdown report generator for CLI structure analysis. It was likely a development/debugging tool that was never integrated into the pipeline. No other project references it. No script invokes it. Keeping it creates confusion and carries a Newtonsoft.Json dependency that nothing else uses.

**Migration:** `git rm -r docs-generation/DocGeneration.Steps.Bootstrap.CliAnalyzer/` and remove from solution file. If the team wants to preserve it, move to a `tools/deprecated/` directory outside the solution.

**Quinn (CI):** Verify no CI workflow builds/tests this project specifically.
**Parker (QA):** No test impact — project has no test project.

---

### Action 2: Merge PostProcessVerifier → ToolFamilyCleanup
| Attribute | Value |
|-----------|-------|
| **Source** | `DocGeneration.Tools.PostProcessVerifier` (1 .cs file) |
| **Target** | `DocGeneration.Steps.ToolFamilyCleanup` |
| **Action** | Add `--verify-only` / `--dry-run` flag to ToolFamilyCleanup |
| **Complexity** | 🟡 Medium |
| **Risk** | Medium — must ensure existing PostProcessVerifier callers switch to new flag |
| **Net project reduction** | -1 |

**Why:** PostProcessVerifier literally re-applies ToolFamilyCleanup's 10 post-processors and writes `.after` comparison files. It already takes a project reference to ToolFamilyCleanup. This is a thin shim that should be a command-line mode, not a separate project.

**Migration:**
1. Add `--verify-only` and `--output-suffix .after` options to ToolFamilyCleanup's Program.cs
2. Port the diff-reporting logic from PostProcessVerifier.Program.cs
3. Update any scripts that call PostProcessVerifier to use ToolFamilyCleanup with new flags
4. Delete PostProcessVerifier project

**Morgan (C#):** Implement the `--verify-only` flag using existing System.CommandLine infrastructure in ToolFamilyCleanup.
**Quinn (CI):** Search for any script/CI references to `PostProcessVerifier`.

---

### Action 3: Merge Core.NaturalLanguage → Core.Shared
| Attribute | Value |
|-----------|-------|
| **Source** | `DocGeneration.Core.NaturalLanguage` (1 .cs file: TextCleanup.cs) |
| **Target** | `DocGeneration.Core.Shared` |
| **Action** | Move TextCleanup.cs into Core.Shared, update all references |
| **Complexity** | 🟡 Medium |
| **Risk** | Medium — 3 step projects reference Core.NaturalLanguage; namespace change may be needed |
| **Net project reduction** | -2 (library + test project) |

**Why:** Core.NaturalLanguage contains a single 472-line utility class. It already depends on Core.Shared. Having a separate assembly for one utility class adds compile-time overhead, solution clutter, and dependency-graph complexity. The class logically belongs in Core.Shared alongside other text utilities.

**Migration:**
1. Move `TextCleanup.cs` to `Core.Shared/NaturalLanguage/TextCleanup.cs` (keep namespace for minimal churn)
2. Move JSON data files (`nl-parameters.json`, `static-text-replacement.json`, `nl-parameter-identifiers.json`) to Core.Shared
3. Update project references in: `Annotations`, `RawTools`, `HorizontalArticles` — remove Core.NaturalLanguage ref (Core.Shared already referenced)
4. Move `Core.NaturalLanguage.Tests` tests into `Core.Shared.Tests`
5. Delete Core.NaturalLanguage project and test project

**Morgan (C#):** Keep the `DocGeneration.Core.NaturalLanguage` namespace on TextCleanup to minimize downstream changes. Only the project reference changes.
**Parker (QA):** Verify TextCleanup tests pass in their new home in Core.Shared.Tests.

---

### Action 4: Standardize Test Frameworks — NUnit → xUnit
| Attribute | Value |
|-----------|-------|
| **Projects affected** | `Core.TextTransformation.Tests`, `Steps.HorizontalArticles.Tests`, `Steps.SkillsRelevance.Tests` |
| **Action** | Migrate 3 NUnit test projects to xUnit |
| **Complexity** | 🟡 Medium |
| **Risk** | Low — mechanical refactor, all assertions have xUnit equivalents |
| **Net project reduction** | 0 (framework change, not removal) |

**Why:** 16 of 19 test projects use xUnit. The 3 NUnit outliers create cognitive overhead (two assertion APIs, two test discovery patterns) and prevent standardizing shared test utilities. Every new team member has to know both frameworks.

**Migration per project:**
1. Replace NuGet refs: `NUnit` → `xunit`, `NUnit3TestAdapter` → `xunit.runner.visualstudio`
2. Replace attributes: `[Test]` → `[Fact]`, `[TestCase(...)]` → `[InlineData(...)]` + `[Theory]`
3. Replace assertions: `Assert.That(x, Is.EqualTo(y))` → `Assert.Equal(y, x)`
4. Remove `[TestFixture]` class attributes (not needed in xUnit)
5. Run tests, verify all pass

**File counts:** TextTransformation.Tests (3 .cs), HorizontalArticles.Tests (5 .cs), SkillsRelevance.Tests (2 .cs) — total ~10 files to migrate.

**Cameron (Test Lead):** Review migration for any NUnit-specific features (e.g., `TestContext`, `SetUp/TearDown`) that need xUnit equivalents.
**Parker (QA):** Verify test counts match before and after migration.

---

### Action 5: Consolidate StripFrontmatter Duplication (AD-018 completion)
| Attribute | Value |
|-----------|-------|
| **Projects affected** | `Tools.Fingerprint/MarkdownAnalyzer.cs`, `PromptRegression.Tests/QualityMetrics.cs` |
| **Action** | Replace local regex implementations with calls to `FrontmatterUtility.StripFrontmatter()` |
| **Complexity** | 🟢 Low |
| **Risk** | Low — existing canonical implementation is well-tested |
| **Net project reduction** | 0 (code cleanup, not project removal) |

**Why:** AD-018 identified 3× `StripFrontmatter` implementations. Four projects now correctly delegate to the canonical `FrontmatterUtility.StripFrontmatter()` in Core.Shared, but two still use their own regex patterns. This is the final cleanup.

**Migration:**
1. `Tools.Fingerprint`: Add project reference to Core.Shared. Replace `MarkdownAnalyzer.StripFrontmatter()` regex with call to `FrontmatterUtility.StripFrontmatter()`.
2. `PromptRegression.Tests`: Already references Core.Shared. Replace `QualityMetrics.StripFrontmatter()` private method with direct call to `FrontmatterUtility.StripFrontmatter()`.
3. Update corresponding tests to verify behavior is identical.

**Morgan (C#):** Straightforward — 2 files, ~10 lines of code change each.

---

### Action 6: Document ToolFamilyCleanup.Validation.Tests Design Intent
| Attribute | Value |
|-----------|-------|
| **Project affected** | `DocGeneration.Steps.ToolFamilyCleanup.Validation.Tests` |
| **Action** | Add README.md explaining why this project has no project references |
| **Complexity** | 🟢 Low |
| **Risk** | None |
| **Net project reduction** | 0 |

**Why:** This test project has zero project references because it intentionally tests a PowerShell script (`Validate-ToolFamily-PostAssembly.ps1`) via `Process.Start()`. Without documentation, every cleanup effort will flag it as broken or orphaned. Three of us already spent time investigating it during this analysis.

**Migration:** Add a `README.md` to the project directory explaining:
- This is a PowerShell integration test project
- It spawns `Validate-ToolFamily-PostAssembly.ps1` as an external process
- It deliberately has no C# project references
- It requires PowerShell 7 to run

**Reeve (Docs):** Write the README.

---

### Action 7: Evaluate Bootstrap Sub-Step Consolidation (FUTURE)
| Attribute | Value |
|-----------|-------|
| **Projects affected** | `CommandParser`, `BrandMappings`, `ToolMetadataEnricher`, `E2eTestPromptParser` + their test projects |
| **Action** | Evaluate merging 4 Bootstrap sub-projects into a single multi-command CLI |
| **Complexity** | 🔴 High |
| **Risk** | High — changes PipelineRunner's invocation pattern, affects all bootstrap scripts |
| **Net project reduction** | -6 (4 source + keep 1 combined test) |

**Why:** Four small Exe projects (1-8 .cs files each) are individually invoked by BootstrapStep during Step 0. They could be unified as subcommands of a single `DocGeneration.Steps.Bootstrap` tool using `System.CommandLine`'s `RootCommand` + subcommands pattern. BrandMappings in particular is just 1 .cs file.

**NOT recommended for this cycle.** This is a larger architectural change that requires:
- Refactoring PipelineRunner's `BootstrapStep` to call a single project with subcommand args
- Merging 4 test projects (with different test data patterns)
- Updating any scripts that invoke individual bootstrap tools
- This should be tracked as a future improvement and evaluated by Riley.

---

## 5. Consolidation Summary

| # | Action | Complexity | Projects Removed | Priority |
|---|--------|-----------|-----------------|----------|
| 1 | Remove CliAnalyzer | 🟢 Low | 1 | P0 — dead code |
| 2 | Merge PostProcessVerifier → ToolFamilyCleanup | 🟡 Medium | 1 | P1 |
| 3 | Merge Core.NaturalLanguage → Core.Shared | 🟡 Medium | 2 | P1 |
| 4 | Standardize NUnit → xUnit | 🟡 Medium | 0 | P2 |
| 5 | Consolidate StripFrontmatter (AD-018) | 🟢 Low | 0 | P2 |
| 6 | Document Validation.Tests design | 🟢 Low | 0 | P2 |
| 7 | Bootstrap sub-step consolidation | 🔴 High | 6 | P3 — future |
| **Total (Actions 1-6)** | | | **4 projects → 38 total** | |
| **Total (including Action 7)** | | | **10 projects → 32 total** | |

---

## 6. What We're NOT Consolidating (and Why)

### Pipeline Step Projects Must Stay Separate
PipelineRunner invokes each step via `dotnet run --project <path>`. This subprocess-based architecture requires each step to be an independent Exe project. Merging step projects would require fundamentally changing the pipeline execution model — that's an architecture decision for Riley, not a consolidation action.

### Core.GenerativeAI Stays Independent
Despite having only 2 .cs files, this library carries Azure.AI.OpenAI and Azure.Identity dependencies. Merging into Core.Shared would force those heavy NuGet packages onto every project that references Shared — including projects that don't use AI (CommandParser, Fingerprint, SkillsRelevance, etc.). The current separation is correct.

### Core.TemplateEngine Stays Independent
Similar reasoning — Handlebars.Net dependency should not pollute Core.Shared.

### Core.TextTransformation Stays Independent
8 .cs files, distinct functionality, System.Text.Json dependency is lightweight but the module is large enough to warrant separation.

### Utilities.ToolMetadataExtractor Stays Independent
Carries Microsoft.CodeAnalysis.CSharp (Roslyn) — a heavy dependency that must not bleed into other projects. Referenced by extraction scripts.

### Tools.Fingerprint Stays Independent
Used by `prompt-regression.sh` for content fingerprinting. Has its own test project. Standalone utility with clear purpose.

### PromptRegression.Tests Stays Independent
Cross-cutting regression test suite that tests across multiple pipeline stages. Not tied to a single source project.

---

## 7. Risks and Mitigations

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Namespace changes break `using` statements | Medium | Low | Keep original namespaces even after moving files |
| PostProcessVerifier callers not updated | Low | Medium | `grep -r PostProcessVerifier` across all scripts before deletion |
| NUnit → xUnit migration misses assertion edge cases | Low | Low | Run `dotnet test` before and after, compare test counts |
| Core.NaturalLanguage data files not found at runtime | Medium | Medium | Use embedded resources or verify file copy rules in Core.Shared.csproj |
| Action 7 (Bootstrap consolidation) scope creep | High | High | Defer to separate sprint; requires Riley's architecture review |

---

## 8. Recommended Execution Order

```
Phase 1 (Low-risk, immediate):
  Action 1: Remove CliAnalyzer               ← Quinn verifies no CI refs, then delete
  Action 6: Document Validation.Tests        ← Reeve writes README
  Action 5: Consolidate StripFrontmatter     ← Morgan, 30-min task

Phase 2 (Medium-risk, same sprint):
  Action 4: NUnit → xUnit standardization    ← Morgan/Parker, ~2 hours
  Action 2: Merge PostProcessVerifier        ← Morgan, ~1 hour + Quinn script check

Phase 3 (Medium-risk, next sprint):
  Action 3: Merge Core.NaturalLanguage       ← Morgan, ~2 hours + integration test
  
Phase 4 (Future evaluation):
  Action 7: Bootstrap consolidation          ← Riley evaluates feasibility
```

---

## 9. Success Criteria

- [ ] Project count reduced from 42 to ≤38 (Actions 1-3)
- [ ] Zero NUnit test projects remain (Action 4)
- [ ] No StripFrontmatter implementations outside FrontmatterUtility (Action 5)
- [ ] All 1,028+ existing tests continue to pass
- [ ] `dotnet build docs-generation.sln` succeeds
- [ ] Pipeline runs successfully for at least 3 namespaces end-to-end
- [ ] No scripts reference deleted projects

---

*This proposal is ready for team review. Each team member should evaluate their section:*
- **Riley:** Actions 3, 7 — architectural feasibility of merges
- **Morgan:** Actions 2, 3, 4, 5 — implementation effort estimates
- **Cameron:** Actions 3, 4 — test strategy implications
- **Quinn:** Actions 1, 2 — CI/script impact verification
- **Parker:** Actions 3, 4, 5 — test coverage preservation
- **Reeve:** Action 6 — documentation
