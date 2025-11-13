# Azure MCP Documentation Generator

Automated documentation generator for the [Microsoft Azure Model Context Protocol (MCP)](https://github.com/Microsoft/MCP) server tools.

## 🎯 What This Does

Generates comprehensive markdown documentation for all Azure MCP server tools, including:
- Individual service documentation files (ACR, AKS, Storage, Key Vault, etc.)
- Tool annotations and parameters
- Complete command reference
- JSON/CSV data exports

**590+ documentation files** generated automatically from the MCP server code.

## 🚀 Quick Start (5 Minutes)

### Prerequisites

- Docker Desktop or Docker Engine
- 8GB RAM available
- ~10GB free disk space

### Run It

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

**Docker Compose:**
```bash
docker-compose up
```

### Get Your Docs

Documentation will be generated in `./generated/multi-page/` directory.

## 📚 Documentation

### For Users
- **[Quick Start Guide](docs/QUICK-START.md)** - Get started in 5 minutes
- **[Docker README](docs/DOCKER-README.md)** - Comprehensive Docker guide
- **[Workflow Comparison](docs/WORKFLOW-COMPARISON.md)** - Old vs new approach

### For Developers
- **[Architecture Guide](docs/ARCHITECTURE.md)** - System architecture and design
- **[Implementation Summary](docs/IMPLEMENTATION-SUMMARY.md)** - Technical details
- **[Fixes Applied](docs/FIXES-APPLIED.md)** - Build issues and solutions
- **[Generator README](docs-generation/README.md)** - Generator internals

### For LLMs (AI Assistants)
- **[.contextdocs](.contextdocs)** - Comprehensive codebase context for AI
  - Use this file when asking LLMs about the project
  - Contains architecture, workflows, and troubleshooting
  - Example: "Based on .contextdocs, explain the filename resolution system"

## 🏗️ Architecture

This project uses a **fully containerized Docker solution**:

```
┌─────────────────────────────────────────────────────────┐
│                    Docker Container                      │
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

### Key Features

✅ **No manual setup** - Everything in Docker container  
✅ **Reproducible** - Same results everywhere  
✅ **Fast** - Docker caching speeds up builds  
✅ **Simple** - One command to run  
✅ **Portable** - Works on Windows, macOS, Linux  

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
│   └── ... (590+ total files)
│
├── cli-output.json                # Raw tool data
├── cli-namespace.json             # Namespace data
├── namespaces.csv                 # CSV export
└── generation-summary.md          # Statistics
```

## 🔧 Development

### Project Structure

```
.
├── Dockerfile                     # Multi-stage build
├── docker-compose.yml             # Orchestration
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

## 📈 Metrics

- **Code Reduction**: 70% fewer lines vs original workflow (476 → 140 lines)
- **Steps Reduction**: 63% fewer steps (16 → 6 steps)
- **Files Generated**: 591 markdown documentation files
- **Services Covered**: 30+ Azure service areas
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
