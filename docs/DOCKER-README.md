# Azure MCP Documentation Generator - Docker Solution

This is a **containerized solution** for generating Azure MCP documentation. Everything you need is packaged in a Docker container - no manual setup required!

## 🚀 Quick Start

### Prerequisites

- Docker Desktop or Docker Engine installed
- 8GB RAM available (4GB minimum)
- ~10GB free disk space for image and build cache

### Option 1: Using Docker Compose (Recommended)

```bash
# Clone this repository
git clone https://github.com/diberry/microsoft-mcp-doc-generation.git
cd microsoft-mcp-doc-generation

# Run the generator
docker-compose up

# Generated docs will be in ./generated/
```

That's it! The container will:
1. Clone the Microsoft/MCP repository
2. Build the MCP server
3. Build the documentation generator
4. Generate all documentation
5. Copy output to `./generated/` on your host

### Option 2: Using Docker CLI

```bash
# Build the image
docker build -t azure-mcp-docgen:latest .

# Run the generator
docker run --rm -v $(pwd)/generated:/output azure-mcp-docgen:latest

# Windows PowerShell:
# docker run --rm -v ${PWD}/generated:/output azure-mcp-docgen:latest

# Windows CMD:
# docker run --rm -v %cd%/generated:/output azure-mcp-docgen:latest
```

## 📁 Generated Output

After running, you'll find:

```
generated/
├── multi-page/              # Generated markdown documentation
│   ├── index.md            # Main index page
│   ├── common-tools.md     # Common tools documentation
│   ├── acr.md              # Azure Container Registry docs
│   ├── storage.md          # Azure Storage docs
│   └── ...                 # All other service area docs
├── cli/
│   ├── cli-output.json     # Raw CLI output data
│   └── cli-namespace.json  # Namespace information
├── namespaces.csv          # Alphabetically sorted namespaces
└── generation-summary.md   # Generation statistics
```

## 🛠️ Advanced Usage

### Custom Configuration

Mount your custom config files:

```bash
docker run --rm \
  -v $(pwd)/generated:/output \
  -v $(pwd)/my-config.json:/docs-generation/config.json:ro \
  -v $(pwd)/my-brand-mapping.json:/docs-generation/brand-to-server-mapping.json:ro \
  azure-mcp-docgen:latest
```

### Using Different MCP Branch

Build with a specific branch:

```bash
docker build \
  --build-arg MCP_BRANCH=feature-branch \
  -t azure-mcp-docgen:dev \
  .
```

### Interactive Debugging

Start a shell in the container for debugging:

```bash
# Using docker-compose (recommended)
docker-compose run --rm docgen-interactive

# Or with plain Docker
docker run --rm -it \
  -v $(pwd)/generated:/output \
  --entrypoint /bin/bash \
  azure-mcp-docgen:latest
```

Inside the container:
```bash
# Navigate to docs-generation
cd /docs-generation

# Run generation manually
pwsh ./Generate-MultiPageDocs.ps1

# Or run with custom options
pwsh ./Generate-MultiPageDocs.ps1 -Format json -CreateIndex $false
```

### Resource Limits

Adjust CPU and memory limits in `docker-compose.yml`:

```yaml
deploy:
  resources:
    limits:
      cpus: '4.0'      # Adjust based on your system
      memory: 8G       # Increase for larger workloads
```

## 🔧 Troubleshooting

### Container exits immediately

Check logs:
```bash
docker-compose logs
```

### Out of memory errors

Increase Docker memory limit:
- Docker Desktop: Settings → Resources → Memory
- Recommended: 8GB or more

### Permission issues with generated files

If files are created with wrong permissions:
```bash
# Fix ownership (Linux/Mac)
sudo chown -R $USER:$USER generated/

# Or run container as your user
docker run --rm \
  --user $(id -u):$(id -g) \
  -v $(pwd)/generated:/output \
  azure-mcp-docgen:latest
```

### Build fails

Clear Docker cache and rebuild:
```bash
docker-compose build --no-cache
```

### MCP server build errors

