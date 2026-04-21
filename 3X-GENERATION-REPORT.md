# Azure MCP 3.x Content Generation Report

**Date**: April 20, 2026  
**Requested By**: Dina  
**Executed By**: Quinn (DevOps/Scripts Engineer)  
**Task**: Run content generation pipeline for functions and storage namespaces against @azure/mcp 3.x

---

## Executive Summary

Successfully ran the Azure MCP documentation generation pipeline against @azure/mcp 3.0.0-beta.3. Generated step 1 output for both Functions and Storage namespaces. Identified new parameters in the 3.x API surface, including `--output` and `--runtime-version` for functions, and `--prefix` for storage blob operations. Discovered two pipeline issues: parameter filtering anomaly and CLI version mismatch in file metadata.

---

## Environment Setup

✅ **Updated @azure/mcp to 3.0.0-beta.3**
- Modified: `test-npm-azure-mcp/package.json`
- Changed: `"@azure/mcp": "^3.0.0-beta.1"` → `"@azure/mcp": "3.0.0-beta.3"`
- Verification: `npm install` completed successfully with 0 vulnerabilities

✅ **.NET Solution Build**
- Build: `dotnet build mcp-doc-generation.sln -c Debug`
- Result: Build succeeded
- Warnings: 24 (NU1510 package pruning suggestions, xUnit analyzer suggestions)
- Errors: 0

---

## Pipeline Execution Results

### Step 0 (Bootstrap) - Completed

The bootstrap phase executed successfully:
- Upstream MCP branch: `release/azure/2.x`
- Solution build completed
- Generated 235 deterministic H2 headings for 55 namespaces
- Fetched upstream documentation from GitHub
- CLI version detected: **3.0.0-beta.3+ae9594a1268710e678c0a6c9f9801212b1ee3694**

### Step 1 (Annotations, Parameters, Raw Tools)

**Functions Namespace:**
- Command: `dotnet run --project DocGeneration.PipelineRunner -- --namespace functions --steps 1 --output ./generated-functions`
- Status: ✅ Completed
- Tools Generated: 3
  - `functions language list`
  - `functions project get`
  - `functions template get`
- Output Directory: `./generated-functions/`

**Storage Namespace:**
- Command: `dotnet run --project DocGeneration.PipelineRunner -- --namespace storage --steps 1 --output ./generated-storage`
- Status: ✅ Completed
- Tools Generated: 7
  - `storage account create`
  - `storage account get`
  - `storage blob container create`
  - `storage blob container get`
  - `storage blob get`
  - `storage blob upload`
  - `storage table list`
- Output Directory: `./generated-storage/`

---

## Generated Content Analysis

### Functions Namespace - New Parameters in 3.x

**functions template get:**

NEW Parameter: `--output`
- **Description**: Output format. 'New' (default) returns all files in a single 'files' list for creating complete projects. 'Add' separates files into 'functionFiles' and 'projectFiles' with merge instructions for adding to existing projects.
- **Type**: string
- **Required**: false

NEW Parameter: `--runtime-version`
- **Description**: Optional runtime version for Java or TypeScript/JavaScript. When provided, template placeholders like {{javaVersion}} or {{nodeVersion}} are replaced automatically.
- **Type**: string
- **Required**: false

**Existing Parameters:**
- `--language` (required): Programming language for the Azure Functions project

### Storage Namespace - New Parameters in 3.x

**storage blob container get:**

Raw CLI Output includes: `--prefix` parameter
- **Description**: The prefix to filter containers when listing containers in a storage account. Only containers whose names start with the specified prefix will be listed.
- **Type**: string
- **Required**: false

