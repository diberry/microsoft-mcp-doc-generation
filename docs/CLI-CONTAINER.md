# Azure MCP CLI Container

Lightweight Docker container for running Azure MCP Server CLI commands without the full documentation generation overhead.

## Overview

The CLI container provides direct access to the Azure MCP Server command-line interface in an isolated, reproducible environment. Unlike the full documentation generator container (2.36GB), the CLI container is optimized for size (~1-2GB) and quick startup.

## Quick Start

### Using Helper Scripts

**Bash (Linux/macOS/WSL):**
```bash
# Show help
./run-mcp-cli.sh --help

# List all tools
./run-mcp-cli.sh tools list

# Show MCP CLI help
./run-mcp-cli.sh --help
```

**PowerShell (Windows/Cross-platform):**
```powershell
# Show help
.\run-mcp-cli.ps1 -Help

# List all tools
.\run-mcp-cli.ps1 tools list

# Show MCP CLI help
.\run-mcp-cli.ps1 --help
```

### Using Docker Directly

```bash
# Build the image
docker build -f Dockerfile.cli -t azure-mcp-cli:latest .

# Run commands
docker run --rm azure-mcp-cli:latest --help
docker run --rm azure-mcp-cli:latest tools list
```

### Using Docker Compose

```bash
# Build the CLI container
docker-compose --profile cli build mcp-cli

# Run commands
docker-compose --profile cli run --rm mcp-cli --help
docker-compose --profile cli run --rm mcp-cli tools list
docker-compose --profile cli run --rm mcp-cli tools list --name-only
```

## Container Details

### Base Image
- `mcr.microsoft.com/dotnet/sdk:9.0`

### Installed Components
- Git (for cloning MCP repository)
- wget (for downloading .NET 10 SDK)
- .NET 10.0 Preview SDK (required by MCP global.json)
- Azure MCP Server (built from Microsoft/MCP repository)

### Container Size
- **CLI Container**: ~1-2GB (single-stage build)
- **Full Generator**: ~2.36GB (3-stage build with PowerShell, docs tools)

### Build Args
- `MCP_BRANCH` - Which branch of Microsoft/MCP to clone (default: `main`)

### Entrypoint
The container uses a wrapper script at `/usr/local/bin/mcp-cli` that:
- Shows `--help` when no arguments are provided
- Passes all arguments directly to `dotnet run --`
- Handles proper command execution in the MCP server directory

## Common Commands

### List All Tools
```bash
./run-mcp-cli.sh tools list
```

Returns JSON with full tool information including:
- Tool ID and name
- Description
- Command syntax
- Available options/parameters

### List Tool Names Only
```bash
./run-mcp-cli.sh tools list --name-only
```

Returns simplified JSON with just tool names:
```json
{
  "status": 200,
  "message": "Success",
  "results": {
    "names": [
      "acr_registry_list",
      "aks_cluster_get",
      "appservice_database_add",
      ...
    ]
  }
}
```

### Show Available Commands
```bash
./run-mcp-cli.sh --help
```

Displays all top-level commands and their descriptions.

### Check Version
```bash
./run-mcp-cli.sh --version
```

Shows the MCP server version information.

## Helper Script Features

### Build Options
```bash
# Build fresh image before running command
./run-mcp-cli.sh --build tools list

# Build without cache
./run-mcp-cli.sh --no-cache --build tools list

# Use specific MCP branch
./run-mcp-cli.sh --branch feature-branch --build tools list

# Disable colored output (useful for CI/CD)
./run-mcp-cli.sh --no-color tools list
```

### Interactive Shell
```bash
# Open bash shell in container for debugging
./run-mcp-cli.sh --shell

# Inside the container:
cd /mcp/servers/Azure.Mcp.Server/src
dotnet run -- tools list
dotnet run -- --help
```

### Environment Variables
```bash
# Override MCP branch without --branch flag
export MCP_BRANCH=develop
./run-mcp-cli.sh --build tools list
```

## Use Cases

### 1. Testing MCP Commands
Quickly test MCP server commands without setting up local .NET environment:
```bash
./run-mcp-cli.sh tools list
./run-mcp-cli.sh acr registry list --help
./run-mcp-cli.sh get_bestpractices --help
```

### 2. CI/CD Integration
Use in automated pipelines to extract MCP metadata:
```bash
docker run --rm azure-mcp-cli:latest tools list --name-only > tools.json
```

### 3. Development Workflow
Test changes from different MCP branches:
```bash
./run-mcp-cli.sh --branch my-feature-branch --build tools list
```

### 4. Documentation Generation
Extract tool information for documentation:
```bash
./run-mcp-cli.sh tools list > tools-full.json
./run-mcp-cli.sh tools list --name-only > tools-names.json
```

## Comparison: CLI vs Full Container

