# GitHub Copilot Instructions for Azure MCP Documentation Generator

This file provides instructions to GitHub Copilot for working with this codebase.

## Project Overview

This is the Azure MCP Documentation Generator - an automated system that generates 590+ markdown documentation files for Microsoft Azure Model Context Protocol (MCP) server tools using a containerized Docker solution.

## ⚠️ CRITICAL: Never Edit Generated Markdown Files

Files under `generated/` and `generated-*/` directories are **programmatically generated output**. Do NOT modify these files directly unless the user explicitly requests it. Instead, fix the **source code** that generates them:

- **Typed steps** (`mcp-tools/DocGeneration.PipelineRunner/Steps/`)
- **Generator projects** (`mcp-tools/DocGeneration.Steps.*/*.cs`)
- **Templates** (`mcp-tools/templates/*.hbs`, project-specific `templates/` dirs)
- **Configuration files** (`mcp-tools/data/*.json`)
- **AI prompts** (`mcp-tools/prompts/`, project-specific `prompts/` dirs)

After fixing the source, the user will regenerate the output with `bash start.sh <namespace>`.

## Architecture

### Typed .NET Pipeline (PipelineRunner)

The pipeline is orchestrated by `DocGeneration.PipelineRunner`, a typed C# runner invoked via `start.sh`:

```
start.sh → PipelineCli → PipelineRunner.RunAsync()
               │
               ├── Step 0: BootstrapStep (Global)
               │     Build .NET, extract CLI metadata, validate brands
               │
               ├── Step 1: AnnotationsParametersRawStep (per namespace)
               │     CLI JSON → annotations, parameters, raw tools
               │
               ├── Step 2: ExamplePromptsStep (AI, per namespace)
               │     Generate 5 NL prompts per tool via Azure OpenAI
               │
               ├── Step 3: ToolGenerationStep (AI, per namespace)
               │     Compose + AI-improve tool descriptions
               │
               ├── Step 4: ToolFamilyCleanupStep (AI, per namespace, retries 2x)
               │     Assemble per-service article + post-assembly validation
               │
               ├── Step 5: SkillsRelevanceStep (warn-only, per namespace)
               │     GitHub Copilot skills mapping
               │
               └── Step 6: HorizontalArticlesStep (AI, per namespace)
                     Overview articles with capabilities, RBAC, best practices
```

Every step implements `IPipelineStep` with typed dependency declarations, failure policies, and optional post-validators. See `docs/ARCHITECTURE.md` for full details.

### Key Files

| Component | Location |
|-----------|----------|
| Pipeline entry point | `DocGeneration.PipelineRunner/Program.cs` |
| Step registry | `DocGeneration.PipelineRunner/Registry/StepRegistry.cs` |
| Step contract | `DocGeneration.PipelineRunner/Contracts/IPipelineStep.cs` |
| Bootstrap (Step 0) | `DocGeneration.PipelineRunner/Steps/Bootstrap/BootstrapStep.cs` |
| Steps 1-6 | `DocGeneration.PipelineRunner/Steps/Namespace/` |
| Post-validators | `DocGeneration.PipelineRunner/Validation/` |
| Workspace manager | `DocGeneration.PipelineRunner/Services/WorkspaceManager.cs` |

### TemplateEngine (shared library)
**Directory**: `mcp-tools/TemplateEngine/`

Shared Handlebars template rendering library used by CSharpGenerator, HorizontalArticleGenerator, and ExamplePromptGeneratorStandalone. Wraps `Handlebars.Net` with custom helpers split into:
- `Helpers/CoreHelpers.cs` - Generic helpers (dates, strings, math)
- `Helpers/McpHelpers.cs` - MCP command structure helpers

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

- **Common parameters** are defined in `mcp-tools/data/common-parameters.json`:
  - `--tenant`, `--auth-method` (infrastructure params)
  - `--retry-delay`, `--retry-max-delay`, `--retry-max-retries`, `--retry-mode`, `--retry-network-timeout`
  - `--subscription` (scoping param — filtered when optional, kept when required)
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

