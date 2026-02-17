# CSharpGenerator Test Plan

This document outlines the test plan for the `CSharpGenerator` project. Tests should be placed in a sibling `CSharpGenerator.Tests` project using xUnit (consistent with existing test projects in this solution).

## Scope

Tests cover only **active** code paths. Deprecated generators have been deleted from the codebase (see [docs/UNUSED-FUNCTIONALITY.md](docs/UNUSED-FUNCTIONALITY.md)).

### Pipeline usage context

CSharpGenerator is invoked by the pipeline (`start.sh` → Step 1) with **only two flag combinations**:
1. `generate-docs <json> <dir> --annotations --version <ver>`
2. `generate-docs <json> <dir> --parameters --version <ver>`

The following flags/modes are **never passed by the pipeline** (used only by standalone/utility scripts):
- `--tool-pages`, `--index`, `--common`, `--commands` — standalone page generation
- `--example-prompts`, `--complete-tools`, `--validate-prompts` — legacy flags (no-op in current code)
- `template` mode — standalone template processing

### Active source files (as of February 2026)

**Pipeline-active (P0/P1 test priority):**
- `Config.cs` — Configuration loading and validation
- `DocumentationGenerator.cs` — Core orchestration, data transformation, common parameter extraction
- `Program.cs` — CLI entry point and argument parsing
- `Generators/AnnotationGenerator.cs` — Annotation file generation (3 params: data, outputDir, templateFile)
- `Generators/FrontmatterUtility.cs` — YAML frontmatter generation (2 active methods)
- `Generators/ParameterGenerator.cs` — Parameter file generation with common-param filtering
- `Generators/ParameterSorting.cs` — Required-first alphabetical sorting
- `Models/` — CliOutput, Tool, Option, ToolMetadata, TransformedData, AreaData, CommonParameter, MetadataValue

**Standalone-only (P2/P3 test priority — not on pipeline critical path):**
- `Generators/PageGenerator.cs` — Area, index, common, and commands page generation (requires `--index`/`--common`/`--commands` flags)

**Dead code (no callers — candidate for deletion, no tests needed):**
- `OptionsDiscovery.cs` — Source-code-based parameter discovery via regex parsing (no method is called from any code in the codebase)

---

## 1. Model deserialization

**Target**: `Models/CliOutput.cs`, `Models/Tool.cs`, `Models/Option.cs`, `Models/ToolMetadata.cs`, `Models/MetadataValue.cs`

| Test | Description |
|---|---|
| `CliOutput_Deserialize_ValidJson` | Deserializes a minimal `cli-output.json` into `CliOutput` with correct tool count |
| `CliOutput_Deserialize_EmptyResults` | Handles `{ "results": [] }` gracefully |
| `Tool_Deserialize_AllFields` | Maps command, description, area, metadata, and options |
| `Tool_Deserialize_MissingOptionalFields` | Missing metadata or description produces null/defaults, not exceptions |
| `Option_Deserialize_RequiredFlag` | `Required` parses as `true`/`false` correctly |
| `ToolMetadata_Deserialize_AllAnnotations` | Parses destructive, idempotent, openWorld, readOnly, secret, localRequired annotations |
| `MetadataValue_Deserialize_ValueAndDescription` | `Value` (bool) and `Description` (string) parse correctly |

**Test data**: Create a `TestData/` folder with minimal JSON fixtures (3–5 tools).

---

## 2. Data transformation

**Target**: `DocumentationGenerator.TransformCliOutput()` (private — test via `GenerateAsync` or extract to internal)

| Test | Description |
|---|---|
| `TransformCliOutput_GroupsByArea` | Tools are grouped by first command word into correct areas |
| `TransformCliOutput_SetsDefaultVersion` | Default version is `"1.0.0"` when none provided |
| `TransformCliOutput_OverridesVersionFromParam` | `cliVersion` parameter overrides default version |
| `TransformCliOutput_SortsParametersOnTransform` | `ParameterSorting.SortByRequiredThenName` is applied during transformation |
| `TransformCliOutput_ExtractsConditionalRequirements` | "Requires at least one" descriptions populate `ConditionalRequiredNote` and `ConditionalRequiredParameters` |
| `TransformCliOutput_SkipsEmptyCommands` | Tools with null/empty `Command` are skipped |
| `TransformCliOutput_SetsAreaOnTool` | `tool.Area` is set to the first command word |

---

## 3. AnnotationGenerator

**Target**: `Generators/AnnotationGenerator.cs`

