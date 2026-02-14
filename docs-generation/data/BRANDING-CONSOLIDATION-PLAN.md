# Branding Data Files Consolidation Plan

## Executive Summary

This document analyzes the duplication between `brand-to-server-mapping.json` and `transformation-config.json`, which both contain Azure service brand name mappings. It proposes a consolidation strategy that would improve maintainability while preserving functionality.

## Current State Analysis

### Files with Branding Data

#### 1. brand-to-server-mapping.json
- **Size**: 290 lines, 44+ service mappings
- **Format**: Array of detailed brand mapping objects
- **Schema**:
  ```json
  {
    "brandName": "Azure Container Registry",
    "mcpServerName": "acr",
    "shortName": "ACR",
    "fileName": "azure-container-registry"
  }
  ```
- **Unique Fields**: `mcpServerName`, `shortName`
- **Used By**:
  - CSharpGenerator (main multi-page documentation)
  - ToolGeneration_Raw (raw tool documentation)
  - ToolFamilyCleanup (tool family processing)
  - PowerShell scripts

#### 2. transformation-config.json
- **Size**: 36 lines, 6 service mappings (partial list)
- **Format**: Nested object with services.mappings array
- **Schema**:
  ```json
  {
    "mcpName": "azure-ai-foundry-agents",
    "brandName": "Azure AI Foundry Agents",
    "filename": "azure-ai-foundry-agents"
  }
  ```
- **Unique Fields**: None (all fields overlap conceptually with brand-to-server-mapping)
- **Used By**:
  - HorizontalArticleGenerator (horizontal articles)
  - TextTransformation library (brand name transformations)

### Overlap Analysis

Both files contain:
- Azure service brand names (`brandName` in both)
- Standardized filename slugs (`fileName` vs `filename`)
- Service identifiers (`mcpServerName` vs `mcpName`)

**Duplication**: Approximately 6 services appear in both files (azure-ai-search, azure-app-configuration, azure-app-service, etc.)

**Inconsistencies**:
- Field naming conventions differ (`fileName` vs `filename`)
- `transformation-config.json` uses nested structure (`services.mappings`)
- `brand-to-server-mapping.json` has additional fields (`shortName`)

## Problem Statement

1. **Maintenance Burden**: When a new service is added, developers must update both files if it needs to appear in both horizontal articles and multi-page documentation
2. **Inconsistency Risk**: Different field names and structures increase the chance of data inconsistencies
3. **Incomplete Coverage**: `transformation-config.json` only has 6 mappings vs 44+ in `brand-to-server-mapping.json`
4. **Unclear Purpose**: Developers may not understand why two files exist for similar purposes

## Proposed Consolidation Strategy

### Option A: Merge into Single File (Recommended)

**Approach**: Extend `brand-to-server-mapping.json` to be the single source of truth for all brand mappings.

**Changes Required**:

1. **Keep `brand-to-server-mapping.json` as master file**
   - Already has comprehensive coverage (44+ services)
   - Already used by most projects
   - Has the most complete schema

2. **Deprecate `transformation-config.json`**
   - Remove file after migration
   - Archive as `transformation-config.json.deprecated`

3. **Update Code References**:

   a. **TextTransformation Library**
      - File: `TextTransformation/ConfigLoader.cs`
      - Change: Load from `brand-to-server-mapping.json` instead
      - Update parser to handle array format
      - Map fields: `brandName` → `brandName`, `fileName` → `filename`
      
   b. **HorizontalArticleGenerator**
      - File: `HorizontalArticleGenerator/Generators/HorizontalArticleGenerator.cs` (line 222)
      - Change: Use `brand-to-server-mapping.json`
      - Update property accessors to use new schema
      
   c. **HorizontalArticleProgram.cs**
      - File: `HorizontalArticleGenerator/HorizontalArticleProgram.cs` (line 94)
      - Change: Point to `brand-to-server-mapping.json`

4. **Add Utility Helper Class**
   - Create `Shared/BrandMappingLoader.cs`
   - Centralized loading logic
   - Consistent error handling
   - Single source of truth for file path resolution
   - Example:
     ```csharp
     public static class BrandMappingLoader
     {
         private static List<BrandMapping>? _cache;
         
         public static async Task<List<BrandMapping>> LoadAsync()
         {
             if (_cache != null) return _cache;
             
             var path = Path.Combine(AppContext.BaseDirectory, 
                 "..", "..", "..", "..", "data", "brand-to-server-mapping.json");
             var json = await File.ReadAllTextAsync(path);
             _cache = JsonSerializer.Deserialize<List<BrandMapping>>(json);
             return _cache ?? new List<BrandMapping>();
         }
         
         public static BrandMapping? GetByMcpName(string mcpName)
         {
             return LoadAsync().Result
                 .FirstOrDefault(m => m.McpServerName.Equals(mcpName, 
                     StringComparison.OrdinalIgnoreCase));
         }
     }
     ```

### Option B: Keep Both Files, Establish Clear Boundaries

