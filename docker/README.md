# Docker Configuration

This directory contains Docker files for the Azure MCP Documentation Generator.

## Files

- **Dockerfile** - Multi-stage build for documentation generation (C# + PowerShell + templates)
- **docker-compose.yml** - Orchestration configuration for running the documentation generator

## Dockerfile

Multi-stage Docker image for generating 590+ markdown documentation files.

**Stages:**
1. **docs-builder** - Builds C# documentation generator from source
2. **runtime** - Provides PowerShell and .NET SDK environment for documentation generation

**Key Features:**
- PowerShell 7.4.6 for orchestration
- .NET 9.0 SDK for running C# generator
- Non-root user support (UID/GID mapping)
- Validates CLI output files exist before generation
- Output volume mount at `/output`

**Prerequisites:**
- CLI output files in `generated/cli/` (run `./run-mcp-cli-output.sh` first)
- Docker and Docker Compose installed

**Build:**
```bash
docker build -t azure-mcp-docgen:latest -f docker/Dockerfile .
```

## docker-compose.yml

Defines services for documentation generation and debugging.

### Services

**docgen** (default service)
- Generates 590+ markdown documentation files
- Runs automatically with `docker-compose up`
- Requires: CLI output files in `generated/cli/`
- Output: Markdown files in `/output`

**docgen-interactive** (debug profile)
- Interactive shell for development and debugging
- Mount for live code editing
- Run with: `docker-compose --profile debug run --rm docgen-interactive`

### Environment Variables

- `UID` / `GID` - Host user ID/GID (auto-detected)
- `SKIP_CLI_GENERATION` - Set to `true` (CLI files pre-generated via npm)
- `DOTNET_ROLL_FORWARD` - Set to `Major` (use latest .NET runtime)

### Resource Limits

Default configuration:
- **Limit**: 4 CPUs, 8 GB RAM
- **Reservation**: 2 CPUs, 4 GB RAM

Adjust in `docker-compose.yml` if needed.

## Usage

### Option 1: Helper Scripts (Recommended)
```bash
# Full pipeline (CLI + documentation)
./run-docker.sh

# Windows
pwsh ./run-docker.ps1

# Docs only (if CLI files already exist)
./run-content-generation-output.sh
```

### Option 2: Docker Compose

Always run from repository root:
```bash
# Generate documentation
docker-compose -f docker/docker-compose.yml up docgen

# Interactive debugging shell
docker-compose -f docker/docker-compose.yml --profile debug run --rm docgen-interactive

# View logs
docker-compose -f docker/docker-compose.yml logs -f docgen
```

### Option 3: Direct Docker Build

```bash
docker build -t azure-mcp-docgen:latest -f docker/Dockerfile .
docker run --rm \
  -v $(pwd)/generated:/output \
  -e SKIP_CLI_GENERATION=true \
  azure-mcp-docgen:latest
```

## Important Notes

1. **Run from repository root** - Always execute docker-compose from the project root, not from `docker/` directory
2. **CLI files required** - Generate CLI output first: `./run-mcp-cli-output.sh`
3. **Volume paths** - All paths use `../` prefix to access repository root from `docker/` directory
4. **User mapping** - Containers automatically run as your host user (UID/GID)
5. **No MCP source** - MCP is installed via npm package, not built in Docker

## Troubleshooting

### "Cannot find context"
```bash
cd /path/to/microsoft-mcp-doc-generation
docker-compose -f docker/docker-compose.yml up docgen
```

### "CLI output files not found"
```bash
./run-mcp-cli-output.sh
```

### Permission issues
```bash
export UID=$(id -u)
export GID=$(id -g)
docker-compose -f docker/docker-compose.yml up docgen
```

### Container exits immediately
```bash
docker-compose -f docker/docker-compose.yml logs docgen
```

## Related Files

- `../run-docker.sh` - Helper script for Docker operations
- `../run-docker.ps1` - Windows PowerShell helper
- `../run-mcp-cli-output.sh` - Generate CLI output files (npm-based)
- `../run-content-generation-output.sh` - Run docs generation only

**Last Updated**: January 2026
