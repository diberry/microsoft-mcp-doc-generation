# Tool Family Generator Feature Plan

## Overview
Add a new .NET package to generate comprehensive tool family documentation files. Each file groups related tools (e.g., all Storage tools, all AKS tools) into a single markdown document with overview, all operations, parameters, and cross-references.

---

## 1. Architecture

### 1.1 New Package Structure
```
docs-generation/
├── ToolFamilyGenerator/
│   ├── ToolFamilyGenerator.csproj
│   ├── Program.cs                          (entry point)
│   ├── ToolFamilyDocumentationGenerator.cs (main logic)
│   ├── Models/
│   │   ├── ToolFamilyData.cs               (family grouping model)
│   │   ├── ToolOperation.cs                (individual tool/operation)
│   │   └── FamilyMetadata.cs               (family description, keywords)
│   ├── Services/
│   │   ├── ToolFamilyResolver.cs           (determine families from CLI data)
│   │   ├── AnnotationLoader.cs             (load annotation content)
│   │   └── HandlebarsTemplateEngine.cs     (shared with other generators)
│   └── Templates/
│       └── tool-family-template.hbs        (Handlebars template)
```

### 1.2 Independence
- **No dependencies** on CSharpGenerator, AnnotationGenerator, or PageGenerator
- **Input**: CLI output JSON, generated annotations folder, generated tools folder
- **Output**: `generated/tool-families/` with one file per family (e.g., `azure-storage-tools.md`)
- **Reusable**: Can be called independently or from Generate-MultiPageDocs.ps1

---

## 2. Data Flow & Logic

### 2.1 Tool Family Identification
**Strategy**: Use namespace file to identify families
- **Input**: `generated/cli/cli-namespace.json` provides service areas with metadata
- Each namespace entry = one tool family
- Family name = namespace command name (e.g., `storage`, `aks`, `signalr`)
- Friendly name = namespace name field (e.g., "Azure Storage", "Azure Kubernetes Service")
- Description = namespace description field

**Example** (from cli-namespace.json):
```json
{
  "name": "storage",
  "command": "storage",
  "description": "Storage operations - Commands for managing and accessing Azure Storage..."
}
→ Family Name: "storage"
→ Family Key: "azure-storage" (brand-mapped)
→ Family File: azure-storage.md
→ Title: "Azure Storage tools for the Azure MCP Server overview"
```

### 2.2 Tool Grouping Within Family
**Strategy**: Organize by resource type and operation type
- Extract resource type from tool command structure using this algorithm:
  - Split command name on spaces: e.g., "storage account create" → ["storage", "account", "create"]
  - **Resource Type** = second word (index 1), titlecased (e.g., "account" → "Account", "blob" → "Blob")
  - **Operation Name** = remaining words from index 2 onward, titlecased as a phrase (e.g., ["create"] → "Create", ["get", "details"] → "Get details")
- Operation names are **phrases** describing the action (not single words)
- Example for Storage (command: "storage account create"):
  ```
  Account: Create (storage account create)
  Account: Get details (storage account get)
  Container: Create container (storage blob container create)
  Container: Get container details (storage blob container get)
  Blob: Get blob details (storage blob get)
  Blob: Upload (storage blob upload)
  ```
- Use pattern: `{RESOURCE_TYPE}: {OPERATION_PHRASE}`

### 2.3 Data Assembly Pipeline
1. **Load Namespace File**: Parse `generated/cli/cli-namespace.json` to identify all families
2. **Load CLI Output**: Parse `generated/cli/cli-output.json` to get all tools
3. **Map Tools to Families**: Group tools from CLI by their namespace/service area (match CLI tool namespace to namespace file entry)
4. **Load Annotations**: For each tool in family, load annotation from `generated/annotations/azure-{family}-{operation}-annotations.md`
5. **Extract Parameters**: From annotation content
6. **Build Model**: Create `ToolFamilyData` object with all tools/operations grouped by resource type
7. **Render Template**: Apply Handlebars template to generate markdown

### 2.4 Sorting & Organization
- **Tool Families**: Alphabetize by namespace (e.g., `acr`, `aks`, `appconfig`, `applens`, `storage`, `storagesync`, etc.)
- **Within Each Family - Resource Types**: Alphabetize by resource type grouping name (e.g., `Account`, `Blob`, `Container`)
- **Within Each Resource Type - Operations**: Alphabetize by operation name phrase (e.g., `Create`, `Get details`, `Upload`)

---

## 3. Data Models

