# Azure MCP Documentation Generator - Usage Guide

**Current System Documentation** - This guide describes the existing three-stage documentation generation system.

## Current System

The Azure MCP documentation generator is a production-ready system that consists of three main stages:

1. **CLI Extraction** (`run-mcp-cli-output.sh`) - Extract tool metadata from MCP server
2. **Markdown Generation** (`run-content-generation-output.sh`) - Generate 44 service documentation files
3. **AI Prompts** (`run-generative-ai-output.sh`) - Create AI-generated usage examples

For a guided walkthrough, use: `./getting-started.sh`

## System Architecture

The current system uses a three-stage pipeline:

### Stage 1: CLI Extraction (`run-mcp-cli-output.sh`)
- Clones Microsoft/MCP repository
- Builds Azure MCP Server
- Extracts tool metadata as JSON
- Output: `generated/cli/*.json`

### Stage 2: Markdown Generation (`run-content-generation-output.sh`)
- Processes JSON metadata
- Uses C# generators with Handlebars templates
- Creates 44 service documentation files
- Output: `generated/tools/*.md`, include files

### Stage 3: AI Example Prompts (`run-generative-ai-output.sh`)
- Uses Azure OpenAI or GitHub Models
- Generates realistic usage examples
- Requires `.env` configuration
- Output: `generated/example-prompts/*.md`

This architecture allows:
- Independent execution of each stage
- Reuse of CLI output across multiple runs
- Quick iteration on templates without rebuilding MCP

## Prerequisites

- **Docker Desktop** or Docker Engine installed and running
- **8GB RAM** available for Docker
- **~10GB free disk space**

## Quick Start (Guided Workflow)

The easiest way to run all three stages:

```bash
./getting-started.sh
```

This interactive script guides you through:
1. **Stage 1**: CLI extraction → `run-mcp-cli-output.sh`
2. **Stage 2**: Markdown generation → `run-content-generation-output.sh`
3. **Stage 3**: AI prompts → `run-generative-ai-output.sh`

Each stage pauses for confirmation before proceeding.

**Output location**: `./generated/`

## Three-Stage Workflow (For Development)

When developing or debugging, run each stage independently:

### Stage 1: Extract MCP CLI Metadata

Generate the CLI JSON files (run once, or when MCP changes):

```bash
./run-mcp-cli-output.sh
```

**What this does:**
- Clones Microsoft/MCP repository (inside Docker container)
- Builds Azure MCP Server with .NET 10
- Runs `tools list` command to get tool metadata
- Runs `tools list --namespace-mode` to get namespace data
- Captures MCP version information
- Saves files to `generated/cli/`:
  - `cli-output.json` - Complete tool metadata
  - `cli-namespace.json` - Service area namespaces
  - `mcp-version.txt` - Version information

**Options:**
```bash
./run-mcp-cli-output.sh --no-cache              # Rebuild from scratch
./run-mcp-cli-output.sh --branch feature-name   # Use specific MCP branch
./run-mcp-cli-output.sh --build-only            # Just build, don't run
./run-mcp-cli-output.sh --skip-build            # Use existing image
```

### Stage 2: Generate Markdown Documentation

Generate documentation using the pre-generated CLI files:

```bash
./run-content-generation-output.sh
```

**What this does:**
- Validates CLI output files exist and are valid
- Builds C# documentation generator
- Processes CLI JSON with Handlebars templates
- Generates 44 service markdown files + include files
- Creates summary reports and CSV exports

**Output location**: `./generated/tools/`

### Stage 3: Generate AI Example Prompts

Generate AI-powered usage examples (requires `.env` configuration):

```bash
./run-generative-ai-output.sh
```

**What this does:**
- Reads tool metadata from Stage 1 output
- Calls Azure OpenAI or GitHub Models API
- Generates contextual usage examples
- Creates scenario-based prompts

**Output location**: `./generated/example-prompts/`

**Requirements:**
- `.env` file with AI credentials (see `.env.example`)
- Azure OpenAI or GitHub Models access

## Advanced Usage

### Use Different MCP Branch

