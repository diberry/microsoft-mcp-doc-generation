# Core.Shared Extraction Plan

> **Issue**: #354 — Extract step-specific utilities from Core.Shared to owning projects
>
> **Status**: Plan (no code changes yet)
>
> **Date**: 2025-07-14

## Problem Statement

`DocGeneration.Core.Shared` contains 25 public types, but many are only used by one or two projects. This inflates the shared surface area, making it harder to reason about dependencies and slowing incremental builds. Types that serve a single step should live in that step's project.

## Methodology

Every caller count below is based on `grep` searches across all `.cs` files in `docs-generation/`. A **production caller** is any non-test project outside `DocGeneration.Core.Shared` itself. Test projects are tracked separately.

## Project Metadata

| Property | Value |
|---|---|
| Target framework | `net9.0` |
| NuGet dependencies | None (pure .NET runtime) |
| InternalsVisibleTo | `DocGeneration.Core.Shared.Tests`, `DocGeneration.Core.GenerativeAI.Tests` |
| Projects referencing Core.Shared | 14 production + 5 test projects |

## Type Inventory

### Legend

- **Prod callers**: Number of distinct production projects (non-test, non-Core.Shared) that reference the type
- **Classification**: "Cross-cutting" (3+ prod callers) or "Step-specific" (0–2 prod callers)