**Environment Variables Required** (from `.env` in `mcp-tools/`):
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

### Horizontal Article Generation (HorizontalArticleGenerator)
**Purpose**: Generates one overview article per tool family (namespace) using Azure OpenAI. Each article covers capabilities, scenarios, prerequisites, RBAC roles, and best practices for all tools in that family.

**Key Components**:
- `HorizontalArticleGenerator/` - Standalone .NET console application
- `Generators/HorizontalArticleGenerator.cs` - Main generator orchestrating the AI → validate → transform → render pipeline
- `Generators/ArticleContentProcessor.cs` - Public class for validation and transformation of AI-generated content
- `TextTransformation/` - Shared library for text transformations (static replacements, trailing period management)
- `prompts/horizontal-article-system-prompt.txt` - System prompt defining JSON schema and content rules
- `prompts/horizontal-article-user-prompt.txt` - User prompt template (Handlebars) with tool data
- `templates/horizontal-article-template.hbs` - Handlebars template for final markdown rendering
- Output: `./generated-{namespace}/horizontal-articles/{family}.md`

**Pipeline Order**: AI response → JSON parse → `Validate()` → `ApplyTransformations()` → Merge → Render

**ArticleContentProcessor Validations** (all service-agnostic):
- Strip trailing periods from titles, capabilities, short descriptions
- Fix broken sentences (period-before-lowercase)
- Strip `learn.microsoft.com` prefix from links
- Remove fabricated `/docs` URL patterns
- Deduplicate additional links that match serviceDocLink
- Detect fabricated RBAC roles via pattern matching ("Administrator" suffix, generic prefixes)
- Validate capability-to-tool ratio
- Validate best practice count minimums

**TextTransformation**:
- `TransformText()` - Static replacements only (e.g., "Azure Active Directory" → "Microsoft Entra ID"). Used for titles, capabilities, short descriptions — fields that must NOT end with a period
- `TransformDescription()` - Static replacements + `EnsureEndsPeriod()`. Used for full sentences (descriptions, overviews)

**⚠️ CRITICAL - Universal Design Principle**:
All validations, transformations, prompts, and tests in the HorizontalArticleGenerator MUST be **universal** — they must work correctly for ALL 52 namespaces and ALL Azure services, tools, and products. Never add service-specific logic, hardcoded service names, or service-specific test data. Instead:
- Use **pattern-based detection** (e.g., regex, suffix checks) instead of hardcoded blocklists
- Use **varied Azure service examples** across tests (Storage, Key Vault, Cosmos DB, Speech, Monitor, etc.) — never concentrate all test data on one service
- Prompts should describe rules generically, not through service-specific examples
- If a validation catches a problem in one service's output, the fix must be a generic pattern that catches the same class of problem across all services

### Adding New Service Area
1. Add to `brand-to-server-mapping.json` if needs brand name
2. Add to `compound-words.json` if has concatenated words
3. Regenerate docs - old include files should be deleted first

### Multi-Namespace Merge (AD-011)

Some Azure services span multiple MCP namespaces but publish as a single article. This uses a **post-assembly merge** pattern:

**Configuration** in `brand-to-server-mapping.json`:
- `mergeGroup`: group identifier (e.g., `"azure-monitor"`)
- `mergeOrder`: position within group (1 = primary)
- `mergeRole`: `"primary"` (owns frontmatter/overview/related) or `"secondary"` (tool H2 sections only)

**Key components**:
- `merge-namespaces.sh` — Post-assembly merge script called by `start.sh`
- `NamespaceMerger.cs` — Typed C# merge logic (ParseArticle/Merge/UpdateToolCount)
- `MergeGroupValidator.cs` — Config validation (exactly 1 primary, unique order, complete fields)

**Adding a new merge group**:
1. Add `mergeGroup`, `mergeOrder`, `mergeRole` to each namespace's entry in `brand-to-server-mapping.json`
2. Run `dotnet test` — `MergeGroupValidator` catches configuration errors
3. Run `./start.sh` — merge runs automatically after all namespaces complete
4. Verify merged output in `generated-{primary-namespace}/tool-family/`

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

