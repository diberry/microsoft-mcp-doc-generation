# Quinn's Project History

## What I Know About This Project's Scripts and CI

### Script Locations

All scripts are in `docs-generation/scripts/`:
- `preflight.ps1` — Setup: validate env, clean output, build solution, generate CLI metadata
- `start-only.sh` — Worker: processes single namespace (assumes preflight done)
- `1-Generate-AnnotationsParametersRaw-One.ps1` — Step 1: annotations, parameters, raw tools
- `2-Generate-ExamplePrompts-One.ps1` — Step 2: AI example prompts
- `3-Generate-ToolGenerationAndAIImprovements-One.ps1` — Step 3: tool generation + AI improvements
- `4-Generate-ToolFamilyCleanup-One.ps1` — Step 4: tool family metadata
- `5-Generate-HorizontalArticles-One.ps1` — Step 5: horizontal articles
- `Shared-Functions.ps1` — Shared PowerShell utilities
- `bash-common.sh` — Shared bash utilities
- `validate-env.ps1` — Environment validation
- `Generate-ToolFamily.ps1` — Tool family generation
- `Invoke-CliAnalyzer.ps1` — CLI analysis and reports

Root scripts:
- `start.sh` — Orchestrator entry point (stays in root)

### GitHub Actions Workflows

In `.github/workflows/`:
- `build-and-test.yml` — Builds `docs-generation.sln` and runs `dotnet test`
- `generate-docs.yml` — Main documentation generation workflow
- `update-azure-mcp.yml` — Updates Azure MCP server dependency
- `test-azure-mcp-update.yml` — Tests MCP server updates

### Key Environment Variables

From `docs-generation/.env` (not committed):
- `FOUNDRY_API_KEY` — Azure OpenAI API key
- `FOUNDRY_ENDPOINT` — Azure OpenAI endpoint URL
- `FOUNDRY_MODEL_NAME` — Model deployment (e.g., "gpt-4o-mini")
- `FOUNDRY_MODEL_API_VERSION` — API version
- `TOOL_FAMILY_CLEANUP_FOUNDRY_MODEL_NAME` — Higher-quality model for cleanup step
- `MCP_SERVER_PATH` — Container path override for MCP server

### Orchestrator/Worker Split (AD-004)

- `start.sh` handles:
  1. Running `preflight.ps1` ONCE (validates, builds, generates CLI)
  2. Determining namespace list (all 52 or single)
  3. Calling `start-only.sh` for each namespace
  4. Tracking success/failure

- `start-only.sh` handles:
  1. Verifying CLI files exist
  2. Running steps 1–5 for one namespace
  3. Using namespace-specific output dir (`generated-<namespace>/`)

### Docker

Multi-stage Dockerfile:
1. `mcp-builder` — Clones and builds Microsoft/MCP with .NET 10
2. `docs-builder` — Builds documentation generators with .NET 9
3. `runtime` — Combines both, installs PowerShell 7.4.6, runs generation

Key note: PowerShell installed via direct `.deb` download (not apt repo) to avoid installation failures.

### CI Notes

- `build-and-test.yml` runs on every PR
- Zero warnings enforced by `--configuration Release`
- Tests must pass before merge
- The `generate-docs.yml` workflow requires environment secrets for AI steps

### Common Issues I've Fixed

1. PowerShell `$var = & dotnet ... 2>&1` — buffers all output. Fixed by removing variable capture
2. `pwsh -Command` with paths fails on Windows — fixed by using `pwsh -File`
3. `[bool]` params from bash — fixed by using `[switch]` type
