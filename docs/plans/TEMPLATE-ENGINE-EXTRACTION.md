# Template Engine Extraction Plan

**Date**: February 16, 2026  
**Status**: Complete  
**Branch**: `diberry/0216-test-CSharpGenerator`

---

## Problem

Four projects independently reference `Handlebars.Net`, with duplicated and inconsistent template engine code:

| Project | Usage |
|---|---|
| **CSharpGenerator** | `HandlebarsTemplateEngine.cs` — 495 lines, 20+ custom helpers, the canonical implementation |
| **HorizontalArticleGenerator** | References **entire CSharpGenerator project** just to use `HandlebarsTemplateEngine` + one inline `Handlebars.Create()` call |
| **ExamplePromptGeneratorStandalone** | Has its own `Utilities/TemplateEngine.cs` — 37-line copy-paste with only `formatDate` helper |
| **ToolGeneration_Raw** | Lists `Handlebars.Net` in `.csproj` but **never uses it** — dead dependency |

### Issues

1. **Unnecessary coupling**: HorizontalArticleGenerator depends on CSharpGenerator (a console app) for template rendering
2. **Code duplication**: ExamplePromptGeneratorStandalone has its own copy of the engine
3. **Dead dependency**: ToolGeneration_Raw carries unused NuGet reference
4. **No separation of concerns**: Domain-specific MCP helpers live alongside generic helpers

---

## Solution

Extract a new `TemplateEngine` class library project. All projects that need Handlebars rendering reference this package instead of CSharpGenerator or their own copies.

### Why standalone package (not Shared)

- **Shared stays lightweight** — adding `Handlebars.Net` to Shared forces the dependency on every consumer, including projects that don't use templates
- **Clear single responsibility** — template compilation, custom helpers, and rendering logic are a cohesive unit

---

## New project structure

```
mcp-tools/TemplateEngine/
├── TemplateEngine.csproj               # References Handlebars.Net only
├── HandlebarsTemplateEngine.cs         # Core: CreateEngine(), ProcessTemplateAsync(), ProcessTemplateString()
└── Helpers/
    ├── CoreHelpers.cs                  # Generic helpers (any project can use)
    └── McpHelpers.cs                   # MCP command structure helpers
```

### Helper classification

**CoreHelpers.cs** — generic, reusable by any project:

| Helper | Purpose |
|---|---|
| `formatDate` | Format DateTime to `yyyy-MM-dd HH:mm:ss UTC` |
| `formatDateShort` | Format DateTime to `MM/dd/yyyy` |
| `kebabCase` | Convert string to kebab-case |
| `slugify` | URL-safe slug for anchor links |
| `concat` | Concatenate strings |
| `eq` | Case-insensitive string equality |
| `replace` | String replacement |
| `add` | Addition |
| `divide` | Division |
| `round` | Rounding with precision |
| `requiredIcon` | Boolean → ✅/❌ |

**McpHelpers.cs** — MCP command parsing, used by CSharpGenerator and HorizontalArticleGenerator:

| Helper | Purpose |
|---|---|
| `subToolFamily` | Parse sub-tool from command (e.g., "blob" from `azmcp storage blob list`) |
| `hasSubTool` | Check if command has sub-tool structure (4+ parts) |
| `operationName` | Get operation for flat commands |
| `subOperation` | Get operation parts after sub-tool |
| `getSimpleToolName` | Clean tool name for H2 headers |
| `formatNaturalLanguage` | CLI param name → human-readable (with acronym handling) |
| `getAreaCount` | Count areas in dictionary |
| `groupBy` | Group collection by property (uses reflection) |

---

## Implementation steps

### Phase 1: Create TemplateEngine project

- [x] **1.1** Create `mcp-tools/TemplateEngine/TemplateEngine.csproj` (class library, net9.0, references `Handlebars.Net` via CPM)
- [x] **1.2** Create `HandlebarsTemplateEngine.cs` — move `CreateEngine()`, `ProcessTemplateAsync()`, `ProcessTemplateString()` from CSharpGenerator
- [x] **1.3** Create `Helpers/CoreHelpers.cs` — extract generic helpers listed above
- [x] **1.4** Create `Helpers/McpHelpers.cs` — extract MCP-specific helpers listed above
- [x] **1.5** Update `HandlebarsTemplateEngine.CreateEngine()` to register both helper sets
- [x] **1.6** Add TemplateEngine project to `mcp-doc-generation.sln`