### Phantom H2 Sections in Generated Output
- **Symptom**: AI-generated heading replacements (Phase 1.5) or tool-family assembly (Step 4) inject empty or malformed `## ` sections that inflate tool counts or cause validation mismatches
- **Solution**: The pipeline now strips phantom H2 sections during heading replacement (Phase 1.5) and post-assembly (Step 4). If you see tool-count mismatch errors in validation reports, check for stray `## ` lines in the tool-family article source.

### ParameterCoverageChecker False Positives
- **Symptom**: Post-assembly validation reports "section freeze" or false-positive missing-parameter warnings, especially for single-word parameters (e.g., `query`) or parameters whose values contain JSON (e.g., `--filter '{"key":"value"}'`)
- **Solution**: `ParameterCoverageChecker` now accepts single-word parameter names and allows JSON-like content inside quoted parameter values. CLI switch prefixes (`--`) are stripped before matching.

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
- Calls `mcp-tools/scripts/preflight.ps1` ONCE which:
  - Validates .env file exists with required AI credentials (STOPS if missing/invalid)
  - Cleans previous generation output  
  - Creates output directory structure
  - Builds .NET solution (all generator projects)
  - Generates CLI metadata for all namespaces
  - Runs brand mapping validation (Step 0)
- Iterates over all 52 namespaces (or single specified namespace)
- Calls `mcp-tools/scripts/start-only.sh` for each namespace
- Tracks success/failure and reports summary

**start-only.sh (Worker)** (located at `mcp-tools/scripts/start-only.sh`):
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

# Single namespace generation via orchestrator
./start.sh advisor              # All steps for advisor only
./start.sh advisor 1            # Step 1 only for advisor
./start.sh advisor 1,2,3        # Steps 1-3 for advisor

# Skip dependency validation for fast iteration
./start.sh advisor 4 --skip-deps   # Run step 4 without requiring steps 1-3

# Direct worker call (requires preflight setup first)
./mcp-tools/scripts/start-only.sh advisor         # All steps for advisor
./mcp-tools/scripts/start-only.sh advisor 1       # Step 1 only for advisor
./mcp-tools/scripts/start-only.sh advisor 1,2,3   # Steps 1-3 for advisor
```

**Benefits**:
- ✅ Preflight setup (validation, build, CLI, validation) runs once
- ✅ .NET solution built early, before any tool-specific work
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

## ⚠️ IMPORTANT: Test-Driven Development (TDD) — Mandatory

**Every code change MUST start with tests and have behavioral test coverage.** See `.squad/decisions.md` AD-007 and AD-010.

**Workflow (non-negotiable):**
1. Write failing test(s) that reproduce the bug or define the feature contract
2. Verify the test(s) FAIL against current code
3. Implement the fix
4. Verify ALL tests pass (new + existing)
5. Commit tests and code together

**Test coverage depth — tests must catch the bug on regression:**
- Bug fixes: ≥1 test that reproduces the exact failure with realistic inputs
- Error handling: ≥1 error-path test + ≥1 happy-path regression guard
- AI-dependent code: ≥1 test simulating AI failure (mock/stub)
- Post-processing: ≥1 test with problem-pattern input + ≥1 clean-input test (no false positives)
- Config changes: ≥1 test proving the config value is loaded AND applied

**Blocked anti-patterns:**
- ❌ Reflection-only tests (method exists, return type) as sole coverage
- ❌ Tests that pass regardless of whether the fix is present
- ❌ Tests that only assert `result.Success` without checking warnings/output content
- ❌ Code changes without corresponding test changes

**Reviewer checklist:** Would this test FAIL if the fix were reverted? If not, it's not a real test.

## ⚠️ CRITICAL: Never Merge PRs — Team Review Required

**NEVER merge a PR. Only the user (Dina) merges PRs after full team review.**

All development work must follow this process in strict order (AD-020):
1. Create a plan (design, test strategy, edge cases)
2. Write failing tests (define the contract before implementation)
3. Write implementation code (make the tests pass)
4. Run tests (all must pass, no regressions)
5. **Create PR — STOP HERE. Do NOT merge.**
6. Full team review (all 9 reviewers: Avery, Riley, Morgan, Quinn, Sage, Cameron, Parker, Reeve, Copilot)
7. User reviews and merges

**Sub-agents spawned for implementation**: Your job ends at step 5. Create the PR, report back. Do NOT call `gh pr merge`. Do NOT merge.

## ⚠️ IMPORTANT: PR Documentation — CHANGELOG + Docs Update Required

**Every PR must update documentation before team review.** See `.squad/decisions.md` AD-026 and `.squad/skills/pr-docs/SKILL.md`.

**Before requesting review on any PR:**
1. **Update `CHANGELOG.md`** — Add entry under `## [Unreleased]` with user-facing description and PR/issue numbers
2. **Update user-facing docs** — Route to the correct file using the documentation routing table in the pr-docs skill
3. **Update README navigation** — If a new doc file was created, add it to the Documentation section

