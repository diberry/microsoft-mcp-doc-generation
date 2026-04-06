# NaturalLanguage → TextTransformation Migration Plan

> **Issue:** #351  
> **Status:** Draft  
> **Last updated:** 2025-07-23

## Table of Contents

- [Current State](#current-state)
- [Overlap Matrix](#overlap-matrix)
- [Caller Audit](#caller-audit)
- [Config File Mapping](#config-file-mapping)
- [Migration Plan](#migration-plan)
- [Risk Assessment](#risk-assessment)
- [Test Strategy](#test-strategy)

---

## Current State

The documentation pipeline contains **two parallel text-transformation systems** that evolved independently. Both perform parameter normalization, static-text replacement, and period enforcement — but they use different architectures, namespaces, and configuration models.

### System A: DocGeneration.Core.NaturalLanguage

**Namespace:** `NaturalLanguageGenerator`  
**Architecture:** Single static class (`TextCleanup`) with global mutable state  
**Test framework:** xUnit  
**Pipeline step:** Step 1 (AnnotationsParametersRaw)

```
┌──────────────────────────────────────────────────────┐
│               TextCleanup (static class)             │
│──────────────────────────────────────────────────────│
│ Static State:                                        │
│   mappedParametersDict : Dictionary<string,string>   │
│   parameterIdentifiersDict : Dictionary<string,str>  │
│   replacerRegex : Regex (precompiled)                │
│──────────────────────────────────────────────────────│
│ Public Methods:                                      │
│   LoadFiles(List<string>)           → bool           │
│   NormalizeParameter(string)        → string         │
│   ReplaceStaticText(string)         → string         │
│   EnsureEndsPeriod(string)          → string         │
│   WrapExampleValues(string)         → string         │
│   CleanAIGeneratedText(string)      → string         │
│──────────────────────────────────────────────────────│
│ Private Helpers:                                     │
│   SplitAndTransformProgrammaticName(string)          │
│   TransformAcronyms(string)                          │
│   IsAcronym(string)                                  │
└──────────────────────────────────────────────────────┘
         │ Reads at startup
         ▼
  ┌─────────────────┐  ┌──────────────────────────┐  ┌──────────────────────────────┐
  │ nl-parameters    │  │ static-text-replacement   │  │ nl-parameter-identifiers      │
  │ .json            │  │ .json                     │  │ .json (auto-discovered)       │
  └─────────────────┘  └──────────────────────────┘  └──────────────────────────────┘
```

**Key characteristics:**
- **Static global state** — dictionaries and regex compiled once via `LoadFiles()`, shared process-wide
- **Hardcoded acronym list** — 27 acronyms in a switch statement inside `TransformAcronyms()`
- **MappedParameter model** — shared with `DocGeneration.Core.Shared`, format: `{ "Parameter": "...", "NaturalLanguage": "..." }`
- **Regex-based replacement** — precompiled multi-key regex ordered by key length (longest first) with word-boundary lookarounds

### System B: DocGeneration.Core.TextTransformation

**Namespace:** `Azure.Mcp.TextTransformation`  
**Architecture:** Instance-based with DI-friendly design (4 classes)  
**Test framework:** NUnit  
**Pipeline step:** Step 6 (HorizontalArticles)

```
┌──────────────────────────────────────────────────────────────────────┐
│                      TransformationEngine                            │
│──────────────────────────────────────────────────────────────────────│
│ Properties:                                                          │
│   TextNormalizer : TextNormalizer                                     │
│   FilenameGenerator : FilenameGenerator                              │
│   Config : TransformationConfig                                      │
│──────────────────────────────────────────────────────────────────────│
│ Public Methods:                                                      │
│   GetServiceDisplayName(string mcpName)    → string                  │
│   GetServiceShortName(string mcpName)      → string                  │
│   TransformDescription(string)             → string (+ period)       │
│   TransformText(string)                    → string (no period)      │
└────────────────┬─────────────────────────────┬───────────────────────┘
                 │                             │
    ┌────────────▼────────────┐   ┌────────────▼────────────────┐
    │     TextNormalizer      │   │     FilenameGenerator        │
    │─────────────────────────│   │─────────────────────────────│
    │ NormalizeParameter()    │   │ GenerateFilename()           │
    │ SplitAndTransform...()  │   │ CleanFilename()              │
    │ ToTitleCase()           │   │ GenerateMainServiceFilename()│
    │ ReplaceStaticText()     │   └─────────────────────────────┘
    │ EnsureEndsPeriod()      │
    └─────────────────────────┘
                 │
    ┌────────────▼────────────┐
    │     ConfigLoader        │
    │─────────────────────────│
    │ LoadAsync()             │
    │ ResolveReferences()     │   Reads → transformation-config.json
    └─────────────────────────┘
                 │
    ┌────────────▼──────────────────────────────┐
    │        TransformationConfig (model)        │
    │───────────────────────────────────────────│
    │ Lexicon (Acronyms, CompoundWords,         │
    │          StopWords, Abbreviations,         │
    │          AzureTerms)                       │
    │ Services (Mappings: ServiceMapping[])      │
    │ Parameters (Mappings: ParameterMapping[])  │
    │ Contexts (Rules per context)               │
    │ CategoryDefaults                           │
    └───────────────────────────────────────────┘
```

**Key characteristics:**
- **Instance-based** — all state held in object instances, no global mutable state
- **Data-driven acronyms** — acronyms defined in `transformation-config.json`, not hardcoded
- **Unified config model** — single JSON file with structured lexicon, service mappings, parameters, and context rules
- **`$ref` resolution** — config supports `$lexicon.acronyms.aks` style references
- **Extra capabilities** — `ToTitleCase()`, `FilenameGenerator`, `GetServiceDisplayName/ShortName()`

---

## Overlap Matrix

| Capability | NaturalLanguage (`TextCleanup`) | TextTransformation (`TextNormalizer`) | Equivalence |
|---|---|---|---|
| **Parameter normalization** | `NormalizeParameter(string)` | `NormalizeParameter(string)` | ✅ Functional equivalent — both split hyphenated names, apply acronym rules, capitalize |
| **Static text replacement** | `ReplaceStaticText(string)` | `ReplaceStaticText(string)` | ✅ Functional equivalent — both use word-boundary regex, case-insensitive |
| **Period enforcement** | `EnsureEndsPeriod(string)` | `EnsureEndsPeriod(string)` | ✅ Functional equivalent — both check `.` `!` `?` endings |
| **Acronym handling** | `TransformAcronyms()` (hardcoded switch, 27 entries) | `TransformWord()` (data-driven from Lexicon) | ⚠️ Logic equivalent, different data source |
| **Programmatic name splitting** | `SplitAndTransformProgrammaticName()` (splits on hyphens) | `SplitAndTransformProgrammaticName()` + `SplitCamelCase()` | ⚠️ TextTransformation also handles camelCase |
| **Example value wrapping** | `WrapExampleValues(string)` | ❌ No equivalent | 🔴 Must be ported to TextTransformation |
| **AI text cleanup** | `CleanAIGeneratedText(string)` | ❌ No equivalent | 🔴 Must be ported to TextTransformation |
| **Config loading** | `LoadFiles(List<string>)` (static, file-pattern search) | `ConfigLoader.LoadAsync()` (instance, explicit path) | ⚠️ Different pattern — TextTransformation is cleaner |
| **Title case conversion** | ❌ Not available | `ToTitleCase(string, string)` | 🟢 TextTransformation-only capability |
| **Filename generation** | ❌ Not available | `FilenameGenerator.GenerateFilename()` | 🟢 TextTransformation-only capability |
| **Service name lookup** | ❌ Not available | `GetServiceDisplayName()`, `GetServiceShortName()` | 🟢 TextTransformation-only capability |
| **Identifier priority** | `parameterIdentifiersDict` checked before `mappedParametersDict` | ❌ Single parameter mapping layer | 🔴 Must be ported — identifier priority is critical (Issue #270) |
| **Combined description transform** | Caller chains: `EnsureEndsPeriod(ReplaceStaticText(...))` | `TransformDescription()` (combines both) | ✅ TextTransformation has convenience method |
| **Text-only transform** | Caller calls `ReplaceStaticText()` alone | `TransformText()` (replacements only, no period) | ✅ TextTransformation has convenience method |

### Summary

- **5 methods** have direct functional equivalents
- **2 methods** (`WrapExampleValues`, `CleanAIGeneratedText`) must be ported to TextTransformation
- **1 feature** (identifier priority from `nl-parameter-identifiers.json`) must be ported
- **3 capabilities** exist only in TextTransformation (title case, filename gen, service name lookup)

---

## Caller Audit

### NaturalLanguage Callers (Production Code)

| File | Pipeline Step | Methods Called | Call Count |
|---|---|---|---|
| `Annotations/Config.cs:79` | Step 1 | `LoadFiles()` | 1 |
| `Annotations/DocumentationGenerator.cs:409,414` | Step 1 | `EnsureEndsPeriod()`, `ReplaceStaticText()`, `NormalizeParameter()` | 3 |
| `Annotations/OptionsDiscovery.cs:61,92,97,114,119,228,271` | Step 1 | `NormalizeParameter()`, `EnsureEndsPeriod()`, `ReplaceStaticText()` | 7 |
| `Annotations/Generators/ParameterGenerator.cs:134,139,140` | Step 1 | `NormalizeParameter()`, `WrapExampleValues()`, `EnsureEndsPeriod()`, `ReplaceStaticText()` | 4 |
| `Annotations/Generators/PageGenerator.cs:66,106,111,112` | Step 1 | `EnsureEndsPeriod()`, `ReplaceStaticText()`, `NormalizeParameter()`, `WrapExampleValues()` | 5 |
| `Annotations/Generators/ParameterSorting.cs:27` | Step 1 | `NormalizeParameter()` | 1 |
| `Annotations/Generators/AnnotationGenerator.cs` | Step 1 | *(imports namespace but no direct calls found)* | 0 |
| `RawTools/Services/RawToolGeneratorService.cs` | Step 1 | *(imports namespace but no direct calls found)* | 0 |

**Total production call sites: 21** (all in Step 1)

### NaturalLanguage Callers (Test Code)

| File | Methods Tested |
|---|---|
| `Core.NaturalLanguage.Tests/TextCleanupTests.cs` | All 6 public methods |
| `Annotations.Tests/NormalizeParameterTests.cs` | `NormalizeParameter()` (50+ cases) |
| `Annotations.Tests/StaticTextReplacementTests.cs` | `ReplaceStaticText()` |
| `Annotations.Tests/WrapExampleValuesTests.cs` | `WrapExampleValues()` (20+ cases) |
| `Annotations.Tests/TextCleanupFixture.cs` | `LoadFiles()` (test setup) |
| `Annotations.Tests/StaticStateCollection.cs` | Static state management |

### TextTransformation Callers (Production Code)

| File | Pipeline Step | Classes/Methods Used | Call Count |
|---|---|---|---|
| `HorizontalArticles/HorizontalArticleProgram.cs:107,113` | Step 6 | `ConfigLoader.LoadAsync()`, `new TransformationEngine()` | 2 |
| `HorizontalArticles/Generators/HorizontalArticleGenerator.cs:40,121,124` | Step 6 | `ConfigLoader`, `TransformationEngine`, `TransformText()`, `TransformDescription()` | 3 |
| `HorizontalArticles/Generators/ArticleContentProcessor.cs:5,21,23` | Step 6 | `TransformationEngine` (field + constructor param) | 2 |

**Total production call sites: 7** (all in Step 6)

### TextTransformation Callers (Test Code)

| File | Classes Tested |
|---|---|
| `Core.TextTransformation.Tests/TransformationEngineTests.cs` | `TransformationEngine`, `TextNormalizer` |
| `Core.TextTransformation.Tests/ConfigLoaderTests.cs` | `ConfigLoader` |
| `Core.TextTransformation.Tests/FilenameGeneratorTests.cs` | `FilenameGenerator` |
| `HorizontalArticles.Tests/ArticleContentProcessorTransformationTests.cs` | `TransformationEngine` integration |

---

## Config File Mapping

### Current Config Files

| File | Used By | Format | Content |
|---|---|---|---|
| `data/nl-parameters.json` | NaturalLanguage | `MappedParameter[]` | Parameter → natural language mappings |
| `data/static-text-replacement.json` | NaturalLanguage | `MappedParameter[]` | Static text → replacement mappings |
| `data/nl-parameter-identifiers.json` | NaturalLanguage | `MappedParameter[]` | Resource type → "{Type} name" mappings |
| `data/transformation-config.json` | TextTransformation | `TransformationConfig` | Unified lexicon, services, parameters, contexts |
| `data/compound-words.json` | TextTransformation (via config) | `Dict<string,string>` | Concatenated → hyphenated words |
| `data/stop-words.json` | TextTransformation (via config) | `string[]` | Words removed from filenames/titles |
| `data/brand-to-server-mapping.json` | TextTransformation (via config) | Service mapping array | Brand names, filenames, short names |

### Proposed Consolidation

After migration, configuration should consolidate into `transformation-config.json`:

| Current Source | Target Location in `transformation-config.json` | Migration Action |
|---|---|---|
| `nl-parameters.json` | `parameters.mappings[]` | Merge entries into `ParameterMapping` format |
| `static-text-replacement.json` | `lexicon.abbreviations` | Convert to `AbbreviationEntry` format |
| `nl-parameter-identifiers.json` | `parameters.identifiers[]` (new section) | Add new section to schema for identifier-priority mappings |
| `compound-words.json` | `lexicon.compoundWords` | Already present |
| `stop-words.json` | `lexicon.stopWords` | Already present |
| `brand-to-server-mapping.json` | `services.mappings[]` | Already partially present |

**Note:** The three NaturalLanguage JSON files (`nl-parameters.json`, `static-text-replacement.json`, `nl-parameter-identifiers.json`) all use the `MappedParameter` format (`{ "Parameter": "...", "NaturalLanguage": "..." }`). TextTransformation uses typed models (`ParameterMapping`, `AbbreviationEntry`, etc.). Config migration requires format conversion.

---

## Migration Plan

### Phase 1: Adapter Layer

**Goal:** NaturalLanguage delegates to TextTransformation internally — zero caller changes, zero behavior changes.

**Duration estimate:** 1–2 PRs

#### Steps

1. **Port missing methods to TextTransformation**
   - Add `WrapExampleValues(string)` to `TextNormalizer`
   - Add `CleanAIGeneratedText(string)` to `TextNormalizer`
   - Add identifier-priority support to `TextNormalizer.NormalizeParameter()` (new `IdentifierMapping` config section)

2. **Add NaturalLanguage config loading to TextTransformation**
   - Extend `ConfigLoader` to accept `nl-parameters.json`, `static-text-replacement.json`, and `nl-parameter-identifiers.json` as supplementary sources
   - Map `MappedParameter[]` format into `TransformationConfig` model at load time

3. **Rewrite `TextCleanup` as a thin adapter**
   - `TextCleanup.LoadFiles()` → creates a `ConfigLoader`, calls `LoadAsync()`, stores a static `TransformationEngine` instance
   - `TextCleanup.NormalizeParameter()` → delegates to `_engine.TextNormalizer.NormalizeParameter()`
   - `TextCleanup.ReplaceStaticText()` → delegates to `_engine.TextNormalizer.ReplaceStaticText()`
   - `TextCleanup.EnsureEndsPeriod()` → delegates to `_engine.TextNormalizer.EnsureEndsPeriod()`
   - `TextCleanup.WrapExampleValues()` → delegates to `_engine.TextNormalizer.WrapExampleValues()`
   - `TextCleanup.CleanAIGeneratedText()` → delegates to `_engine.TextNormalizer.CleanAIGeneratedText()`
   - Maintain identical public API signatures

4. **Add project reference**
   - `DocGeneration.Core.NaturalLanguage.csproj` → add `<ProjectReference>` to `DocGeneration.Core.TextTransformation.csproj`

5. **Verify all existing tests pass unchanged**

#### Phase 1 exit criteria
- All existing NaturalLanguage tests pass without modification
- All existing TextTransformation tests pass without modification
- All existing Annotations tests pass without modification
- Full pipeline run (`./start.sh advisor 1`) produces identical output

---

### Phase 2: Caller Migration

**Goal:** Each calling file switches from `TextCleanup` (static) to `TransformationEngine` (instance) — one project at a time.

**Duration estimate:** 3–5 PRs (one per logical group)

#### PR 2a: Config.cs + DocumentationGenerator.cs

1. In `Config.cs`: replace `TextCleanup.LoadFiles()` with `ConfigLoader` + `TransformationEngine` instantiation
2. Pass `TransformationEngine` instance through to `DocumentationGenerator`
3. In `DocumentationGenerator.cs`: replace `TextCleanup.*()` calls with `engine.*()` calls
4. Remove `using NaturalLanguageGenerator;`

#### PR 2b: OptionsDiscovery.cs

1. Accept `TransformationEngine` via constructor or method parameter
2. Replace all 7 `TextCleanup.*()` call sites
3. Remove `using NaturalLanguageGenerator;`

#### PR 2c: Generators (ParameterGenerator, PageGenerator, ParameterSorting)

1. Accept `TransformationEngine` via constructor or method parameter
2. Replace all `TextCleanup.*()` calls in:
   - `ParameterGenerator.cs` (4 call sites)
   - `PageGenerator.cs` (5 call sites)
   - `ParameterSorting.cs` (1 call site)
3. Remove `using NaturalLanguageGenerator;`

#### PR 2d: AnnotationGenerator.cs + RawToolGeneratorService.cs

1. Remove unused `using NaturalLanguageGenerator;` imports (no actual calls found)

#### PR 2e: Migrate tests

1. Migrate `NormalizeParameterTests.cs` → test against `TextNormalizer.NormalizeParameter()`
2. Migrate `StaticTextReplacementTests.cs` → test against `TextNormalizer.ReplaceStaticText()`
3. Migrate `WrapExampleValuesTests.cs` → test against `TextNormalizer.WrapExampleValues()`
4. Migrate `TextCleanupTests.cs` → test against `TextNormalizer` (or keep as adapter-level regression tests)
5. Remove `TextCleanupFixture.cs` and `StaticStateCollection.cs`
6. **Note:** NaturalLanguage tests use xUnit; TextTransformation tests use NUnit. Migrated tests should use NUnit to match the target project's convention.

#### Phase 2 exit criteria
- Zero files import `using NaturalLanguageGenerator;`
- All tests pass
- Full pipeline run (`./start.sh advisor`) produces identical output to pre-migration baseline

---

### Phase 3: Legacy Removal

**Goal:** Delete the NaturalLanguage project and its dedicated config files.

**Duration estimate:** 1 PR

#### Steps

1. **Delete project files:**
   - `DocGeneration.Core.NaturalLanguage/` (entire directory)
   - `DocGeneration.Core.NaturalLanguage.Tests/` (entire directory)

2. **Remove from solution:**
   - Remove project entries from `docs-generation.sln`

3. **Remove project references:**
   - Remove `<ProjectReference>` to NaturalLanguage from all `.csproj` files

4. **Consolidate config files** (if not done in Phase 2):
   - Merge `nl-parameters.json` entries into `transformation-config.json` → `parameters.mappings`
   - Merge `static-text-replacement.json` entries into `transformation-config.json` → `lexicon.abbreviations`
   - Merge `nl-parameter-identifiers.json` entries into `transformation-config.json` → `parameters.identifiers`
   - Delete the three legacy JSON files
   - Update any scripts or configs that reference the old file paths

5. **Update documentation:**
   - Remove NaturalLanguage references from `docs/ARCHITECTURE.md`
   - Remove references from `.github/copilot-instructions.md`
   - Update `docs/PROJECT-GUIDE.md`

6. **Clean up `DocGeneration.Core.Shared`:**
   - If `MappedParameter` is no longer used, remove it
   - If `LogFileHelper` is only used by NaturalLanguage, evaluate removal

#### Phase 3 exit criteria
- No references to `NaturalLanguageGenerator` namespace in codebase
- No references to `TextCleanup` class in codebase
- Solution builds cleanly
- All tests pass
- Full pipeline run produces identical output to pre-migration baseline

---

## Risk Assessment

### Phase 1 Risks

| Risk | Severity | Likelihood | Mitigation |
|---|---|---|---|
| **Behavioral divergence in NormalizeParameter** — hardcoded acronym switch (NL) vs. data-driven lexicon (TT) may produce different output for edge cases | High | Medium | Before wiring adapter, run both implementations side-by-side on all 208 tools and diff outputs. Port any missing acronyms to `transformation-config.json`. |
| **Regex boundary differences** — NL uses lookaround `(?<![A-Za-z0-9_-])`, TT may use different word-boundary pattern | Medium | Medium | Extract both regex patterns and verify identical behavior on known edge cases (partial matches, hyphenated words). |
| **Identifier priority not in TT** — NL checks `parameterIdentifiersDict` before `mappedParametersDict` (Issue #270). TT has single mapping layer. | High | Certain | Must implement identifier-priority in `TextNormalizer` before adapter can delegate. |
| **Static state initialization order** — NL loads files eagerly via `Config.Load()`. TT uses async `ConfigLoader.LoadAsync()`. Mixing may cause race conditions. | Medium | Low | Adapter wraps async load in synchronous call (`Task.Run(...).GetAwaiter().GetResult()`). Risky but contained to adapter. |

### Phase 2 Risks

| Risk | Severity | Likelihood | Mitigation |
|---|---|---|---|
| **Test framework mismatch** — NL tests use xUnit, TT tests use NUnit. Migrating tests requires framework conversion. | Low | Certain | Migrate one test class at a time. Both frameworks coexist in the solution already. |
| **Constructor injection ripple** — Passing `TransformationEngine` through constructors may require changes to callers' callers. | Medium | Medium | Trace full call graph before each PR. Use optional parameters with null defaults during transition. |
| **Output drift during incremental migration** — If Phase 2 spans weeks, main branch changes may introduce new TextCleanup calls. | Low | Medium | Add a Roslyn analyzer or CI check that warns on new `TextCleanup` usage. |

### Phase 3 Risks

| Risk | Severity | Likelihood | Mitigation |
|---|---|---|---|
| **Config consolidation data loss** — Merging three JSON files into one may drop entries or change semantics. | Medium | Low | Script the merge; diff entry counts before and after. Add a validation test that asserts all original keys are present in consolidated config. |
| **Broken external references** — Scripts, CI, or docs may reference deleted file paths. | Low | Medium | Grep entire repo for `NaturalLanguage`, `TextCleanup`, `nl-parameters.json`, etc. before deleting. |
| **MappedParameter orphans** — `DocGeneration.Core.Shared` may still export `MappedParameter` type used elsewhere. | Low | Low | Search for all `MappedParameter` usage before removal. |

---

## Test Strategy

### Baseline Capture (Before Any Migration)

1. **Capture golden output:**
   ```bash
   ./start.sh advisor 1   # Generate Step 1 output for a representative namespace
   ```
   Save `generated-advisor/` as baseline for diff comparison.

2. **Run all tests:**
   ```bash
   dotnet test docs-generation/docs-generation.sln
   ```
   Record pass/fail counts as baseline.

### Phase Gate: Phase 1 → Phase 2

| Verification | Command | Pass Criteria |
|---|---|---|
| Unit tests | `dotnet test docs-generation/docs-generation.sln` | Same pass count as baseline, zero new failures |
| NaturalLanguage tests | `dotnet test DocGeneration.Core.NaturalLanguage.Tests` | All pass (adapter is transparent) |
| TextTransformation tests | `dotnet test DocGeneration.Core.TextTransformation.Tests` | All pass (new methods have tests) |
| Output comparison | `./start.sh advisor 1` then `diff -r` against baseline | Zero diff in generated markdown |
| Side-by-side validation | Run both systems on all 208 tools, compare outputs | Zero divergence |

### Phase Gate: Phase 2 → Phase 3

| Verification | Command | Pass Criteria |
|---|---|---|
| Unit tests | `dotnet test docs-generation/docs-generation.sln` | All pass |
| No legacy imports | `grep -r "NaturalLanguageGenerator" --include="*.cs" docs-generation/` | Zero matches (except adapter if still present) |
| Output comparison | `./start.sh advisor 1` then `diff -r` against Phase 1 baseline | Zero diff |
| Full pipeline | `./start.sh advisor` (all steps) | Succeeds with identical output |

### Phase Gate: Phase 3 → Done

| Verification | Command | Pass Criteria |
|---|---|---|
| Solution builds | `dotnet build docs-generation/docs-generation.sln` | Zero errors |
| All tests | `dotnet test docs-generation/docs-generation.sln` | All pass |
| No orphan references | `grep -r "TextCleanup\|NaturalLanguage\|nl-parameters" --include="*.cs" --include="*.csproj" --include="*.json" docs-generation/` | Zero matches |
| Output comparison | `./start.sh advisor` then `diff -r` against Phase 2 baseline | Zero diff |
| Full catalog spot-check | `./start.sh storage 1` and `./start.sh compute 1` | Succeeds with reasonable output |

### Continuous Validation

During the entire migration, every PR must:

1. Pass all existing tests (`dotnet test`)
2. Produce a `diff -r` against the previous phase baseline showing zero changes in generated output
3. Include at least one test that would fail if the migration introduced a behavioral regression

---

## Appendix: Method Migration Reference

Quick lookup for developers migrating individual call sites:

| Old Call (NaturalLanguage) | New Call (TextTransformation) |
|---|---|
| `TextCleanup.LoadFiles(files)` | `var loader = new ConfigLoader(path); var config = await loader.LoadAsync(); var engine = new TransformationEngine(config);` |
| `TextCleanup.NormalizeParameter(name)` | `engine.TextNormalizer.NormalizeParameter(name)` |
| `TextCleanup.ReplaceStaticText(text)` | `engine.TextNormalizer.ReplaceStaticText(text)` |
| `TextCleanup.EnsureEndsPeriod(text)` | `engine.TextNormalizer.EnsureEndsPeriod(text)` |
| `TextCleanup.EnsureEndsPeriod(TextCleanup.ReplaceStaticText(text))` | `engine.TransformDescription(text)` |
| `TextCleanup.ReplaceStaticText(text)` (without period) | `engine.TransformText(text)` |
| `TextCleanup.WrapExampleValues(text)` | `engine.TextNormalizer.WrapExampleValues(text)` *(after Phase 1 port)* |
| `TextCleanup.CleanAIGeneratedText(text)` | `engine.TextNormalizer.CleanAIGeneratedText(text)` *(after Phase 1 port)* |
