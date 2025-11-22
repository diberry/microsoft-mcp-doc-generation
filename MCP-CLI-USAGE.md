# Azure MCP CLI Container Usage Guide

## Overview

The `run-mcp-cli.sh` (Bash) and `run-mcp-cli.ps1` (PowerShell) scripts are wrapper scripts that make it easy to run the Azure MCP CLI via Docker without needing to install .NET or clone the MCP repository locally.

## Understanding the Output

The scripts clearly separate their own output from the MCP CLI output:

```
════════════════════════════════════════════════════════════
  Azure MCP CLI Container Helper - WRAPPER SCRIPT HELP
════════════════════════════════════════════════════════════
[... wrapper script help ...]

════════════════════════════════════════════════════════════
  MCP CLI OUTPUT
════════════════════════════════════════════════════════════
[... actual MCP CLI output ...]
```

## Basic Usage

### Important: All Commands Go to MCP CLI

**Key concept**: The wrapper script has no commands of its own (except `--help`, `--build`, etc.). Everything else is passed through to the Azure MCP CLI running inside Docker.

```bash
# This runs: wrapper script help only
./run-mcp-cli.sh --help

# This runs: azmcp --help (inside Docker)
./run-mcp-cli.sh -- --help

# This runs: azmcp tools list (inside Docker)
./run-mcp-cli.sh tools list
```

### Showing Help

