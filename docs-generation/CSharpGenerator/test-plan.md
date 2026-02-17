# CSharpGenerator Test Plan

This document outlines the test plan for the `CSharpGenerator` project. Tests should be placed in a sibling `CSharpGenerator.Tests` project using xUnit (consistent with existing test projects in this solution).

## Scope

Tests cover only **active** code paths. Deprecated generators (`CompleteToolGenerator`, `ExamplePromptGenerator`, `ToolFamilyPageGenerator`, `ParamAnnotationGenerator`) are excluded.

---

## 1. Model deserialization

**Target**: `Models/CliOutput.cs`, `Models/Tool.cs`, `Models/Option.cs`, `Models/ToolMetadata.cs`

| Test | Description |
|---|---|
| `CliOutput_Deserialize_ValidJson` | Deserializes a minimal `cli-output.json` into `CliOutput` with correct tool count |
| `CliOutput_Deserialize_EmptyResults` | Handles `{ "results": [] }` gracefully |
| `Tool_Deserialize_AllFields` | Maps command, description, area, metadata, and options |
| `Tool_Deserialize_MissingOptionalFields` | Missing metadata or description produces null/defaults, not exceptions |
| `Option_Deserialize_RequiredFlag` | `Required` parses as `true`/`false` correctly |
| `ToolMetadata_Deserialize_Annotations` | Parses destructive, idempotent, readOnly, secret, localRequired annotations |

**Test data**: Create a `TestData/` folder with minimal JSON fixtures (3–5 tools).

---

## 2. Data transformation

**Target**: `DocumentationGenerator.TransformCliOutput()`

| Test | Description |
|---|---|
| `TransformCliOutput_GroupsByArea` | Tools are grouped into correct areas |
| `TransformCliOutput_SetsVersion` | Version string propagates to `TransformedData.Version` |
| `TransformCliOutput_LoadsCommonParameters` | Common parameters loaded from fixture JSON |
| `TransformCliOutput_FiltersCommonParams` | Common optional parameters removed from tool option lists |
| `TransformCliOutput_KeepsRequiredCommonParams` | Common parameters that are `Required` for a tool are retained |

---

## 3. AnnotationGenerator

**Target**: `Generators/AnnotationGenerator.cs`

| Test | Description |
|---|---|
| `Generate_CreatesFilePerTool` | One `.md` file per tool in `annotations/` |
| `Generate_IncludesFrontmatter` | Output starts with `---` YAML block |
| `Generate_WritesAnnotationFlags` | Destructive/idempotent/readOnly/secret/localRequired appear when present |
| `Generate_OmitsMissingFlags` | Absent annotations are omitted, not rendered as empty |
| `Generate_UsesCleanedFilename` | Filename follows brand-mapping / compound-word resolution |

---

## 4. ParameterGenerator

**Target**: `Generators/ParameterGenerator.cs`

| Test | Description |
|---|---|
| `Generate_CreatesFilePerTool` | One `.md` file per tool in `parameters/` |
| `Generate_IncludesFrontmatter` | Output starts with `---` YAML block |
| `Generate_RendersMarkdownTable` | Output contains a pipe-delimited table with headers |
| `Generate_SortsRequiredFirst` | Required parameters appear before optional parameters |
| `Generate_AlphabeticalWithinGroup` | Within required/optional groups, parameters are alphabetical |
| `Generate_ExcludesCommonOptional` | Common optional parameters (e.g., `--tenant`) are omitted |
| `Generate_IncludesCommonRequired` | Common parameters marked `Required` for this tool are included |
| `Generate_NormalizesParameterNames` | `--resource-group` becomes "Resource group" in the Name column |
| `Generate_NormalizesDescriptions` | Descriptions end with a period, have sentence-case capitalization |
| `Generate_EmptyParams` | Tool with zero non-common parameters produces a file with no table rows (or skip file) |

---

## 5. PageGenerator

**Target**: `Generators/PageGenerator.cs`

| Test | Description |
|---|---|
| `GenerateAreaPage_RendersToolList` | Area page lists all tools in that area |
| `GenerateIndexPage_ListsAllAreas` | Index page lists every area with links |
| `GenerateCommonPage_ListsSharedParams` | Common tools page documents shared parameters |
| `GenerateCommandsPage_ListsAllCommands` | Commands page has one entry per tool command |
| `Generate_SkipsDisabledPages` | Pages are not generated when corresponding flag is false |

---

## 6. FrontmatterUtility

**Target**: `Generators/FrontmatterUtility.cs`

| Test | Description |
|---|---|
| `Generate_ContainsRequiredFields` | Output includes `ms.topic`, `ms.date` |
| `Generate_IncludesVersionWhenProvided` | Version metadata appears in frontmatter |
| `Generate_UsesCurrentDate` | `ms.date` reflects current date |
| `Generate_ProducesValidYaml` | Output between `---` delimiters is parsable YAML |

---

## 7. ParameterSorting

**Target**: `Generators/ParameterSorting.cs`