### 3.1 ToolFamilyData.cs
```csharp
public class ToolFamilyData
{
    public string FamilyName { get; set; }              // e.g., "storage" (from namespace.command)
    public string FamilyKey { get; set; }               // e.g., "azure-storage" (brand-mapped, used for filename)
    public string Title { get; set; }                   // e.g., "Azure Storage tools..."
    public string Description { get; set; }             // From namespace.description
    public List<string> Keywords { get; set; }          // SEO keywords (from config or derived)
    public string Author { get; set; }                  // Default: "diberry"
    public string ServiceUrl { get; set; }              // Link to Azure service docs (from config)
    public List<ToolGroup> Groups { get; set; }         // Grouped by resource type (alphabetized)
    public List<string> RelatedContent { get; set; }    // Related links (from config)
}

public class ToolGroup
{
    public string ResourceType { get; set; }            // e.g., "Account", "Container", "Blob"
    public List<ToolOperation> Operations { get; set; }
}

public class ToolOperation
{
    public string OperationName { get; set; }           // e.g., "Create", "Get details"
    public string CommandName { get; set; }             // e.g., "storage account create"
    public string Description { get; set; }             // From annotation or CLI
    public string Prerequisites { get; set; }           // From annotation
    public List<string> ExamplePrompts { get; set; }    // From annotation
    public List<ToolParameter> Parameters { get; set; }
    public string AnnotationContent { get; set; }       // For includes
    public string AnnotationFileName { get; set; }      // e.g., "azure-storage-account-create-annotations.md"
}

public class ToolParameter
{
    public string Name { get; set; }
    public bool IsRequired { get; set; }
    public string Description { get; set; }
}
```

---

## 4. Configuration & Metadata

### 4.1 Tool Family Metadata File (Optional)
Create `docs-generation/tool-family-config.json` for optional customization:
```json
{
  "families": [
    {
      "namespace": "storage",
      "keywords": ["azure mcp server", "azmcp", "storage account", "blob storage"],
      "serviceUrl": "https://learn.microsoft.com/azure/storage/common/storage-introduction",
      "relatedContent": [
        "[What are the Azure MCP Server tools?](index.md)",
        "[Get started using Azure MCP Server](../get-started.md)",
        "[Azure Storage](/azure/storage/common/storage-introduction)"
      ]
    },
    {
      "namespace": "aks",
      "keywords": ["azure mcp server", "aks", "kubernetes"],
      "serviceUrl": "https://learn.microsoft.com/azure/aks/",
      ...
    }
  ]
}
```

**Notes**: 
- Config is keyed by `namespace` (e.g., "storage", "aks") to match cli-namespace.json entries
- If config entry is missing for a namespace, defaults apply (derived keywords, generic related content)
- Brand mapping via `brand-to-server-mapping.json` is required; if a namespace is not in the mapping, fallback to `azure-{namespace}` format

### 4.2 Resource Type Mapping
Derive from CLI tool names automatically or from above config.

---

## 5. Handlebars Template Structure

### 5.1 Template: `tool-family-template.hbs`
```handlebars
---
title: {{title}}
description: "{{description}}"
keywords: {{#each keywords}}"{{this}}"{{#unless @last}}, {{/unless}}{{/each}}
author: {{author}}
ms.author: {{author}}
ms.date: {{date}}
...
---

# {{title}}

{{description}}

[!INCLUDE [tip-about-params](../includes/tools/parameter-consideration.md)]

{{#each groups}}
## {{resourceType}}

{{#each operations}}
### {{resourceType}}: {{operationName}}

<!-- {{commandName}} -->

{{{description}}}

**Prerequisites**: {{prerequisites}}

Example prompts include:

{{#each examplePrompts}}
- {{this}}
{{/each}}

| Parameter | Required or optional | Description |
{{#each parameters}}
| **{{name}}** | {{#if isRequired}}Required{{else}}Optional{{/if}} | {{description}} |
{{/each}}

**Success verification**: Returns...

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

[!INCLUDE [{{commandName}}](../includes/tools/annotations/{{annotationFileName}}]]

{{/each}}
{{/each}}

## Related content

{{#each relatedContent}}
- {{this}}
{{/each}}
```

---

## 6. Integration with Generate-MultiPageDocs.ps1

### 6.1 New Parameter
```powershell
param(
    ...
    [bool]$ToolFamilies = $true
)
```

### 6.2 Call Location (after CSharp generator completes)
```powershell
# Step 3.5: Generate tool family documentation
if ($ToolFamilies) {
    Write-Progress "Step 3.5: Generating tool family documentation..."
    
    $namespaceInputPath = Join-Path $cliOutputPath "cli-namespace.json"
    $toolFamilyArgs = @(
        "generate-tool-families",
        $namespaceInputPath,
        $cliInputPath,
        (Join-Path $parentOutputDir "tool-families"),
        (Join-Path $parentOutputDir "annotations")
    )
    
    $toolFamilyOutput = & dotnet run --configuration Release -- $toolFamilyArgs 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Tool family generation failed: $toolFamilyOutput"
    } else {
        Write-Success "Tool family files generated successfully"
    }
}
```