**Exemptions** (state in PR comment): test-only changes, internal refactors with no behavior change.

**Documentation routing (quick reference):**
- New tool/feature → `docs/PROJECT-GUIDE.md`
- Pipeline changes → `docs/ARCHITECTURE.md`
- Script changes → `docs/START-SCRIPTS.md`
- Quality/compliance → `docs/acrolinx-compliance-strategy.md`
- Config changes → `docs/PROJECT-GUIDE.md`
- New prereqs → `README.md`

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
- `mcp-tools/README.md` - Generator details
- `mcp-tools/CSharpGenerator/Generators/COMPLETE-TOOLS-README.md` - Complete tools feature (NEW)

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
7. **Modularization**: Isolate new features into separate modules when possible
   - PowerShell: Create separate `.ps1` files for distinct functionality (e.g., `validate-env.ps1`, `preflight.ps1`)
   - Bash: Create separate `.sh` files for reusable scripts
   - C#: Create separate projects or packages in `mcp-tools/` (e.g., `GenerativeAI`, `Shared`)
   - Node.js: Create separate npm packages when appropriate
   - Benefits: Testability, reusability, maintainability, clear separation of concerns
   - Example: Environment validation extracted to `validate-env.ps1` instead of inline in preflight script
8. **Cross-platform (bash ↔ PowerShell)**: See "Cross-Platform Script Interop" section below
9. **Project README**: Every .NET project and standalone package in `mcp-tools/` MUST have a `README.md` in its project root directory. When creating a new project, include a README covering: purpose, usage, architecture/key files, dependencies, and how to run tests. When modifying an existing project (adding features, changing behavior, updating CLI options, etc.), review the project's README and update it if the changes affect anything documented there.

## When Helping with Code

### For PowerShell Changes
- Consider both container and local environments
- Use `Push-Location` / `Pop-Location` for directory navigation
- Check `$env:MCP_SERVER_PATH` for container detection
- **Modularization**: Extract distinct functionality into separate `.ps1` files
  - Example: Environment validation → `validate-env.ps1`
  - Example: Preflight setup → `preflight.ps1`
  - Makes scripts testable, reusable, and maintainable
- **Critical**: Never capture dotnet output with `$var = & dotnet ... 2>&1`
  - This buffers ALL output until completion, making long-running tasks appear frozen
  - Instead use: `& dotnet ...` (streams output in real-time)
  - Applies to: `Generate-MultiPageDocs.ps1`, `Generate-CompleteTools.ps1`, all orchestration scripts
  - Exception: Short commands where you need to parse output immediately

### Cross-Platform Script Interop (Bash ↔ PowerShell)

**All scripts must work on Windows (Git Bash/MSYS2), macOS, and Linux.**

#### Calling PowerShell from Bash — ALWAYS use `pwsh -File`

```bash
# ✅ CORRECT — pwsh -File lets the shell handle path translation
pwsh -File "$SCRIPT_DIR/MyScript.ps1" -Param1 "value" -SwitchParam

# ❌ WRONG — pwsh -Command receives MSYS/Unix paths that PowerShell can't resolve
pwsh -Command "$SCRIPT_DIR/MyScript.ps1 -Param1 'value'"
# Fails on Windows Git Bash: '/c/Users/...' is not recognized as a cmdlet
```