Methods: `GenerateAnnotationFilesAsync`, `GenerateToolAnnotationsSummaryAsync`

| Test | Description |
|---|---|
| `GenerateAnnotationFiles_CreatesFilePerTool` | One `.md` file per tool in output directory |
| `GenerateAnnotationFiles_IncludesFrontmatter` | Output starts with `---` YAML block containing `ms.topic: include` |
| `GenerateAnnotationFiles_WritesAllAnnotationFlags` | Destructive, idempotent, openWorld, readOnly, secret, localRequired appear when present |
| `GenerateAnnotationFiles_OmitsMissingFlags` | Absent annotations are omitted, not rendered as empty |
| `GenerateAnnotationFiles_UsesCleanedFilename` | Filename follows brand-mapping / compound-word / stop-word resolution via `ToolFileNameBuilder` |
| `GenerateAnnotationFiles_SetsHasAnnotationFlag` | `tool.HasAnnotation` is set to `true` after generation |
| `GenerateAnnotationFiles_SkipsNullCommand` | Tools with null/empty `Command` are skipped |
| `GenerateAnnotationFiles_TracksMissingMappings` | Areas without brand mappings or compound words produce a `missing-brand-mappings.txt` report |
| `GenerateToolAnnotationsSummary_AllToolsIncluded` | Summary page lists all tools from all areas |
| `GenerateToolAnnotationsSummary_IncludesAnnotationContent` | Reads and includes annotation file content when file exists |

---

## 4. ParameterGenerator

**Target**: `Generators/ParameterGenerator.cs`

| Test | Description |
|---|---|
| `Generate_CreatesFilePerTool` | One `.md` file per tool in output directory |
| `Generate_IncludesFrontmatter` | Output starts with `---` YAML block containing `ms.topic: include` |
| `Generate_ExcludesCommonOptional` | Common optional parameters (e.g., `--tenant`) are omitted from output |
| `Generate_IncludesCommonRequired` | Common parameters marked `Required` for this tool are included |
| `Generate_NormalizesParameterNames` | `--resource-group` becomes human-readable form via `TextCleanup.NormalizeParameter` |
| `Generate_NormalizesDescriptions` | Descriptions end with a period via `TextCleanup.EnsureEndsPeriod` |
| `Generate_AppliesStaticTextReplacement` | `TextCleanup.ReplaceStaticText` is applied to descriptions |
| `Generate_SetsHasParametersFlag` | `tool.HasParameters` is set to `true` after generation |
| `Generate_SkipsNullCommand` | Tools with null/empty `Command` are skipped |
| `Generate_ConditionalRequiredText` | Parameters in `ConditionalRequiredParameters` get `"Required*"` or `"Optional*"` suffix |
| `Generate_HasConditionalRequiredFlag` | `hasConditionalRequired` is true in template data when conditional params exist |

---

## 5. PageGenerator (standalone-only — not on pipeline critical path)

**Target**: `Generators/PageGenerator.cs`

> **Note**: These methods are only reachable via `--index`, `--common`, `--commands` flags which are used by standalone/utility scripts (`Debug-MultiPageDocs.ps1`, `Generate-Commands.ps1`, `Generate-Common.ps1`, `Generate-Index.ps1`), NOT by the pipeline (`start.sh` → Step 1). Test at P2 priority.

Methods: `GenerateAreaPageAsync`, `GenerateCommonToolsPageAsync`, `GenerateIndexPageAsync`, `GenerateCommandsPageAsync`

| Test | Description |
|---|---|
| `GenerateAreaPage_FiltersCommonParameters` | Area page tool listings exclude common optional parameters |
| `GenerateAreaPage_IncludesAnnotationContent` | Area page reads annotation file content from `annotations/` directory |
| `GenerateAreaPage_NormalizesDescriptions` | Tool descriptions use `TextCleanup.EnsureEndsPeriod` and `ReplaceStaticText` |
| `GenerateCommonPage_ListsSharedParams` | Common tools page documents shared parameters from `SourceDiscoveredCommonParams` |
| `GenerateCommonPage_OutputPath` | Output goes to `common-general/common-tools.md` |
| `GenerateIndexPage_ListsAllAreas` | Index page lists every area |
| `GenerateIndexPage_OutputPath` | Output goes to `common-general/index.md` |
| `GenerateCommandsPage_IncludesCommonParams` | Commands page includes common parameters data |
| `GenerateCommandsPage_OutputPath` | Output goes to `common-general/azmcp-commands.md` |

---

## 6. FrontmatterUtility

**Target**: `Generators/FrontmatterUtility.cs`