| # | Type | Purpose | Prod callers | Production caller list | Test callers | Classification |
|---|---|---|---|---|---|---|
| 1 | `BrandMapping` | Brand-to-server name mapping model with merge config | 6 | PipelineRunner, Annotations, RawTools, Bootstrap.BrandMappings, HorizontalArticles, ToolFamilyCleanup | TextTransformation.Tests, Bootstrap.BrandMappings.Tests, PipelineRunner.Tests, PromptRegression.Tests, ToolFamilyCleanup.Tests, ExamplePrompts.Generation.Tests | Cross-cutting |
| 2 | `CliVersionReader` | Reads MCP CLI version from cli-version.json | 6 | PipelineRunner, Annotations, RawTools, ExamplePrompts.Generation, HorizontalArticles, ToolFamilyCleanup | — | Cross-cutting |
| 3 | `DataFileLoader` | Cached loader for brand mappings, compound words, stop words, common parameters | 6 | PipelineRunner, Annotations, RawTools, Bootstrap.BrandMappings, HorizontalArticles, ToolFamilyCleanup | — | Cross-cutting |
| 4 | `ToolFileNameBuilder` | Deterministic filename builder for all content types | 7 | PipelineRunner, Annotations, RawTools, ExamplePrompts.Generation, ExamplePrompts.Validation, ToolGeneration.Composition, ToolFamilyCleanup | Core.Shared.Tests, PipelineRunner.Tests, Annotations.Tests, ExamplePrompts.Generation.Tests | Cross-cutting |
| 5 | `FileNameContext` | Immutable context for filename generation (brand mappings, compound words, stop words) | 4 | Annotations, ExamplePrompts.Generation, ExamplePrompts.Validation, ToolGeneration.Composition | Core.Shared.Tests, Annotations.Tests, ExamplePrompts.Generation.Tests | Cross-cutting |
| 6 | `FrontmatterUtility` | YAML frontmatter generation and stripping | 5 | Annotations (via local wrapper), RawTools, ExamplePrompts.Generation (via local wrapper), ToolGeneration.Composition, ToolFamilyCleanup | Core.Shared.Tests, Annotations.Tests, ExamplePrompts.Generation.Tests, PromptRegression.Tests | Cross-cutting |
| 7 | `LogFileHelper` | Centralized debug log writer (one file per process) | 9 | PipelineRunner (implicit via DataFileLoader), Annotations, RawTools, Bootstrap.BrandMappings, ExamplePrompts.Generation, HorizontalArticles, ToolGeneration.Composition, SkillsRelevance, Core.NaturalLanguage, Core.GenerativeAI | Core.Shared.Tests, Core.GenerativeAI.Tests | Cross-cutting |
| 8 | `PromptTokenResolver` | Resolves shared tokens (e.g. `{{ACROLINX_RULES}}`) in prompt files | 4 | ToolGeneration.Improvements, ExamplePrompts.Generation, ToolFamilyCleanup, HorizontalArticles | Core.Shared.Tests, PromptRegression.Tests, ToolFamilyCleanup.Tests | Cross-cutting |
| 9 | `DeterministicH2HeadingGenerator` | Generates unique H2 headings via verb mapping and disambiguation | 2 | PipelineRunner (BootstrapStep), ToolFamilyCleanup | Core.Shared.Tests | Step-specific |
| 10 | `ParameterCoverageChecker` | Checks whether example prompts cover required parameters with concrete values | 2 | PipelineRunner (PostAssemblyValidator), ExamplePrompts.Validation | Core.Shared.Tests | Step-specific |
| 11 | `PromptCoverage` | Result record for ParameterCoverageChecker | 2 | PipelineRunner (PostAssemblyValidator), ExamplePrompts.Validation | Core.Shared.Tests | Step-specific |
| 12 | `CommonParameterDefinition` | Model for common-parameters.json entries | 1 | Annotations | Annotations.Tests | Step-specific |
| 13 | `MappedParameter` | Model for nl-parameters.json (parameter + natural language pair) | 1 | Core.NaturalLanguage | Core.NaturalLanguage.Tests | Step-specific |
| 14 | `MergeGroupValidator` | Validates merge group config in brand-to-server-mapping.json | 0 | — | Bootstrap.BrandMappings.Tests | Step-specific |
| 15 | `AiResponseArchiveEntry` | Archived AI response model for audit trail | 0 | — | Core.Shared.Tests | Step-specific |
| 16 | `AiResponseArchiveWriter` | Writes AI response archives to disk | 0 | — | Core.Shared.Tests | Step-specific |
| 17 | `PromptHasher` | SHA256 hashing for prompt content and files | 0 | — | Core.Shared.Tests | Step-specific |
| 18 | `PromptSnapshot` | Snapshot of prompt file at a point in time | 0 | — | Core.Shared.Tests | Step-specific |
| 19 | `StepResultFile` | Structured result JSON schema (v1/v2/v3) for generator processes | 0 | — | Core.Shared.Tests | Step-specific |
| 20 | `StepResultReader` | Reads step-result.json files | 0 | — | Core.Shared.Tests | Step-specific |
| 21 | `StepResultWriter` | Writes step-result.json files | 0 | — | Core.Shared.Tests | Step-specific |
| 22 | `StepResultStatus` | Enum: Success / Failure / Partial | 0 | — | Core.Shared.Tests | Step-specific |
| 23 | `TokenUsageRecord` | Per-call Azure OpenAI token usage | 0 | — | Core.Shared.Tests | Step-specific |
| 24 | `TokenUsageSummary` | Aggregated token usage across a pipeline step | 0 | — | Core.Shared.Tests | Step-specific |
| 25 | `DisplayNameBuilder` | Builds display-friendly tool names from command strings | 0 | — | — | Dead code |

## Types to Keep in Core.Shared

These 8 types are genuinely cross-cutting (3+ production callers) and belong in the shared library:

| Type | Prod callers | Justification |
|---|---|---|
| `LogFileHelper` | 9 | Near-ubiquitous logging; used by almost every generator project and both Core libraries |
| `ToolFileNameBuilder` | 7 | Central filename contract; every step that reads or writes content files depends on it |
| `BrandMapping` | 6 | Fundamental data model threaded through pipeline config, filename generation, and merge logic |
| `CliVersionReader` | 6 | Every generator needs the MCP CLI version for frontmatter and metadata |
| `DataFileLoader` | 6 | Cached data provider for brand mappings, stop words, compound words; cross-cutting by design |
| `FrontmatterUtility` | 5 | Shared YAML frontmatter contract; multiple projects delegate to it via local wrappers |
| `FileNameContext` | 4 | Immutable parameter object for `ToolFileNameBuilder`; always co-travels with it |
| `PromptTokenResolver` | 4 | Shared token resolution used by 4 AI-dependent steps |

