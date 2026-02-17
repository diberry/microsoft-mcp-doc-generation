# Unused Functionality in CSharpGenerator

This document identifies functionality in the `CSharpGenerator` project that has been superseded by newer, dedicated .NET packages. The functionality remains in CSharpGenerator but is no longer actively used by the generation pipeline.

**Analysis Date**: February 9, 2026  
**Analysis Method**: Cross-referenced all PowerShell orchestration scripts (`.ps1` files in `/docs-generation/` and `/docs-generation/scripts/`) against CSharpGenerator capabilities and compared with standalone project usage.

---

## Summary

The `CSharpGenerator` project was originally a monolithic generator handling all documentation tasks. Over time, specific functionality has been extracted into dedicated packages for better maintainability and modularity. However, the original implementations remain in CSharpGenerator.

**Superseded Components**:
1. Example Prompt Generation (moved to `ExamplePromptGeneratorStandalone`)
2. Tool Generation/Composition (moved to `ToolGeneration_Raw`, `ToolGeneration_Composed`, `ToolGeneration_Improved`)
3. Complete Tool Generation (moved to `ToolGeneration_Composed`)
4. Tool Family Assembly (moved to `ToolFamilyCleanup`)
5. Horizontal Article Generation (moved to `HorizontalArticleGenerator`)
6. Parameter-Annotation Combined Files (deprecated, use separate files)

**Deprecated Models**:
1. `ExamplePromptsResponse` (only used by deprecated ExamplePromptGenerator)

---

## 1. Complete Tool Generation

### Location in CSharpGenerator
- **File**: `CSharpGenerator/Generators/CompleteToolGenerator.cs`
- **Lines**: ~248 lines
- **Integration Point**: Called from `DocumentationGenerator.cs` when `--complete-tools` flag is used

### Superseded By
- **Package**: `ToolGeneration_Composed/`
- **Used By**: 
  - `2-Generate-ToolGenerationAndAIImprovements.ps1` (uses ToolGeneration_Composed)
  - `3-Generate-ToolGenerationAndAIImprovements-One.ps1` (uses ToolGeneration_Composed)

### Current Status
**UNUSED** - Complete tool generation now uses the ToolGeneration_Raw → ToolGeneration_Composed → ToolGeneration_Improved pipeline.

### Functionality
CompleteToolGenerator would:
- Read annotation, parameter, and example-prompts files
- Strip YAML frontmatter
- Embed content into a single complete file
- Output to `./generated/tools/`

This is **exactly what ToolGeneration_Composed does**, but the new approach:
- ToolGeneration_Raw creates files with placeholders
- ToolGeneration_Composed replaces placeholders (same output as CompleteToolGenerator)
- ToolGeneration_Improved applies AI enhancements

### Why It Was Moved
- **Modular Pipeline**: Separates raw generation, composition, and AI improvement into distinct stages
- **Better Testing**: Each stage can be tested independently
- **Flexibility**: Can skip AI improvement step if needed

---

## 2. Example Prompt Generation

### Location in CSharpGenerator
- **File**: `CSharpGenerator/Generators/ExamplePromptGenerator.cs`
- **Lines**: ~383 lines
- **Integration Point**: Called from `DocumentationGenerator.cs` when `--example-prompts` flag is used

### Superseded By
- **Package**: `ExamplePromptGeneratorStandalone/`
- **Used By**: 
  - `3-Generate-ExamplePrompts.ps1` (lines 98-115)
  - `2-Generate-ExamplePrompts-One.ps1` (lines 155-175)

### Current Status
**UNUSED** - All example prompt generation now goes through the standalone package.

### Evidence
```powershell
# From 3-Generate-ExamplePrompts.ps1 (line 101)
Write-Progress "Generating example prompts via ExamplePromptGeneratorStandalone..."

$generatorProject = Join-Path $scriptDir "ExamplePromptGeneratorStandalone"
& dotnet run --project $generatorProject --configuration Release -- $generatedDir
```

**CSharpGenerator is never called with `--example-prompts` flag** in any active orchestration scripts.

### Why It Was Moved
- **Separation of Concerns**: AI-based generation logic isolated from core template processing
- **Faster Iteration**: Changes to prompt generation don't require rebuilding entire CSharpGenerator
- **Dependency Management**: Azure OpenAI dependencies contained in one package
- **Standalone Testing**: Can test prompt generation independently

---

## 2. Tool File Generation (Raw/Composed/Improved)