**Wrapper script help (doesn't call Docker):**
```bash
./run-mcp-cli.sh --help
```

**MCP CLI help (calls: azmcp --help in Docker):**
```bash
./run-mcp-cli.sh -- --help
```

### Running MCP Commands

All these examples call the MCP CLI inside Docker:

**Without separator (simpler syntax):**
```bash
# Calls: azmcp tools list
./run-mcp-cli.sh tools list

# Calls: azmcp tools list --name-only
./run-mcp-cli.sh tools list --name-only

# Calls: azmcp tools list --namespace-mode
./run-mcp-cli.sh tools list --namespace-mode
```

**With separator (explicit and clearer for --options):**
```bash
# Calls: azmcp tools list
./run-mcp-cli.sh -- tools list

# Calls: azmcp tools list --name-only
./run-mcp-cli.sh -- tools list --name-only

# Calls: azmcp --version
./run-mcp-cli.sh -- --version
```

## Wrapper Script Options

These options control the wrapper script behavior and must come BEFORE the `--` separator or MCP commands:

| Option | Description |
|--------|-------------|
| `--build` | Build the Docker image first |
| `--no-cache` | Build without using Docker cache |
| `--branch <name>` | Use specific MCP branch (default: main) |
| `--shell` | Open interactive shell in container |
| `--no-color` | Disable colored output (Bash only) |
| `--help` | Show wrapper script help |

## Examples

### Basic MCP Commands (all run inside Docker)
```bash
# List all MCP tools with full details
# Calls: azmcp tools list
./run-mcp-cli.sh tools list

# List just tool names (faster, JSON output)
# Calls: azmcp tools list --name-only
./run-mcp-cli.sh tools list --name-only

# List service namespaces (e.g., storage, keyvault, aks)
# Calls: azmcp tools list --namespace-mode
./run-mcp-cli.sh tools list --namespace-mode

# Get MCP CLI version
# Calls: azmcp --version
./run-mcp-cli.sh -- --version
```

### Building the Docker Image (wrapper option)
```bash
# Build image first, then run MCP command
# Wrapper does: docker build, then calls: azmcp tools list
./run-mcp-cli.sh --build tools list

# Force clean build (no cache), then run MCP command
# Wrapper does: docker build --no-cache, then calls: azmcp tools list
./run-mcp-cli.sh --build --no-cache tools list
```

### Using Different MCP Git Branches (wrapper option)
```bash
# Build image from a specific git branch, then call MCP CLI
# Wrapper does: docker build with MCP_BRANCH=feature-branch, then calls: azmcp tools list
./run-mcp-cli.sh --branch feature-branch tools list

# Or set via environment variable
export MCP_BRANCH=my-branch
./run-mcp-cli.sh tools list  # Uses my-branch
```

### Interactive Shell (wrapper option - no MCP command)
```bash
# Open a shell inside the Docker container (doesn't call MCP CLI)
./run-mcp-cli.sh --shell

# Then inside the container, manually run MCP CLI:
cd /mcp/servers/Azure.Mcp.Server/src
dotnet run -- tools list              # Run any MCP command
dotnet run -- --help                  # Get help
dotnet run -- tools list --name-only  # Explore options
```

### Understanding the Output
```bash
# All these produce JSON output from MCP CLI

# Full tool details (large JSON)
./run-mcp-cli.sh tools list

# Just tool names (smaller JSON, good for parsing)
./run-mcp-cli.sh tools list --name-only

# Just namespaces (e.g., ["storage", "keyvault", "aks"])
./run-mcp-cli.sh tools list --namespace-mode
```

## PowerShell Usage

The PowerShell version works the same way but uses PowerShell parameter syntax:

```powershell
# Wrapper script help
.\run-mcp-cli.ps1 -Help

# MCP CLI help
.\run-mcp-cli.ps1 -- --help

# List tools
.\run-mcp-cli.ps1 tools list

# Build first
.\run-mcp-cli.ps1 -Build tools list

# Different branch
.\run-mcp-cli.ps1 -Branch feature-branch tools list

# Interactive shell
.\run-mcp-cli.ps1 -Shell
```

## Common Workflows

### First Time Setup
```bash
# Build the image
./run-mcp-cli.sh --build

# Verify it works
./run-mcp-cli.sh -- --version
```

### Development Workflow
```bash
# Use latest MCP main branch
./run-mcp-cli.sh --build --no-cache tools list

# Work with a feature branch
./run-mcp-cli.sh --branch my-feature --build tools list
```

### Debugging
```bash
# Open shell to inspect
./run-mcp-cli.sh --shell

# Inside container, manually run commands
dotnet run -- tools list
dotnet run -- --help
```

## What Runs Where?

Understanding what executes where:

| Command | What Happens | Where It Runs |
|---------|--------------|---------------|
| `./run-mcp-cli.sh --help` | Shows wrapper script help | Host machine (your shell) |
| `./run-mcp-cli.sh --build` | Builds Docker image | Docker build (clones MCP repo) |
| `./run-mcp-cli.sh --shell` | Opens bash shell | Inside Docker container |
| `./run-mcp-cli.sh tools list` | Calls `azmcp tools list` | Inside Docker container |
| `./run-mcp-cli.sh -- --version` | Calls `azmcp --version` | Inside Docker container |

**Key insight**: Only `--help`, `--build`, `--no-cache`, `--branch`, and `--shell` are wrapper options. Everything else goes to the MCP CLI inside Docker.

## Tips

1. **Use `--` separator** when passing options that might conflict with wrapper options (like `--help`)
2. **Build once** - After the first `--build`, subsequent runs use the cached image
3. **Force rebuild** - Use `--no-cache` when you need to pull latest MCP changes
4. **Check version** - Use `./run-mcp-cli.sh -- --version` to see what MCP version is in the container
5. **All output is JSON** - MCP CLI returns structured JSON, not plain text

## Troubleshooting

### "Image not found" error
The script will automatically build the image if it doesn't exist. Or manually build:
```bash
./run-mcp-cli.sh --build
```

### Need latest MCP changes
Force a clean rebuild:
```bash
./run-mcp-cli.sh --build --no-cache
```

### Docker permission issues
Make sure your user is in the `docker` group:
```bash
sudo usermod -aG docker $USER
# Log out and back in
```

## Related Documentation

- [CLI Container Documentation](./docs/CLI-CONTAINER.md) - Technical details about the CLI container
- [Quick Start Guide](./docs/QUICK-START.md) - Getting started with the full documentation generator
- [Architecture](./docs/ARCHITECTURE.md) - System architecture overview
