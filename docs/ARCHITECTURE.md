# Architecture Guide

Quick reference for understanding the Azure MCP Documentation Generator architecture.

## System Overview

```
┌─────────────────────────────────────────────────────────────┐
│                   Documentation Generation System            │
└─────────────────────────────────────────────────────────────┘

┌──────────────┐      ┌──────────────┐      ┌──────────────┐
│   Docker     │  OR  │    Local     │      │   GitHub     │
│  Container   │      │   Machine    │      │   Actions    │
└──────┬───────┘      └──────┬───────┘      └──────┬───────┘
       │                     │                     │
       └─────────────────────┴─────────────────────┘
                            │
                            ▼
              ┌─────────────────────────┐
              │  PowerShell Orchestrator │
              │ Generate-MultiPageDocs.ps1│
              └────────────┬────────────┘
                          │
         ┌────────────────┼────────────────┐
         ▼                ▼                ▼
  ┌──────────┐    ┌──────────────┐  ┌──────────┐
  │   MCP    │    │      C#      │  │Templates │
  │   CLI    │    │  Generator   │  │Handlebars│
  │          │    │              │  │   .hbs   │
  └────┬─────┘    └──────┬───────┘  └────┬─────┘
       │                 │               │
       │    JSON Data    │   Templates   │
       └────────►────────┴───────◄───────┘
                        │
                        ▼
              ┌─────────────────────┐
              │  Generated Docs      │
              │  590+ Markdown Files │
              └─────────────────────┘
```

## Three-Layer Architecture

### Layer 1: Orchestration (PowerShell)

**File**: `docs-generation/Generate-MultiPageDocs.ps1`

**Responsibilities**:
- Environment detection (container vs local)
- Navigate to MCP server location
- Execute Azure MCP CLI tools
- Build C# generator
- Invoke generator with data
- Copy output to final location

**Key Decision**: Detects container environment via `$env:MCP_SERVER_PATH`

```powershell
# Container: /mcp/servers/Azure.Mcp.Server/src
# Local:     ../servers/Azure.Mcp.Server/src
$mcpServerPath = if ($env:MCP_SERVER_PATH) { 
    $env:MCP_SERVER_PATH 
} else { 
    "..\servers\Azure.Mcp.Server\src" 
}
```

### Layer 2: Generation (C# + Handlebars)

**Directory**: `docs-generation/CSharpGenerator/`

**Main Components**:
1. **Program.cs** - CLI entry point, argument parsing
2. **DocumentationGenerator.cs** - Core generation engine
3. **HandlebarsTemplateEngine.cs** - Template processor
4. **Config.cs** - Configuration loader

**Process Flow**:
```
JSON Input → Parse Tools → Apply Config → Process Templates → Output Markdown
```

**Configuration Used**:
- `brand-to-server-mapping.json` - Brand names (priority 1)
- `compound-words.json` - Word transformations (priority 2)
- `stop-words.json` - Words to remove from include filenames
- `nl-parameters.json` - Natural language parameter names

### Layer 3: Templates (Handlebars)

**Directory**: `docs-generation/templates/`

**Template Types**:

| Template | Purpose | Output Location |
|----------|---------|-----------------|
| `commands-template.hbs` | Main service docs | `multi-page/*.md` |
| `annotation-template.hbs` | Tool annotations | `multi-page/annotations/*.md` |
| `parameter-template.hbs` | Tool parameters | `multi-page/parameters/*.md` |
| `param-annotation-template.hbs` | Combined | `multi-page/param-and-annotation/*.md` |
| `common-tools.hbs` | Common tools | `multi-page/common-tools.md` |
| `area-template.hbs` | Area-specific | Used within other templates |

## Data Flow

