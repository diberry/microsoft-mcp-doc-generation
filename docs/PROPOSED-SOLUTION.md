# Cleaner Documentation Generation Solution

> **✅ IMPLEMENTED:** Solution 2 (Docker Container) was chosen and successfully implemented. This document is kept for historical reference showing the original analysis and decision process.
>
> See [IMPLEMENTATION-SUMMARY.md](./IMPLEMENTATION-SUMMARY.md) for complete implementation details.

## Current Problems

1. **Tight Coupling**: docs-generation must be copied into microsoft/mcp repository
2. **Complex Workflow**: Requires checking out two repos and copying folders
3. **Difficult Local Testing**: Complex setup to run locally
4. **Maintenance Burden**: Changes require coordinating two repositories

## Proposed Solutions (Choose One)

### ✅ Option 1: Standalone CLI Tool (Recommended)

Make docs-generation work independently by accepting the MCP server location as a parameter.

**Architecture:**
```
docs-generation/
├── Generate-MultiPageDocs.ps1  (modified to accept --mcp-server-path)
├── CSharpGenerator/
├── templates/
└── config files

External MCP Server:
├── Can be anywhere (local path, git clone, container)
├── Must be built: dotnet build
└── CLI accessible: dotnet run -- tools list
```

**Benefits:**
- ✅ No folder copying required
- ✅ Works with any MCP server location
- ✅ Easy local testing
- ✅ Simple CI/CD
- ✅ Can be published as standalone tool

**Usage:**
```bash
# Local development
./Generate-MultiPageDocs.ps1 -McpServerPath /path/to/mcp/servers/Azure.Mcp.Server/src

# GitHub Actions
./Generate-MultiPageDocs.ps1 -McpServerPath $GITHUB_WORKSPACE/MCP/servers/Azure.Mcp.Server/src

# With published NuGet package
dotnet tool install -g azure-mcp-docgen
azure-mcp-docgen --mcp-server-path /path/to/mcp/servers/Azure.Mcp.Server/src
```

**Changes Required:**
1. Update `Generate-MultiPageDocs.ps1` to accept `-McpServerPath` parameter
2. Remove hardcoded `../` path assumptions
3. Update GitHub workflow to remove copy step

---

### ✅ Option 2: Docker Container (Simplest for Distribution)

Package everything in a container that handles the complexity internally.

**Architecture:**
```
Dockerfile:
├── FROM mcr.microsoft.com/dotnet/sdk:9.0
├── Clone microsoft/mcp (or COPY from context)
├── Build MCP server
├── COPY docs-generation/
├── Build docs-generation
└── ENTRYPOINT: Run generation script

docker-compose.yml:
├── Build container
└── Mount ./generated as volume
```

**Benefits:**
- ✅ Zero external dependencies
- ✅ Reproducible builds
- ✅ Works anywhere Docker runs
- ✅ Perfect for CI/CD
- ✅ Easy for contributors

**Usage:**
```bash
# Local development
docker-compose up

# Manual run
docker run -v $(pwd)/output:/output azure-mcp-docgen

# GitHub Actions
docker build -t docgen .
docker run -v $GITHUB_WORKSPACE/output:/output docgen
```

**Files Required:**
1. `Dockerfile` - Multi-stage build
2. `docker-compose.yml` - Easy local development
3. `.dockerignore` - Optimize build context

---

### ✅ Option 3: Hybrid Approach (Best of Both)

Standalone CLI + Docker option for maximum flexibility.

**Architecture:**
```
docs-generation/
├── Generate-MultiPageDocs.ps1  (accepts --mcp-server-path)
├── Dockerfile                  (builds container with embedded MCP)
├── docker-compose.yml          (easy local development)
└── run-local.sh                (helper script for local dev)
```

**Benefits:**
- ✅ Flexible: Use locally or in container
- ✅ Portable: Share container image
- ✅ Developer-friendly: Multiple options

**Usage:**
```bash
# Option A: Direct local execution (requires MCP server)
./run-local.sh /path/to/mcp

# Option B: Docker (no MCP server needed)
docker-compose up

# Option C: Published tool
dotnet tool install -g azure-mcp-docgen
azure-mcp-docgen --mcp-server /path/to/mcp
```

---

## Recommended Implementation: Hybrid Approach

### Phase 1: Make Standalone (Quick Win)

**Step 1: Update Generate-MultiPageDocs.ps1**
```powershell
param(
    [string]$McpServerPath = "../servers/Azure.Mcp.Server/src",  # Default for backward compatibility
    [ValidateSet('json', 'yaml', 'both')]
    [string]$Format = 'both',
    # ... existing parameters
)

# Validate MCP server path
if (-not (Test-Path $McpServerPath)) {
    throw "MCP server path not found: $McpServerPath"
}

# Use $McpServerPath instead of hardcoded paths
Push-Location $McpServerPath
# ... rest of script
```

