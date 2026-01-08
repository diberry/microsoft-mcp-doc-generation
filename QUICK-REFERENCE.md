# Quick Reference - Updated Scripts

## Overview
All root-level scripts have been updated to use npm-based Azure MCP CLI instead of Docker-based approach.

---

## Main Orchestrators

### Linux/Mac: `run-docker.sh`
```bash
# Generate CLI output and documentation
./run-docker.sh

# Documentation only (if CLI files already exist)
./run-docker.sh --skip-cli-generation

# Build docs image without running
./run-docker.sh --build-only

# Interactive debugging
./run-docker.sh --interactive
```

### Windows: `run-docker.ps1`
```powershell
# Generate CLI output and documentation
pwsh ./run-docker.ps1

# Documentation only (if CLI files already exist)
pwsh ./run-docker.ps1 -SkipCliGeneration

# Build docs image without running
pwsh ./run-docker.ps1 -BuildOnly

# Interactive debugging
pwsh ./run-docker.ps1 -Interactive
```

**What it does**:
1. Step 1: Generates CLI output JSON files locally using npm
2. Step 2: Runs Docker to generate documentation

---

## CLI Output Generation

### `run-mcp-cli-output.sh` (Standalone)
```bash
# Generate CLI output files
./run-mcp-cli-output.sh

# Show help
./run-mcp-cli-output.sh --help
```

**Output Files**:
- `generated/cli/cli-output.json`
- `generated/cli/cli-namespace.json`
- `generated/cli/cli-version.json`

**What it does**:
- Calls Get-McpCliOutput.ps1 internally
- Uses npm in test-npm-azure-mcp package
- Generates clean JSON files ready for documentation generation

---

## Documentation Generation

### `run-content-generation-output.sh` (Docs Only)
```bash
# Generate documentation from existing CLI files
./run-content-generation-output.sh

# Skip building Docker image
./run-content-generation-output.sh --skip-build

# Interactive shell (debugging)
./run-content-generation-output.sh --interactive

# Rebuild Docker image (clears cache)
./run-content-generation-output.sh --no-cache
```

**Requirements**:
- CLI files must exist in `generated/cli/`
- Run `./run-mcp-cli-output.sh` first if missing

**What it does**:
- Validates CLI output files exist
- Builds or uses existing Docker image
- Generates ~591 markdown documentation files

---

## Direct CLI Access

### `run-mcp-cli.sh` (CLI Wrapper)
```bash
# List all tools
./run-mcp-cli.sh tools list

# Show CLI version
./run-mcp-cli.sh -- --version

# Show CLI help
./run-mcp-cli.sh -- --help

# Install dependencies
./run-mcp-cli.sh --install

# Show script help
./run-mcp-cli.sh --help
```

**What it does**:
- Provides easy access to `npx azmcp` commands
- Automatically installs npm dependencies if needed
- Runs from `test-npm-azure-mcp` directory

---

## Complete Workflow

### Full Pipeline (Everything from Scratch)
```bash
# 1. Generate CLI output
./run-mcp-cli-output.sh

# 2. Generate documentation
./run-docker.sh --skip-cli-generation

# OR in one command
./run-docker.sh    # Does both steps
```

### Fastest (Reusing Existing Files)
```bash
# Just regenerate docs (if CLI files haven't changed)
./run-docker.sh --skip-cli-generation
```

### Debugging
```bash
# Check CLI commands directly
./run-mcp-cli.sh tools list

# Debug documentation generation
./run-content-generation-output.sh --interactive
```

---

## Key Changes from Previous Version

| Aspect | Before | After |
|--------|--------|-------|
| CLI Generation | Docker build + dotnet | npm scripts |
| Branch Parameter | `--branch main` | Removed (uses npm version) |
| Speed | ~10 minutes | ~30 seconds |
| Dependencies | MCP source code required | npm package only |
| Docker Used | Both CLI + Docs | Docs only |
| Effort | Build from scratch each time | Reuse npm package |

---

## Environment Variables

### For Generative AI Features
Create `docs-generation/.env`:
```bash
AZURE_OPENAI_ENDPOINT=https://...
AZURE_OPENAI_API_KEY=...
```

These are loaded and passed to Docker container for example prompt generation.

---

## Troubleshooting

### "Docker not installed"
- Install Docker Desktop or Docker Engine
- Docs generation requires Docker

### "PowerShell not found"
- Install: `brew install powershell` (Mac) or `choco install powershell-core` (Windows)
- Required for Get-McpCliOutput.ps1

### "npm modules not found"
```bash
cd test-npm-azure-mcp
npm install
cd ..
./run-mcp-cli-output.sh
```

### "CLI output files missing"
```bash
# Generate them
./run-mcp-cli-output.sh

# Verify files exist
ls -la generated/cli/
```

### Permissions denied on Linux/Mac
```bash
# Fix script permissions
chmod +x run-*.sh
chmod +x docs-generation/*.ps1
```

---

## Files Overview

```
Root Scripts:
├── run-docker.sh ..................... Main orchestrator (Linux/Mac)
├── run-docker.ps1 .................... Main orchestrator (Windows)
├── run-mcp-cli-output.sh ............. CLI generation only
├── run-content-generation-output.sh .. Docs generation only
└── run-mcp-cli.sh .................... Direct CLI access

Core Scripts:
├── docs-generation/
│   ├── Generate-MultiPageDocs.ps1 .... Main docs generator
│   ├── Get-McpCliOutput.ps1 ......... CLI output generator
│   └── [other generators]

npm Package:
└── test-npm-azure-mcp/
    ├── package.json ................. npm scripts
    └── node_modules/ ................ @azure/mcp CLI
```

---

## Tips & Tricks

### Speed Up Builds
```bash
# Reuse existing Docker image
./run-docker.sh --skip-cli-generation
```

### Clean Everything
```bash
# Remove Docker image (will rebuild on next run)
docker rmi azure-mcp-docgen:latest

# Remove generated files
rm -rf generated/
```

### Check Generated Files
```bash
# Count documentation files
find generated/multi-page -name "*.md" | wc -l

# Check file sizes
du -sh generated/

# View specific documentation
cat generated/multi-page/acr.md
```

### Version Information
```bash
# Check Azure MCP CLI version
./run-mcp-cli.sh -- --version

# Check Node.js/npm versions
node --version
npm --version

# Check PowerShell version
pwsh --version

# Check Docker version
docker --version
```

---

## Next Steps

1. ✅ Run: `./run-docker.sh` (Linux/Mac) or `pwsh ./run-docker.ps1` (Windows)
2. ✅ Wait for documentation generation to complete
3. ✅ Check output: `ls -la generated/multi-page/`
4. ✅ Verify files: `cat generated/multi-page/tools.md`
5. ✅ Deploy as needed

---

## Questions?

Each script has built-in help:
```bash
./run-docker.sh --help
./run-mcp-cli-output.sh --help
./run-content-generation-output.sh --help
./run-mcp-cli.sh --help
```

See `MIGRATION-SUMMARY.md` for detailed technical information.