## Extraction Candidates

### Group A — Data Models with Single Consumer

These are simple POCO/record types used by exactly one production project. Lowest risk extraction.

#### A1. `CommonParameterDefinition` → `Steps.AnnotationsParametersRaw.Annotations`

| Attribute | Detail |
|---|---|
| Current file | `Core.Shared/CommonParameterDefinition.cs` |
| Sole prod consumer | `Steps.AnnotationsParametersRaw.Annotations` (via `DataFileLoader.LoadCommonParametersAsync()`) |
| Migration risk | **Low** — Simple data class. `DataFileLoader` returns `List<CommonParameterDefinition>`; after extraction, DataFileLoader would need to return a generic type or the Annotations project would define the model. |
| Dependency chain | `DataFileLoader.LoadCommonParametersAsync()` deserializes into this type. Either keep a shared interface or move the deserialization method too. |
| Compile breaks | `DataFileLoader.cs`, `Annotations.Tests/ModelDeserializationTests.cs` |
| Recommended approach | Move type to Annotations project. Add a generic `LoadJsonAsync<T>()` method to DataFileLoader so it doesn't depend on the model type. |

#### A2. `MappedParameter` → `Core.NaturalLanguage`

| Attribute | Detail |
|---|---|
| Current file | `Core.Shared/MappedParameter.cs` |
| Sole prod consumer | `Core.NaturalLanguage` (`TextCleanup.cs`) |
| Migration risk | **Low** — Simple 2-property class. |
| Dependency chain | `DataFileLoader.LoadParameterMappingsAsync()` returns `List<MappedParameter>`. Same pattern as A1. |
| Compile breaks | `DataFileLoader.cs`, `Core.NaturalLanguage.Tests/TextCleanupTests.cs` |
| Recommended approach | Move type to Core.NaturalLanguage. Refactor DataFileLoader to use generic deserialization. |

### Group B — Validators with Narrow Scope

#### B1. `MergeGroupValidator` → `Steps.Bootstrap.BrandMappings`

| Attribute | Detail |
|---|---|
| Current file | `Core.Shared/MergeGroupValidator.cs` |
| Prod consumers | 0 (only called from `Bootstrap.BrandMappings.Tests`) |
| Migration risk | **Low** — Static class with a `Validate()` method. Depends on `BrandMapping` (stays in Core.Shared). |
| Dependency chain | Takes `List<BrandMapping>`, returns validation errors. No reverse dependencies. |
| Compile breaks | `Bootstrap.BrandMappings.Tests/MergeGroupValidatorTests.cs` (just update namespace) |
| Recommended approach | Move to Bootstrap.BrandMappings. Tests already live there. |

#### B2. `ParameterCoverageChecker` + `PromptCoverage` → `Steps.ExamplePrompts.Validation`

| Attribute | Detail |
|---|---|
| Current files | `Core.Shared/ParameterCoverageChecker.cs` |
| Prod consumers | 2 — `ExamplePrompts.Validation`, `PipelineRunner` (PostAssemblyValidator) |
| Migration risk | **Medium** — Two consumers. PipelineRunner would need a project reference to ExamplePrompts.Validation, or the validator interface could be abstracted. |
| Dependency chain | `ParameterCoverageChecker` is self-contained (no Core.Shared dependencies). `PipelineRunner.Validation.ToolFamilyPostAssemblyValidator` calls `GetConcretePromptCoverage()`. |
| Compile breaks | `PipelineRunner/Validation/ToolFamilyPostAssemblyValidator.cs`, `Core.Shared.Tests/ParameterCoverageCheckerTests.cs` |
| Recommended approach | Move to ExamplePrompts.Validation. Add project reference from PipelineRunner → ExamplePrompts.Validation. Move tests to ExamplePrompts.Validation.Tests (or create if absent). |

