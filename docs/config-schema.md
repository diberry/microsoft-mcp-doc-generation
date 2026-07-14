# Configuration Schema Documentation

This document describes the JSON configuration files used by the Azure MCP Documentation Generator pipeline.

## config/namespace-mapping.json

**Purpose**: Maps Azure MCP Server namespace identifiers to their corresponding published article filenames.

**Location**: `config/namespace-mapping.json` (repository root)

**Used by**:
- PowerShell: `mcp-tools/validation/Scan-McpToolCoverage.ps1` (coverage audit script)
- C#: `DocGeneration.PipelineRunner.Services.NamespaceMappingLoader` (pipeline validation)

**Schema**:

```json
{
  "namespace": "filename.md"
}
```

**Fields**:
- **namespace** (string, required): The MCP Server namespace identifier (e.g., "storage", "keyvault", "cosmos")
- **filename.md** (string, required): The published article filename (e.g., "azure-storage.md", "azure-key-vault.md")

**Example**:

```json
{
  "storage": "azure-storage.md",
  "keyvault": "azure-key-vault.md",
  "cosmos": "azure-cosmos-db.md",
  "appconfig": "app-configuration.md",
  "group": "resource-group.md"
}
```

**Notes**:
- This file contains **57 entries** mapping all active Azure MCP Server namespaces
- Namespace identifiers are **lowercase** (e.g., "keyvault", not "KeyVault")
- Filenames may differ from `brand-to-server-mapping.json` fileName field due to generation pipeline transformations
- This mapping reflects **actual published filenames** after generation, not template names

**Validation**:
- C# unit tests: `DocGeneration.PipelineRunner.Tests/Unit/NamespaceMappingLoaderTests.cs`
- PowerShell Pester tests: `mcp-tools/validation/tests/Scan-McpToolCoverage.Tests.ps1`

**Related files**:
- `mcp-tools/data/brand-to-server-mapping.json` — Maps brand names to MCP server names for GENERATION (different purpose)

---

## mcp-tools/data/brand-to-server-mapping.json

**Purpose**: Maps Azure service brand names to MCP server names, short names, and composition configuration for documentation GENERATION.

**Location**: `mcp-tools/data/brand-to-server-mapping.json`

**Schema**: See `DocGeneration.PipelineRunner.Services.BrandMappingEntry` for the complete model.

**Key fields**:
- `brandName`: Full Azure service brand name
- `mcpServerName`: MCP server namespace identifier
- `shortName`: Abbreviated service name
- `fileName`: Base filename for generated documentation (without `.md` extension)
- `composition`: Document composition strategy ("standalone", "merge", "split")
- `mergeGroup`, `mergeOrder`, `mergeRole`: Multi-namespace merge configuration

**Note**: The `fileName` field + ".md" may NOT match the actual published filename in `namespace-mapping.json` due to pipeline transformations.

---

## Updating namespace-mapping.json

When adding a new Azure service namespace:

1. Add the namespace → filename entry to `config/namespace-mapping.json`
2. Run C# unit tests: `dotnet test DocGeneration.PipelineRunner.Tests`
3. Run PowerShell Pester tests: `pwsh -File mcp-tools/validation/tests/Scan-McpToolCoverage.Tests.ps1`
4. Commit both the config file update and any corresponding test changes

---

## History

- **2026-07-13**: Initial extraction from PowerShell hashtable (#582, Phase 1 of #574)
  - Extracted 57 namespace mappings from `Scan-McpToolCoverage.ps1` inline hashtable
  - Created C# loader (`NamespaceMappingLoader`) with unit tests
  - Updated PowerShell script to read from JSON with fallback to inline mapping
  - Added Pester tests for JSON loading behavior