**Why**: Git Bash on Windows translates paths for executables but NOT inside string arguments.
With `-Command`, the entire string is passed as-is to PowerShell, which doesn't understand `/c/Users/...`.
With `-File`, bash resolves the path before invoking pwsh.

#### PowerShell parameter types for `-File` compatibility

When a `.ps1` script is called via `pwsh -File` from bash:

- **Use `[switch]` NOT `[bool]`** for flag parameters:
  ```powershell
  # ✅ CORRECT — works with: pwsh -File script.ps1 -SkipBuild
  [switch]$SkipBuild
  
  # ❌ WRONG — pwsh -File requires explicit value: -SkipBuild $true (awkward from bash)
  [bool]$SkipBuild = $false
  ```

- **Don't use `[int[]]` or `[string[]]` for array params passed from bash** — bash cannot construct PowerShell array syntax. Accept a string and parse it:
  ```powershell
  # ✅ CORRECT — accepts both "1,2,3" (from bash) and @(1,2,3) (from PowerShell)
  $Steps = @(1, 2, 3, 4, 5)
  # Then normalize after param():
  if ($Steps -is [string]) {
      $Steps = $Steps -split ',' | ForEach-Object { [int]$_.Trim() }
  }
  
  # ❌ WRONG — bash can't pass @(1,2,3) syntax via -File
  [int[]]$Steps = @(1, 2, 3, 4, 5)
  ```

#### Script directory and path resolution

All `.ps1` scripts live in `mcp-tools/scripts/`. When referencing sibling directories:

```powershell
$scriptDir = $PSScriptRoot                    # → mcp-tools/scripts/
$docsGenDir = Split-Path -Parent $scriptDir   # → mcp-tools/

# Reference project directories via $docsGenDir, NOT $scriptDir
$csharpGen = Join-Path $docsGenDir "CSharpGenerator"      # ✅
$brandMap  = Join-Path $docsGenDir "data/brand-to-server-mapping.json"  # ✅
$sln       = Join-Path (Split-Path $docsGenDir -Parent) "mcp-doc-generation.sln"  # ✅

# Reference sibling scripts via $scriptDir
& "$scriptDir\Generate-Annotations.ps1"  # ✅

# Default OutputPath should account for script depth (scripts/ → mcp-tools/ → repo root)
[string]$OutputPath = "../../generated"  # ✅ (relative to $scriptDir)
```

**Key rule**: `$docsGenDir` must always be defined at the top level of the script (not inside an `if` block) so it's available everywhere.

#### Bash wrapper scripts (.sh calling .ps1)

Pattern for bash scripts calling PowerShell (e.g., `start-only.sh`):
```bash
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$SCRIPT_DIR/bash-common.sh"
pwsh -File "$SCRIPT_DIR/MyScript.ps1" -ToolFamily "$TOOL_FAMILY" -Steps "$STEPS" -SkipBuild
```