### Location in CSharpGenerator
- **Related Code**: Template processing for tool files in `DocumentationGenerator.cs`
- **Evidence**: References to tool page generation logic scattered throughout

### Superseded By
- **Packages**: 
  - `ToolGeneration_Raw/` - Creates initial tool files with placeholders
  - `ToolGeneration_Composed/` - Replaces placeholders with actual content
  - `ToolGeneration_Improved/` - Applies AI improvements

### Current Usage
- **Used By**:
  - `1-Generate-AnnotationsParametersRaw.ps1` → calls `ToolGeneration_Raw`
  - `2-Generate-ToolGenerationAndAIImprovements.ps1` → calls `ToolGeneration_Composed` and `ToolGeneration_Improved`
  - `scripts/Generate-RawTools.ps1` (lines 86-108)

### Current Status
**UNUSED** - All tool generation now uses the dedicated 3-phase pipeline packages.

### Evidence
```powershell
# From scripts/Generate-RawTools.ps1 (line 96)
& dotnet run `
    --project "ToolGeneration_Raw" `
    --configuration Release `
    --no-build `
    -- $generatedDir
```

**CSharpGenerator is never called with `--tool-pages` flag** in any active scripts.

---

## 3. Tool Family Page Assembly

### Location in CSharpGenerator
- **File**: `CSharpGenerator/Generators/ToolFamilyPageGenerator.cs`
- **Integration**: Called from `DocumentationGenerator.cs` for generating family-level pages

### Superseded By
- **Package**: `ToolFamilyCleanup/`
- **Used By**:
  - `4-Generate-ToolFamilyCleanup-One.ps1`
  - `4-Generate-ToolFamilyFiles.ps1`

### Current Status
**PARTIALLY USED** - The `ToolFamilyPageGenerator` class exists and may still be referenced, but the standalone `ToolFamilyCleanup` package is preferred for orchestrated workflows.

### Evidence
```powershell
# From 4-Generate-ToolFamilyCleanup-One.ps1
# Uses ToolFamilyCleanup package, not CSharpGenerator
```

---

## 4. Scripts Calling Deprecated CSharpGenerator Functionality

### Generate-CompleteTools.ps1
- **Location**: `scripts/Generate-CompleteTools.ps1`
- **Called By**: 
  - `4-Generate-ToolFamilyFiles.ps1` (line 62)
  - `Generate-ToolPages.ps1` (line 85)
- **Functionality**: Calls CSharpGenerator with `--complete-tools` flag
- **Status**: **DEPRECATED** - Use `2-Generate-ToolGenerationAndAIImprovements.ps1` instead
- **Deprecation Note**: Added to script header

---

## 5. Horizontal Article Generation

### Location in CSharpGenerator
**NOT PRESENT** - This functionality was never in CSharpGenerator; it was created directly as a standalone package.

### Implemented In
- **Package**: `HorizontalArticleGenerator/`
- **Used By**:
  - `5-Generate-HorizontalArticles-One.ps1` (line 137, 157)
  - `Generate-HorizontalArticles.ps1` (line 77, 90)

### Current Status
**N/A** - Never existed in CSharpGenerator.

---

## 5. Other Potentially Unused Features

### 5.1 Index Page Generation
- **Flag**: `--index`
- **Current Usage**: Only referenced in legacy scripts
- **Status**: **RARELY USED** - May be legacy functionality

### 5.2 Common Tools Page Generation
- **Flag**: `--common`
- **Location**: `scripts/Generate-Common.ps1`
- **Status**: **STILL USED** - Active in orchestration

### 5.3 Commands Page Generation
- **Flag**: `--commands`
- **Location**: `scripts/Generate-Commands.ps1`
- **Status**: **STILL USED** - Active in orchestration

### 5.4 Service Options Discovery
- **Removed** — `ServiceOptionsDiscovery.cs` deleted; `--no-service-options` flag removed
- Was never called by the active pipeline (`1-Generate-AnnotationsParametersRaw-One.ps1`)

---

## CSharpGenerator - Currently Active Features

These features are **actively used** and should be retained:

1. **Annotations Generation** (`--annotations` flag)
   - Used by: `scripts/Generate-Annotations.ps1`
   - Generates annotation include files for tool metadata

2. **Parameters Generation** (`--parameters` flag)
   - Used by: `scripts/Generate-Parameters.ps1`
   - Generates parameter include files for tool options

