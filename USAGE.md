# Azure MCP Documentation Generator - Usage Guide

This guide explains how to use the Azure MCP documentation generator system, which consists of two main steps: generating MCP CLI output files and generating documentation from those files.

## Overview

The documentation generation process is split into two independent steps:

1. **CLI Output Generation**: Clones Microsoft/MCP repository, builds the server, and generates JSON files containing tool metadata
2. **Documentation Generation**: Processes the CLI JSON files to create 590+ markdown documentation files

This separation allows you to:
- Run each step independently during development
- Reuse CLI output across multiple documentation runs
- Skip expensive MCP cloning/building when only changing documentation templates

## Prerequisites

- **Docker Desktop** or Docker Engine installed and running
- **8GB RAM** available for Docker
- **~10GB free disk space**

## Quick Start (All-in-One)

The fastest way to generate complete documentation:

```bash
./run-docker.sh
```

This automatically:
1. Builds the CLI output generator container
2. Generates CLI output files (`generated/cli/*.json`)
3. Builds the documentation generator container
4. Generates 590+ documentation markdown files (`generated/tools/*.md`)

**Output location**: `./generated/`

## Two-Step Workflow (For Development)

When developing or debugging, you can run each step independently:

### Step 1: Generate MCP CLI Output

Generate the CLI JSON files (only needs to be done once, or when MCP changes):

```bash
./run-mcp-cli-output.sh
```

**What this does:**
- Clones Microsoft/MCP repository (inside Docker container)
- Builds Azure MCP Server with .NET 10
- Runs `tools list` command to get tool metadata
- Runs `tools list --namespace-mode` to get namespace data
- Captures CLI version information
- Saves three files to `generated/cli/`:
  - `cli-output.json` - Complete tool metadata (~540KB)
  - `cli-namespace.json` - Service area namespaces (~12KB)
  - `cli-version.json` - Version and generation metadata

**Options:**
```bash
./run-mcp-cli-output.sh --no-cache              # Rebuild from scratch
./run-mcp-cli-output.sh --branch feature-name   # Use specific MCP branch
./run-mcp-cli-output.sh --build-only            # Just build, don't run
./run-mcp-cli-output.sh --skip-build            # Use existing image
```

### Step 2: Generate Documentation

Generate documentation using the pre-generated CLI files:

```bash
./run-docker.sh --skip-cli-generation
```

**What this does:**
- Validates CLI output files exist and are valid
- Builds C# documentation generator
- Processes CLI JSON with Handlebars templates
- Generates 590+ markdown documentation files
- Creates summary reports and CSV exports

**Output location**: `./generated/tools/`

## Advanced Usage

### Use Different MCP Branch

```bash
# Generate CLI output from specific MCP branch
./run-mcp-cli-output.sh --branch feature-branch

# Then generate docs
./run-docker.sh --skip-cli-generation
```

### Rebuild Everything from Scratch

```bash
# Clear Docker caches and rebuild
./run-mcp-cli-output.sh --no-cache
./run-docker.sh --no-cache
```

### Interactive Debug Mode

```bash
# Start interactive shell in documentation generator container
./run-docker.sh --interactive

# Inside container, run commands manually:
pwsh ./Generate-MultiPageDocs.ps1
```

### Local PowerShell Execution (Without Docker)

If you have the Microsoft/MCP repository cloned locally at `../servers/`, you can run PowerShell scripts directly:

```bash
# Generate CLI output locally
cd docs-generation
pwsh ./Get-McpCliOutput.ps1

# Generate documentation locally
pwsh ./Generate-MultiPageDocs.ps1
```

**Requirements for local execution:**
- .NET 9.0 SDK
- .NET 10.0 Preview SDK
- PowerShell 7.4+
- Microsoft/MCP repository cloned at `../servers/`

## Output Files

### CLI Output Files (`generated/cli/`)

| File | Size | Description |
|------|------|-------------|
| `cli-output.json` | ~540KB | Complete tool metadata with parameters |
| `cli-namespace.json` | ~12KB | Service area namespace definitions |
| `cli-version.json` | <1KB | Version, timestamp, and generation metadata |

### Documentation Files (`generated/tools/`)

- **30+ service files**: Main documentation for each Azure service (e.g., `acr.md`, `aks.md`)
- **200+ annotation files**: Tool descriptions and examples (`annotations/*.md`)
- **200+ parameter files**: Parameter details for each tool (`parameters/*.md`)
- **150+ combined files**: Combined annotations and parameters (`param-and-annotation/*.md`)

### Additional Files

- `generated/namespaces.csv` - Alphabetically sorted list of service namespaces
- `generated/generation-summary.md` - Complete generation statistics
- `generated/tool-annotations.md` - Consolidated tool annotations