### Group C — H2 Heading Generator (2 consumers)

#### C1. `DeterministicH2HeadingGenerator` → `Steps.ToolFamilyCleanup`

| Attribute | Detail |
|---|---|
| Current file | `Core.Shared/DeterministicH2HeadingGenerator.cs` (large: 21 KB) |
| Prod consumers | 2 — `PipelineRunner` (BootstrapStep, heading validation), `ToolFamilyCleanup` (primary consumer, article assembly) |
| Migration risk | **Medium** — Large file with many public static members. Two consumers would both need access. |
| Dependency chain | Self-contained; no Core.Shared type dependencies. |
| Compile breaks | `PipelineRunner/Steps/Bootstrap/BootstrapStep.cs`, `Core.Shared.Tests/DeterministicH2HeadingGeneratorTests.cs` |
| Recommended approach | Move to ToolFamilyCleanup (primary consumer). PipelineRunner already references ToolFamilyCleanup transitively or add a direct reference. Move tests to ToolFamilyCleanup.Tests. |

### Group D — Pipeline Results Infrastructure (0 consumers, cohesive cluster)

These 8 types form a self-contained "pipeline results" subsystem. None has external production callers today — they are tested infrastructure designed for future PipelineRunner integration.

| Type | File | Internal deps |
|---|---|---|
| `StepResultFile` | `StepResultFile.cs` | `StepResultStatus`, `TokenUsageSummary`, `PromptSnapshot` |
| `StepResultReader` | `StepResultReader.cs` | `StepResultFile` |
| `StepResultWriter` | `StepResultWriter.cs` | `StepResultFile`, `PromptSnapshot` |
| `StepResultStatus` | `StepResultFile.cs` | — |
| `TokenUsageRecord` | `TokenUsage.cs` | — |
| `TokenUsageSummary` | `TokenUsage.cs` | `TokenUsageRecord` |
| `PromptHasher` | `PromptHasher.cs` | `PromptSnapshot` |
| `PromptSnapshot` | `PromptHasher.cs` | — |

**Recommendation**: Extract as a new project `DocGeneration.Core.PipelineResults` or keep in Core.Shared until PipelineRunner integration activates them. Since no production code calls these types, extraction carries zero runtime risk but adds a project to the solution.

| Attribute | Detail |
|---|---|
| Migration risk | **Low** (no prod callers) but **medium effort** (8 types, 6 test files to move) |
| Compile breaks | Only `Core.Shared.Tests` — 6 test files: `StepResultFileTests`, `StepResultReaderTests`, `StepResultWriterTests`, `TokenUsageTests`, `PromptHasherTests`, `PromptVersioningIntegrationTests` |
| Recommended approach | Create `DocGeneration.Core.PipelineResults` project. Move all 8 types + tests. Core.Shared.Tests shrinks significantly. |

### Group E — AI Archival Infrastructure (0 consumers, planned)

#### E1. `AiResponseArchiveEntry` + `AiResponseArchiveWriter` → `Core.GenerativeAI`