### 6.3 Output Directory
- Created: `generated/tool-families/`
- Files: `azure-{family}-tools.md` (e.g., `azure-storage-tools.md`)

---

## 7. Input/Output Specification

### 7.1 Inputs (Required)
- **Namespace File**: `generated/cli/cli-namespace.json` (primary input for family identification)
- **CLI Output**: `generated/cli/cli-output.json` (tools grouped by service area)
- **Annotations Folder**: `generated/annotations/` (tool-level annotation includes)

### 7.1b Inputs (Optional)
- **Config**: `docs-generation/tool-family-config.json` (customization per family)

### 7.2 Outputs
- **Tool Family Files**: `generated/tool-families/azure-{family}-tools.md`
- **Summary**: `generated/logs/tool-families-generation.log`

### 7.3 CLI Arguments
```
dotnet run -- generate-tool-families <cli-namespace-json> <cli-output-json> <output-dir> <annotations-dir>
```

---

## 8. Implementation Phases

### Phase 1: Core Models & Services (Independent)
- [ ] Create `ToolFamilyGenerator.csproj`
- [ ] Implement `ToolFamilyData.cs`, `ToolOperation.cs`, `ToolParameter.cs`
- [ ] Implement `ToolFamilyResolver.cs` to group tools by family
- [ ] Implement `AnnotationLoader.cs` to fetch annotation content
- [ ] Create `tool-family-config.json`

### Phase 2: Template & Rendering
- [ ] Create `tool-family-template.hbs`
- [ ] Copy/adapt `HandlebarsTemplateEngine.cs` from CSharpGenerator
- [ ] Implement `ToolFamilyDocumentationGenerator.cs`
- [ ] Test template rendering with sample data

### Phase 3: Integration
- [ ] Create `Program.cs` entry point with CLI argument parsing
- [ ] Build and validate standalone execution
- [ ] Integrate into `Generate-MultiPageDocs.ps1`
- [ ] Test full pipeline

### Phase 4: Polish & Documentation
- [ ] Add logging and error handling
- [ ] Document in README
- [ ] Update solution file references

---

## 9. Expected Output Example

**File**: `generated/tool-families/azure-storage-tools.md`

Structure (from your `final-tool-family-file.md`):
- Front matter (YAML)
- Main title & overview
- Prerequisites & tip includes
- Resource groups (Account, Container, Blob)
  - For each resource:
    - Operation name & description
    - Prerequisites
    - Example prompts
    - Parameters table
    - Success verification
    - Annotation include
- Related content section

---

## 10. Key Decisions & Assumptions

| Item | Decision |
|------|----------|
| **Tool Family Grouping** | By service area from CLI output + brand mapping |
| **Resource Grouping** | Automatic (derived from tool command structure) or config-based |
| **Parameter Extraction** | From annotations OR from CLI if available |
| **Template Engine** | Handlebars.Net (shared with CSharpGenerator) |
| **Configuration** | JSON file (`tool-family-config.json`) for metadata |
| **Independence** | No cross-dependencies; can run standalone |
| **Output Location** | `generated/tool-families/` |
| **Integration** | Optional feature in Generate-MultiPageDocs.ps1 (default: enabled) |

---

## 11. Success Criteria

- [ ] New package builds independently
- [ ] Generates one markdown file per tool family
- [ ] Each file matches `final-tool-family-file.md` format
- [ ] Files include correct annotation includes
- [ ] Parameters are extracted and formatted correctly
- [ ] Integrates cleanly into Generate-MultiPageDocs.ps1
- [ ] Works with `-ToolFamilies $true` or `$false` parameter
- [ ] All 30+ services have corresponding family files
- [ ] No duplicated data between tool families and other outputs

---

## 12. Implementation Details & Handling

### Parameter Extraction
- Extract parameters from annotation content using regex or HTML parsing
- Expected format: Parameter tables in markdown (| **Name** | Required | Description |)
- If no parameters found in annotation, leave `Parameters` list empty

### Missing Annotations
- If annotation file doesn't exist for a tool, log a warning but continue generation
- Render operation without annotation include (still include other fields like description, prerequisites, examples)
- Track missing annotations in generation log for review

### Missing Example Prompts
- If no example prompts found in annotation, leave `ExamplePrompts` list empty
- Template should handle empty list gracefully (skip "Example prompts include:" section)

### Date Field in Template
- Use current date at generation time: `{{date}}` should be formatted as `MM/DD/YYYY` or `yyyy-MM-dd` (consistent with existing docs)
- Set in `ToolFamilyDocumentationGenerator` when building `ToolFamilyData`

### Brand Mapping Fallback
- Attempt lookup in `brand-to-server-mapping.json` using namespace name as key
- If not found, fallback to `azure-{namespace}` format (e.g., namespace "storage" → "azure-storage")
- Log info message about fallback usage

---

**Ready to proceed with implementation.**