Check if you need a different MCP branch:
```bash
docker build --build-arg MCP_BRANCH=stable -t azure-mcp-docgen:latest .
```

## 📊 What Gets Built

The Docker image contains:

1. **Microsoft/MCP Repository** (cloned from GitHub)
   - Azure MCP Server (built)
   - All required dependencies

2. **Documentation Generator**
   - PowerShell orchestration script
   - C# generator with Handlebars templates
   - All configuration files

3. **Runtime Environment**
   - .NET 9.0 SDK
   - PowerShell 7+
   - All required tools

**Total image size:** ~2-3 GB (includes SDK and build artifacts)

## 🎯 Use Cases

### Local Development

```bash
# Generate docs locally
docker-compose up

# Edit templates or config
# Re-run to see changes
docker-compose up --build
```

### CI/CD Pipeline

See `.github/workflows/generate-docs-docker.yml` for GitHub Actions example.

Key steps:
1. Checkout repository
2. Build Docker image
3. Run container with volume mount
4. Upload artifacts

### Scheduled Generation

Add to crontab:
```bash
# Run daily at 2 AM
0 2 * * * cd /path/to/repo && docker-compose up
```

### Team Distribution

Share the Docker image:
```bash
# Save image
docker save azure-mcp-docgen:latest | gzip > azure-mcp-docgen.tar.gz

# Load on another machine
docker load < azure-mcp-docgen.tar.gz

# Or push to registry
docker tag azure-mcp-docgen:latest myregistry/azure-mcp-docgen:latest
docker push myregistry/azure-mcp-docgen:latest
```

## 🔄 Comparison with Original Workflow

| Aspect | Original | Docker Solution |
|--------|----------|-----------------|
| **Setup** | Clone 2 repos, copy folders | One docker command |
| **Dependencies** | Manual install (.NET, PowerShell, etc.) | All included in container |
| **CI/CD** | 300+ lines of workflow | 60 lines of workflow |
| **Reproducibility** | Varies by environment | 100% reproducible |
| **Maintenance** | Update multiple steps | Update one Dockerfile |
| **Distribution** | Share scripts | Share image |

## 📝 Files in This Solution

- **`Dockerfile`** - Multi-stage build definition
- **`docker-compose.yml`** - Easy orchestration for local dev
- **`.dockerignore`** - Optimizes build context
- **`.github/workflows/generate-docs-docker.yml`** - Simplified CI/CD workflow
- **`DOCKER-README.md`** - This file

## 🤝 Contributing

To modify the generator:

1. Edit files in `docs-generation/`
2. Rebuild the image: `docker-compose build`
3. Test: `docker-compose up`
4. Commit changes

The container will automatically use your updated code.

## 📦 Publishing the Image

### GitHub Container Registry

Uncomment the push steps in `.github/workflows/generate-docs-docker.yml` to automatically publish to GitHub Container Registry.

Users can then pull and run:
```bash
docker pull ghcr.io/diberry/microsoft-mcp-doc-generation/azure-mcp-docgen:latest
docker run --rm -v $(pwd)/generated:/output ghcr.io/diberry/microsoft-mcp-doc-generation/azure-mcp-docgen:latest
```

### Docker Hub

```bash
docker tag azure-mcp-docgen:latest yourusername/azure-mcp-docgen:latest
docker push yourusername/azure-mcp-docgen:latest
```

## 🎉 Benefits of This Approach

✅ **Zero Setup** - Just install Docker  
✅ **Reproducible** - Same results everywhere  
✅ **Isolated** - No conflicts with system packages  
✅ **Portable** - Share via Docker image  
✅ **Simple CI/CD** - One build, one run command  
✅ **Easy Updates** - Rebuild image with new code  
✅ **Team-Friendly** - Everyone uses same environment  

## 📚 Next Steps

1. Try it: `docker-compose up`
2. Review generated docs in `./generated/`
3. Customize templates in `docs-generation/templates/`
4. Share the image with your team
5. Add to your CI/CD pipeline

---

**Need help?** Open an issue on GitHub or check the troubleshooting section above.
