# Quick Start Guide - Docker Solution

Get documentation generated in 5 minutes or less!

## Prerequisites

- **Docker Desktop** or **Docker Engine** installed
  - Windows/Mac: [Docker Desktop](https://www.docker.com/products/docker-desktop/)
  - Linux: [Docker Engine](https://docs.docker.com/engine/install/)
- **8GB RAM** available (4GB minimum)
- **~10GB free disk space**

## 🚀 Three Ways to Run

### Option 1: Docker Compose (Easiest) ⭐

```bash
# Clone and run
git clone https://github.com/diberry/microsoft-mcp-doc-generation.git
cd microsoft-mcp-doc-generation
docker-compose up

# Docs will be in ./generated/
```

### Option 2: Helper Script (Recommended for Regular Use)

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

### Option 3: Direct Docker Command (Most Control)

**Linux/macOS:**
```bash
docker build -t azure-mcp-docgen:latest .
docker run --rm -v $(pwd)/generated:/output azure-mcp-docgen:latest
```

**Windows PowerShell:**
```powershell
docker build -t azure-mcp-docgen:latest .
docker run --rm -v ${PWD}/generated:/output azure-mcp-docgen:latest
```

**Windows CMD:**
```cmd
docker build -t azure-mcp-docgen:latest .
docker run --rm -v %cd%/generated:/output azure-mcp-docgen:latest
```

## ✅ What You'll Get

After running (10-15 minutes on first build):

```
generated/
├── multi-page/              # 📄 Your documentation is here!
│   ├── index.md            # Main index
│   ├── common-tools.md     # Common tools
│   ├── acr.md              # Azure Container Registry
│   ├── storage.md          # Azure Storage
│   ├── keyvault.md         # Key Vault
│   └── ... (50+ more files)
├── cli/
│   ├── cli-output.json     # Raw data
│   └── cli-namespace.json  # Namespace data
├── namespaces.csv          # CSV export
└── generation-summary.md   # Statistics
```

## 🎯 Common Use Cases

### Generate Docs Locally

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

### Debug Issues

```bash
./run-docker.sh --interactive
# Inside container:
pwsh ./Generate-MultiPageDocs.ps1
```

### Just Build (Don't Run)

```bash
./run-docker.sh --build-only
```

## 🔧 Troubleshooting

### "Docker daemon is not running"

**Windows/Mac:** Start Docker Desktop  
**Linux:** `sudo systemctl start docker`

### "Out of memory" errors

Increase Docker memory:
- Docker Desktop → Settings → Resources → Memory
- Set to 8GB or more

### "Permission denied" on generated files

**Linux/macOS:**
```bash
sudo chown -R $USER:$USER generated/
```

**Or run as your user:**
```bash
docker run --rm \
  --user $(id -u):$(id -g) \
  -v $(pwd)/generated:/output \
  azure-mcp-docgen:latest
```

### Build fails with network errors

Retry with fresh build:
```bash
./run-docker.sh --no-cache
```

### Container exits immediately

Check logs:
```bash
docker logs $(docker ps -lq)
```

Or use interactive mode:
```bash
./run-docker.sh --interactive
```

## 📊 Performance

| Step | Time | Cached Time |
|------|------|-------------|
| **Clone MCP** | 2-3 min | 10 sec |
| **Build MCP** | 3-5 min | 30 sec |
| **Build Generator** | 1-2 min | 10 sec |
| **Generate Docs** | 2-3 min | 2-3 min |
| **Total** | **10-15 min** | **5-7 min** |

*Subsequent runs use Docker cache and are much faster!*

## 🎓 Next Steps

1. ✅ **View your docs:** Open `generated/multi-page/index.md`
2. 📝 **Customize templates:** Edit files in `docs-generation/templates/`
3. ⚙️ **Adjust config:** Modify `docs-generation/config.json`
4. 🔄 **Regenerate:** Run the command again
5. 📤 **Share:** Commit to Git or upload to docs site

## 💡 Tips

- **First run is slow** - Docker downloads base images (~2GB)
- **Subsequent runs are fast** - Docker caches everything
- **Use `--no-cache`** if you want completely fresh build
- **Use `--interactive`** for debugging
- **Generated files persist** - They're in `./generated/` on your host

## 🤝 Getting Help

- 📖 Full documentation: See `DOCKER-README.md`
- 🔍 Troubleshooting: See troubleshooting section above
- 🐛 Issues: Open an issue on GitHub
- 💬 Questions: Check existing issues or create a discussion

## 🎉 Success!

If you see this message, you're done:
```
✅ Documentation generated successfully!
📄 Generated XX markdown files
```

Your documentation is ready in the `generated/` directory!

---

**Total time from zero to docs:** ~15 minutes (first time), ~5 minutes (subsequent runs)