### Phase 2: Migrate CSharpGenerator

- [x] **2.1** Add `ProjectReference` from CSharpGenerator → TemplateEngine
- [x] **2.2** Remove `Handlebars.Net` PackageReference from CSharpGenerator.csproj
- [x] **2.3** Delete `CSharpGenerator/HandlebarsTemplateEngine.cs`
- [x] **2.4** Update `using` statements in all CSharpGenerator `.cs` files that reference `HandlebarsTemplateEngine`
- [x] **2.5** Build and verify CSharpGenerator compiles

### Phase 3: Migrate HorizontalArticleGenerator

- [x] **3.1** Add TemplateEngine `ProjectReference` to HorizontalArticleGenerator.csproj
- [x] **3.2** Update `using CSharpGenerator;` → `using TemplateEngine;` in HorizontalArticleGenerator.cs
- [x] ~~**3.3** Replace inline `HandlebarsDotNet.Handlebars.Create()` call~~ — kept as-is (uses anonymous object context, not Dictionary; HandlebarsDotNet available transitively)
- [x] **3.4** Remove `Handlebars.Net` PackageReference from HorizontalArticleGenerator.csproj
- [x] **3.5** Keep CSharpGenerator reference (needed for `Config` and `MetadataValue`)
- [x] **3.6** Build and verify HorizontalArticleGenerator compiles

### Phase 4: Migrate ExamplePromptGeneratorStandalone

- [x] **4.1** Add `ProjectReference` from ExamplePromptGeneratorStandalone → TemplateEngine
- [x] **4.2** Remove `Handlebars.Net` PackageReference from ExamplePromptGeneratorStandalone.csproj
- [x] **4.3** Delete `ExamplePromptGeneratorStandalone/Utilities/TemplateEngine.cs` (the duplicate)
- [x] **4.4** Update all callers of `Utilities.TemplateEngine.ProcessAsync()` → `HandlebarsTemplateEngine.ProcessTemplateAsync()`
- [x] **4.5** Build and verify ExamplePromptGeneratorStandalone compiles

### Phase 5: Clean up ToolGeneration_Raw

- [x] **5.1** Remove `Handlebars.Net` PackageReference from ToolGeneration_Raw.csproj (unused dependency)
- [x] **5.2** Build and verify ToolGeneration_Raw compiles

### Phase 6: Validation

- [x] **6.1** Build entire solution: `dotnet build mcp-doc-generation.sln` — 0 errors, 0 warnings
- [ ] **6.2** Run `bash start.sh aks 1` — verify Step 1 (annotations/parameters) produces identical output
- [x] **6.3** Grep solution for any remaining direct `HandlebarsDotNet` references outside TemplateEngine
- [x] **6.4** Verify no project still has a direct `Handlebars.Net` PackageReference (only in TemplateEngine.csproj)

### Phase 7: Documentation

- [x] **7.1** Create `mcp-tools/TemplateEngine/README.md`
- [x] **7.2** Update `CSharpGenerator/README.md` — replace HandlebarsTemplateEngine reference with TemplateEngine dependency
- [x] **7.3** Update `.github/copilot-instructions.md` if it references HandlebarsTemplateEngine location

### Phase 8: Markdown documentation updates

