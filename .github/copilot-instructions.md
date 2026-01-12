# GitHub Copilot Instructions for Azure MCP Documentation Generator

This file provides instructions to GitHub Copilot for working with this codebase.

## Project Overview

This is the Azure MCP Documentation Generator - an automated system that generates 590+ markdown documentation files for Microsoft Azure Model Context Protocol (MCP) server tools using a containerized Docker solution.

## Architecture

### Three-Tier System

1. **Orchestration Layer** (PowerShell)
   - `docs-generation/Generate-MultiPageDocs.ps1` - Main entry point
   - Detects container vs local environment via `$env:MCP_SERVER_PATH`
   - Calls Azure MCP CLI to extract tool data
   - Invokes C# generator with JSON input

2. **Generation Layer** (C# + .NET 9.0)
   - `docs-generation/CSharpGenerator/` - Console app that processes JSON
   - Uses Handlebars.Net 2.1.6 for template processing
   - Applies configuration for filename resolution

3. **Template Layer** (Handlebars)
   - `docs-generation/templates/*.hbs` - Define documentation structure
   - `commands-template.hbs` - Main service docs
   - `annotation-template.hbs`, `parameter-template.hbs` - Includes
   - `tool-complete-template.hbs` - Complete tool documentation (NEW)

## Key Components

### PowerShell Orchestrator
**File**: `docs-generation/Generate-MultiPageDocs.ps1`

Environment detection:
```powershell
$mcpServerPath = if ($env:MCP_SERVER_PATH) { 
    $env:MCP_SERVER_PATH  # Container: /mcp/servers/Azure.Mcp.Server/src
} else { 
    "..\servers\Azure.Mcp.Server\src"  # Local
}
```

### C# Generator
**Directory**: `docs-generation/CSharpGenerator/`

Key files:
- `Program.cs` - Entry point
- `DocumentationGenerator.cs` - Core logic
  - **Parameter Count**: The parameter count displayed in console output and `generation-summary.md` represents the count of **non-common parameters only** (those shown in the parameter tables). Common parameters (like `--subscription-id`, `--resource-group`) are filtered out to match what users see in the documentation.
- `HandlebarsTemplateEngine.cs` - Template processing
- `Config.cs` - Configuration loader
- `Generators/CompleteToolGenerator.cs` - Complete tool documentation generator (NEW)
  - See `Generators/COMPLETE-TOOLS-README.md` for detailed documentation

### Configuration Files

**Three-Tier Filename Resolution** (for include files):
1. `brand-to-server-mapping.json` - Brand names (highest priority)
2. `compound-words.json` - Word transformations (medium priority)
3. Original area name (fallback)

Other configs:
- `stop-words.json` - Words removed from include filenames
- `nl-parameters.json` - Natural language parameter mappings
- `static-text-replacement.json` - Text replacements

## Docker Containerization

### Multi-Stage Build
- **Stage 1 (mcp-builder)**: Clone and build Microsoft/MCP with .NET 10
- **Stage 2 (docs-builder)**: Build documentation generators
- **Stage 3 (runtime)**: Combine all, install PowerShell 7.4.6, run generation

### Key Paths
- Container MCP: `/mcp/servers/Azure.Mcp.Server/src`
- Container output: `/output` (mounted to host `./generated/`)
- Working dir: `/docs-generation`

## Important Patterns

### Parameter Counting Logic
**Critical**: The parameter count shown in console output and `generation-summary.md` reflects only **non-common parameters** that appear in the documentation parameter tables.

- **Common parameters** (e.g., `--subscription-id`, `--resource-group`, `--tenant`, `--auth-method`, `--retry-*`) are filtered out
- **Exception**: Common parameters that are **required** for a specific tool are kept in the table
- The count matches what users see in the parameter tables in the generated `.md` files
- **Filtering occurs in**:
  - `ParameterGenerator.cs` - For parameter include files (line ~110)
  - `PageGenerator.cs` - For area pages (line ~130)
  - `DocumentationGenerator.cs` - For console summary (line ~234)
- Example: If a tool has 9 total parameters but 7 are common optional, the count shows "2 params"

### Complete Tools Feature (NEW)
**Purpose**: Generates comprehensive single-file documentation combining example prompts, parameters, and annotations

**Key Components**:
- `CompleteToolGenerator.cs` - Generator class
- `tool-complete-template.hbs` - Template with embedded content
- `--complete-tools` CLI flag - Enables generation
- Output: `./generated/tools/{tool}.complete.md` (208 files)

**Architecture**:
- Runs AFTER annotations, parameters, and example-prompts generation
- Reads and embeds content from those files (no duplication)
- Strips frontmatter before embedding
- Keeps annotations as [!INCLUDE] reference only
- See `CSharpGenerator/Generators/COMPLETE-TOOLS-README.md` for details

### Adding New Service Area
1. Add to `brand-to-server-mapping.json` if needs brand name
2. Add to `compound-words.json` if has concatenated words
3. Regenerate docs - old include files should be deleted first

### Modifying Templates
1. Edit `.hbs` file in `templates/`
2. Rebuild: `dotnet build CSharpGenerator/`
3. Regenerate: `pwsh ./Generate-MultiPageDocs.ps1`

### Filename Generation
Include files use 3-tier resolution:
- Format: `{base-filename}-{operation-parts}-{type}.md`
- Example: `azure-storage-account-get-annotations.md`
- Main service files (like `acr.md`) NOT affected by cleaning

## Common Issues

### .NET 10 SDK Missing
- **Solution**: Installed via dotnet-install.sh in Dockerfile
- Required by Microsoft/MCP global.json

### PowerShell Installation Fails
- **Solution**: Use direct .deb download, not apt repository
- Version: 7.4.6

### ParameterCount Property Missing
- **Location**: DocumentationGenerator.cs line 400
- **Solution**: Line is commented out with TODO

### Path Not Found in Container
- **Solution**: Use `$env:MCP_SERVER_PATH` for environment detection

## Output Structure

```
generated/
├── cli/
│   ├── cli-output.json          # Raw MCP CLI data
│   └── cli-namespace.json       # Namespace data
├── namespaces.csv              # CSV export
├── tools/                      # Complete tool documentation (NEW)
│   └── *.complete.md           # 208 complete tool files (--complete-tools flag)
└── multi-page/                 # 591 markdown files
    ├── *.md                    # Main service docs (30+ services)
    ├── annotations/            # Tool annotation includes (208 files)
    ├── parameters/             # Tool parameter includes (208 files)
    ├── example-prompts/        # Example prompt includes (208 files)
    └── param-and-annotation/   # Combined includes (208 files)
```

## Development Workflows

### Local Development
```bash
cd docs-generation
pwsh ./Generate-MultiPageDocs.ps1
```

### Docker Development
```bash
./run-docker.sh                 # Basic run
./run-docker.sh --no-cache      # Fresh build
./run-docker.sh --interactive   # Debug mode
./run-docker.sh --branch main   # Specific MCP branch
```

### VS Code Debugging
```bash
pwsh ./Debug-MultiPageDocs.ps1  # Prepare environment
# Then F5 in VS Code with "Debug Generate Docs" config
```

## Dependencies

- **.NET 9.0 SDK** - Generator projects
- **.NET 10.0 Preview** - MCP server (10.0.100-rc.2.25502.107)
- **PowerShell 7.4.6** - Orchestration
- **Handlebars.Net 2.1.6** - Templates (version in Directory.Packages.props)
- **Docker** - Containerization
- **Node.js 22** - Dev container

## Central Package Management

Versions defined in `Directory.Packages.props`, NOT in individual `.csproj` files:
```xml
<PackageVersion Include="Handlebars.Net" Version="2.1.6" />
```

## File Locations

### Core Files
- `Dockerfile` - Multi-stage build definition
- `docker-compose.yml` - Orchestration
- `run-docker.sh` / `run-docker.ps1` - Helper scripts

### Documentation
- `README.md` - Project overview
- `.contextdocs` - Comprehensive LLM context
- `docs/ARCHITECTURE.md` - Architecture guide
- `docs/QUICK-START.md` - 5-minute guide
- `docs-generation/README.md` - Generator details
- `docs-generation/CSharpGenerator/Generators/COMPLETE-TOOLS-README.md` - Complete tools feature (NEW)

### Workflows
- `.github/workflows/generate-docs.yml` - CI/CD (140 lines, 70% reduction)

## Key Metrics

- **Files Generated**: 800+ markdown files total
  - 591 multi-page documentation files
  - 208 complete tool files (with `--complete-tools`)
- **Service Areas**: 30+ Azure services
- **Tools Documented**: 208 tools
- **Build Time**: 10-15 min first run, 5-7 min cached
- **Docker Image**: 2.36GB
- **Workflow Reduction**: 476 → 140 lines (70% reduction)

## Code Conventions

1. **PowerShell**: Use `$env:MCP_SERVER_PATH` for environment detection
2. **C# Generator**: Follow Central Package Management (CPM)
3. **Templates**: Use Handlebars helpers for loops and conditionals
4. **Configuration**: Brand mapping > Compound words > Original name
5. **File naming**: Lowercase with hyphens for include files

## When Helping with Code

### For PowerShell Changes
- Consider both container and local environments
- Use `Push-Location` / `Pop-Location` for directory navigation
- Check `$env:MCP_SERVER_PATH` for container detection

### For C# Changes
- Follow .NET 9.0 patterns
- Use Central Package Management (no versions in .csproj)
- Update `Config.cs` for new configuration files
- Test with `dotnet build` before running
- **For new generators**: Place in `Generators/` directory, follow existing patterns
  - Use dependency injection for shared functions (brand mapping, filename cleaning)
  - Filter common parameters using `ExtractCommonParameters` unless they're required
  - Document in separate README.md file within the generator directory

### For Template Changes
- Use Handlebars syntax: `{{#each}}`, `{{#if}}`, etc.
- Test by regenerating a service area
- Check output in `generated/multi-page/`

### For Docker Changes
- Consider multi-stage build optimization
- Test with `--no-cache` for clean build
- Verify volume mounts work correctly

## Additional Context

For comprehensive architecture details, workflows, and troubleshooting:
- See `.contextdocs` in root directory
- See `docs/ARCHITECTURE.md` for visual diagrams
- See `docs/USING-CONTEXTDOCS.md` for LLM integration guide

## Last Updated

January 12, 2026