| Test | Description |
|---|---|
| `Sort_RequiredBeforeOptional` | All required params precede optional params |
| `Sort_AlphabeticalWithinGroup` | Within each group, params are ordered alphabetically by name |
| `Sort_EmptyList` | Empty input returns empty output |
| `Sort_AllRequired` | All-required list is alphabetical |
| `Sort_AllOptional` | All-optional list is alphabetical |

---

## 8. HandlebarsTemplateEngine

**Target**: `TemplateEngine/HandlebarsTemplateEngine.cs` (shared library)

| Test | Description |
|---|---|
| `CreateEngine_RegistersHelpers` | Engine creates without exceptions |
| `ProcessTemplateString_BasicSubstitution` | `{{name}}` replaced with data value |
| `ProcessTemplateString_EachLoop` | `{{#each items}}` iterates over array |
| `ProcessTemplateString_IfConditional` | `{{#if flag}}` renders conditionally |
| `ProcessTemplateString_MissingVariable` | Undefined variable produces empty string, not error |
| `ProcessTemplateAsync_WritesFile` | Renders template to file on disk |

---

## 9. OptionsDiscovery

**Target**: `OptionsDiscovery.cs`

| Test | Description |
|---|---|
| `DiscoverCommonOptions_ParsesValidSource` | Parses a mock `OptionDefinitions.cs` file with 3+ options |
| `DiscoverCommonOptions_ExtractsName` | Each option has correct `Name` value |
| `DiscoverCommonOptions_ExtractsDescription` | Each option has correct `Description` value |
| `DiscoverCommonOptions_ExtractsRequired` | Required flag is parsed correctly |
| `DiscoverCommonOptions_MissingFile` | Returns empty list when source file does not exist |

---

## 10. ServiceOptionsDiscovery

**Target**: `ServiceOptionsDiscovery.cs`

| Test | Description |
|---|---|
| `DiscoverServiceOptions_ParsesValidSource` | Parses a mock `ServiceOptionDefinitions.cs` file |
| `DiscoverServiceOptions_MissingFile` | Returns empty list when source file does not exist |

---

## 11. Config

**Target**: `Config.cs`

| Test | Description |
|---|---|
| `Load_ValidConfigJson` | Loads config and resolves file paths |
| `Load_MissingRequiredFile` | Throws descriptive error when referenced data file is missing |
| `Load_InitializesTextCleanup` | `TextCleanup` instance is initialized from config data |

---

## 12. Program (CLI argument parsing)

**Target**: `Program.cs`

| Test | Description |
|---|---|
| `Parse_GenerateDocsMode` | `generate-docs <json> <dir>` sets mode correctly |
| `Parse_TemplateMode` | `template <tmpl> <data> <out>` sets mode correctly |
| `Parse_AnnotationsFlag` | `--annotations` sets the annotations boolean |
| `Parse_ParametersFlag` | `--parameters` sets the parameters boolean |
| `Parse_VersionFlag` | `--version 1.0` captures version string |
| `Parse_NoArguments` | Exits with usage message and non-zero exit code |
| `Parse_UnknownFlag` | Unknown flags are ignored or produce a warning |

---

## 13. Integration tests

End-to-end tests that verify the full pipeline with fixture data.

| Test | Description |
|---|---|
| `EndToEnd_Annotations_ProducesFiles` | Run `generate-docs` with `--annotations` on fixture JSON; verify file count and content |
| `EndToEnd_Parameters_ProducesFiles` | Run `generate-docs` with `--parameters` on fixture JSON; verify file count and content |
| `EndToEnd_Template_RendersOutput` | Run `template` mode with fixture template + data; verify output file matches expected |

**Test data**: Use a minimal `cli-output.json` with 3 tools across 2 areas to keep tests fast and deterministic.

---

## Test infrastructure

- **Framework**: xUnit (consistent with `ExamplePromptValidator.Tests`, `Shared.Tests`, `GenerativeAI.Tests`, `TextTransformation.Tests`)
- **Assertions**: `FluentAssertions` (if already used in solution) or xUnit built-in
- **Mocking**: Consider `NSubstitute` or direct fakes for file system operations
- **Project location**: `docs-generation/CSharpGenerator.Tests/CSharpGenerator.Tests.csproj`
- **Test data**: `docs-generation/CSharpGenerator.Tests/TestData/` directory with fixture JSON files and template files

## Priority

| Priority | Area | Rationale |
|---|---|---|
| **P0** | ParameterGenerator, AnnotationGenerator | Core pipeline — every run uses these |
| **P0** | ParameterSorting | Affects output correctness |
| **P0** | Model deserialization | Foundation for everything |
| **P1** | Data transformation (common param filtering) | Complex logic, hard to debug without tests |
| **P1** | FrontmatterUtility | Every file depends on correct frontmatter |
| **P1** | HandlebarsTemplateEngine (TemplateEngine project) | Template rendering correctness |
| **P2** | PageGenerator | Currently not called by pipeline |
| **P2** | OptionsDiscovery, ServiceOptionsDiscovery | Regex-based parsing, fragile to upstream changes |
| **P2** | Config, Program CLI parsing | Low complexity |

| **P3** | Integration tests | Build after unit tests stabilize |
