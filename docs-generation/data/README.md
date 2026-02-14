# Data Files Documentation

This directory contains all JSON configuration and data files used by the documentation generation system. These files control various aspects of how Azure MCP documentation is generated, including branding, naming conventions, parameter handling, and text transformations.

## Files Overview

### 1. brand-to-server-mapping.json
**Purpose**: Maps Azure service brand names to MCP server names and standardized filenames.

**Structure**: Array of objects with properties:
- `brandName`: Official Azure service brand name (e.g., "Azure Container Registry")
- `mcpServerName`: Internal MCP server identifier (e.g., "acr")
- `shortName`: Abbreviated display name (e.g., "ACR")
- `fileName`: Standardized filename slug (e.g., "azure-container-registry")

**Used By**:
- `CSharpGenerator` - Main documentation generator for multi-page docs
  - `DocumentationGenerator.cs` (line 71-74) - Loads for filename generation
  - `Generators/AnnotationGenerator.cs` (line 399) - References in comments
  - `Generators/ReportGenerator.cs` (line 211) - References in reports
- `ToolGeneration_Raw` - Raw tool documentation generator
  - `Program.cs` (line 95-98) - Loads for brand name mapping
- `ToolFamilyCleanup` - Tool family cleanup generator
  - `Services/CleanupGenerator.cs` (line 58) - Loads for brand mappings
- `HorizontalArticleGenerator` - Horizontal article generator
  - `Models/StaticArticleData.cs` (line 15) - Documentation reference
- PowerShell Scripts:
  - `4-Generate-ToolFamilyCleanup-One.ps1` (lines 234, 268) - Loads for tool family processing

**Size**: 290 lines (44+ service mappings)

**Update When**: 
- New Azure service areas are added to the MCP server
- Service brand names change officially
- Filename conventions need standardization

---

### 2. common-parameters.json
**Purpose**: Defines common parameters shared across all Azure MCP tools that should be filtered from parameter tables.

**Structure**: Array of objects with properties:
- `name`: Parameter name (e.g., "--tenant", "--subscription")
- `type`: Data type (string, number, integer)
- `description`: Human-readable description
- `isRequired`: Boolean indicating if parameter is required

**Used By**:
- `CSharpGenerator` - Main documentation generator
  - `DocumentationGenerator.cs` (line 775) - Loads common parameters to filter them from documentation tables
  - Filters these parameters unless they are required for a specific tool

**Size**: 56 lines (9 common parameters)

**Common Parameters Defined**:
- `--tenant` - Microsoft Entra ID tenant
- `--subscription` - Azure subscription
- `--auth-method` - Authentication method
- `--resource-group` - Azure resource group
- `--retry-delay` - Retry delay in seconds
- `--retry-max-delay` - Maximum retry delay
- `--retry-max-retries` - Maximum retry attempts
- `--retry-mode` - Retry strategy (fixed/exponential)
- `--retry-network-timeout` - Network timeout

**Update When**:
- New common parameters are added to all Azure MCP tools
- Common parameter definitions change

---

### 3. compound-words.json
**Purpose**: Transforms concatenated or hyphenated words into standardized filename formats.

**Structure**: Dictionary mapping original terms to transformed terms:
```json
{
  "activitylog": "activity-log",
  "nodepool": "node-pool",
  "eventhub": "event-hub"
}
```

**Used By**:
- `CSharpGenerator` - Main documentation generator
  - `DocumentationGenerator.cs` (line 54) - Loads for filename generation
  - `Generators/AnnotationGenerator.cs` (line 400) - References in comments
  - `Generators/ReportGenerator.cs` (line 214) - References in reports
  - Used in three-tier filename resolution (brand mapping → compound words → original name)

**Size**: 24 lines (23 word transformations)

**Update When**:
- Service areas have concatenated words that need separation
- New compound word patterns are identified in service names

---

### 4. config.json
**Purpose**: Central configuration file that references other required configuration files.

**Structure**: Object with `RequiredFiles` array:
```json
{
  "RequiredFiles": [
    "static-text-replacement.json",
    "nl-parameters.json"
  ]
}
```

**Used By**:
- `CSharpGenerator` - Main documentation generator
  - `Program.cs` (line 18) - Loads and validates configuration
  - `Config.cs` (lines 54-61) - Resolves file paths and sets static properties
- `HorizontalArticleGenerator` - Horizontal article generator
  - `HorizontalArticleProgram.cs` (line 27) - Loads configuration

**Size**: 6 lines

**Update When**:
- New required configuration files are added to the system
- File dependencies change

---

### 5. nl-parameters.json
**Purpose**: Maps technical parameter names to natural language equivalents for better readability.

**Structure**: Array of objects with properties:
- `Parameter`: Technical parameter name
- `NaturalLanguage`: Human-readable equivalent

