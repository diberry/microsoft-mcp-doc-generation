# Azure MCP Documentation Generator

Automated documentation generator for the [Microsoft Azure Model Context Protocol (MCP)](https://github.com/Microsoft/MCP) server tools.

## 🎯 What This Does

Generates comprehensive markdown documentation for all Azure MCP server tools, including:
- Individual service documentation files (ACR, AKS, Storage, Key Vault, etc.)
- Tool annotations and parameters
- Complete command reference
- AI-generated example prompts for each tool
- JSON/CSV data exports

**591 documentation files** generated automatically from the MCP server code.

## 🚀 Quick Start (5 Minutes)

### Prerequisites

- Docker Desktop or Docker Engine
- 8GB RAM available
- ~10GB free disk space

### All-in-One Workflow (Recommended)

Automatically generates CLI output + documentation:

**Linux/macOS:**
```bash
git clone https://github.com/diberry/microsoft-mcp-doc-generation.git
cd microsoft-mcp-doc-generation
./run-docker.sh
```

**Windows PowerShell:**
```powershell
git clone https://github.com/diberry/microsoft-mcp-doc-generation.git
cd microsoft-mcp-doc-generation
.\run-docker.ps1
```

Documentation will be generated in `./generated/multi-page/` directory.

### Three-Stage Workflow (For Development)

For iterative development or when you want to run stages independently:

**Guided Interactive Workflow:**
```bash
./getting-started.sh
```
This script guides you through all three stages with confirmations.

**Or run stages manually:**

**Stage 1: Extract MCP CLI Metadata** (run once, or when MCP changes)
```bash
./run-mcp-cli-output.sh
```

**Stage 2: Generate Markdown Documentation** (can be run repeatedly)
```bash
./run-content-generation-output.sh
```

**Stage 3: Generate AI Example Prompts** (requires .env file)
```bash
./run-generative-ai-output.sh
```

**Output:**
- Stage 1: `generated/cli/` - CLI output files (cli-output.json, cli-namespace.json, mcp-version.txt)
- Stage 2: `generated/multi-page/` - 591 documentation markdown files
- Stage 3: `generated/example-prompts/` - AI-generated usage examples

**Note:** Stage 3 requires a `.env` file with AI service credentials (Azure OpenAI or GitHub Models).

See **[USAGE.md](docs/USAGE.md)** for detailed usage guide and troubleshooting.

### Additional Tools

#### Lightweight CLI Container (For Interactive MCP Commands)
Need to run raw MCP CLI commands interactively? Use the lightweight container:

**Linux/macOS:**
```bash
./run-mcp-cli.sh tools list
./run-mcp-cli.sh --help
```

**Windows PowerShell:**
```powershell
.\run-mcp-cli.ps1 tools list
.\run-mcp-cli.ps1 -Help
```

**Docker Compose:**
```bash
docker-compose -f docker/docker-compose.yml --profile cli run --rm mcp-cli tools list
```

See [CLI Container Guide](docs/CLI-CONTAINER.md) for details.

## 📚 Documentation

### For Users
- **[USAGE.md](USAGE.md)** - Complete usage guide with two-step workflow ⭐ **START HERE**
- **[Quick Start Guide](docs/QUICK-START.md)** - Get started in 5 minutes
- **[Docker README](docs/DOCKER-README.md)** - Full generator container guide
- **[CLI Container Guide](docs/CLI-CONTAINER.md)** - Lightweight MCP CLI container
- **[Workflow Comparison](docs/WORKFLOW-COMPARISON.md)** - Old vs new approach

### For Developers
- **[Architecture Guide](docs/ARCHITECTURE.md)** - System architecture and design
- **[Implementation Summary](docs/IMPLEMENTATION-SUMMARY.md)** - Technical details
- **[Fixes Applied](docs/FIXES-APPLIED.md)** - Build issues and solutions
- **[Generator README](docs-generation/README.md)** - Generator internals
- **[Version Capture](docs/VERSION-CAPTURE.md)** - CLI version tracking in generated docs

### For LLMs (AI Assistants)
- **[.contextdocs](.contextdocs)** - Comprehensive codebase context for AI
  - Use this file when asking LLMs about the project
  - Contains architecture, workflows, and troubleshooting
  - Example: "Based on .contextdocs, explain the filename resolution system"

## 🏗️ Architecture

This project provides **two containerized solutions**:

### Full Documentation Generator (2.36GB)
```
┌─────────────────────────────────────────────────────────┐
│          Docker Container (3-Stage Build)               │
├─────────────────────────────────────────────────────────┤
│  1. Clone Microsoft/MCP repository                      │
│  2. Build Azure MCP Server (.NET 10)                    │
│  3. Build Documentation Generator (C# + Handlebars)     │
│  4. Run PowerShell generation script                    │
│  5. Output to /output (volume mounted to ./generated/)  │
└─────────────────────────────────────────────────────────┘
         ↓
  ./generated/multi-page/
    ├── acr.md
    ├── aks.md
    ├── storage.md
    ├── keyvault.md
    └── ... (590+ files)
```

### Lightweight CLI Container (~1-2GB)
```
┌─────────────────────────────────────────────────────────┐
│          Docker Container (Single-Stage)                │
├─────────────────────────────────────────────────────────┤
│  1. Clone Microsoft/MCP repository                      │
│  2. Build Azure MCP Server (.NET 10)                    │
│  3. Provide CLI wrapper for direct command access       │
└─────────────────────────────────────────────────────────┘
         ↓
  Direct CLI access: tools list, --help, etc.
```

### Key Features

✅ **No manual setup** - Everything in Docker container  
✅ **Reproducible** - Same results everywhere  
✅ **Fast** - Docker caching speeds up builds  
✅ **Simple** - One command to run  
✅ **Portable** - Works on Windows, macOS, Linux  
✅ **Flexible** - Full generator or lightweight CLI  

## 🎮 Usage Examples

### Basic Generation

```bash
./run-docker.sh
```

### Rebuild from Scratch

```bash
./run-docker.sh --no-cache
```

### Use Different MCP Branch

```bash
./run-docker.sh --branch feature-branch
```

### Debug Mode

```bash
./run-docker.sh --interactive
```

### Build Only (No Generation)

```bash
./run-docker.sh --build-only
```

## 📂 Generated Output

```
generated/
├── multi-page/                    # 📄 Your documentation
│   ├── index.md                   # Main index
│   ├── common-tools.md            # Common tools
│   ├── azmcp-commands.md          # All commands (469KB)
│   ├── acr.md                     # Azure Container Registry
│   ├── aks.md                     # Azure Kubernetes Service
│   ├── appconfig.md               # App Configuration
│   ├── storage.md                 # Azure Storage
│   ├── keyvault.md                # Key Vault
│   ├── annotations/               # Tool annotation includes (547 files)
│   ├── parameters/                # Tool parameter includes
│   └── param-and-annotation/      # Combined includes
│
├── cli/
│   ├── cli-output.json            # Raw tool data (715KB)
│   ├── cli-namespace.json         # Namespace data
│   └── mcp-version.txt            # MCP server version
├── example-prompts/               # AI-generated usage examples
├── namespaces.csv                 # CSV export
├── generation-summary.md          # Statistics
└── logs/                          # Generation logs
```

## 🔧 Development

### Project Structure

```
.
├── docker/                        # Docker configuration
│   ├── Dockerfile                 # Multi-stage doc generator
│   ├── Dockerfile.cli             # Lightweight CLI container
│   ├── Dockerfile.mcp-cli-output  # CLI output generator
│   └── docker-compose.yml         # Container orchestration
├── run-docker.sh                  # Linux/macOS helper
├── run-docker.ps1                 # Windows helper
├── docs/                          # 📚 Documentation
│   ├── QUICK-START.md
│   ├── DOCKER-README.md
│   └── ...
├── docs-generation/               # Generator source
│   ├── CSharpGenerator/           # C# doc generator
│   ├── NaturalLanguageGenerator/  # NL processing
│   ├── templates/                 # Handlebars templates
│   └── Generate-MultiPageDocs.ps1 # Main script
└── .github/workflows/
    └── generate-docs.yml          # CI/CD automation
```

### Modifying C# Generator Code

The C# generator is built **inside the Docker image** at build time, not at runtime. If you modify any C# code in `docs-generation/CSharpGenerator/`, you **must rebuild the Docker image** to see your changes:

```bash
# Rebuild with fresh build (recommended)
./run-content-generation-output.sh --no-cache

# Or rebuild the image only
./run-content-generation-output.sh --build-only --no-cache
```

**Why `--no-cache` is important:**
- Docker caches build layers for speed
- Without `--no-cache`, Docker may reuse old cached layers with your old code
- The `--no-cache` flag forces Docker to rebuild everything from scratch

**How to verify your changes are applied:**
```bash
# Check when the DLL was last built
ls -la docs-generation/CSharpGenerator/bin/Release/net9.0/CSharpGenerator.dll

# Check when your generated files were created
ls -la generated/annotations/*.md

# If the DLL timestamp is older than your generated files, rebuild the image!
```

**What gets built when:**
- **Image build time** (Dockerfile): C# code is compiled into DLLs
- **Container runtime**: PowerShell script runs, calling the pre-built DLLs
- **Changing C# code**: Requires image rebuild
- **Changing templates/config**: No rebuild needed (mounted at runtime)

### Customizing Templates

Edit Handlebars templates in `docs-generation/templates/`:
- `commands-template.hbs` - Main service documentation
- `parameter-template.hbs` - Parameter includes
- `annotation-template.hbs` - Tool annotations
- `common-tools.hbs` - Common tools section

### Modifying Configuration

Edit `docs-generation/config.json` for:
- Brand-to-server mappings
- Compound word handling
- Stop words
- Static text replacements

## 🤖 GitHub Actions

Documentation is automatically generated:
- **Nightly** at 2:00 AM UTC
- **On push** to main branch
- **On pull requests**
- **Manually** via workflow_dispatch

Artifacts are uploaded with 30-day retention.

### Manual Trigger

1. Go to Actions tab
2. Select "Generate MCP Documentation"
3. Click "Run workflow"
4. Download artifacts when complete

## 📊 Performance

| Step | Time (First Run) | Time (Cached) |
|------|-----------------|---------------|
| Clone MCP | 2-3 min | 10 sec |
| Build MCP | 3-5 min | 30 sec |
| Build Generator | 1-2 min | 10 sec |
| Generate Docs | 2-3 min | 2-3 min |
| **Total** | **10-15 min** | **5-7 min** |

## 🐛 Troubleshooting

### Docker Issues

**Docker not running:**
```bash
# Linux
sudo systemctl start docker

# Windows/Mac - Start Docker Desktop
```

**Permission errors on generated files:**
```bash
sudo chown -R $USER:$USER generated/
# Or run with --user flag (see docs/DOCKER-README.md)
```

**Out of memory:**
- Increase Docker memory to 8GB
- Docker Desktop → Settings → Resources

### Build Issues

**Network errors during build:**
```bash
./run-docker.sh --no-cache
```

**Container exits immediately:**
```bash
docker logs $(docker ps -lq)
./run-docker.sh --interactive
```

See [FIXES-APPLIED.md](docs/FIXES-APPLIED.md) for detailed troubleshooting.

## 📊 Metrics

- **Code Reduction**: 70% fewer lines vs original workflow (476 → 140 lines)
- **Steps Reduction**: 63% fewer steps (16 → 6 steps)
- **Files Generated**: 591 markdown documentation files
- **Tools Documented**: 181 Azure MCP tools
- **Service Areas**: 44 Azure service areas
- **Docker Image**: 2.36GB (includes full SDK and MCP server)

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Test locally with `./run-docker.sh`
5. Submit a pull request

## 📝 License

This project follows the Microsoft MCP project licensing.

## 🔗 Related Projects

- [Microsoft MCP](https://github.com/Microsoft/MCP) - Main MCP repository
- [Azure MCP Server](https://github.com/Microsoft/MCP/tree/main/servers/Azure.Mcp.Server) - Azure tools server

## ⭐ Why Docker?

The original workflow required:
- Manual folder copying between repositories
- Complex 476-line GitHub Actions workflow
- 16+ setup and build steps
- Platform-specific configuration
- Difficult local testing

The Docker solution provides:
- ✅ Single command execution
- ✅ 70% less workflow code
- ✅ Perfect reproducibility
- ✅ Easy local development
- ✅ No manual dependency management

See [WORKFLOW-COMPARISON.md](docs/WORKFLOW-COMPARISON.md) for detailed comparison.

---

**Need help?** Check the [Quick Start Guide](docs/QUICK-START.md) or open an issue!
