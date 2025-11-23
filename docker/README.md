# Docker Configuration

This directory contains all Docker-related files for the Azure MCP Documentation Generator.

## Files

- **Dockerfile** - Multi-stage build for full documentation generator (C# + PowerShell + templates)
- **Dockerfile.cli** - Lightweight container for direct MCP CLI access
- **Dockerfile.mcp-cli-output** - Container for extracting CLI metadata
- **docker-compose.yml** - Orchestration configuration for all services

## Using Docker Compose

All docker-compose commands must be run from the **repository root** with the `-f docker/docker-compose.yml` flag:

```bash
# From repository root
cd /workspaces/microsoft-mcp-doc-generation

# Run documentation generator
docker-compose -f docker/docker-compose.yml up docgen

# Run CLI container (requires --profile cli)
docker-compose -f docker/docker-compose.yml --profile cli run --rm mcp-cli tools list

# Interactive debug mode (requires --profile debug)
docker-compose -f docker/docker-compose.yml --profile debug run --rm docgen-interactive
```

## Alternative: Use Helper Scripts

For easier usage, use the provided shell scripts from the repository root:

```bash
# Full documentation generation
./run-docker.sh

# CLI output only
./run-mcp-cli-output.sh

# Content generation only (requires existing CLI output)
./run-content-generation-output.sh

# Direct MCP CLI access
./run-mcp-cli.sh tools list
```

## Services

### docgen
Full documentation generator service. Generates 44+ markdown files from CLI output.

**Build context**: `..` (repository root)  
**Volumes**: `../generated:/output`

### mcp-cli (profile: cli)
Lightweight CLI container for running MCP commands directly.

**Build context**: `..` (repository root)  
**Usage**: `docker-compose -f docker/docker-compose.yml --profile cli run --rm mcp-cli [command]`

### docgen-interactive (profile: debug)
Interactive debugging service with shell access.

**Build context**: `..` (repository root)  
**Volumes**: 
- `../generated:/output` (output files)
- `../docs-generation:/docs-generation` (live code mounting)

## Important Notes

1. **Context paths**: All build contexts point to `..` (parent directory) since docker-compose.yml is in a subdirectory
2. **Volume paths**: All volume mounts use `../` prefix to access repository root
3. **Working directory**: Always run docker-compose from repository root, not from `docker/` directory
4. **User mapping**: Containers run as host user (UID/GID passed via environment variables)

## Troubleshooting

### "Cannot find context"
Make sure you're running from the repository root:
```bash
cd /workspaces/microsoft-mcp-doc-generation
docker-compose -f docker/docker-compose.yml up
```

### "Volume not found"
Volume paths are relative to the docker-compose.yml location. Ensure you're not running from the `docker/` directory.

### Permission issues
Set user ID environment variables:
```bash
export UID=$(id -u)
export GID=$(id -g)
docker-compose -f docker/docker-compose.yml up docgen
```