Active methods: `GenerateAnnotationFrontmatter`, `GenerateParameterFrontmatter`

| Test | Description |
|---|---|
| `AnnotationFrontmatter_ContainsRequiredFields` | Output includes `ms.topic: include`, `ms.date`, `mcp-cli.version` |
| `AnnotationFrontmatter_IncludesIncludeComment` | Contains `# [!INCLUDE [...]` reference to annotation path |
| `AnnotationFrontmatter_IncludesAzmcpComment` | Contains `# azmcp <command>` comment |
| `AnnotationFrontmatter_UsesCurrentDate` | `ms.date` reflects current UTC date |
| `AnnotationFrontmatter_HandlesNullVersion` | Null version defaults to `"unknown"` |
| `ParameterFrontmatter_ContainsRequiredFields` | Output includes `ms.topic: include`, `ms.date`, `mcp-cli.version` |
| `ParameterFrontmatter_IncludesIncludeComment` | Contains `# [!INCLUDE [...]` reference to parameter path |
| `ParameterFrontmatter_HandlesNullVersion` | Null version defaults to `"unknown"` |
| `BothMethods_ProducesValidYaml` | Content between `---` delimiters is parsable YAML |

---

## 7. ParameterSorting

**Target**: `Generators/ParameterSorting.cs`

| Test | Description |
|---|---|
| `Sort_RequiredBeforeOptional` | All required params precede optional params |
| `Sort_AlphabeticalWithinGroup` | Within each group, params are ordered alphabetically by normalized name |
| `Sort_UsesNormalizedNameForOrdering` | Sorting uses `TextCleanup.NormalizeParameter` results, not raw `--param` names |
| `Sort_CaseInsensitive` | Alphabetical ordering is case-insensitive |
| `Sort_EmptyList` | Empty input returns empty output |
| `Sort_AllRequired` | All-required list is alphabetical by normalized name |
| `Sort_AllOptional` | All-optional list is alphabetical by normalized name |

---

## 8. OptionsDiscovery — DEAD CODE (no tests needed)

**Target**: `OptionsDiscovery.cs`

> **⚠️ DEAD CODE**: `OptionsDiscovery` has no callers anywhere in the codebase. No method from this class is invoked by any other file. This file is a candidate for deletion. Do NOT write tests for dead code.

| Test | Description |
|---|---|
| ~~`DiscoverCommonOptions_ParsesStaticClasses`~~ | ~~Dead code — no tests needed~~ |

> All tests in this section are removed. Delete `OptionsDiscovery.cs` instead of writing tests for it.

---

## 9. Config

**Target**: `Config.cs`

| Test | Description |
|---|---|
| `Load_ValidConfigJson` | Loads config, resolves file paths, sets `NLParametersPath` and `TextReplacerParametersPath` |
| `Load_MissingConfigFile` | Throws `FileNotFoundException` with descriptive message |
| `Load_MissingRequiredFile` | Throws `FileNotFoundException` when a required data file is missing |
| `Load_EmptyConfig` | Throws `InvalidDataException` for empty or null config |
| `Load_InitializesTextCleanup` | `TextCleanup.LoadFiles` is called with resolved required file paths |
| `Load_MissingNLOrTextReplacer` | Throws `InvalidDataException` when `nl-parameters.json` or `static-text-replacement.json` is absent from config |

---

## 10. Program (CLI argument parsing)

**Target**: `Program.cs`

> **Pipeline context**: The pipeline only uses `generate-docs` mode with `--annotations` and `--parameters` flags. The `template` mode and `--index`/`--common`/`--commands` flags are standalone-only.

| Test | Description |
|---|---|
| `Parse_GenerateDocsMode` | `generate-docs <json> <dir>` enters documentation generation mode |
| `Parse_TemplateMode` | `template <tmpl> <data> <out>` enters template processing mode (standalone-only) |
| `Parse_AnnotationsFlag` | `--annotations` sets the annotations boolean **(pipeline-active)** |
| `Parse_ParametersFlag` | `--parameters` is not a recognized flag (parameters always generated) |
| `Parse_VersionFlag` | `--version 1.0` captures version string **(pipeline-active)** |
| `Parse_VersionFromCliVersionJson` | Falls back to `CliVersionReader.ReadCliVersionAsync` when `--version` not provided |
| `Parse_NoArguments` | Exits with usage message and non-zero exit code |
| `Parse_UnknownMode` | Unknown mode returns exit code 1 |
| `Parse_TemplateWithAdditionalContext` | 4th argument to `template` mode is parsed as additional JSON context (standalone-only) |
| `Parse_LegacyFlags_Accepted` | `--example-prompts`, `--complete-tools`, `--validate-prompts` are accepted without error (no-op) |