⚠️ **Issue**: This parameter appears in raw CLI data but is FILTERED OUT by generation pipeline (see issue #1 below)

**storage blob get:**

Raw CLI Output includes: `--prefix` parameter
- **Description**: The prefix to filter blobs when listing blobs in a container. Only blobs whose names start with the specified prefix will be listed.
- **Type**: string
- **Required**: false

⚠️ **Issue**: Same filtering issue as above

---

## Published Documentation Comparison

### Functions (azure-functions.md)

**Published (Old) Coverage:**
- Limited to `functionapp get` command only
- Single tool category: "Get function app details"
- Shows only: function app name parameter (optional)

**3.x CLI Expansion:**
- NEW tools exposed: `functions language list`, `functions project get`, `functions template get`
- These are distinct developer-facing operations not previously documented
- NEW parameters: `--output` (2 modes), `--runtime-version`

**Gap**: Published docs do not cover the new functions development tools or new parameters

### Storage (azure-storage.md)

**Published (Old) Coverage:**
- Documents all 7 storage commands
- `container get`: Shows --account and --container parameters only

**3.x CLI Additions:**
- `container get`: Has `--prefix` parameter in raw CLI output
- `blob get`: Has `--prefix` parameter in raw CLI output

**Gap**: Published docs do NOT include the --prefix parameter (likely due to pipeline filtering)

---

## Issues Identified

### Issue 1: ⚠️ Parameter Filtering Anomaly

**Severity**: Medium  
**Component**: Parameter Generation Pipeline

**Observations**:
1. Raw CLI data (`generated-storage/cli/cli-output.json`) includes `--prefix` parameter for:
   - `storage blob container get`
   - `storage blob get`
2. Parameter generation step filters this out
3. Generated parameter files (`generated-storage/parameters/`) show only 2 params instead of 3
4. Parameter count log shows: `storage blob container get - get [ 2 params]` (should be 3)

**Root Cause**: Unknown - likely filtering logic in `ParameterGenerator.cs` treating `--prefix` as "common parameter" or applying incorrect filtering

**Impact**: Generated documentation missing optional parameters that enable filtering by prefix

**Next Steps**: Review `mcp-tools/DocGeneration.Steps.AnnotationsParametersRaw/` parameter generation code

### Issue 2: ⚠️ CLI Version Mismatch in Frontmatter

**Severity**: Low  
**Component**: File Metadata

**Observations**:
1. File frontmatter shows: `mcp-cli.version: 2.0.0-beta.38+0410ff6ade5c70a207a8e7c7a7c78be69f7f1d76`
2. Bootstrap reports: `3.0.0-beta.3+ae9594a1268710e678c0a6c9f9801212b1ee3694`
3. cli-version.json file shows correct: `3.0.0-beta.3+ae9594a1268710e678c0a6c9f9801212b1ee3694`

**Root Cause**: Version string in frontmatter appears to be cached or embedded from earlier run, not dynamically loaded from bootstrap

**Impact**: Metadata shows incorrect CLI version (cosmetic issue, but confusing)

### Issue 3: Functions Tool Coverage Expansion

**Severity**: Information  
**Component**: API Surface

**Observations**:
1. Published docs show only `functionapp get` command
2. 3.x API exposes three distinct tool families under functions:
   - `functions language list` - list supported languages
   - `functions project get` - get project scaffolding information
   - `functions template get` - get function templates
3. These appear to be new tools in the 3.x API

**Impact**: Documentation needs update to cover new tool families and their parameters

---

## Generated File Locations

```
Generated Content Structure:
├── generated-functions/
│   ├── annotations/
│   │   ├── azure-functions-development-language-list-annotations.md
│   │   ├── azure-functions-development-project-get-annotations.md
│   │   └── azure-functions-development-template-get-annotations.md
│   ├── parameters/
│   │   ├── azure-functions-development-*.md (parameter tables)
│   │   └── azure-functions-development-*.json (structured data)
│   ├── tools-raw/
│   │   └── azure-functions-development-*.md (raw tool descriptions)
│   ├── cli/
│   │   ├── cli-version.json (3.0.0-beta.3)
│   │   ├── cli-output.json (full tool catalog)
│   │   └── cli-namespace.json (namespace-specific data)
│   └── logs/
│       ├── parameters-generator.log
│       ├── annotations-generator.log
│       └── raw-tool-generator.log
│
└── generated-storage/
    └── (similar structure with 7 storage tools)
```

---

## Recommendations

1. **Fix Parameter Filtering**: Investigate why `--prefix` is filtered from storage blob operations. Add to allowed/exposed parameters if intended.

2. **Update Functions Documentation**: Publish content for new 3.x functions tools (language list, project get, template get).

3. **Add Storage Prefix Parameter**: Once filtering is fixed, update published storage docs to include --prefix parameter for container and blob listing.

4. **Version Metadata**: Verify bootstrap is correctly propagating CLI version to generated files.

5. **Test Full Pipeline**: Run steps 2-6 (example prompts, horizontal articles, composed tools, complete tools, skills relevance) to ensure full pipeline works with 3.x.

---

## Files Modified

- `test-npm-azure-mcp/package.json` - Updated @azure/mcp version

## Output Generated

- `generated-functions/` - Complete step 1 output for functions namespace
- `generated-storage/` - Complete step 1 output for storage namespace
- `3X-GENERATION-REPORT.md` - This report

---

## Appendix: Command Summary

```bash
# Update package
cd test-npm-azure-mcp
npm install

# Build solution
dotnet build mcp-doc-generation.sln -c Debug

# Run pipeline for functions
dotnet run --project mcp-tools/DocGeneration.PipelineRunner/DocGeneration.PipelineRunner.csproj \
  -- --namespace functions --steps 1 --output ./generated-functions

# Run pipeline for storage  
dotnet run --project mcp-tools/DocGeneration.PipelineRunner/DocGeneration.PipelineRunner.csproj \
  -- --namespace storage --steps 1 --output ./generated-storage

# Check generated parameters
ls ./generated-functions/parameters/
ls ./generated-storage/parameters/

# Verify CLI version
cat ./generated-functions/cli/cli-version.json
cat ./generated-storage/cli/cli-version.json
```

---

**Report Complete**  
Time: 2026-04-20 20:31:30 UTC
