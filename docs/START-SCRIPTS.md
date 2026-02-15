# Start Scripts - Orchestrator/Worker Pattern

This document describes the orchestrator/worker pattern used by `start.sh` and `start-only.sh` for generating documentation across all Azure MCP tool families.

## Overview

The documentation generation uses a two-script pattern:

- **`start.sh`**: Orchestrator that coordinates full catalog generation
- **`start-only.sh`**: Worker that generates documentation for a single namespace

This separation enables:
- ✅ Efficient resource usage (CLI metadata generated once)
- ✅ Clear separation of concerns
- ✅ Ability to process individual namespaces
- ✅ Future parallelization support

## Scripts

### start.sh (Orchestrator)

**Purpose**: Generate documentation for all tool families in the catalog

**What it does**:
1. Cleans previous generation output
2. Generates CLI metadata once (cli-output.json, cli-namespace.json, cli-version.json)
3. Runs brand mapping validation once (Step 0)
4. Creates output directory structure
5. Extracts all 52 namespaces from cli-namespace.json
6. Iterates over each namespace, calling `start-only.sh`
7. Tracks and reports success/failure statistics

**Usage**:
```bash
# Run all steps for all namespaces
./start.sh

# Run specific steps for all namespaces
./start.sh 1,2,3
```

**Exit Codes**:
- `0`: Success
- `1`: Validation failure or other error

**Output**:
- `generated/cli/` - CLI metadata files (shared by all namespaces)
- `generated/tool-family/` - One .md file per namespace
- Summary report with success/failure counts

### start-only.sh (Worker)

**Purpose**: Generate documentation for a single tool family/namespace

**What it does**:
1. Verifies CLI metadata files exist (fails if not found)
2. Ensures output directory structure exists
3. Calls `docs-generation/generate-tool-family.sh` for the specified namespace
4. Generates namespace-specific documentation

**Usage**:
```bash
# Generate documentation for advisor namespace
./start-only.sh advisor

# Generate with specific steps
./start-only.sh advisor 1,2,3

# Run only step 1
./start-only.sh advisor 1
```

**Prerequisites**:
- CLI metadata files must exist in `./generated/cli/`
- Run `start.sh` first or manually generate CLI metadata

**Exit Codes**:
- `0`: Success
- `1`: CLI metadata files not found or generation failure

**Output**:
- `generated/tool-family/<namespace>.md` - Single namespace documentation file

## Workflow Diagram

```
start.sh (Orchestrator)
│
├─ Generate CLI metadata (ONCE)
│  ├─ cli-version.json
│  ├─ cli-output.json
│  └─ cli-namespace.json
│
├─ Run validation (ONCE)
│  └─ Brand mapping validation
│
└─ For each namespace (52 total):
   │
   ├─ start-only.sh acr
   │  └─ generate-tool-family.sh acr
   │     └─ generated/tool-family/acr.md
   │
   ├─ start-only.sh advisor
   │  └─ generate-tool-family.sh advisor
   │     └─ generated/tool-family/advisor.md
   │
   ├─ start-only.sh aks
   │  └─ generate-tool-family.sh aks
   │     └─ generated/tool-family/aks.md
   │
   └─ ... (49 more namespaces)
```

## Key Architectural Changes

### Before (Old start-only.sh)

```bash
# Old behavior - inefficient
./start-only.sh advisor
  ├─ rm -rf generated/           # Destructive
  ├─ Generate CLI metadata       # Redundant for each call
  ├─ Run validation              # Redundant for each call
  └─ Generate advisor docs
```

**Problems**:
- ❌ Regenerates CLI metadata for every namespace
- ❌ Runs validation for every namespace
- ❌ Destructive (removes all generated files)
- ❌ Can't be orchestrated or parallelized

### After (New Orchestrator/Worker Pattern)

```bash
# New behavior - efficient
./start.sh
  ├─ Generate CLI metadata (ONCE)    # Shared by all
  ├─ Run validation (ONCE)           # Shared by all
  └─ For each namespace:
     └─ ./start-only.sh <namespace>  # Uses shared CLI files
```

**Benefits**:
- ✅ CLI metadata generated once, shared by all namespaces
- ✅ Validation runs once, not 52 times
- ✅ Non-destructive worker (can run on existing output)
- ✅ Clear orchestration point for full catalog generation
- ✅ Can process individual namespaces independently
- ✅ Future: Can parallelize namespace processing

## Available Namespaces

The system generates documentation for 52 Azure service namespaces:

```
acr, advisor, aks, appconfig, applens, applicationinsights, appservice,
azuremigrate, azureterraformbestpractices, bicepschema, cloudarchitect,
communication, compute, confidentialledger, cosmos, datadog, deploy,
eventgrid, eventhubs, fileshares, foundry, functionapp, 
get_azure_bestpractices, grafana, group, keyvault, kusto, loadtesting,
managedlustre, marketplace, monitor, mysql, policy, postgres, pricing,
quota, redis, resourcehealth, role, search, servicebus, signalr, sql,
storage, synapse, tags, validate, vm, vmsizes, vmss, webapp, webpubsub
```