**Step 2: Update GitHub Workflow**
```yaml
- name: Checkout Microsoft/MCP
  uses: actions/checkout@v5
  with:
    repository: Microsoft/MCP
    path: MCP

- name: Checkout docs-generation
  uses: actions/checkout@v5
  with:
    path: docs-generation

- name: Build MCP
  working-directory: ./MCP
  run: dotnet build

- name: Generate Documentation
  working-directory: ./docs-generation/docs-generation
  run: |
    pwsh ./Generate-MultiPageDocs.ps1 -McpServerPath ../../MCP/servers/Azure.Mcp.Server/src
```

### Phase 2: Add Docker Support (Enhanced)

**Step 1: Create Dockerfile**
```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

# Install PowerShell
RUN apt-get update && apt-get install -y wget apt-transport-https software-properties-common && \
    wget -q https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb && \
    dpkg -i packages-microsoft-prod.deb && \
    apt-get update && apt-get install -y powershell

# Clone and build MCP server
WORKDIR /mcp
RUN git clone --depth 1 https://github.com/Microsoft/MCP.git . && \
    dotnet build servers/Azure.Mcp.Server/src/Azure.Mcp.Server.csproj

# Copy docs-generation
WORKDIR /docs-generation
COPY docs-generation/ .
RUN dotnet build docs-generation.sln --configuration Release

# Runtime stage
FROM mcr.microsoft.com/dotnet/sdk:9.0
RUN apt-get update && apt-get install -y powershell && rm -rf /var/lib/apt/lists/*

COPY --from=build /mcp /mcp
COPY --from=build /docs-generation /docs-generation

WORKDIR /docs-generation
VOLUME ["/output"]

ENTRYPOINT ["pwsh", "./Generate-MultiPageDocs.ps1", "-McpServerPath", "/mcp/servers/Azure.Mcp.Server/src"]
CMD ["-OutputPath", "/output"]
```

**Step 2: Create docker-compose.yml**
```yaml
version: '3.8'

services:
  docgen:
    build:
      context: .
      dockerfile: Dockerfile
    volumes:
      - ./generated:/output
    environment:
      - DOTNET_ENVIRONMENT=Development
```

**Step 3: Create run-local.sh**
```bash
#!/bin/bash
set -e

MCP_PATH="${1:-../MCP}"

if [ ! -d "$MCP_PATH" ]; then
    echo "❌ MCP path not found: $MCP_PATH"
    echo "Usage: ./run-local.sh /path/to/mcp"
    exit 1
fi

echo "✅ Using MCP at: $MCP_PATH"
echo "📦 Building MCP server..."
dotnet build "$MCP_PATH/servers/Azure.Mcp.Server/src"

echo "📦 Building docs-generation..."
dotnet build docs-generation/docs-generation.sln

echo "📝 Generating documentation..."
pwsh docs-generation/Generate-MultiPageDocs.ps1 -McpServerPath "$MCP_PATH/servers/Azure.Mcp.Server/src"

echo "✅ Documentation generated in: docs-generation/generated/"
```

---

## Comparison Matrix

| Feature | Current | Option 1 (Standalone) | Option 2 (Docker) | Option 3 (Hybrid) |
|---------|---------|----------------------|-------------------|-------------------|
| **Setup Complexity** | High | Low | Very Low | Low |
| **Local Development** | Complex | Easy | Very Easy | Very Easy |
| **CI/CD Simplicity** | Complex | Simple | Very Simple | Very Simple |
| **Distribution** | Manual | NuGet/Script | Container | Both |
| **Flexibility** | Low | High | Medium | Very High |
| **Maintenance** | High | Low | Low | Low |
| **Reproducibility** | Medium | High | Very High | Very High |

---

## Migration Path

### Immediate (1-2 hours):
1. ✅ Update `Generate-MultiPageDocs.ps1` to accept `-McpServerPath` parameter
2. ✅ Test locally with explicit path
3. ✅ Update GitHub workflow to remove copy operation

### Short-term (1 day):
1. ✅ Add Dockerfile and docker-compose.yml
2. ✅ Add run-local.sh helper script
3. ✅ Document new usage patterns

### Optional (Future):
1. ⭐ Publish as dotnet tool: `dotnet tool install -g azure-mcp-docgen`
2. ⭐ Publish Docker image: `docker pull azuremcp/docgen:latest`
3. ⭐ Add GitHub Action: `uses: azure/mcp-docgen@v1`

---

## Recommendation

**Start with Option 3 (Hybrid)** because it:
- Solves your immediate problem (no copying!)
- Provides flexibility for different use cases
- Requires minimal changes to existing code
- Can be enhanced incrementally
- Works locally AND in CI/CD

The implementation is straightforward and gives you the best of both worlds.
