# Package Rationalization Plan (#345)

> **Status**: Plan only — no implementation.
> **Author**: Riley (Architect)
> **Date**: 2026-04-08
>
> **This is a plan document. Implementation requires separate team approval.**

---

## Table of Contents

1. [Executive Summary](#executive-summary)
2. [.NET Project Inventory](#net-project-inventory)
3. [npm Package Inventory](#npm-package-inventory)
4. [Central Package Management Analysis](#central-package-management-analysis)
5. [Dependency Graph](#dependency-graph)
6. [Findings](#findings)
7. [Recommendations](#recommendations)
8. [Implementation Order](#implementation-order)

---

## Executive Summary

The repository contains **47 .NET projects** across two solutions and **3 npm packages**. All .NET projects target `net10.0` via either per-project settings or `Directory.Build.props`. Central package management (CPM) is used in both solution trees, but with **version divergence** across the two `Directory.Packages.props` files for shared packages.

Key findings:
- **5 version conflicts** between docs-generation and skills-generation for the same NuGet packages
- **1 orphan test project** with zero project references (standalone TestData-based tests)
- **4 standalone Bootstrap executables** that could share a common CLI/JSON infrastructure library
- **3 npm packages** with zero shared dependencies and no workspace configuration
- Good overall architecture — the pipeline step decomposition is clean and well-factored

---

## .NET Project Inventory

### Solution: `docs-generation.sln` (44 projects)

#### Core Libraries (5 projects)

| # | Project | Type | Purpose | NuGet Dependencies | Project References |
|---|---------|------|---------|-------------------|-------------------|
| 1 | Core.Shared | Library | Common types, models, constants | — | — |
| 2 | Core.GenerativeAI | Library | Azure OpenAI client wrapper, retry logic | Azure.AI.OpenAI, Azure.Identity, Microsoft.Extensions.AI, Microsoft.Extensions.AI.OpenAI | Core.Shared |
| 3 | Core.NaturalLanguage | Library | Natural language parameter processing | — | Core.Shared |
| 4 | Core.TemplateEngine | Library | Handlebars template rendering, helpers | Handlebars.Net | — |
| 5 | Core.TextTransformation | Library | Static text replacements, trailing period management | System.Text.Json | — |

#### Pipeline Runner (1 project)

| # | Project | Type | Purpose | NuGet Dependencies | Project References |
|---|---------|------|---------|-------------------|-------------------|
| 6 | PipelineRunner | Exe | Pipeline orchestrator, step registry, CLI entry point | System.CommandLine | Core.Shared |

#### Bootstrap Steps (5 executables)

| # | Project | Type | Purpose | NuGet Dependencies | Project References |
|---|---------|------|---------|-------------------|-------------------|
| 7 | Steps.Bootstrap.BrandMappings | Exe | Brand name mapping validation | System.Text.Json | Core.GenerativeAI, Core.Shared |
| 8 | Steps.Bootstrap.CliAnalyzer | Exe | MCP CLI JSON metadata extraction | System.Text.Json, System.CommandLine, Newtonsoft.Json | — |
| 9 | Steps.Bootstrap.CommandParser | Exe | CLI command parsing and namespace extraction | System.Text.Json, System.CommandLine | — |
| 10 | Steps.Bootstrap.E2eTestPromptParser | Exe | Parse E2E test prompts from MCP source | System.Text.Json | — |
| 11 | Steps.Bootstrap.ToolMetadataEnricher | Exe | Enrich tool metadata with additional attributes | System.CommandLine | — |

#### Namespace Steps (9 executables)

| # | Project | Type | Purpose | NuGet Dependencies | Project References |
|---|---------|------|---------|-------------------|-------------------|
| 12 | Steps.AnnotationsParametersRaw.Annotations | Exe | Generate annotation include files | — | Core.Shared, Core.NaturalLanguage, Core.TemplateEngine, Steps.ToolFamilyCleanup |
| 13 | Steps.AnnotationsParametersRaw.RawTools | Exe | Generate raw tool files | — | Core.Shared, Core.NaturalLanguage |
| 14 | Steps.ExamplePrompts.Generation | Exe | AI-generate 5 example prompts per tool | — | Core.GenerativeAI, Core.Shared, Core.TemplateEngine |
| 15 | Steps.ExamplePrompts.Validation | Exe | Validate generated example prompts | — | Core.Shared, Core.GenerativeAI |
| 16 | Steps.HorizontalArticles | Exe | AI-generate overview articles per tool family | — | Steps.Annotations, Core.Shared, Core.GenerativeAI, Core.NaturalLanguage, Core.TextTransformation, Core.TemplateEngine |
| 17 | Steps.SkillsRelevance | Exe | GitHub Copilot skills mapping | — | Core.Shared |
| 18 | Steps.ToolFamilyCleanup | Exe | Assemble per-service article, post-assembly validation | — | Core.GenerativeAI, Core.Shared |
| 19 | Steps.ToolGeneration.Composition | Exe | Compose tool descriptions from templates | — | Core.Shared |
| 20 | Steps.ToolGeneration.Improvements | Exe | AI-improve tool descriptions | — | Core.Shared, Core.GenerativeAI |

#### Utility/Tool Projects (3 executables)

| # | Project | Type | Purpose | NuGet Dependencies | Project References |
|---|---------|------|---------|-------------------|-------------------|
| 21 | Tools.Fingerprint | Exe | Output fingerprinting for change detection | System.Text.Json | — |
| 22 | Tools.PostProcessVerifier | Exe | Post-processing verification | — | Steps.ToolFamilyCleanup |
| 23 | Utilities.ToolMetadataExtractor | Exe | Extract metadata from MCP C# source via Roslyn | Microsoft.CodeAnalysis.CSharp, Microsoft.Extensions.Logging, Microsoft.Extensions.Logging.Console, System.CommandLine, Newtonsoft.Json | Core.Shared |

#### Test Infrastructure (1 library)

| # | Project | Type | Purpose | NuGet Dependencies | Project References |
|---|---------|------|---------|-------------------|-------------------|
| 24 | TestInfrastructure | Library | Shared test utilities and helpers | — | — |

#### Test Projects (20 projects)

| # | Project | Tests For | Project References |
|---|---------|-----------|-------------------|
| 25 | Core.GenerativeAI.Tests | Core.GenerativeAI | Core.GenerativeAI, Core.Shared |
| 26 | Core.NaturalLanguage.Tests | Core.NaturalLanguage | Core.NaturalLanguage |
| 27 | Core.Shared.Tests | Core.Shared | Core.Shared |
| 28 | Core.TemplateEngine.Tests | Core.TemplateEngine | Core.TemplateEngine |
| 29 | Core.TextTransformation.Tests | Core.TextTransformation | Core.TextTransformation |
| 30 | PipelineRunner.Tests | PipelineRunner | PipelineRunner, Steps.ToolFamilyCleanup, Steps.ToolGeneration.Composition, TestInfrastructure |
| 31 | PromptRegression.Tests | Prompt regression | Core.Shared, TestInfrastructure |
| 32 | Steps.AnnotationsParametersRaw.Annotations.Tests | Annotations | Steps.Annotations, Core.TemplateEngine, TestInfrastructure |
| 33 | Steps.Bootstrap.BrandMappings.Tests | BrandMappings | Steps.Bootstrap.BrandMappings, Core.Shared, TestInfrastructure |
| 34 | Steps.Bootstrap.CommandParser.Tests | CommandParser | Steps.Bootstrap.CommandParser |
| 35 | Steps.Bootstrap.E2eTestPromptParser.Tests | E2eTestPromptParser | Steps.Bootstrap.E2eTestPromptParser |
| 36 | Steps.Bootstrap.ToolMetadataEnricher.Tests | ToolMetadataEnricher | Steps.Bootstrap.ToolMetadataEnricher |
| 37 | Steps.ExamplePrompts.Generation.Tests | ExamplePrompts.Generation | Steps.ExamplePrompts.Generation |
| 38 | Steps.ExamplePrompts.Validation.Tests | ExamplePrompts.Validation | Steps.ExamplePrompts.Validation |
| 39 | Steps.HorizontalArticles.Tests | HorizontalArticles | Steps.HorizontalArticles, Core.TextTransformation |
| 40 | Steps.SkillsRelevance.Tests | SkillsRelevance | Steps.SkillsRelevance |
| 41 | Steps.ToolFamilyCleanup.Tests | ToolFamilyCleanup | Steps.ToolFamilyCleanup, Core.Shared, TestInfrastructure |
| 42 | Steps.ToolFamilyCleanup.Validation.Tests | ToolFamilyCleanup (data-driven) | ⚠️ **None** (standalone, uses TestData files only) |
| 43 | Steps.ToolGeneration.Improvements.Tests | ToolGeneration.Improvements | Steps.ToolGeneration.Improvements |
| 44 | Tools.Fingerprint.Tests | Fingerprint | Tools.Fingerprint |

### Solution: `skills-generation.slnx` (3 projects)

| # | Project | Type | Purpose | NuGet Dependencies | Project References |
|---|---------|------|---------|-------------------|-------------------|
| 45 | SkillsGen.Cli | Exe | Skills generation CLI entry point | System.CommandLine, Microsoft.Extensions.Hosting, Microsoft.Extensions.Logging.Console | SkillsGen.Core |
| 46 | SkillsGen.Core | Library | Core skills generation logic, AI, templates, YAML | System.CommandLine, Microsoft.Extensions.Hosting, Microsoft.Extensions.Http, Microsoft.Extensions.Logging, Handlebars.Net, Azure.AI.OpenAI, Microsoft.Extensions.AI, Microsoft.Extensions.AI.OpenAI, YamlDotNet | — |
| 47 | SkillsGen.Core.Tests | Library | Tests for SkillsGen.Core | Microsoft.NET.Test.Sdk, xunit, xunit.runner.visualstudio, coverlet.collector, NSubstitute, FluentAssertions | SkillsGen.Core |

---

## npm Package Inventory

| # | Package | Purpose | Dependencies | Scripts | Lock File |
|---|---------|---------|-------------|---------|-----------|
| 1 | `azure-mcp-summary-generator` | Generate documentation summaries (JS port from PowerShell) | None | `start`, `summary`, `test` | ❌ |
| 2 | `test-npm-azure-mcp` | MCP CLI metadata extraction, validation | `@azure/mcp: ^2.0.0-beta.38` | `start`, `validate`, `generate:report`, `test:report`, + 8 CLI helpers | ✅ |
| 3 | `verify-quantity` | Verify completeness of generated docs | None | `verify`, `report` | ✅ |

**Notes:**
- No npm workspace configuration (no root `package.json` with `workspaces`)
- No shared npm dependencies between packages
- `summary-generator` has no lock file (should have one for reproducible builds)
- `test-npm-azure-mcp` is the only package with a runtime dependency

---

## Central Package Management Analysis

Both solution trees use `Directory.Packages.props` for centralized version management. Below is a side-by-side comparison of all packages declared:

### Shared Packages — Version Comparison

| Package | docs-generation | skills-generation | Match? |
|---------|----------------|-------------------|--------|
| System.CommandLine | 2.0.0-beta4.22272.1 | 2.0.0-beta4.22272.1 | ✅ |
| Handlebars.Net | 2.1.6 | 2.1.6 | ✅ |
| Azure.AI.OpenAI | 2.1.0 | 2.1.0 | ✅ |
| Microsoft.Extensions.AI | **10.4.0** | **9.5.0** | ❌ CONFLICT |

> ⚠️ **Breaking change warning:** The Microsoft.Extensions.AI conflict crosses a **major version boundary** (10.x vs 9.x). This likely involves breaking API changes, removed or renamed types, and behavioral differences. Resolving this requires a migration effort — not just a version bump. Consult the [Microsoft.Extensions.AI migration guide](https://learn.microsoft.com/dotnet/ai/migration-guide) before upgrading.
| Microsoft.Extensions.AI.OpenAI | **10.4.0** | **10.3.0** | ❌ CONFLICT |
| Microsoft.Extensions.Logging | **8.0.0** | **9.0.0** | ❌ CONFLICT |
| Microsoft.Extensions.Logging.Console | **8.0.0** | **9.0.0** | ❌ CONFLICT |
| Microsoft.NET.Test.Sdk | **17.6.3** | **17.12.0** | ❌ CONFLICT |
| xunit | **2.4.2** | **2.9.3** | ❌ |
| xunit.runner.visualstudio | **2.4.5** | **2.8.2** | ❌ |

### Packages Unique to docs-generation

| Package | Version | Used By |
|---------|---------|---------|
| System.Text.Json | 10.0.4 | Core.TextTransformation, Bootstrap.BrandMappings, Bootstrap.CliAnalyzer, Bootstrap.E2eTestPromptParser, Tools.Fingerprint |
| Azure.Identity | 1.19.0 | Core.GenerativeAI |
| Microsoft.CodeAnalysis.CSharp | 4.8.0 | Utilities.ToolMetadataExtractor |
| Newtonsoft.Json | 13.0.3 | Bootstrap.CliAnalyzer, Utilities.ToolMetadataExtractor, PromptRegression.Tests |

### Packages Unique to skills-generation

| Package | Version | Used By |
|---------|---------|---------|
| Microsoft.Extensions.Hosting | 9.0.0 | SkillsGen.Cli, SkillsGen.Core |
| Microsoft.Extensions.Http | 9.0.0 | SkillsGen.Core |
| YamlDotNet | 16.3.0 | SkillsGen.Core |
| coverlet.collector | 6.0.2 | SkillsGen.Core.Tests |
| NSubstitute | 5.3.0 | SkillsGen.Core.Tests |
| FluentAssertions | 7.0.0 | SkillsGen.Core.Tests |

---

## Dependency Graph

```
┌─────────────────────────────────────────────────────────────────────┐
│                     DOCS-GENERATION SOLUTION                        │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  ┌──────────────────── CORE LIBRARIES ────────────────────┐        │
│  │                                                         │        │
│  │  Core.Shared ◄──── Core.NaturalLanguage                │        │
│  │       ▲                                                 │        │
│  │       │                                                 │        │
│  │       └──── Core.GenerativeAI                           │        │
│  │                  (Azure.AI.OpenAI, Azure.Identity,      │        │
│  │                   M.E.AI, M.E.AI.OpenAI)                │        │
│  │                                                         │        │
│  │  Core.TemplateEngine (Handlebars.Net) [standalone]      │        │
│  │  Core.TextTransformation (System.Text.Json) [standalone]│        │
│  └─────────────────────────────────────────────────────────┘        │
│       ▲  ▲  ▲  ▲  ▲                                                │
│       │  │  │  │  │                                                 │
│  ┌────┴──┴──┴──┴──┴──── PIPELINE RUNNER ──────────────────┐        │
│  │                                                         │        │
│  │  PipelineRunner (Exe) ──► Core.Shared                   │        │
│  │    (System.CommandLine)                                  │        │
│  └─────────────────────────────────────────────────────────┘        │
│                                                                     │
│  ┌──────────────── BOOTSTRAP STEPS (Step 0) ──────────────┐        │
│  │                                                         │        │
│  │  Bootstrap.BrandMappings ──► Core.GenerativeAI,         │        │
│  │                               Core.Shared               │        │
│  │  Bootstrap.CliAnalyzer    [standalone, no project refs]  │        │
│  │  Bootstrap.CommandParser  [standalone, no project refs]  │        │
│  │  Bootstrap.E2eTestPromptParser [standalone, no refs]     │        │
│  │  Bootstrap.ToolMetadataEnricher [standalone, no refs]    │        │
│  └─────────────────────────────────────────────────────────┘        │
│                                                                     │
│  ┌──────────────── NAMESPACE STEPS (Steps 1-6) ───────────┐        │
│  │                                                         │        │
│  │  Step 1: Annotations ──► Core.Shared, Core.NL,          │        │
│  │                          Core.TemplateEngine,            │        │
│  │                          Steps.ToolFamilyCleanup         │        │
│  │  Step 1: RawTools ──► Core.Shared, Core.NL              │        │
│  │  Step 2: ExamplePrompts.Generation ──► Core.GenAI,       │        │
│  │                          Core.Shared, Core.TemplateEngine│        │
│  │  Step 2: ExamplePrompts.Validation ──► Core.Shared,      │        │
│  │                          Core.GenAI                      │        │
│  │  Step 3: ToolGeneration.Improvements ──► Core.Shared,    │        │
│  │                          Core.GenAI                      │        │
│  │  Step 3: ToolGeneration.Composition ──► Core.Shared      │        │
│  │  Step 4: ToolFamilyCleanup ──► Core.GenAI, Core.Shared   │        │
│  │  Step 5: SkillsRelevance ──► Core.Shared                │        │
│  │  Step 6: HorizontalArticles ──► Annotations, Core.Shared│        │
│  │           Core.GenAI, Core.NL, Core.TextTransformation,  │        │
│  │           Core.TemplateEngine                            │        │
│  └─────────────────────────────────────────────────────────┘        │
│                                                                     │
│  ┌──────────────── UTILITY PROJECTS ──────────────────────┐        │
│  │  Tools.Fingerprint       [standalone]                   │        │
│  │  Tools.PostProcessVerifier ──► Steps.ToolFamilyCleanup  │        │
│  │  Utilities.ToolMetadataExtractor ──► Core.Shared        │        │
│  └─────────────────────────────────────────────────────────┘        │
│                                                                     │
│  ┌──────────────── TEST INFRASTRUCTURE ───────────────────┐        │
│  │  TestInfrastructure [standalone shared test lib]         │        │
│  │    Referenced by: PipelineRunner.Tests,                  │        │
│  │      PromptRegression.Tests, Annotations.Tests,         │        │
│  │      BrandMappings.Tests, ToolFamilyCleanup.Tests       │        │
│  └─────────────────────────────────────────────────────────┘        │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────┐
│                    SKILLS-GENERATION SOLUTION                       │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  SkillsGen.Cli (Exe) ──► SkillsGen.Core                           │
│  SkillsGen.Core.Tests ──► SkillsGen.Core                          │
│                                                                     │
│  [No cross-references to docs-generation projects]                  │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────┐
│                       NPM PACKAGES                                  │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  summary-generator      [standalone, zero deps]                     │
│  test-npm-azure-mcp     [standalone, @azure/mcp dep]                │
│  verify-quantity         [standalone, zero deps]                     │
│                                                                     │
│  [No cross-references between npm packages]                         │
│  [No cross-references to .NET projects]                             │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
```

---

## Findings

### F-1: NuGet Version Conflicts Between Solutions

**Severity**: Medium
**Details**: 5 packages are declared at different versions in the two `Directory.Packages.props` files.

| Package | docs-generation | skills-generation | Gap |
|---------|----------------|-------------------|-----|
| Microsoft.Extensions.AI | 10.4.0 | 9.5.0 | Major (10.x vs 9.x) |
| Microsoft.Extensions.AI.OpenAI | 10.4.0 | 10.3.0 | Patch |
| Microsoft.Extensions.Logging | 8.0.0 | 9.0.0 | Major (8.x vs 9.x) |
| Microsoft.Extensions.Logging.Console | 8.0.0 | 9.0.0 | Major |
| Microsoft.NET.Test.Sdk | 17.6.3 | 17.12.0 | Minor |

Testing packages also differ: xunit 2.4.2 vs 2.9.3, xunit.runner.visualstudio 2.4.5 vs 2.8.2.

**Impact**: If these solutions are ever merged or share libraries, version conflicts will cause build failures. The docs-generation testing packages are notably outdated.

### F-2: Dual JSON Serialization Libraries

**Severity**: Low
**Details**: The docs-generation solution uses both `System.Text.Json` (5 projects) and `Newtonsoft.Json` (3 projects: Bootstrap.CliAnalyzer, Utilities.ToolMetadataExtractor, PromptRegression.Tests). Both ship to the same runtime output.

**Impact**: Increased binary size, potential serialization inconsistencies, dual mental model for developers.

### F-3: Standalone Bootstrap Executables Without Shared Infrastructure

**Severity**: Low
**Details**: Four Bootstrap step executables are completely standalone (zero project references):
- Bootstrap.CliAnalyzer
- Bootstrap.CommandParser
- Bootstrap.E2eTestPromptParser
- Bootstrap.ToolMetadataEnricher

Each independently implements JSON parsing and CLI argument handling. They don't reference `Core.Shared` despite other steps doing so.

**Impact**: Potential code duplication in JSON/CLI handling. These projects were likely written early before the core libraries existed.

### F-4: Orphan Test Project

**Severity**: Low
**Details**: `DocGeneration.Steps.ToolFamilyCleanup.Validation.Tests` has **zero ProjectReferences** — it tests only against `TestData/**` files copied to output. It doesn't reference the `ToolFamilyCleanup` project it nominally validates.

**Impact**: No functional concern — it works as intended (data-driven validation tests). The naming could be confusing since it appears associated with ToolFamilyCleanup but tests independently.

### F-5: Missing `Directory.Build.props` for docs-generation

**Severity**: Low
**Details**: The skills-generation tree uses a `Directory.Build.props` to centralize `TargetFramework`, `Nullable`, `ImplicitUsings`, and `TreatWarningsAsErrors`. The docs-generation tree (44 projects) sets `TargetFramework` per-project in each `.csproj`.

**Impact**: 44 files must be updated individually to change the target framework. Risk of inconsistency if a new project forgets to set the framework.

### F-6: No npm Workspace Configuration

**Severity**: Low
**Details**: Three npm packages exist independently with no root `package.json` workspace configuration. They have no shared dependencies and don't cross-reference each other.

**Impact**: Minimal — these are small utility scripts. A workspace would add overhead for little benefit given their independence.

### F-7: Missing Lock File for summary-generator

**Severity**: Low
**Details**: `summary-generator` has no `package-lock.json`. Although it has zero dependencies, a lock file ensures reproducible installs for any future deps and is a best practice.

### F-8: SkillsGen.Core Has High Package Density

**Severity**: Informational
**Details**: `SkillsGen.Core` references 9 NuGet packages, making it the most dependency-heavy project. It bundles HTTP client, DI/hosting, AI, templates, and YAML in a single library.

**Impact**: If the skills-generation solution grows, this project may benefit from the same Core.* decomposition used in docs-generation.

### F-9: Newtonsoft.Json Used Alongside System.Text.Json

**Severity**: Low
**Details**: `Newtonsoft.Json` is used by 3 projects (Bootstrap.CliAnalyzer, Utilities.ToolMetadataExtractor, PromptRegression.Tests) while the rest of the solution uses `System.Text.Json`. This is the older JSON library — Microsoft recommends `System.Text.Json` for new .NET projects.

### F-10: `System.CommandLine` at Pre-Release Version

**Severity**: Informational
**Details**: Both solutions reference `System.CommandLine` at `2.0.0-beta4.22272.1` — a beta from 2022. This package has been in beta for years with no stable release.

**Impact**: No immediate action needed. The API surface used is stable. Worth monitoring for a stable release.

---

## Recommendations

### R-1: Unify `Directory.Packages.props` Versions (Priority: HIGH)

**What**: Align all shared package versions between docs-generation and skills-generation.

**Action**:
- Upgrade docs-generation testing packages to match skills-generation (xunit 2.9.3, Microsoft.NET.Test.Sdk 17.12.0, etc.)
- Align `Microsoft.Extensions.AI` and `Microsoft.Extensions.AI.OpenAI` to the highest common version (10.4.0)
- Align `Microsoft.Extensions.Logging` to 9.0.0 (newer)

**Rationale**: Version alignment prevents future merge conflicts and ensures consistent behavior across the codebase. Testing package upgrades are low-risk.

**Risk**: LOW — testing package upgrades rarely break tests; M.E.AI upgrades need AI integration test validation.

### R-2: Add `Directory.Build.props` for docs-generation (Priority: MEDIUM)

**What**: Create `docs-generation/Directory.Build.props` mirroring skills-generation's pattern.

**Action**:
```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
</Project>
```
Remove `<TargetFramework>` from all 44 individual `.csproj` files.

**Rationale**: Single source of truth for framework settings. Makes future .NET upgrades a one-line change.

**Risk**: LOW — purely structural, no behavior change. Requires testing the full build after removal.

### R-3: Consolidate to `System.Text.Json` Only (Priority: MEDIUM)

**What**: Migrate the 3 `Newtonsoft.Json` usages to `System.Text.Json`.

**Projects affected**:
- `Bootstrap.CliAnalyzer` — JSON parsing
- `Utilities.ToolMetadataExtractor` — JSON serialization
- `PromptRegression.Tests` — test data parsing

**Rationale**: Standardize on the framework-recommended JSON library. `System.Text.Json` is already used by 5 other projects. Reduces dependency footprint.

**Risk**: MEDIUM — requires verifying JSON parsing behavior is equivalent (Newtonsoft.Json handles some edge cases differently than System.Text.Json). Each project should be migrated and tested independently.

### R-4: Add Project References to Standalone Bootstrap Steps (Priority: LOW)

**What**: Evaluate adding `Core.Shared` references to the 4 standalone Bootstrap executables.

**Projects**: CliAnalyzer, CommandParser, E2eTestPromptParser, ToolMetadataEnricher

**Rationale**: These projects may have duplicated utility code (file I/O helpers, path handling) that already exists in Core.Shared.

**Risk**: LOW — but requires code audit to confirm duplication exists. If these projects are intentionally self-contained for Docker layer caching or independent execution, the standalone design may be preferable.

**Recommendation**: Audit first, then decide. Mark as "investigate" before committing to refactoring.

### R-5: Consider Unified `Directory.Packages.props` (Priority: LOW)

**What**: Evaluate merging both `Directory.Packages.props` into a single root-level file.

**Rationale**: A single CPM file would eliminate version drift entirely. .NET supports hierarchical `Directory.Packages.props` — a root file with per-directory overrides is possible.

**Risk**: MEDIUM — requires both solutions to agree on all package versions simultaneously. May conflict with solution-specific build processes.

**Recommendation**: Only pursue after R-1 (version alignment) is complete. Start with a root file that declares shared versions, with solution-level overrides for unique packages.

### R-6: Generate `package-lock.json` for summary-generator (Priority: LOW)

**What**: Run `npm install` in `summary-generator/` to generate a lock file, then commit it.

**Rationale**: Best practice for reproducible builds, even with zero dependencies.

**Risk**: NEGLIGIBLE.

### R-7: Monitor SkillsGen.Core for Future Decomposition (Priority: INFORMATIONAL)

**What**: No action now. If the skills-generation solution adds more projects, consider decomposing `SkillsGen.Core` into focused libraries following the docs-generation `Core.*` pattern.

**Rationale**: With 9 NuGet packages and mixed concerns (HTTP, AI, templates, YAML, hosting), the project could become unwieldy. The docs-generation solution's decomposition (Core.Shared, Core.GenerativeAI, Core.TemplateEngine, Core.TextTransformation, Core.NaturalLanguage) is a good model.

**Risk**: None — this is a monitoring recommendation only.

---

## Implementation Order

Recommendations are ordered by priority and dependency:

| Phase | Recommendation | Effort | Risk | Depends On |
|-------|---------------|--------|------|------------|
| **Phase 1** | R-1: Unify package versions | Small (config changes) | Low | — |
| **Phase 1** | R-6: Generate summary-generator lock file | Trivial | Negligible | — |
| **Phase 2** | R-2: Add Directory.Build.props for docs-generation | Small (structural) | Low | — |
| **Phase 3** | R-3: Consolidate to System.Text.Json | Medium (3 projects) | Medium | — |
| **Phase 4** | R-4: Audit standalone Bootstrap steps | Small (investigation) | Low | — |
| **Phase 5** | R-5: Unified root Directory.Packages.props | Medium | Medium | R-1 |
| *Monitor* | R-7: SkillsGen.Core decomposition | — | — | Future growth |

**Total estimated effort**: ~3-5 developer-days across all phases (excluding R-7).

---

## Appendix A: Project Count Summary

| Category | Count |
|----------|-------|
| .NET libraries (non-test) | 7 |
| .NET executables | 17 |
| .NET test projects | 20 |
| .NET test infrastructure | 1 |
| **Total .NET projects** | **47** |
| npm packages | 3 |
| Solution files | 2 |
| Directory.Packages.props files | 2 |
| Directory.Build.props files | 1 (skills-generation only) |

## Appendix B: NuGet Package Usage Matrix

| Package | Projects Using It |
|---------|------------------|
| System.Text.Json | Core.TextTransformation, Bootstrap.BrandMappings, Bootstrap.CliAnalyzer, Bootstrap.E2eTestPromptParser, Tools.Fingerprint |
| System.CommandLine | PipelineRunner, Bootstrap.CliAnalyzer, Bootstrap.CommandParser, Bootstrap.ToolMetadataEnricher, SkillsGen.Cli, SkillsGen.Core |
| Azure.AI.OpenAI | Core.GenerativeAI, SkillsGen.Core |
| Azure.Identity | Core.GenerativeAI |
| Microsoft.Extensions.AI | Core.GenerativeAI, SkillsGen.Core |
| Microsoft.Extensions.AI.OpenAI | Core.GenerativeAI, SkillsGen.Core |
| Handlebars.Net | Core.TemplateEngine, SkillsGen.Core |
| Newtonsoft.Json | Bootstrap.CliAnalyzer, Utilities.ToolMetadataExtractor, PromptRegression.Tests |
| Microsoft.CodeAnalysis.CSharp | Utilities.ToolMetadataExtractor |
| Microsoft.Extensions.Logging | Utilities.ToolMetadataExtractor, SkillsGen.Core |
| Microsoft.Extensions.Logging.Console | Utilities.ToolMetadataExtractor, SkillsGen.Cli |
| Microsoft.Extensions.Hosting | SkillsGen.Cli, SkillsGen.Core |
| Microsoft.Extensions.Http | SkillsGen.Core |
| YamlDotNet | SkillsGen.Core |

---

> **This is a plan document. Implementation requires separate team approval.**