```
1. PowerShell calls MCP CLI
   └─> dotnet run -- tools list --output cli/cli-output.json

2. PowerShell calls namespace extraction
   └─> dotnet run -- tools list-namespaces --output cli/cli-namespace.json

3. PowerShell builds C# generator
   └─> dotnet build CSharpGenerator/CSharpGenerator.csproj

4. PowerShell invokes generator
   └─> dotnet run --project CSharpGenerator/ generate-docs \
       cli/cli-output.json \
       multi-page/ \
       --index \
       --common \
       --commands

5. Generator processes data
   ├─> Loads configuration files
   ├─> Parses CLI JSON
   ├─> Applies filename resolution (3-tier)
   ├─> Processes Handlebars templates
   └─> Writes markdown files

6. PowerShell copies output (Docker only)
   └─> cp -r generated/* /output/
```

## Filename Resolution (3-Tier System)

For include files only (annotations, parameters, param-and-annotation):

```
┌─────────────────────────────────────────┐
│  1. Brand Mapping (Highest Priority)    │
│     brand-to-server-mapping.json        │
│     "acr" → "azure-container-registry"  │
└──────────────┬──────────────────────────┘
               │ Not Found
               ▼
┌─────────────────────────────────────────┐
│  2. Compound Words (Medium Priority)    │
│     compound-words.json                 │
│     "eventhub" → "event-hub"            │
└──────────────┬──────────────────────────┘
               │ Not Found
               ▼
┌─────────────────────────────────────────┐
│  3. Original Name (Fallback)            │
│     Use lowercase area name as-is       │
└─────────────────────────────────────────┘
```

**Example**: Tool `azureaibestpractices get`
1. Check brand mapping → Not found
2. Check compound words → Found: "azure-ai-best-practices"
3. Result: `azure-ai-best-practices-get-annotations.md`

## Docker Architecture

### Multi-Stage Build

```
Stage 1: mcp-builder
├─ Base: .NET SDK 9.0
├─ Install .NET 10 SDK (required by MCP)
├─ Clone Microsoft/MCP
└─ Build Azure MCP Server

Stage 2: docs-builder  
├─ Base: .NET SDK 9.0
├─ Build CSharpGenerator
├─ Build NaturalLanguageGenerator
└─ Build Shared libraries

Stage 3: runtime
├─ Base: .NET SDK 9.0
├─ Install PowerShell 7.4.6
├─ Install .NET 10 SDK
├─ Copy MCP server (from Stage 1)
├─ Copy generators (from Stage 2)
├─ Copy scripts & config
└─ CMD: Run Generate-MultiPageDocs.ps1
```

### Container Execution

```
Docker Run
  │
  ├─> Mount: ./generated → /output
  ├─> Env:   MCP_SERVER_PATH=/mcp/servers/Azure.Mcp.Server/src
  ├─> Env:   DOTNET_ROLL_FORWARD=Major
  │
  └─> Execute: pwsh Generate-MultiPageDocs.ps1
      │
      ├─> Build MCP Server
      ├─> Generate CLI output
      ├─> Build generator
      ├─> Generate docs
      └─> Copy to /output
          │
          └─> Available on host at ./generated/
```

## File Organization

### Configuration Hierarchy

```
docs-generation/
├── config.json                    # Main configuration
├── brand-to-server-mapping.json   # Tier 1: Brand names
├── compound-words.json            # Tier 2: Word mappings
├── stop-words.json                # Word filtering
├── nl-parameters.json             # Parameter naming
└── static-text-replacement.json   # Text substitutions
```

**Priority**: Brand mapping > Compound words > Original name

### Output Structure

```
generated/
├── cli/
│   ├── cli-output.json          # Raw MCP CLI data
│   └── cli-namespace.json       # Namespace data
├── namespaces.csv              # CSV export
│
└── multi-page/                 # Documentation root
    ├── index.md                # Main index
    ├── common-tools.md         # Cross-service tools
    ├── azmcp-commands.md       # All commands (469KB)
    │
    ├── acr.md                  # Main service files
    ├── aks.md                  # (Not affected by cleaning)
    ├── storage.md
    ├── keyvault.md
    ├── ... (30+ service files)
    │
    ├── annotations/            # Tool annotations includes
    │   └── azure-*-annotations.md
    │
    ├── parameters/             # Tool parameters includes
    │   └── azure-*-parameters.md
    │
    └── param-and-annotation/   # Combined includes
        └── azure-*-param-annotation.md
```