| Feature | CLI Container | Full Generator |
|---------|--------------|----------------|
| **Size** | ~1-2GB | ~2.36GB |
| **Build Stages** | 1 (single-stage) | 3 (mcp-builder → docs-builder → runtime) |
| **Build Time** | ~2-3 minutes | ~10-15 minutes |
| **PowerShell** | ❌ No | ✅ Yes (7.4.6) |
| **C# Generators** | ❌ No | ✅ Yes (CSharpGenerator, etc.) |
| **Handlebars Templates** | ❌ No | ✅ Yes |
| **MCP Server CLI** | ✅ Yes | ✅ Yes |
| **Use Case** | Quick CLI access | Full documentation generation |

## Troubleshooting

### Container Won't Build
**Issue**: Build fails with git or wget errors
```bash
# Solution: Build without cache
./run-mcp-cli.sh --no-cache --build tools list
```

### .NET 10 SDK Not Found
**Issue**: Build fails with SDK version errors
```bash
# Solution: The Dockerfile.cli uses dotnet-install.sh to get .NET 10
# If it fails, check Microsoft's .NET download URLs
```

### Command Not Found
**Issue**: `./run-mcp-cli.sh: command not found`
```bash
# Solution: Make script executable
chmod +x run-mcp-cli.sh
```

### Wrong MCP Branch
**Issue**: Need to test different branch
```bash
# Solution: Rebuild with specific branch
./run-mcp-cli.sh --branch your-branch --no-cache --build tools list
```

### Image Already Exists
**Issue**: Want to rebuild but image exists
```bash
# Solution: Remove old image first
docker rmi azure-mcp-cli:latest
./run-mcp-cli.sh --build tools list
```

## Advanced Usage

### Custom Build Args
```bash
docker build \
  -f Dockerfile.cli \
  --build-arg MCP_BRANCH=develop \
  -t azure-mcp-cli:develop \
  .

docker run --rm azure-mcp-cli:develop tools list
```

### Save Output to File
```bash
# Save tool list to file
./run-mcp-cli.sh tools list > output/tools.json

# Save tool names to file
./run-mcp-cli.sh tools list --name-only > output/tools-names.json
```

### Integration with Full Generator
```bash
# Use CLI container to extract metadata
docker-compose --profile cli run --rm mcp-cli tools list > cli-output.json

# Then use full generator to create documentation
docker-compose up docgen
```

### Debugging Inside Container
```bash
# Open interactive shell
./run-mcp-cli.sh --shell

# Inside container, manually run commands:
cd /mcp/servers/Azure.Mcp.Server/src
dotnet run -- tools list
dotnet run -- --help
dotnet run -- get_bestpractices --resource general --action code-generation
```

## File Locations

### Container Paths
- MCP Repository: `/mcp/`
- MCP Server: `/mcp/servers/Azure.Mcp.Server/src`
- CLI Wrapper: `/usr/local/bin/mcp-cli`

### Host Paths
- Dockerfile: `./Dockerfile.cli`
- Helper Scripts: `./run-mcp-cli.sh`, `./run-mcp-cli.ps1`
- Docker Compose: `./docker-compose.yml` (profile: `cli`)

## Performance Notes

### Build Performance
- **First Build**: ~2-3 minutes (downloads .NET SDKs, clones MCP)
- **Cached Build**: ~30 seconds (uses Docker layer cache)
- **No Cache Build**: ~2-3 minutes (forces full rebuild)

### Runtime Performance
- **Container Startup**: <1 second
- **Command Execution**: Varies by command (typically 1-5 seconds)

### Optimization Tips
1. Use `--build` only when needed (image persists)
2. Use cached builds for faster iteration
3. Use `--no-cache` only when troubleshooting
4. Consider pre-building image in CI/CD pipelines

## Related Documentation

- [Main README](../README.md) - Project overview
- [Docker README](./DOCKER-README.md) - Full generator container
- [Quick Start](./QUICK-START.md) - 5-minute setup guide
- [Architecture](./ARCHITECTURE.md) - System design details

## Examples

### Extract All Tool Names
```bash
./run-mcp-cli.sh tools list --name-only | \
  jq -r '.results.names[]' > tool-names.txt
```

### Count Available Tools
```bash
./run-mcp-cli.sh tools list --name-only | \
  jq '.results.names | length'
```

### Filter Tools by Prefix
```bash
./run-mcp-cli.sh tools list --name-only | \
  jq -r '.results.names[] | select(startswith("acr_"))'
```

### Get Help for Specific Command
```bash
./run-mcp-cli.sh acr registry list --help
./run-mcp-cli.sh get_bestpractices --help
```

## Contributing

When modifying the CLI container:

1. **Update Dockerfile.cli** - Make changes to build process
2. **Test locally** - `./run-mcp-cli.sh --no-cache --build --help`
3. **Update helper scripts** - Ensure run-mcp-cli.sh/ps1 work correctly
4. **Update documentation** - This file and README.md
5. **Test with Docker Compose** - `docker-compose --profile cli build`

## License

Same as parent project. See [LICENSE](../LICENSE) for details.