---

## 11. DocumentationGenerator helper methods

**Target**: `DocumentationGenerator.cs` — public and testable helper methods

| Test | Description |
|---|---|
| `ParseCommand_TwoParts` | `"subscription list"` → `("subscription", "list")` |
| `ParseCommand_ThreeParts` | `"aks cluster get"` → `("aks cluster", "get")` |
| `ParseCommand_FourParts` | `"storage account key list"` → `("storage account key", "list")` |
| `ParseCommand_EmptyString` | Returns `("", "")` |
| `ParseCommand_SingleWord` | Returns `("", "")` for insufficient parts |
| `ExtractCommonParameters_ThresholdFiltering` | Only parameters appearing in ≥50% of tools are common |
| `ExtractCommonParameters_SortedByUsageThenName` | Results sorted by usage percentage descending, then name |
| `ConvertCamelCaseToTitleCase_Basic` | `"openWorld"` → `"Open World"` |
| `ConvertCamelCaseToTitleCase_Single` | `"secret"` → `"Secret"` |
| `ConvertCamelCaseToTitleCase_MultipleWords` | `"localRequired"` → `"Local Required"` |
| `ConvertCamelCaseToTitleCase_Empty` | Empty string returns empty string |

---

## 12. Integration tests

End-to-end tests that verify the full pipeline with fixture data.

| Test | Description |
|---|---|
| `EndToEnd_Annotations_ProducesFiles` | Run `generate-docs` with `--annotations` on fixture JSON; verify file count and frontmatter |
| `EndToEnd_Parameters_ProducesFiles` | Run `generate-docs` on fixture JSON; verify parameter files created with correct content |
| `EndToEnd_Template_RendersOutput` | Run `template` mode with fixture template + data; verify output file matches expected |
| `EndToEnd_CommonPage_Generated` | Run with `--common`; verify `common-general/common-tools.md` created |
| `EndToEnd_IndexPage_Generated` | Run with `--index`; verify `common-general/index.md` created |
| `EndToEnd_CommandsPage_Generated` | Run with `--commands`; verify `common-general/azmcp-commands.md` created |
| `EndToEnd_MissingMappingsReport` | Run with tools that have unmapped areas; verify `missing-word-choice.md` created |
| `EndToEnd_DirectoryStructure` | Verify `annotations/`, `parameters/`, `common-general/` directories created |

**Test data**: Use a minimal `cli-output.json` with 3 tools across 2 areas to keep tests fast and deterministic.

---

## Test infrastructure

- **Framework**: xUnit (consistent with `ExamplePromptValidator.Tests`, `Shared.Tests`, `GenerativeAI.Tests`, `TextTransformation.Tests`, `TemplateEngine.Tests`)
- **Assertions**: xUnit built-in assertions
- **Mocking**: Consider `NSubstitute` or direct fakes for file system operations
- **Temp directories**: Use `Path.GetTempPath()` / `Directory.CreateTempSubdirectory()` for output; clean up in `Dispose`
- **Project location**: `docs-generation/CSharpGenerator.Tests/CSharpGenerator.Tests.csproj`
- **Test data**: `docs-generation/CSharpGenerator.Tests/TestData/` directory with fixture JSON files and template files
- **Internal access**: Add `[assembly: InternalsVisibleTo("CSharpGenerator.Tests")]` to CSharpGenerator for testing private/internal methods

## Priority

| Priority | Area | Rationale |
|---|---|---|
| **P0** | ParameterGenerator, AnnotationGenerator | Core pipeline — every run uses these (Step 1) |
| **P0** | ParameterSorting | Affects output correctness (called during TransformCliOutput) |
| **P0** | Model deserialization | Foundation for everything |
| **P1** | Data transformation (common param filtering) | Complex logic, hard to debug without tests |
| **P1** | FrontmatterUtility | Every generated file depends on correct frontmatter |
| **P1** | DocumentationGenerator helpers (ParseCommand, ConvertCamelCaseToTitleCase) | Public methods used by other packages |
| **P2** | PageGenerator | Standalone-only — not on pipeline critical path (requires `--commands`/`--index`/`--common` flags) |
| **P2** | Config, Program CLI parsing | Low complexity |
| **P3** | Integration tests | Build after unit tests stabilize |
| **N/A** | ~~OptionsDiscovery~~ | **Dead code** — no callers, candidate for deletion |