```bash
# Generate CLI output from specific MCP branch
./run-mcp-cli-output.sh --branch feature-branch

# Then generate docs (uses existing CLI output)
./run-content-generation-output.sh
```

### Rebuild Everything from Scratch

```bash
# Clear Docker caches and rebuild all stages
./run-mcp-cli-output.sh --no-cache
./run-content-generation-output.sh --no-cache
./run-generative-ai-output.sh --no-cache
```

### Interactive Debug Mode

```bash
# Start interactive shell in content generation container
./run-content-generation-output.sh --interactive

# Inside container, run commands manually:
pwsh ./Generate-MultiPageDocs.ps1
```

### Local PowerShell Execution (Without Docker)

If you have the Microsoft/MCP repository cloned locally, you can run PowerShell scripts directly:

```bash
# Generate CLI output locally
cd docs-generation
pwsh ./Get-McpCliOutput.ps1

# Generate documentation locally
pwsh ./Generate-MultiPageDocs.ps1
```

**Requirements for local execution:**
- .NET 9.0 SDK
- .NET 10.0 Preview SDK (10.0.100-rc.2.25502.107 or later)
- PowerShell 7.4+
- Microsoft/MCP repository (path auto-detected via `$env:MCP_SERVER_PATH`)

**Note**: Local execution auto-detects whether running in container or on host system.

## Output Files

### CLI Output Files (`generated/cli/`)

| File | Description |
|------|-------------|
| `cli-output.json` | Complete tool metadata with parameters |
| `cli-namespace.json` | Service area namespace definitions |
| `mcp-version.txt` | MCP server version and build information |

### Documentation Files (`generated/tools/`)

- **44 service files**: Main documentation for each Azure service (e.g., `acr.md`, `aks.md`, `appconfig.md`)
- **Annotation files**: Tool descriptions and examples (`annotations/*.md`)
- **Parameter files**: Parameter details for each tool (`parameters/*.md`)
- **Combined files**: Merged annotations and parameters (`param-and-annotation/*.md`)

**Total**: 44 main documentation files + associated include files

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
├── docker/
│   ├── Dockerfile.mcp-cli-output   # CLI output container definition
│   ├── Dockerfile                  # Documentation generator container
│   └── Dockerfile.cli              # Lightweight CLI container
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

### First-Time Setup

```bash
# Run guided walkthrough (recommended)
./getting-started.sh
```

### Daily Development

```bash
# Generate CLI output once (reuse for multiple doc runs)
./run-mcp-cli-output.sh

# Iterate on documentation templates
./run-content-generation-output.sh  # Fast: ~2-3 minutes
# Edit templates in docs-generation/templates/
./run-content-generation-output.sh  # Regenerate
```

### Testing MCP Changes

```bash
# Test specific MCP branch
./run-mcp-cli-output.sh --branch feature-xyz

# Generate docs with new CLI output
./run-content-generation-output.sh
```

### Fresh Start

```bash
# Clean everything
rm -rf generated/
docker rmi mcp-cli-output:latest content-generation:latest

# Rebuild from scratch
./getting-started.sh
```

## Performance Notes

| Operation | Time (First Run) | Time (Cached) | Notes |
|-----------|------------------|---------------|-------|
| Stage 1: CLI extraction | 8-10 min | 3-5 min | Clones & builds MCP server |
| Stage 2: Markdown generation | 5-7 min | 2-3 min | C# generators + templates |
| Stage 3: AI prompts | 10-15 min | 10-15 min | Depends on API latency |
| **All stages** | 23-32 min | 15-23 min | Complete pipeline |

**Development Tip**: Run Stage 1 once, then iterate on Stage 2 by modifying templates. Stage 1 output rarely changes unless MCP server updates.

## Next Steps

- See [README.md](README.md) for project overview
- See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for system architecture
- See [docs/QUICK-START.md](docs/QUICK-START.md) for 5-minute quick start
- See [.github/copilot-instructions.md](.github/copilot-instructions.md) for developer notes

## Support

For issues or questions:
- GitHub Issues: https://github.com/diberry/microsoft-mcp-doc-generation/issues
- Microsoft/MCP Issues: https://github.com/Microsoft/MCP/issues
