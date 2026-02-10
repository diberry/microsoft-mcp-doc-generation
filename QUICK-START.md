# Azure MCP Documentation Generator - Quick Start

This guide shows you how to use the consolidated `generate.sh` script to generate Azure MCP documentation.

## Overview

The `generate.sh` script provides a single, unified interface for all documentation generation tasks. It replaces the previous separate scripts (`start.sh`, `start-only.sh`, `start-horizontal.sh`).

## Prerequisites

- **Node.js + npm** - For MCP CLI metadata extraction
- **PowerShell (pwsh)** - For orchestration scripts
- **.NET SDK** - For generator projects
- **Azure OpenAI credentials** (optional) - Required for AI-generated content
  - Set in `docs-generation/.env` file

## Quick Start

### View Available Commands

```bash
./generate.sh help
```

### Generate Everything

Generate complete documentation for all Azure MCP tools:

```bash
./generate.sh all
```

This runs the full pipeline:
1. Extracts CLI metadata from Azure MCP
2. Generates CLI analysis report
3. Creates common documentation files
4. Generates all tool documentation
5. Creates validation reports

**Duration**: 30-60 minutes depending on AI generation steps

### Generate Reports Only

Quickly generate CLI analysis and common files without generating tool documentation:

```bash
./generate.sh reports
```

**Duration**: 1-2 minutes

**Output**:
- `./generated/reports/cli-analysis-report.md`
- `./generated/common-general/common-tools.md`
- `./generated/common-general/service-start-option.md`

### Generate a Single Tool Family

Generate documentation for a specific service/namespace:

```bash
# Generate KeyVault documentation
./generate.sh family keyvault

# Generate Storage documentation
./generate.sh family storage

# Generate Advisor documentation
./generate.sh family advisor
```

**Duration**: 5-20 minutes depending on tool count and AI steps

### Generate with Specific Steps

Control which generation steps to run for a tool family:

```bash
# Run only Step 1 (annotations, parameters, raw tools)
./generate.sh family pricing --steps 1

# Run Steps 1-3 (up to example prompts)
./generate.sh family advisor --steps 1,2,3

# Run all steps (default)
./generate.sh family storage --steps 1,2,3,4,5
```

**Step breakdown**:
- **Step 1**: Annotations, parameters, raw tool files
- **Step 2**: Example prompts (AI-generated)
- **Step 3**: Composed and AI-improved tool files
- **Step 4**: Tool family assembly
- **Step 5**: Horizontal articles (how-to guides)

## Advanced Options

### Keep Existing Files

By default, `generate.sh` cleans the `./generated/` directory before running. To keep existing files:

```bash
./generate.sh family advisor --no-clean
```

## Output Location

All generated files are written to:

```
./generated/
├── cli/                    # CLI metadata (JSON files)
├── reports/                # Analysis and validation reports
├── common-general/         # Common documentation files
├── annotations/            # Tool annotation includes
├── parameters/             # Parameter documentation includes
├── example-prompts/        # Example prompt files
├── tools/                  # Complete tool documentation
├── tools-raw/             # Raw tool files (placeholders)
├── tools-composed/        # Composed tool files (with content)
├── tools-ai-improved/     # AI-enhanced tool files
├── tool-family/           # Tool family documentation
└── logs/                  # Generation logs
```

## Common Workflows

### Quick Test

```bash
# Fast test with reports only
./generate.sh reports
```

### Development Workflow

```bash
# 1. Generate a small tool family to test changes
./generate.sh family advisor --steps 1

# 2. If Step 1 looks good, add more steps
./generate.sh family advisor --steps 1,2,3 --no-clean

# 3. Full generation for final verification
./generate.sh family advisor
```

### Production Build

```bash
# Full generation for all tools
./generate.sh all
```

## Migration from Old Scripts

If you were using the old scripts, here's the mapping:

| Old Script | New Command |
|------------|-------------|
| `./start.sh` | `./generate.sh all` |
| `./start-only.sh advisor` | `./generate.sh family advisor` |
| `./start-only.sh advisor 1,2,3` | `./generate.sh family advisor --steps 1,2,3` |
| `./start-horizontal.sh` | Integrated into `./generate.sh family <name>` (Step 5) |

The old scripts have been moved to `scripts/archive/` for reference.

## Troubleshooting

### Build Errors

If you see .NET build errors, the projects may need to be built manually:

```bash
cd docs-generation
dotnet build ../docs-generation.sln
cd ..
```

**Note**: There's a known pre-existing build issue with the CliAnalyzer project (missing Reports namespace). The `reports` command works because it uses pre-built binaries.

### Missing CLI Files

If you see "CLI files not found" errors:

```bash
# Ensure you have the test-npm-azure-mcp package
cd test-npm-azure-mcp
npm install
cd ..

# Then try again
./generate.sh reports
```

### Azure OpenAI Errors

If AI-generated content fails:

1. Check that `docs-generation/.env` exists with credentials:
   ```
   FOUNDRY_API_KEY=your-key
   FOUNDRY_ENDPOINT=https://your-endpoint.openai.azure.com/
   FOUNDRY_MODEL_NAME=gpt-4o-mini
   FOUNDRY_MODEL_API_VERSION=2024-08-01-preview
   ```

2. Or skip AI steps:
   ```bash
   ./generate.sh family advisor --steps 1
   ```

## More Information

- **Detailed Architecture**: See `docs/ARCHITECTURE.md`
- **PowerShell Scripts**: See `docs/GENERATION-SCRIPTS.md`
- **C# Generators**: See `docs-generation/CSharpGenerator/docs/README.md`
- **Horizontal Articles**: See `docs-generation/HorizontalArticleGenerator/README.md`

## Getting Help

Run the help command to see all options:

```bash
./generate.sh help
```

Or check the output of any command for detailed progress information.

---

**Last Updated**: February 10, 2026