## Key Dependencies

### Runtime Requirements

- **.NET 9.0 SDK** - For CSharpGenerator, NaturalLanguageGenerator, Shared
- **.NET 10.0 Preview** - For Azure MCP Server (global.json requirement)
- **PowerShell 7.4+** - For orchestration script
- **Handlebars.Net 2.1.6** - Template engine

### Central Package Management

Versions defined in `Directory.Packages.props`:
```xml
<PackageVersion Include="Handlebars.Net" Version="2.1.6" />
```

Project files reference without version:
```xml
<PackageReference Include="Handlebars.Net" />
```

## Common Patterns

### Adding a New Service Area

1. **Add brand mapping** (if needed):
   ```json
   {
     "mcpServerName": "newservice",
     "brandName": "Azure New Service",
     "fileName": "azure-new-service"
   }
   ```

2. **Add compound words** (if needed):
   ```json
   {
     "newservice": "new-service"
   }
   ```

3. **Regenerate**:
   ```bash
   ./run-docker.sh
   ```

### Modifying Templates

1. Edit `.hbs` file in `templates/`
2. Rebuild generator: `dotnet build CSharpGenerator/`
3. Regenerate: `pwsh ./Generate-MultiPageDocs.ps1`
4. Verify output in `generated/multi-page/`

### Debugging

1. Run debug script: `pwsh ./Debug-MultiPageDocs.ps1`
2. Attach VS Code debugger: F5 → "Debug Generate Docs"
3. Set breakpoints in CSharpGenerator/*
4. Step through code execution

## Performance Characteristics

| Phase | Time (First) | Time (Cached) |
|-------|-------------|---------------|
| Clone MCP | 2-3 min | 10 sec |
| Build MCP | 3-5 min | 30 sec |
| Build Generator | 1-2 min | 10 sec |
| Generate Docs | 2-3 min | 2-3 min |
| **Total** | **10-15 min** | **5-7 min** |

**Docker Image Size**: 2.36GB (includes SDK + MCP server)

## Environment Modes

### Local Development
- MCP repo cloned separately
- Relative paths: `../servers/Azure.Mcp.Server/src`
- Direct PowerShell execution
- Output: `./generated/`

### Docker Container
- MCP cloned inside container
- Absolute paths: `/mcp/servers/Azure.Mcp.Server/src`
- Detected via `$env:MCP_SERVER_PATH`
- Volume mount: `/output` → host `./generated/`

### GitHub Actions
- Docker build and run
- Artifacts uploaded (30-day retention)
- Scheduled nightly + manual trigger
- 140 lines vs 476 (70% reduction)

## Quick Reference

**Start local generation**:
```bash
pwsh docs-generation/Generate-MultiPageDocs.ps1
```

**Start Docker generation**:
```bash
./run-docker.sh
```

**Debug in VS Code**:
```bash
pwsh docs-generation/Debug-MultiPageDocs.ps1
# Then F5 in VS Code
```

**Rebuild from scratch**:
```bash
./run-docker.sh --no-cache
```

**Check output**:
```bash
ls -lh generated/multi-page/*.md
find generated/multi-page -name "*.md" | wc -l  # Should be 591
```

## Further Reading

- [README.md](../README.md) - Project overview
- [.contextdocs](../.contextdocs) - Comprehensive LLM context
- [QUICK-START.md](./QUICK-START.md) - 5-minute guide
- [DOCKER-README.md](./DOCKER-README.md) - Docker details
- [docs-generation/README.md](../docs-generation/README.md) - Generator docs