| Attribute | Detail |
|---|---|
| Current files | `Core.Shared/AiResponseArchiveEntry.cs`, `Core.Shared/AiResponseArchiveWriter.cs` |
| Prod consumers | 0 (infrastructure added in #340, not yet wired into generators) |
| Migration risk | **Low** — No production callers. Logically belongs in Core.GenerativeAI since it archives AI responses. |
| Dependency chain | `AiResponseArchiveWriter` depends on `AiResponseArchiveEntry` and `LogFileHelper`. LogFileHelper stays in Core.Shared; Core.GenerativeAI already references Core.Shared. |
| Compile breaks | `Core.Shared.Tests/AiResponseArchiveWriterTests.cs`, `Core.Shared.Tests/AiResponseArchiveEntryTests.cs` |
| Recommended approach | Move to Core.GenerativeAI. Move tests to Core.GenerativeAI.Tests (which already has InternalsVisibleTo from Core.Shared). |

### Group F — Dead Code

#### F1. `DisplayNameBuilder` → Delete

| Attribute | Detail |
|---|---|
| Current file | `Core.Shared/DisplayNameBuilder.cs` |
| Prod consumers | 0 |
| Test consumers | 0 |
| Recommendation | Delete. No callers exist anywhere in the codebase. |

## Migration Order

Ordered from lowest risk to highest risk. Each phase is independently shippable.

| Phase | Types | Target | Risk | Effort | Test files to move |
|---|---|---|---|---|---|
| **1** | `DisplayNameBuilder` | Delete | None | Trivial | 0 |
| **2** | `MergeGroupValidator` | Bootstrap.BrandMappings | Low | Small | 0 (tests already there) |
| **3** | `MappedParameter` | Core.NaturalLanguage | Low | Small | 0 (tests already in NaturalLanguage.Tests) |
| **4** | `CommonParameterDefinition` | Annotations | Low | Small | 1 (`ModelDeserializationTests`) |
| **5** | `AiResponseArchiveEntry`, `AiResponseArchiveWriter` | Core.GenerativeAI | Low | Small | 2 (`AiResponseArchiveEntryTests`, `AiResponseArchiveWriterTests`) |
| **6** | Pipeline results cluster (8 types) | New `Core.PipelineResults` | Low | Medium | 6 test files |
| **7** | `ParameterCoverageChecker`, `PromptCoverage` | ExamplePrompts.Validation | Medium | Medium | 1 (`ParameterCoverageCheckerTests`) + PipelineRunner ref change |
| **8** | `DeterministicH2HeadingGenerator` | ToolFamilyCleanup | Medium | Medium | 1 (`DeterministicH2HeadingGeneratorTests`) + PipelineRunner ref change |

## Test Impact Summary

| Phase | Test project(s) affected | Changes needed |
|---|---|---|
| 1 | None | None |
| 2 | `Bootstrap.BrandMappings.Tests` | Update `using` namespace only |
| 3 | `Core.NaturalLanguage.Tests` | Update `using` namespace only |
| 4 | `Annotations.Tests` | Move `ModelDeserializationTests` or update namespace |
| 5 | `Core.Shared.Tests` → `Core.GenerativeAI.Tests` | Move 2 test files, update namespaces |
| 6 | `Core.Shared.Tests` → new `Core.PipelineResults.Tests` | Move 6 test files, create new test project |
| 7 | `Core.Shared.Tests` → `ExamplePrompts.Validation.Tests` | Move 1 test file; add PipelineRunner → ExamplePrompts.Validation reference |
| 8 | `Core.Shared.Tests` → `ToolFamilyCleanup.Tests` | Move 1 test file; verify PipelineRunner → ToolFamilyCleanup reference |

## DataFileLoader Refactoring Note

Phases 3 and 4 move `MappedParameter` and `CommonParameterDefinition` out of Core.Shared, but `DataFileLoader` currently deserializes into these types. Two options:

1. **Generic deserialization**: Add `LoadJsonAsync<T>(string path)` to DataFileLoader so it doesn't need to know the concrete type. Each consumer provides its own model.
2. **Move deserialization methods**: Move `LoadParameterMappingsAsync()` to Core.NaturalLanguage and `LoadCommonParametersAsync()` to Annotations. DataFileLoader keeps only cross-cutting load methods.

Option 2 is cleaner but requires careful ordering (Phase 3 before Phase 4, or do both together).

## Impact After Full Extraction

| Metric | Before | After |
|---|---|---|
| Public types in Core.Shared | 25 | 8 |
| Types classified as step-specific | 17 | 0 |
| Dead code types | 1 | 0 |
| Core.Shared.Tests test files | ~15 | ~5 |

The remaining 8 types are all genuinely cross-cutting utilities used by 4–9 production projects each.