## Generation Steps

Both scripts support step-based generation (default: `1,2,3,4,5`):

| Step | Description | Duration | Requires OpenAI |
|------|-------------|----------|-----------------|
| 1 | Annotations, parameters, raw tools | ~1 min | No |
| 2 | Example prompts (AI-generated) | ~10-15 min | Yes |
| 3 | Composed and AI-improved tools | ~5-10 min | Yes |
| 4 | Tool family metadata and assembly | ~3-5 min | Yes |
| 5 | Horizontal articles | ~5-10 min | Yes |

**Examples**:
```bash
# Fast generation (Step 1 only - no AI)
./start.sh 1

# Add example prompts
./start.sh 1,2

# Full pipeline (all steps)
./start.sh 1,2,3,4,5
# or just:
./start.sh
```

## Environment Requirements

### Node.js/npm
- Required for CLI metadata generation
- Used by: `test-npm-azure-mcp` package
- Generates: cli-version.json, cli-output.json, cli-namespace.json

### PowerShell
- Required for all generation steps
- Used by: `docs-generation/Generate-ToolFamily.ps1` and related scripts

### .NET SDK
- Required for building generator projects
- Used by: C# generators in `docs-generation/`

### Azure OpenAI (Optional)
- Required for Steps 2, 3, 4, 5
- Not required for Step 1 (base generation)
- Environment variables:
  - `FOUNDRY_API_KEY`
  - `FOUNDRY_ENDPOINT`
  - `FOUNDRY_MODEL_NAME`

## Testing

### Test Individual Namespace
```bash
# Ensure CLI metadata exists
mkdir -p generated/cli
cd test-npm-azure-mcp
npm run --silent get:version > ../generated/cli/cli-version.json
npm run --silent get:tools-json > ../generated/cli/cli-output.json
npm run --silent get:tools-namespace > ../generated/cli/cli-namespace.json
cd ..

# Test single namespace
./start-only.sh acr 1
```

### Test Full Catalog
```bash
# Full generation (all namespaces, all steps)
./start.sh

# Fast test (step 1 only)
./start.sh 1
```

## Output Structure

```
generated/
├── cli/
│   ├── cli-version.json         # MCP version
│   ├── cli-output.json          # All tools metadata
│   └── cli-namespace.json       # Namespace list
├── tool-family/
│   ├── acr.md                   # Per-namespace documentation
│   ├── advisor.md
│   ├── aks.md
│   └── ... (52 total)
├── tools/                       # Individual tool files
├── annotations/                 # Tool annotations
├── parameters/                  # Parameter documentation
├── example-prompts/             # AI-generated examples
└── logs/                        # Generation logs
```

## Error Handling

### start-only.sh Errors

**"CLI metadata files not found"**
```bash
# Solution: Generate CLI metadata first
cd test-npm-azure-mcp
npm run --silent get:version > ../generated/cli/cli-version.json
npm run --silent get:tools-json > ../generated/cli/cli-output.json
npm run --silent get:tools-namespace > ../generated/cli/cli-namespace.json
cd ..
```

**"Generation failed for namespace"**
- Check logs in `generated/logs/`
- Verify PowerShell and .NET are installed
- Check if namespace is valid

### start.sh Errors

**"Brand mapping validation failed"**
- Review: `generated/reports/brand-mapping-suggestions.json`
- Add missing mappings to: `docs-generation/data/brand-to-server-mapping.json`
- Re-run: `./start.sh`

**"Namespace failed"**
- Orchestrator continues to next namespace
- Failed namespaces reported at end
- Individual namespace logs in `generated/logs/`

## Performance

### Timing Estimates

| Configuration | Duration | Notes |
|--------------|----------|-------|
| Single namespace (Step 1) | ~1 min | No AI calls |
| Single namespace (Steps 1-2) | ~10-15 min | Includes AI prompts |
| Single namespace (All steps) | ~25-30 min | Full pipeline |
| All namespaces (Step 1) | ~52 min | 52 × ~1 min |
| All namespaces (All steps) | ~22-26 hours | 52 × ~25-30 min |

**Note**: Times assume sequential processing. Future parallelization could significantly reduce total time.

## Future Enhancements

### Potential Improvements
1. **Parallel Processing**: Process multiple namespaces simultaneously
2. **Incremental Updates**: Only regenerate changed namespaces
3. **Resume Support**: Continue from last failed namespace
4. **Progress Tracking**: Real-time progress indicators
5. **Namespace Filtering**: Generate specific namespace subsets

### Example: Parallel Processing (Future)
```bash
# Conceptual - not yet implemented
./start.sh --parallel 4  # Process 4 namespaces at once
```

## Last Updated

February 15, 2026
