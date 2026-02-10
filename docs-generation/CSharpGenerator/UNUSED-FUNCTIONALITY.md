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
3. Tool Family Assembly (moved to `ToolFamilyCleanup`)
4. Horizontal Article Generation (moved to `HorizontalArticleGenerator`)

---

## 1. Example Prompt Generation

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

## 4. Horizontal Article Generation

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
- **Flag**: `--no-service-options` (inverted logic)
- **File**: `ServiceOptionsDiscovery.cs`
- **Status**: **STILL USED** - Active in `Generate.ps1`

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
