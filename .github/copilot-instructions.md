# GitHub Copilot Instructions for Azure MCP Documentation Generator

This file provides instructions to GitHub Copilot for working with this codebase.

## Project Overview

This is the Azure MCP Documentation Generator - an automated system that generates 590+ markdown documentation files for Microsoft Azure Model Context Protocol (MCP) server tools using a containerized Docker solution.

## Architecture

### Three-Tier System

1. **Orchestration Layer** (PowerShell)
   - `docs-generation/Generate.ps1` - Main entry point
   - Detects container vs local environment via `$env:MCP_SERVER_PATH`
   - Calls Azure MCP CLI to extract tool data
   - Invokes C# generator with JSON input

2. **Generation Layer** (C# + .NET 9.0)
   - `docs-generation/CSharpGenerator/` - Console app that processes JSON
   - Uses Handlebars.Net 2.1.6 for template processing
   - Applies configuration for filename resolution

3. **Template Layer** (Handlebars)
   - `docs-generation/templates/*.hbs` - Define documentation structure
   - `tool-family-page.hbs` - Main service docs
   - `annotation-template.hbs`, `parameter-template.hbs` - Includes
   - `tool-complete-template.hbs` - Complete tool documentation (NEW)

## Key Components

### PowerShell Orchestrator
**File**: `docs-generation/Generate.ps1`

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
  - **Parameter Count**: The parameter count displayed in console output and `generation-summary.md` represents the count of **non-common parameters only** (those shown in the parameter tables). Common parameters are defined in `docs-generation/common-parameters.json` and are filtered out to match what users see in the documentation.
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

- **Common parameters** are defined in `docs-generation/common-parameters.json`:
  - `--tenant`, `--subscription`, `--auth-method`, `--resource-group`
  - `--retry-delay`, `--retry-max-delay`, `--retry-max-retries`, `--retry-mode`, `--retry-network-timeout`
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
- Output: `./generated/tools/{tool}.complete.md` (one per tool)

**Architecture**:
- Runs AFTER annotations, parameters, and example-prompts generation
- Reads and embeds content from those files (no duplication)
- Strips frontmatter before embedding
- Keeps annotations as [!INCLUDE] reference only
- See `CSharpGenerator/Generators/COMPLETE-TOOLS-README.md` for details

### Example Prompts Generation (ExamplePromptGeneratorStandalone)
**Purpose**: Standalone .NET package that generates 5 natural language example prompts per tool using Azure OpenAI

**Key Components**:
- `ExamplePromptGeneratorStandalone/` - Standalone console application (NEW)
- `Program.cs` - CLI entry point, processes all tools sequentially
- `Generators/ExamplePromptGenerator.cs` - Core AI generation logic with regex parameter replacement
- `GenerativeAI/GenerativeAIClient.cs` - Azure OpenAI client with retry logic
- Embedded resources: `prompts/` (system/user prompts), `templates/` (Handlebars template)
- Output: 
  - `./generated/example-prompts/{tool}-example-prompts.md` (AI-generated prompt files)
  - `./generated/example-prompts-prompts/{tool}-input-prompt.md` (input prompts for debugging)

**Environment Variables Required** (from `.env` in `docs-generation/`):
- `FOUNDRY_API_KEY` - Azure OpenAI API key
- `FOUNDRY_ENDPOINT` - Azure OpenAI endpoint URL
- `FOUNDRY_MODEL_NAME` - Model deployment name (e.g., "gpt-4o-mini")
- `FOUNDRY_MODEL_API_VERSION` - API version (optional)

**Processing Flow (Sequential, not batch)**:
1. For each tool, generate custom user prompt from template with tool-specific parameters
2. Call Azure OpenAI with system + user prompts
3. Parse JSON response (5 example prompts)
4. Save input prompt to `example-prompts-prompts/` and output prompts to `example-prompts/`
5. Move to next tool (sequential processing ensures incremental progress)

**Rate Limiting & Retry Logic**:
- `GenerativeAIClient.cs` implements exponential backoff retry logic
- **5 retries** with delays: 1s → 2s → 4s → 8s → 16s
- Only retries on rate limit errors (429 status codes, "rate limit", "too many requests", "quota")
- All other exceptions fail immediately
- Critical for handling 208 sequential API calls without failing

**JSON Response Parsing**:
- AI responses may include preamble text (e.g., "STEP 1:", "STEP 2:")
- Parser extracts JSON by finding first `{` and last `}` if not in code blocks
- Handles both ```json code blocks and plain JSON
- Cleans smart quotes and HTML entities from prompts

**Common Issues**:
- If generation stops without errors, check Azure OpenAI credentials in `.env`
- If only 15-20 files generate, rate limits are being hit (retry logic should handle this)
- If JSON parsing fails, AI may be returning unexpected format (check raw response in logs)

### Adding New Service Area
1. Add to `brand-to-server-mapping.json` if needs brand name
2. Add to `compound-words.json` if has concatenated words
3. Regenerate docs - old include files should be deleted first

### Modifying Templates
1. Edit `.hbs` file in `templates/`
2. Rebuild: `dotnet build CSharpGenerator/`
3. Regenerate: `pwsh ./Generate.ps1`

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

## Default Output Directory (Important)

All generation scripts should default to the repository root `./generated` directory unless an explicit output path is provided. This ensures consistent output locations across PowerShell, bash, and C# tooling.

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
    ├── annotations/            # Tool annotation includes
    ├── parameters/             # Tool parameter includes
    ├── example-prompts/        # Example prompt includes
    └── param-and-annotation/   # Combined includes
```

## Development Workflows

### Start Scripts (Orchestrator/Worker Pattern)

**NEW**: Root-level scripts for full catalog generation

**start.sh (Orchestrator)**:
- Generates CLI metadata ONCE for all namespaces
- Runs validation ONCE
- Iterates over all 52 namespaces
- Calls `start-only.sh` for each namespace
- Tracks success/failure and reports summary

**start-only.sh (Worker)**:
- Takes a single namespace parameter
- Uses existing CLI metadata files (no regeneration)
- Generates documentation for that namespace only
- Designed to be called by start.sh orchestrator

**Usage**:
```bash
# Full catalog generation (all 52 namespaces)
./start.sh                      # All steps
./start.sh 1                    # Step 1 only (fast, no AI)
./start.sh 1,2,3                # Steps 1-3

# Single namespace generation
./start-only.sh advisor         # All steps for advisor
./start-only.sh advisor 1       # Step 1 only for advisor
./start-only.sh advisor 1,2,3   # Steps 1-3 for advisor
```

**Benefits**:
- ✅ CLI metadata generated once, shared by all namespaces
- ✅ Validation runs once, not 52 times
- ✅ Non-destructive worker (can run on existing output)
- ✅ Clear orchestration point for full catalog generation
- ✅ Can process individual namespaces independently

**Documentation**: See `docs/START-SCRIPTS.md` for complete details

### Local Development
```bash
cd docs-generation
pwsh ./Generate.ps1
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

## ⚠️ IMPORTANT: Testing Projects with Generative AI

**Critical for time management**: Any project that uses generative AI (Azure OpenAI) will make sequential API calls to generate content. This can take **15-30+ minutes** to complete a full run (~200+ tools × ~2-4 seconds per API call).

**When testing or debugging projects with GenerativeAI:**
1. **Only run to the point you know what you're testing passes** - do NOT let full generation complete
2. **Do NOT run full test suites** unless specifically needed for final validation
3. **Cancel after first 2-5 successful tool outputs** to verify:
   - API credentials are loaded correctly
   - Template processing works
   - Output files are created in correct locations
   - File content looks reasonable
4. **Use `Ctrl+C` to cancel** - safe to interrupt between tools
5. **Save 20-30 minutes of waiting** for each test iteration

**Affected Projects**:
- `ExamplePromptGeneratorStandalone` - Generates 5 prompts per tool via Azure OpenAI (sequential processing for all tools)
- Any other project using `GenerativeAI` package

**Quick Test Pattern**:
```bash
# Run tool, let it process 2-3 tools successfully, then Ctrl+C
dotnet run --project ProjectName -- [args]
# After 3-5 successes, verify:
# - Console shows ✅ checkmarks for at least 3 tools
# - Output files exist in target directory
# - File content is valid (frontmatter, command comment, etc.)
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
- **Tools Documented**: 200+ Azure MCP tools
- **Build Time**: 10-15 min first run, 5-7 min cached
- **Docker Image**: 2.36GB
- **Workflow Reduction**: 476 → 140 lines (70% reduction)

## Code Conventions

1. **PowerShell**: Use `$env:MCP_SERVER_PATH` for environment detection
2. **C# Generator**: Follow Central Package Management (CPM)
3. **Templates**: Use Handlebars helpers for loops and conditionals
4. **Configuration**: Brand mapping > Compound words > Original name
5. **File naming**: Lowercase with hyphens for include files
6. **Console Output**: Never buffer dotnet output - stream in real-time for visibility

## When Helping with Code

### For PowerShell Changes
- Consider both container and local environments
- Use `Push-Location` / `Pop-Location` for directory navigation
- Check `$env:MCP_SERVER_PATH` for container detection
- **Critical**: Never capture dotnet output with `$var = & dotnet ... 2>&1`
  - This buffers ALL output until completion, making long-running tasks appear frozen
  - Instead use: `& dotnet ...` (streams output in real-time)
  - Applies to: `Generate-MultiPageDocs.ps1`, `Generate-CompleteTools.ps1`, all orchestration scripts
  - Exception: Short commands where you need to parse output immediately

### For C# Changes
- Follow .NET 9.0 patterns
- Use Central Package Management (no versions in .csproj)
- Update `Config.cs` for new configuration files
- Test with `dotnet build` before running
- **For new generators**: Place in `Generators/` directory, follow existing patterns
  - Use dependency injection for shared functions (brand mapping, filename cleaning)
  - Filter common parameters using `ExtractCommonParameters` unless they're required
  - Document in separate README.md file within the generator directory
- **For Azure OpenAI integration**:
  - Always implement retry logic with exponential backoff (see `GenerativeAIClient.cs`)
  - Only retry on rate limit errors (429, "rate limit", "quota")
  - Parse AI responses robustly - extract JSON by finding `{` and `}` markers
  - Handle preamble text (AI may return steps/reasoning before JSON)
  - Use `Console.WriteLine` extensively for debugging - output streams to PowerShell logs

### CRITICAL: AI-Based Generation vs Hardcoded Logic

**⚠️ IMPORTANT RULE**: Do NOT hardcode programmatic transformations that should be generated by AI.

**When to use AI generation instead of hardcoding**:
- Content generation (headings, descriptions, summaries)
- Text transformations (formatting, restructuring, style improvements)
- Pattern-based improvements (naming conventions, capitalization rules)
- Context-dependent outputs (abstracts, examples, related content)

**When hardcoding is acceptable**:
- Data extraction and parsing (regex, string splitting)
- File I/O and path manipulation
- Validation and error handling
- Configuration loading and merging
- Structural transformations (YAML frontmatter, JSON parsing)

**Example - CORRECT (AI-based)**:
- Create `H2HeadingGenerator.cs` that calls `GenerativeAIClient.GetChatCompletionAsync()`
- Use detailed system prompt to guide AI (verb selection, naming rules, format)
- Pass extracted data (command, description, family) to AI for generation

**Example - WRONG (Hardcoded)**:
```csharp
// DON'T DO THIS - Use AI instead
private string GenerateHeading(string command)
{
    if (command.Contains("list")) return "List " + GetNoun(command);
    if (command.Contains("create")) return "Create " + GetNoun(command);
    // ... more hardcoded rules
}
```

**Implementation checklist for AI-generated content**:
1. ✅ Create a dedicated generator class (e.g., `H2HeadingGenerator.cs`)
2. ✅ Add system and user prompt files in `prompts/` directory
3. ✅ Call `GenerativeAIClient` with environment variable credentials
4. ✅ Implement proper error handling and fallback logic
5. ✅ Extract markdown from AI responses if wrapped in code fences
6. ✅ Add markdown stripping in response handler (check for ` ```markdown ... ``` `)
7. ✅ Use phase-based integration in orchestration (e.g., Phase 1.5)
8. ✅ Log token usage and progress to console

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

## Debugging & Troubleshooting

### Example Prompts Not Generating
1. **Check environment variables** in `docs-generation/.env`:
   - `FOUNDRY_API_KEY`, `FOUNDRY_ENDPOINT`, `FOUNDRY_MODEL_NAME` must be set
   - Generator will skip example prompts if credentials are missing (with warning)
2. **Check console output** - should see "DEBUG: Generating example prompt for..." for each tool
3. **Rate limiting** - expect retries with exponential backoff (1s, 2s, 4s, 8s, 16s delays)
4. **JSON parsing failures** - AI may return preamble text; parser finds first `{` and last `}`
5. **Use Test-ExamplePrompts.ps1** for isolated debugging with real-time output

### PowerShell Output Not Visible
- **Symptom**: Long-running commands appear frozen, no output
- **Cause**: Output buffering with `$var = & dotnet ... 2>&1`
- **Solution**: Remove variable capture, use `& dotnet ...` directly
- **Files**: `Generate.ps1`, `Generate-CompleteTools.ps1`
- **Benefit**: Output streams to console AND transcript logs in real-time

### Testing Changes
- **Quick test**: `pwsh ./Test-ExamplePrompts.ps1` (shows real-time output)
- **Full generation**: `pwsh ./Generate.ps1 -OutputPath ../generated`
- **Complete tools only**: `pwsh ./Generate-CompleteTools.ps1`
- **Logs**: Check `generated/logs/generation-*.log` for transcript
- **Verify output**: Count files in `generated/example-prompts/` (should match tool count)

## Last Updated

February 6, 2026