3. **Combined Param+Annotation Generation**
   - Used implicitly when both annotations and parameters are generated
   - Creates `param-and-annotation/` include files

4. **Complete Tools Generation** (`--complete-tools` flag)
   - Used by: `scripts/Generate-CompleteTools.ps1`
   - Generates standalone tool documentation files

5. **Template Processing Mode** (`template` command)
   - Generic Handlebars template processor
   - May be used for custom generation tasks

6. **Brand Mapping & Filename Cleaning**
   - Shared utility functions used across all generators
   - Critical infrastructure for consistent naming

---

## Recommendations

### Option 1: Keep Deprecated Code (Low Risk)
- **Pros**: 
  - No breaking changes
  - Fallback if standalone packages fail
  - Minimal effort required
- **Cons**: 
  - Code bloat (~383 lines for ExamplePromptGenerator alone)
  - Maintenance burden
  - Confusion for new contributors

### Option 2: Remove Deprecated Code (High Impact)
- **Pros**:
  - Cleaner codebase
  - Clear separation of concerns
  - Easier to understand architecture
- **Cons**:
  - Requires thorough testing
  - Potential hidden dependencies
  - Risk of breaking legacy workflows

### Option 3: Mark as Deprecated (Recommended)
- **Actions**:
  1. Add `[Obsolete("Use ExamplePromptGeneratorStandalone instead")]` attributes to:
     - `ExamplePromptGenerator` class
     - `--example-prompts` flag handling in `DocumentationGenerator`
  2. Add comments explaining superseding packages
  3. Update README with migration notes
  4. Remove in next major version (breaking change)

---

## Migration Path

If you decide to remove deprecated functionality:

### Phase 1: Mark as Obsolete (Safe)
```csharp
[Obsolete("Use ExamplePromptGeneratorStandalone package instead. This will be removed in v3.0")]
public class ExamplePromptGenerator { ... }
```

### Phase 2: Remove Flags (v3.0 Breaking Change)
- Remove `--example-prompts` flag from `Program.cs`
- Remove `generateExamplePrompts` parameter from `DocumentationGenerator.GenerateAsync()`

### Phase 3: Delete Classes (v3.0)
- Delete `Generators/ExamplePromptGenerator.cs`
- Remove related dependencies from `DocumentationGenerator.cs`

### Phase 4: Update Documentation
- Update `CSharpGenerator/README.md` with removed features
- Update main project README with standalone package references

---

## Testing Strategy

Before removing any code:

1. **Run full generation pipeline** on all 50 namespaces
2. **Compare output** against known-good baseline
3. **Check for usage** in:
   - All `.ps1` files (root and docs-generation/)
   - All `.sh` files (root and docs-generation/)
   - GitHub Actions workflows (`.github/workflows/`)
   - Docker build scripts
4. **Grep for flag references**:
   ```bash
   grep -r "\-\-example-prompts" .
   grep -r "\-\-tool-pages" .
   ```

---

## Appendix: Script Analysis

### Scripts That Call CSharpGenerator

**Active Usage** (Annotations/Parameters/Commands/Common):
- `scripts/Generate-Annotations.ps1` → `--annotations`
- `scripts/Generate-Parameters.ps1` → `--parameters`
- `scripts/Generate-Commands.ps1` → `--commands`
- `scripts/Generate-Common.ps1` → `--common`
- `scripts/Generate-CompleteTools.ps1` → `--complete-tools`

**Deprecated Calls** (Example Prompts):
- `scripts/Generate-ExamplePromptsAI.ps1` → **Uses CSharpGenerator with `--example-prompts`**
  - **Status**: This script exists but is NOT called by main orchestration
  - **Replacement**: `3-Generate-ExamplePrompts.ps1` uses `ExamplePromptGeneratorStandalone`

### Scripts That DON'T Call CSharpGenerator

**Use Standalone Packages**:
- `3-Generate-ExamplePrompts.ps1` → `ExamplePromptGeneratorStandalone`
- `2-Generate-ExamplePrompts-One.ps1` → `ExamplePromptGeneratorStandalone`
- `scripts/Generate-RawTools.ps1` → `ToolGeneration_Raw`
- `2-Generate-ToolGenerationAndAIImprovements.ps1` → `ToolGeneration_Composed`, `ToolGeneration_Improved`
- `5-Generate-HorizontalArticles-One.ps1` → `HorizontalArticleGenerator`

---

## Conclusion

