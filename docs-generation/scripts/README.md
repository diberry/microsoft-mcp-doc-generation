# docs-generation/scripts

Scripts for the Azure MCP Documentation Generator pipeline and related development tooling.

## Directory structure

```
scripts/
├── (root)          # Active pipeline scripts called from start.sh
├── batch/          # All-namespace orchestrators (superseded by start.sh loop)
├── standalone/     # Individual generator scripts for running a single step
├── utilities/      # Dev, debug, testing, and CI helpers
└── legacy/         # Fully superseded scripts
```

## Active pipeline scripts (root)

These 13 scripts form the generation pipeline invoked by the root `start.sh`:

```
start.sh
├── preflight.ps1                                    # One-time setup (build, CLI metadata, validation)
│   ├── validate-env.ps1                             # Validates .env has required AI credentials
│   └── 0-Validate-BrandMappings.ps1                 # Validates brand-to-server-mapping.json
└── start-only.sh                                    # Worker: processes a single namespace
    └── generate-tool-family.sh                      # Bash wrapper → pwsh -File Generate-ToolFamily.ps1
        └── Generate-ToolFamily.ps1                  # Per-namespace orchestrator (Steps 1-5)
            ├── Invoke-CliAnalyzer.ps1               # Runs CliAnalyzer .NET project on CLI JSON
            ├── 1-Generate-AnnotationsParametersRaw-One.ps1  # Step 1: annotations, parameters, raw tools
            ├── 2-Generate-ExamplePrompts-One.ps1            # Step 2: AI example prompts + validation
            ├── 3-Generate-ToolGenerationAndAIImprovements-One.ps1  # Step 3: composed + AI-improved tools
            ├── 4-Generate-ToolFamilyCleanup-One.ps1         # Step 4: tool family assembly + cleanup
            └── 5-Generate-HorizontalArticles-One.ps1        # Step 5: horizontal how-to articles
```

### Script details

| Script | Purpose |
|---|---|
| `preflight.ps1` | One-time setup: validates environment, builds .NET solution, generates CLI metadata, runs brand validation |
| `validate-env.ps1` | Checks `.env` file has required `FOUNDRY_*` credentials for Azure OpenAI |
| `0-Validate-BrandMappings.ps1` | Validates `brand-to-server-mapping.json` consistency with CLI namespaces |
| `start-only.sh` | Worker script that processes a single namespace; called by `start.sh` in a loop |
| `generate-tool-family.sh` | Bash wrapper that invokes `Generate-ToolFamily.ps1` via `pwsh -File` for cross-platform compatibility |
| `Generate-ToolFamily.ps1` | Main per-namespace orchestrator running Steps 1–5 in sequence |
| `Invoke-CliAnalyzer.ps1` | Runs the CliAnalyzer .NET project to extract tool metadata from CLI JSON |
| `1-Generate-AnnotationsParametersRaw-One.ps1` | Step 1: generates annotation, parameter, and raw tool include files for one namespace |
| `2-Generate-ExamplePrompts-One.ps1` | Step 2: generates AI example prompts via Azure OpenAI and validates them for one namespace |
| `3-Generate-ToolGenerationAndAIImprovements-One.ps1` | Step 3: runs ToolGeneration_Composed (placeholder replacement) and ToolGeneration_Improved (AI improvements) for one namespace |
| `4-Generate-ToolFamilyCleanup-One.ps1` | Step 4: assembles tool family files with AI metadata for one namespace |
| `5-Generate-HorizontalArticles-One.ps1` | Step 5: generates horizontal how-to articles using AI for one namespace |

## batch/

All-namespace versions of the step scripts. These process every namespace in a single run and were superseded by the `start.sh` orchestrator/worker pattern that loops over namespaces calling the `-One.ps1` variants.

| Script | Equivalent pipeline step |
|---|---|
| `1-Generate-AnnotationsParametersRaw.ps1` | Step 1 (all namespaces) |
| `2-Generate-ToolGenerationAndAIImprovements.ps1` | Step 3 (all namespaces) |
| `3-Generate-ExamplePrompts.ps1` | Step 2 (all namespaces) |
| `4-Generate-ToolFamilyFiles.ps1` | Step 4 (all namespaces) |
| `5-Validate-total-tool-count-and-family.ps1` | Cross-namespace validation: checks total tool counts between CLI output and generated files |
| `Generate-ToolPages.ps1` | Orchestrates complete tool page generation across all namespaces |

## standalone/

Individual generator scripts for running a single generation step directly. Useful during development and debugging to re-run one generator without executing the full pipeline.

| Script | Purpose |
|---|---|
| `Generate-Annotations.ps1` | Generates annotation include files (tool metadata: destructive, idempotent, etc.) |
| `Generate-Parameters.ps1` | Generates parameter include files (CLI options for each tool) |
| `Generate-RawTools.ps1` | Generates raw tool `.md` files from CLI output |
| `Generate-ExamplePrompts.ps1` | Runs ExamplePromptGeneratorStandalone against existing CLI data |
| `Generate-ExamplePromptsAI.ps1` | Generates NL example prompts via Azure OpenAI |
| `Generate-HorizontalArticles.ps1` | Generates horizontal how-to articles using AI |
| `Generate-Commands.ps1` | Generates the commands reference page listing all tools |
| `Generate-Common.ps1` | Generates a "most common tools" summary page |
| `Generate-Index.ps1` | Generates the documentation index page |
| `GenerateToolFamilyCleanup-multifile.ps1` | Multi-phase tool family assembly with AI metadata |
| `Validate.ps1` | Final validation checking all expected files were generated |
| `Validate-ExamplePrompts-RequiredParams.ps1` | Validates example prompts contain required parameters (regex, no LLM) |
| `verify-annotation-hints.js` | Verifies that annotation INCLUDE statements have required "Tool annotation hints" line before them |
| `verify-annotation-references.js` | Verifies annotation files are properly referenced in tool files (no orphans, no duplicates, no missing files) |

## utilities/

Development, debugging, testing, and CI helper scripts.

| Script | Purpose |
|---|---|
| `Debug-MultiPageDocs.ps1` | Sets up environment for VS Code F5 debugging of the C# generator |
| `Test-ExamplePrompts.ps1` | Debug/test harness for example prompts with verbose logging |
| `parse-commands.ps1` | Parses CLI output JSON into structured CSV (namespace/family/tool/operation) |
| `compare-generation.sh` | Compares baseline vs current generation output (file counts + diffs) |
| `copilot-verify.sh` | Verifies GitHub repo settings for Copilot/Actions (permissions, approvals) |

## legacy/

Fully superseded scripts retained for reference.

| Script | Superseded by |
|---|---|
| `Generate.ps1` | Original monolithic orchestrator. Replaced by `start.sh` → `Generate-ToolFamily.ps1` pipeline |
| `Generate-CompleteTools.ps1` | Used `--complete-tools` flag. Replaced by Raw → Composed → Improved pipeline |
