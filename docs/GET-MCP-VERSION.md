# Get MCP Version Scripts

Simple scripts to retrieve the version number from the Microsoft Azure MCP server without generating full documentation.

## Purpose

These lightweight scripts:
- Build a minimal Docker container with the Azure MCP server
- Run `--version` command to extract version number
- Display the version in a clean format
- Cache the Docker image for faster subsequent runs

## Usage

### Linux/macOS (Bash)

```bash
# Get version from main branch
./get-mcp-version.sh

# Get version from specific branch
./get-mcp-version.sh --branch develop

# Force rebuild (e.g., to get latest version)
./get-mcp-version.sh --rebuild
```

### Windows (PowerShell)

```powershell
# Get version from main branch
.\get-mcp-version.ps1

# Get version from specific branch
.\get-mcp-version.ps1 -Branch develop

# Force rebuild
.\get-mcp-version.ps1 -Rebuild
```

## Help

```bash
./get-mcp-version.sh --help
```

```powershell
Get-Help .\get-mcp-version.ps1
```

## Example Output

```
========================================
Azure MCP Version Checker
========================================

✅ Docker is ready
✅ Using existing Docker image
WARNING: Use --rebuild to force rebuild

🔍 Retrieving MCP version...

========================================
✅ MCP Version Found
========================================

Version: 2.0.0-beta.4+12ef1fb57a0107622e25739243f59086a4900983
Branch:  main

Docker image size: 2.1GB

🎉 Done!
```

## Requirements

- Docker Desktop or Docker Engine
- ~2GB disk space for Docker image
- Internet connection (first run only)

## Performance

- **First run**: ~5-8 minutes (builds Docker image)
- **Subsequent runs**: ~5-10 seconds (uses cached image)

## Comparison with Full Generator

| Feature | get-mcp-version | run-docker |
|---------|----------------|------------|
| Purpose | Get version only | Generate docs |
| Docker image | ~2.1GB | ~2.4GB |
| First run | 5-8 min | 10-15 min |
| Cached run | 5-10 sec | 5-7 min |
| Output | Version string | 591 markdown files |

## When to Use

Use `get-mcp-version.sh`:
- ✅ Quick version check
- ✅ CI/CD version validation
- ✅ Testing different branches
- ✅ Monitoring version updates

Use `run-docker.sh`:
- ✅ Generate documentation
- ✅ Create markdown files
- ✅ Full documentation build

## Notes

- The Docker image is cached and reused between runs
- Use `--rebuild` to get the latest version from the repository
- The image name is `azure-mcp-version-checker:latest`
- You can manually remove the image with: `docker rmi azure-mcp-version-checker:latest`