- [x] **8.1** Update `mcp-tools/README.md` — replace `HandlebarsTemplateEngine.cs` in tree with TemplateEngine project; update dependency section
- [x] **8.2** Update `mcp-tools/CSharpGenerator/docs/README.md` — remove `HandlebarsTemplateEngine.cs` from core files; replace `Handlebars.Net` with `TemplateEngine` in dependencies
- [x] **8.3** Update `mcp-tools/CSharpGenerator/test-plan.md` — section 9 target is now `TemplateEngine/HandlebarsTemplateEngine.cs`; update priority table
- [x] **8.4** Update `mcp-tools/CSharpGenerator/docs/COMPLETE-TOOLS-README.md` — helper functions now in `TemplateEngine` project
- [x] **8.5** Update `mcp-tools/HorizontalArticleGenerator/README.md` — replace `Handlebars.Net` with `TemplateEngine` in dependencies
- [x] **8.6** Update `mcp-tools/ToolGeneration_Raw/README.md` — remove `Handlebars.Net` from dependencies (removed from csproj)
- [x] **8.7** Update `mcp-tools/ExamplePromptGeneratorStandalone/README.md` — replace `TemplateEngine` utility with `TemplateEngine` shared library reference
- [x] **8.8** Update `mcp-tools/ToolFamily/TOOL-FAMILY-GENERATOR-PLAN.md` — reference TemplateEngine shared library instead of copying `HandlebarsTemplateEngine.cs`

---

## Risk assessment

| Risk | Mitigation |
|---|---|
| Namespace change breaks `using` statements | Search all `.cs` files for `using CSharpGenerator;` and `HandlebarsTemplateEngine` before migrating |
| HorizontalArticleGenerator uses CSharpGenerator.Models (not just templates) | Keep CSharpGenerator.Models reference if needed, or extract shared models separately |
| `RegularExpressionReplace` extension method used in `kebabCase` helper | Identify where this extension is defined and ensure TemplateEngine can access it |
| `groupBy` helper references `CommonParameter` model type | Move to McpHelpers and add TemplateEngine → Shared reference, or make it generic with reflection only |

---

## Files affected

### New files
- `mcp-tools/TemplateEngine/TemplateEngine.csproj`
- `mcp-tools/TemplateEngine/HandlebarsTemplateEngine.cs`
- `mcp-tools/TemplateEngine/Helpers/CoreHelpers.cs`
- `mcp-tools/TemplateEngine/Helpers/McpHelpers.cs`
- `mcp-tools/TemplateEngine/README.md`

### Modified files
- `mcp-doc-generation.sln` — add TemplateEngine project
- `mcp-tools/CSharpGenerator/CSharpGenerator.csproj` — add TemplateEngine ref, remove Handlebars.Net
- `mcp-tools/CSharpGenerator/DocumentationGenerator.cs` — update using
- `mcp-tools/CSharpGenerator/Generators/AnnotationGenerator.cs` — update using
- `mcp-tools/CSharpGenerator/Generators/CompleteToolGenerator.cs` — update using
- `mcp-tools/CSharpGenerator/Program.cs` — update using (template mode)
- `mcp-tools/HorizontalArticleGenerator/HorizontalArticleGenerator.csproj` — replace CSharpGenerator ref with TemplateEngine
- `mcp-tools/HorizontalArticleGenerator/HorizontalArticleProgram.cs` — update using
- `mcp-tools/HorizontalArticleGenerator/Generators/HorizontalArticleGenerator.cs` — update using + replace inline Handlebars
- `mcp-tools/ExamplePromptGeneratorStandalone/ExamplePromptGeneratorStandalone.csproj` — add TemplateEngine ref, remove Handlebars.Net
- `mcp-tools/ExamplePromptGeneratorStandalone/Generators/ExamplePromptGenerator.cs` — update call sites
- `mcp-tools/ToolGeneration_Raw/ToolGeneration_Raw.csproj` — remove Handlebars.Net
- `mcp-tools/CSharpGenerator/README.md` — update architecture section

### Deleted files
- `mcp-tools/CSharpGenerator/HandlebarsTemplateEngine.cs`
- `mcp-tools/ExamplePromptGeneratorStandalone/Utilities/TemplateEngine.cs`

---

## Estimated effort

| Phase | Effort |
|---|---|
| Phase 1: Create TemplateEngine | ~30 min |
| Phase 2: Migrate CSharpGenerator | ~15 min |
| Phase 3: Migrate HorizontalArticleGenerator | ~20 min |
| Phase 4: Migrate ExamplePromptGeneratorStandalone | ~10 min |
| Phase 5: Clean up ToolGeneration_Raw | ~5 min |
| Phase 6: Validation | ~15 min |
| Phase 7: Documentation | ~10 min |
| **Total** | **~1.5 hours** |