**Primary Unused Functionality**:
1. `Generators/ExamplePromptGenerator.cs` - Superseded by `ExamplePromptGeneratorStandalone`
2. Tool generation logic (if it exists in CSharpGenerator) - Superseded by `ToolGeneration_*` packages

**Recommended Action**: 
- Mark `ExamplePromptGenerator` as obsolete immediately
- Delete `scripts/Generate-ExamplePromptsAI.ps1` (legacy script that uses deprecated flag)
- Plan removal for next breaking version

**Safe to Remove**:
- `ExamplePromptGenerator.cs` class
- `--example-prompts` flag handling in `Program.cs` and `DocumentationGenerator.cs`
- Related prompt template loading logic in `DocumentationGenerator.cs` (lines ~186-238)

---

## 6. Combined Parameter and Annotation File Generation

### Location in CSharpGenerator
- **File**: `CSharpGenerator/Generators/ParamAnnotationGenerator.cs`
- **Lines**: ~235 lines
- **Integration Point**: Called from `DocumentationGenerator.cs` during base documentation generation

### Current Status
**DISABLED** - Combined parameter-and-annotation files are no longer generated in the pipeline.

### Evidence
The class is entirely commented out in `ParamAnnotationGenerator.cs`:
```csharp
/*
DEPRECATED: Combined parameter and annotation file generation has been disabled.
Keeping code in place for reference but not used. 
Use separate annotations and parameters files instead (or complete tool files with --complete-tools flag).

public class ParamAnnotationGenerator
{
    // ... entire class commented out (~235 lines)
}
*/
```

The initialization in `DocumentationGenerator.cs` is also commented out (line ~265).

### Why It Was Disabled
- **Redundancy**: Annotations and parameters files already exist separately in the output
- **Cleaner Organization**: Separate files provide better flexibility and easier maintenance
- **Reduced Output Clutter**: Eliminates unnecessary duplicate files
- **Better Alternatives**: Complete tools (with `--complete-tools` flag) provide better combined output when needed

---

## 7. ExamplePromptsResponse Model

### Location in CSharpGenerator
- **File**: `CSharpGenerator/Models/ExamplePromptsResponse.cs`
- **Lines**: ~12 lines (class definition)
- **Integration Point**: Used only by deprecated `ExamplePromptGenerator.cs`

### Superseded By
- **Package**: `ExamplePromptGeneratorStandalone/` has its own version of this model
- **Usage**: No active code references this model (all references are in commented-out ExamplePromptGenerator)

### Current Status
**UNUSED** - Model is commented out. Only used by deprecated ExamplePromptGenerator.

### Functionality
This model was used to deserialize AI-generated example prompts from Azure OpenAI:
```csharp
public class ExamplePromptsResponse
{
    [JsonPropertyName("toolName")]
    public string? ToolName { get; set; }
    
    [JsonPropertyName("prompts")]
    public List<string> Prompts { get; set; } = new();
}
```

### Why It Was Deprecated
- **Single Use**: Only used by ExamplePromptGenerator which is deprecated
- **Standalone Alternative**: ExamplePromptGeneratorStandalone has its own copy of this model
- **Zero Active References**: All references are in commented-out code

---

## 8. Updated Conclusion

**All Deprecated Functionality Status**: ✅ COMMENTED OUT IN PLACE

1. `Generators/ExamplePromptGenerator.cs` - Disabled (383 lines) ✓
2. Tool generation logic (tool pages) - Disabled (section commented out) ✓
3. `Generators/ParamAnnotationGenerator.cs` - Disabled (235 lines) ✓
4. `Generators/ToolFamilyPageGenerator.cs` - Disabled (250 lines) ✓
5. `Generators/CompleteToolGenerator.cs` - Disabled (248 lines) ✓
6. `Models/ExamplePromptsResponse.cs` - Disabled (model class) ✓

**Current Build Status**: ✅ Builds successfully - all deprecated code is in place but not executed

**Next Steps**: 
- Keep deprecated code in place for backwards compatibility reference
- Plan removal for next major version
- Standalone packages are active replacements:
  - `ExamplePromptGeneratorStandalone` → replaces ExamplePromptGenerator
  - `ToolGeneration_Raw`, `ToolGeneration_Composed`, `ToolGeneration_Improved` → replace tool generation
  - `ToolFamilyCleanup` → replaces ToolFamilyPageGenerator
  - Use separate `annotations/` and `parameters/` files → replaces ParamAnnotationGenerator
