# Azure MCP Documentation System Overview

**Last Updated**: November 23, 2025

## System Purpose

The Azure MCP Documentation Generator is an automated three-stage pipeline that extracts tool metadata from Microsoft Azure Model Context Protocol (MCP) servers and generates comprehensive documentation including markdown files, structured reports, and AI-generated example prompts.

## Three-Stage Pipeline

### Stage 1: MCP CLI Tool Extraction
**Script**: `run-mcp-cli-output.sh`  
**Container**: `docker/Dockerfile.mcp-cli-output`  
**Output**: `generated/cli/`

Runs the Azure MCP CLI to extract complete tool metadata as JSON:
- Tool names, descriptions, and categories
- Parameter schemas and types
- Annotations and usage hints
- Service area classifications

**Key Files Generated:**
- `cli-output.json` - Complete tool metadata
- `cli-namespace.json` - Service namespace structure
- `mcp-version.txt` - MCP server version

### Stage 2: Markdown Documentation Generation
**Script**: `run-content-generation-output.sh`  
**Container**: `docker/Dockerfile` (C# generator)  
**Output**: `generated/tools/`, `generated/annotations/`, `generated/parameters/`

Processes the JSON metadata through a C# console application with Handlebars templates to generate:
- **Main service documentation** (e.g., `acr.md`, `aks.md`) - 44 service area files
- **Include files** for parameters and annotations - 547 additional files
- **CSV exports** for spreadsheet analysis
- **Summary reports** with generation statistics

**Technology Stack:**
- .NET 9.0 SDK
- Handlebars.Net 2.1.6 templates
- Custom configuration-driven filename resolution

### Stage 3: AI Example Prompt Generation
**Script**: `run-generative-ai-output.sh`  
**Container**: `docker/Dockerfile` (with AI integration)  
**Output**: `generated/example-prompts/`

Uses Azure OpenAI or GitHub Models to generate realistic usage examples for each tool:
- Contextual example prompts
- Multi-scenario coverage
- Natural language queries
- Best practice demonstrations

**Requirements:**
- `.env` file with AI service credentials
- Configured AI endpoint and model

## Architecture Components

### Orchestration Layer (PowerShell + Bash)
- `getting-started.sh` - Interactive three-step guide
- `run-mcp-cli-output.sh` - Stage 1 orchestrator
- `run-content-generation-output.sh` - Stage 2 orchestrator
- `run-generative-ai-output.sh` - Stage 3 orchestrator
- `Generate-MultiPageDocs.ps1` - PowerShell generator entry point
- `Get-McpCliOutput.ps1` - CLI extraction entry point

### Generation Layer (C# + .NET)
**Directory**: `docs-generation/CSharpGenerator/`

Core components:
- `Program.cs` - Entry point and CLI argument parsing
- `DocumentationGenerator.cs` - Main generation logic
- `HandlebarsTemplateEngine.cs` - Template processing
- `Config.cs` - Configuration management
- `OptionsDiscovery.cs` - Parameter metadata extraction

### Template Layer (Handlebars)
**Directory**: `docs-generation/templates/`

Templates define documentation structure:
- `tool-family-page.hbs` - Main service documentation
- `annotation-template.hbs` - Tool annotation includes
- `parameter-template.hbs` - Parameter reference includes
- Supporting partials for reusable components

### Configuration Files
**Directory**: `docs-generation/`

Three-tier filename resolution:
1. `brand-to-server-mapping.json` - Brand name mappings (highest priority)
2. `compound-words.json` - Word transformations (medium priority)
3. Original area name (fallback)

Additional configs:
- `stop-words.json` - Words excluded from include filenames
- `nl-parameters.json` - Natural language parameter mappings
- `static-text-replacement.json` - Text replacement rules
- `config.json` - General configuration

## Docker Containerization

### Multi-Stage Build Strategy

**Stage 1 (mcp-builder)**: Build MCP Server
- Uses .NET 10.0 Preview SDK
- Clones microsoft/mcp repository
- Builds Azure.Mcp.Server

**Stage 2 (docs-builder)**: Build Documentation Generators
- Uses .NET 9.0 SDK
- Builds C# generator projects
- Installs dependencies

**Stage 3 (runtime)**: Combined Runtime
- Merges MCP server + generators
- Installs PowerShell 7.4.6
- Sets up execution environment

### User Mapping & Permissions

**Problem Solved**: Containers previously ran as root (UID 0), creating files owned by root on the host.

**Solution**: All containers now run as the host user via:
```dockerfile
ARG USER_ID=1000
ARG GROUP_ID=1000
RUN groupadd -g ${GROUP_ID} vscode && \
    useradd -m -u ${USER_ID} -g ${GROUP_ID} vscode
USER vscode
```

**Benefits:**
- No sudo required for file cleanup
- Files owned by host user, not root
- Better security (non-root execution)
- Cross-platform compatible (Linux, macOS, WSL)

### Volume Mounts

All containers mount:
- `./generated:/output` - Output directory for generated files
- Container-specific working directories for source code

## Output Structure

```
generated/
├── cli/                          # Stage 1: CLI extraction
│   ├── cli-output.json          # Raw tool metadata
│   ├── cli-namespace.json       # Service namespaces
│   └── mcp-version.txt          # MCP version
├── tools/                        # Stage 2: Main documentation
│   ├── acr.md                   # Azure Container Registry
│   ├── aks.md                   # Azure Kubernetes Service
│   ├── appconfig.md             # App Configuration
│   └── ... (44 service files)
├── annotations/                  # Stage 2: Tool annotations
│   └── *.md                     # 200+ annotation includes
├── parameters/                   # Stage 2: Tool parameters
│   └── *.md                     # 200+ parameter includes
├── param-and-annotation/         # Stage 2: Combined includes
│   └── *.md                     # 100+ combined includes
├── example-prompts/              # Stage 3: AI examples
│   └── *.md                     # AI-generated prompts
├── logs/                         # Generation logs
│   └── *.log
├── namespaces.csv               # CSV export
├── generation-summary.md        # Statistics report
└── reports/
    └── tools-metadata-report.md     # Comprehensive metadata
```

## Key Metrics

- **Total Files Generated**: 591 markdown files
- **Service Areas**: 44 Azure services
- **Docker Build Time**: 10-15 min (first run), 5-7 min (cached)
- **Docker Image Size**: ~2.36GB
- **Technology Stack**: .NET 9.0, .NET 10.0, PowerShell 7.4.6, Node.js 22

## Parameter Counting Logic

**Important**: The parameter count in console output and `generation-summary.md` shows only **non-common parameters** that appear in documentation tables.

**Common parameters excluded** (defined in `docs-generation/common-parameters.json`):
- `--tenant`, `--subscription`, `--auth-method`, `--resource-group`
- `--retry-delay`, `--retry-max-delay`, `--retry-max-retries`, `--retry-mode`, `--retry-network-timeout`

**Exception**: If `--resource-group` or any other common parameter is **required** for a specific tool, it MUST appear in the parameter table.
- `--subscription-id`
- `--resource-group`
- `--output`
- `--tenant`
- Other universal Azure parameters

**Example**: A tool with 9 total parameters but 7 common ones displays "2 params" in reports, matching what users see in the generated documentation.

## Development Workflows

### Local Development
```bash
cd docs-generation
pwsh ./Generate-MultiPageDocs.ps1
```

### Docker Development
```bash
./getting-started.sh          # Interactive guided workflow
# OR run stages individually:
./run-mcp-cli-output.sh       # Stage 1
./run-content-generation-output.sh  # Stage 2
./run-generative-ai-output.sh # Stage 3 (requires .env)
```

### Docker Build Options
```bash
./run-docker.sh --no-cache    # Fresh build
./run-docker.sh --interactive # Debug mode
./run-docker.sh --branch main # Specific MCP branch
```

### VS Code Debugging
```bash
pwsh ./Debug-MultiPageDocs.ps1  # Prepare environment
# Then F5 with "Debug Generate Docs" launch config
```

## Dependencies

### Runtime Dependencies
- **.NET 9.0 SDK** - Documentation generators
- **.NET 10.0 Preview** - MCP server (10.0.100-rc.2.25502.107)
- **PowerShell 7.4.6** - Script orchestration
- **Docker** - Containerization
- **Node.js 22** - Dev container environment

### NuGet Packages (Central Package Management)
- **Handlebars.Net 2.1.6** - Template engine
- Versions defined in `Directory.Packages.props`
- Not in individual `.csproj` files

## Best Practices

### 1. Never Use Sudo
```bash
# ❌ WRONG - Breaks user mapping
sudo ./run-docker.sh

# ✅ CORRECT - Run as normal user
./run-docker.sh
```

Scripts detect sudo and exit with an error. User mapping ensures proper permissions without elevation.

### 2. Environment Detection
PowerShell scripts auto-detect container vs local:
```powershell
$mcpServerPath = if ($env:MCP_SERVER_PATH) { 
    $env:MCP_SERVER_PATH  # Container
} else { 
    "../servers/Azure.Mcp.Server/src"  # Local
}
```

### 3. Adding New Service Areas
1. Add brand mapping to `brand-to-server-mapping.json` if needed
2. Add compound word rules to `compound-words.json` if needed
3. Regenerate documentation
4. Old include files are automatically cleaned up

### 4. Modifying Templates
1. Edit `.hbs` file in `templates/`
2. Rebuild: `dotnet build CSharpGenerator/`
3. Regenerate: `pwsh ./Generate-MultiPageDocs.ps1`

## Common Issues & Solutions

### Issue: .NET 10 SDK Not Found
**Solution**: Installed via `dotnet-install.sh` in Dockerfile. Required by Microsoft/MCP `global.json`.

### Issue: PowerShell Installation Fails
**Solution**: Use direct `.deb` download, not apt repository. Version 7.4.6 required.

### Issue: Permission Denied on Cleanup
**Solution**: Never run with sudo. Scripts use user mapping to ensure proper ownership.

### Issue: Container Path Not Found
**Solution**: Use `$env:MCP_SERVER_PATH` for environment detection in PowerShell scripts.

## CI/CD Integration

**GitHub Actions Workflow**: `.github/workflows/generate-docs.yml`

Automated documentation generation on:
- Pull requests
- Pushes to main branch
- Manual workflow dispatch

**Key Features:**
- 70% reduction in workflow size (476 → 140 lines)
- Leverages Docker containerization
- Automated artifact uploads
- Branch-specific MCP builds

## File Naming Conventions

### Include Files
**Format**: `{base-filename}-{operation-parts}-{type}.md`

**Three-tier resolution:**
1. Brand mapping (e.g., "acr" for Azure Container Registry)
2. Compound words (e.g., "appconfig" for App Configuration)
3. Original area name (fallback)

**Example**: `azure-storage-account-get-annotations.md`

### Main Service Files
**Format**: `{service-abbreviation}.md`

Not affected by include file cleaning logic.

**Example**: `acr.md`, `aks.md`, `appconfig.md`

## Monitoring & Logging

### Console Output
- Colored progress indicators
- Real-time status updates
- Error messages with troubleshooting hints
- User ID verification logging

### Log Files
**Directory**: `generated/logs/`

- Generation timestamps
- Error stack traces
- Performance metrics
- Diagnostic information

### Summary Reports
- `generation-summary.md` - Statistics and metrics
- `reports/tools-metadata-report.md` - Comprehensive tool listing
- `namespaces.csv` - Spreadsheet-compatible export

## Security Considerations

1. **Non-Root Execution** - All containers run as non-root user
2. **User Mapping** - Files owned by host user, not root
3. **No Sudo Required** - Eliminates need for elevated permissions
4. **Credential Management** - AI keys stored in `.env` (gitignored)
5. **Volume Isolation** - Only `./generated/` mounted to containers

## Future Enhancements

- [ ] Parallel generation for improved performance
- [ ] Incremental builds (only changed tools)
- [ ] Template validation and linting
- [ ] Multi-language documentation support
- [ ] API documentation generation
- [ ] Interactive documentation browser

## Documentation Resources

- `README.md` - Project overview
- `.contextdocs` - Comprehensive LLM context
- `docs/ARCHITECTURE.md` - Visual architecture diagrams
- `docs/QUICK-START.md` - 5-minute quickstart guide
- `.github/copilot-instructions.md` - GitHub Copilot guidance

---

**System Status**: Production Ready  
**Last Major Update**: November 23, 2025  
**Maintained By**: Azure MCP Documentation Team