**Used By**:
- `CSharpGenerator` - Main documentation generator
  - `Config.cs` (line 54-56) - Loads and sets NLParametersPath
- `NaturalLanguageGenerator` - Natural language text processing
  - `TextCleanup.cs` (line 39-59) - Loads for parameter name normalization

**Size**: 18 lines (4 parameter mappings)

**Mappings Defined**:
- `auth-method` → "Authentication method"
- `cluster-uri` → "Cluster URI"
- `param` → "Parameter"
- `nodepool` → "Node pool"

**Update When**:
- New parameters need natural language translations
- Parameter naming conventions change

---

### 6. static-text-replacement.json
**Purpose**: Provides text replacements for common abbreviations and acronyms in descriptions.

**Structure**: Array of objects with properties:
- `Parameter`: Text to find
- `NaturalLanguage`: Replacement text

**Used By**:
- `CSharpGenerator` - Main documentation generator
  - `Config.cs` (line 58-60) - Loads and sets TextReplacerParametersPath
- `NaturalLanguageGenerator` - Natural language text processing
  - `TextCleanup.cs` (line 43-72) - Loads for text transformations

**Size**: 35 lines (8 replacements)

**Replacements Defined**:
- `e.g.` → "for example"
- `i.e.` → "in other words"
- `id` → "ID"
- `uri` → "URI"
- `url` → "URL"
- `ai` → "AI"
- `sku` → "SKU"
- `VMSS` → "Virtual machine scale set (VMSS)"

**Update When**:
- New abbreviations or acronyms need standardized replacements
- Text transformation rules change

---

### 7. stop-words.json
**Purpose**: Defines words to be removed from include filenames to keep them concise.

**Structure**: Simple array of strings:
```json
["a", "or", "and", "the", "in"]
```

**Used By**:
- `CSharpGenerator` - Main documentation generator
  - `DocumentationGenerator.cs` (line 39) - Loads for filename cleaning

**Size**: 1 line (5 stop words)

**Update When**:
- Additional words should be excluded from filenames
- Filename generation conventions change

---

### 8. transformation-config.json
**Purpose**: Consolidated configuration for text transformation engine with service brand name mappings.

**Structure**: Object with nested `services.mappings` array:
- `mcpName`: MCP service identifier
- `brandName`: Azure service brand name
- `filename`: Standardized filename

**Used By**:
- `HorizontalArticleGenerator` - Horizontal article generator
  - `Generators/HorizontalArticleGenerator.cs` (line 222) - Loads for brand name transformations
  - `HorizontalArticleProgram.cs` (line 94) - Loads for text transformation engine
- `TextTransformation` - Text transformation library
  - `ConfigLoader.cs` (line 16) - Loads transformation configuration
  - `TransformationEngine` uses this for applying brand name mappings

**Size**: 36 lines (6 service mappings - partial list)

**Services Defined**:
- azure-ai-foundry-agents
- azure-ai-search
- azure-app-configuration
- azure-app-service
- azure-applens-resource
- azure-application-insights

**Update When**:
- New services need brand name transformations
- Existing brand names change

---

## File Path Resolution

All .NET projects use relative path resolution from `AppContext.BaseDirectory`:
```csharp
Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "data", "filename.json")
```

PowerShell scripts use `Join-Path` with `$scriptDir`:
```powershell
Join-Path $scriptDir "data/filename.json"
```

## Three-Tier Filename Resolution

For include files, the system uses a three-tier resolution strategy:

1. **Brand Mapping** (highest priority): Check `brand-to-server-mapping.json`
2. **Compound Words** (medium priority): Check `compound-words.json`
3. **Original Name** (fallback): Use the original area name

Example:
- Input: "acr"
- Check brand-to-server-mapping.json: Found → "azure-container-registry"
- Use: "azure-container-registry"

## Testing After Changes

After modifying any data file, run these builds to verify:

```bash
# Test CSharpGenerator
dotnet build CSharpGenerator/CSharpGenerator.csproj

# Test ToolGeneration_Raw
dotnet build ToolGeneration_Raw/ToolGeneration_Raw.csproj

# Test HorizontalArticleGenerator
dotnet build HorizontalArticleGenerator/HorizontalArticleGenerator.csproj

# Test ToolFamilyCleanup
dotnet build ToolFamilyCleanup/ToolFamilyCleanup.csproj
```

## Related Documentation

- Main README: `../README.md`
- CSharpGenerator docs: `../CSharpGenerator/docs/README.md`
- TextTransformation docs: `../TextTransformation/README.md`

## Migration Notes

**Date**: February 14, 2026

All JSON configuration files were moved from `docs-generation/` root to `docs-generation/data/` to improve organization and clarity. All .NET projects and PowerShell scripts have been updated to reference the new location.