## Troubleshooting

### CLI Output Generation Failed

**Error**: `❌ CLI output generation failed`

**Solutions:**
1. Check Docker logs for specific error
2. Rebuild without cache: `./run-mcp-cli-output.sh --no-cache`
3. Verify Docker has enough memory (4GB minimum)
4. Check available disk space (~5GB needed)

### CLI Output Files Not Found

**Error**: `CLI output files not found at generated/cli/`

**Solutions:**
1. Run CLI generation first: `./run-mcp-cli-output.sh`
2. Verify files exist: `ls -lh generated/cli/`
3. Check file permissions if using sudo/root Docker

### Invalid CLI Output Files

**Error**: `CLI output files are missing or invalid`

**Solutions:**
1. Regenerate CLI output: `./run-mcp-cli-output.sh --no-cache`
2. Verify JSON is valid: `jq . generated/cli/cli-output.json`
3. Check file is not empty: `wc -c generated/cli/cli-output.json`

### Documentation Generation Failed

**Error**: `❌ Documentation generation failed`

**Solutions:**
1. Verify CLI output files are valid
2. Check Docker logs for C# generator errors
3. Rebuild C# generator: `cd docs-generation && dotnet build --no-incremental`
4. Try without example prompts (requires Azure OpenAI credentials)

### Docker Permission Errors

**Error**: `Permission denied` when cleaning `generated/` directory

**Solutions:**
```bash
# Use sudo to remove Docker-created files
sudo rm -rf generated/

# Or change ownership
sudo chown -R $USER:$USER generated/
```

## GitHub Actions Usage

The GitHub Actions workflow automatically runs both steps:

1. **Trigger**: Push to main, PR, or nightly at 2:00 AM UTC
2. **Step 1**: Generate CLI output → Upload as `mcp-cli-output` artifact
3. **Step 2**: Generate documentation → Upload as `generated-docs` artifact

**Artifacts available for 30 days** after each run.

## File Locations Quick Reference

```
microsoft-mcp-doc-generation/
├── run-mcp-cli-output.sh           # CLI output generator wrapper
├── run-docker.sh                    # Documentation generator wrapper
├── Dockerfile.mcp-cli-output        # CLI output container definition
├── Dockerfile                       # Documentation generator container
├── docs-generation/
│   ├── Get-McpCliOutput.ps1        # CLI output PowerShell script
│   ├── Generate-MultiPageDocs.ps1  # Documentation PowerShell script
│   └── templates/                  # Handlebars templates
└── generated/
    ├── cli/                        # CLI output files (Step 1)
    │   ├── cli-output.json
    │   ├── cli-namespace.json
    │   └── cli-version.json
    └── tools/                      # Documentation files (Step 2)
        ├── acr.md
        ├── aks.md
        ├── annotations/
        ├── parameters/
        └── param-and-annotation/
```

## Common Workflows

### Daily Development

```bash
# Generate CLI output once (reuse for multiple doc runs)
./run-mcp-cli-output.sh

# Iterate on documentation templates
./run-docker.sh --skip-cli-generation  # Fast: ~2-3 minutes
# Edit templates...
./run-docker.sh --skip-cli-generation  # Regenerate
```

### Testing MCP Changes

```bash
# Test specific MCP branch
./run-mcp-cli-output.sh --branch feature-xyz

# Generate docs with new CLI output
./run-docker.sh --skip-cli-generation
```

### Fresh Start

```bash
# Clean everything
rm -rf generated/
docker rmi azure-mcp-cli-output:latest azure-mcp-docgen:latest

# Rebuild from scratch
./run-docker.sh
```

## Performance Notes

| Operation | Time (Cold) | Time (Cached) | Notes |
|-----------|-------------|---------------|-------|
| CLI output generation | 8-10 min | 3-5 min | Clones & builds MCP |
| Documentation generation (full) | 10-15 min | 5-7 min | Includes building docs generator |
| Documentation generation (skip CLI) | 5-7 min | 2-3 min | Uses existing CLI files |

**Tip**: Use `--skip-cli-generation` during development to iterate quickly on documentation templates without rebuilding MCP.

## Next Steps

- See [README.md](README.md) for project overview
- See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for system architecture
- See [docs/QUICK-START.md](docs/QUICK-START.md) for 5-minute quick start
- See [.github/copilot-instructions.md](.github/copilot-instructions.md) for developer notes

## Support

For issues or questions:
- GitHub Issues: https://github.com/diberry/microsoft-mcp-doc-generation/issues
- Microsoft/MCP Issues: https://github.com/Microsoft/MCP/issues