### For C# Changes
- Follow .NET 9.0 patterns
- Use Central Package Management (no versions in .csproj)
- Update `Config.cs` for new configuration files
- Test with `dotnet build` before running
- **Zero warnings policy**: The CI build uses `--configuration Release` which treats warnings as errors. All compiler warnings (nullable, unused variables, etc.) must be resolved before pushing. Run `dotnet build mcp-doc-generation.sln --configuration Release` locally and fix any warnings.
- **Branch protection**: The `build-and-test` CI workflow must pass before merging. All tests (668+ across 12 test projects) run automatically on every PR.
- **For new .NET projects**: Always add to `mcp-doc-generation.sln` (`dotnet sln add`) and verify the full solution builds (`dotnet build mcp-doc-generation.sln`). This ensures the project is included in CI build and test via `.github/workflows/build-and-test.yml`. If the project includes tests, add a corresponding `.Tests` project to the solution as well. **Every new project MUST include a `README.md`** in its directory covering purpose, usage, architecture, and dependencies.
- **When modifying existing projects**: Review the project's `README.md` and update it if the changes affect documented behavior, CLI options, architecture, dependencies, or usage patterns.
- **Every bug fix MUST include tests**: When fixing a bug or error, add one or more unit tests that reproduce the bug and verify the fix. Tests must be placed in a `.Tests` project that is part of `mcp-doc-generation.sln` so that CI (`dotnet test mcp-doc-generation.sln`) runs them automatically. If no `.Tests` project exists for the affected project, create one (xunit, CPM, added to the solution). If the code under test has `private` methods that need testing, change them to `internal` and add `<InternalsVisibleTo Include="ProjectName.Tests" />` to the source project's `.csproj`.
- **For new generators**: Place in `Generators/` directory, follow existing patterns
  - Use dependency injection for shared functions (brand mapping, filename cleaning)
  - Filter infrastructure parameters (tenant, auth-method, retry-*) and scoping params (subscription) using `common-parameters.json` — all are filtered when optional, kept when required
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
1. **Check environment variables** in `mcp-tools/.env`:
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

## ⚠️ Universal Design Principle

All generators, validators, transformations, prompts, templates, and tests MUST be **service-agnostic**. They must produce correct output for every one of the 52 Azure MCP namespaces without service-specific logic.

- **No hardcoded service names** in validation logic — use pattern-based detection
- **No service-specific tests** — use varied Azure service examples across test cases
- **No service-specific prompt examples** — describe rules generically or use diverse service examples
- **No service-specific blocklists** — detect fabrication patterns structurally (e.g., role name suffix, URL path patterns)
- **Test across services** — when writing tests, draw examples from Storage, Key Vault, Cosmos DB, Speech, Monitor, AKS, SQL, and other services to ensure coverage

If a bug is found in one service's generated output, the fix MUST be a generic rule that catches the same class of problem across all services.

## Skills Generation Pipeline (skills-generation/)

### Overview
Independent .NET pipeline in `skills-generation/` that generates 24 Azure Skills documentation pages from GitHub Copilot for Azure source data.

### Key Commands
- **Build**: `dotnet build skills-generation/skills-generation.slnx --configuration Release`
- **Test**: `dotnet test skills-generation/skills-generation.slnx`
- **Generate (local)**: `dotnet run --project skills-generation/SkillsGen.Cli -- generate-skills --all --no-llm --source local --source-path <path-to-skills> --tests-path <path-to-tests> --out ./generated-skills/`
- **Start script**: `./start-azure-skills.sh`
- **Vale lint**: `./skills-generation/scripts/lint-vale.ps1`
- **Coverage check**: `./skills-generation/scripts/check-coverage.ps1`

### Architecture
8-module pipeline: fetcher → skill-parser → trigger-parser → tier-assessor → template-filler + llm-rewriter → acrolinx-post-processor → validator → orchestrator

### Important Patterns
- **Always use local source** — clone `microsoft/GitHub-Copilot-for-Azure` once and use `--source local` to avoid GitHub API rate limits
- **Vale before push** — run Vale lint locally before pushing generated content to catch Microsoft style issues
- **Triple-curly Handlebars** — template uses `{{{var}}}` (raw output) not `{{var}}` (HTML-escaped) since output is markdown
- **Acrolinx post-processor** — applies static text replacements, acronym expansion, contractions, URL normalization, and technical term wrapping
- **Display names from inventory** — `skills-inventory.json` provides display names; parser derives from slug as fallback
- **Smart prerequisites** — detected from source file extensions (.ps1→PowerShell, .bicep→Bicep, .tf→Terraform)

### CI Gates
- `skills-generation-ci.yml` — build + 152 tests + coverage threshold (80% line, 70% branch) + MCP regression check
- `vale-lint` job — Vale prose linting on generated output (report only, non-blocking)

### Test Coverage
- 152 xUnit tests, 81%+ line coverage
- TDD required per AD-007/AD-010

## Last Updated

March 28, 2026