**Approach**: Maintain both files but clarify their distinct purposes and ensure consistency.

**Changes Required**:

1. **Update Documentation**
   - Add clear comments in both files explaining their purpose
   - Document when to use which file
   - Add validation rules

2. **Add Validation Script**
   - Create `scripts/Validate-BrandMappings.ps1`
   - Check for inconsistencies between files
   - Run in CI/CD pipeline
   - Example checks:
     - Same `mcpName`/`mcpServerName` have same `brandName`
     - Same `filename`/`fileName` for matching services
     - No orphaned entries

3. **Define Clear Ownership**
   - `brand-to-server-mapping.json` → Multi-page documentation, general use
   - `transformation-config.json` → Text transformation engine only
   
4. **Create Sync Utility**
   - Create `scripts/Sync-BrandMappings.ps1`
   - Semi-automatic sync from master to transformation config
   - Reduces manual update burden

### Option C: Introduce Schema Versioning

**Approach**: Create a new unified schema that both systems can use, with backward compatibility.

**Changes Required**:

1. **Create New Unified Schema**
   - File: `brand-mappings-v2.json`
   - Combines best of both schemas
   - Supports multiple consumers
   
2. **Implement Schema Version Detection**
   - All loaders check schema version
   - Automatic migration from v1 to v2
   
3. **Gradual Migration**
   - Phase 1: Add v2 support alongside v1
   - Phase 2: Migrate all consumers to v2
   - Phase 3: Remove v1 support

## Recommendation

**Recommended Approach: Option A (Merge into Single File)**

**Rationale**:
1. **Simplest to implement**: Only requires updating 3 files (ConfigLoader, HorizontalArticleGenerator, HorizontalArticleProgram)
2. **Clearest for developers**: Single source of truth eliminates confusion
3. **Easiest to maintain**: One file to update when services change
4. **Best coverage**: `brand-to-server-mapping.json` already has 7x more services
5. **Lowest risk**: Most projects already use `brand-to-server-mapping.json`

**Estimated Effort**: 4-6 hours
- Code changes: 2-3 hours
- Testing: 1-2 hours  
- Documentation: 1 hour

## Implementation Checklist

If Option A is approved:

- [ ] Create `Shared/BrandMappingLoader.cs` utility class
- [ ] Update `TextTransformation/ConfigLoader.cs` to use brand-to-server-mapping.json
- [ ] Update `HorizontalArticleGenerator/Generators/HorizontalArticleGenerator.cs`
- [ ] Update `HorizontalArticleGenerator/HorizontalArticleProgram.cs`
- [ ] Test all affected projects build successfully
- [ ] Test generation pipeline end-to-end
- [ ] Update `TextTransformation/README.md` documentation
- [ ] Update `HorizontalArticleGenerator/README.md` documentation
- [ ] Update `data/README.md` to remove transformation-config.json
- [ ] Archive `transformation-config.json` as `.deprecated`
- [ ] Update `.gitignore` to exclude .deprecated files
- [ ] Add migration notes to CHANGELOG

## Impact Assessment

### Low Risk Changes
- `brand-to-server-mapping.json` already used by 4+ projects
- Schema is well-established and proven
- All builds pass with current references

### Medium Risk Changes
- HorizontalArticleGenerator needs schema mapping updates
- TextTransformation library needs parser updates

### High Risk Changes
- None identified (backward compatibility maintained)

## Rollback Plan

If consolidation causes issues:
1. Restore `transformation-config.json` from git history
2. Revert code changes in affected projects
3. Rebuild affected projects
4. Regenerate documentation

## Success Criteria

1. All projects build without errors
2. Generated documentation matches previous output
3. Single file contains all brand mappings
4. No duplication between files
5. Clear documentation of changes

## Questions for Review

1. Are there use cases for `transformation-config.json` that aren't covered by `brand-to-server-mapping.json`?
2. Should we maintain `transformation-config.json` for backward compatibility in external tools?
3. Should the utility helper be in `Shared` or a new `BrandMapping` project?
4. Should we add TypeScript type definitions for the unified schema?

## Appendices

### A. Field Mapping Table

| brand-to-server-mapping.json | transformation-config.json | Notes |
|------------------------------|---------------------------|-------|
| brandName | brandName | Direct match |
| mcpServerName | mcpName | Different property names |
| fileName | filename | Different casing |
| shortName | *(none)* | Unique to brand-to-server |

### B. Service Coverage Comparison

- `brand-to-server-mapping.json`: 44 services
- `transformation-config.json`: 6 services
- Overlap: 6 services (100% of transformation-config)
- Unique to brand-to-server: 38 services

### C. Related Files

- `compound-words.json` - Not duplicative; handles word transformations
- `stop-words.json` - Not duplicative; handles filename cleaning
- `nl-parameters.json` - Not duplicative; handles parameter naming

---

**Document Version**: 1.0  
**Date**: February 14, 2026  
**Author**: GitHub Copilot  
**Status**: Proposal - Awaiting Review
