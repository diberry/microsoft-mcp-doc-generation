# docs-generation/scripts

Scripts for the Azure MCP documentation generation pipeline and related development tooling.

## Directory structure

```
scripts/
├── (root)          # Active pipeline scripts called from start.sh
├── standalone/     # Individual generator scripts for running a single step
└── utilities/      # Dev, debug, testing, and CI helpers
```

## Active pipeline scripts (root)

These scripts form the current generation flow invoked by the root `start.sh` orchestrator:

```
start.sh
├── preflight.ps1                                         # One-time setup (build, CLI metadata, env + brand validation)
│   ├── validate-env.ps1                                  # Validates .env for Azure OpenAI-backed steps
│   └── 0-Validate-BrandMappings.ps1                      # Validates brand-to-server-mapping.json
└── start-only.sh                                         # Worker: processes one namespace/family
    └── Generate-ToolFamily.ps1                           # Per-namespace orchestrator (core Steps 1-4, optional Steps 5-6)
        ├── Invoke-CliAnalyzer.ps1                        # Runs CliAnalyzer on CLI JSON
        ├── 1-Generate-AnnotationsParametersRaw-One.ps1   # Step 1: annotations, parameters, raw tools
        ├── 2-Generate-ExamplePrompts-One.ps1             # Step 2: example prompts + validation
        ├── 3-Generate-ToolGenerationAndAIImprovements-One.ps1 # Step 3: composed + AI-improved tools
        ├── 4-Generate-ToolFamilyCleanup-One.ps1          # Step 4: tool family assembly
        ├── Validate-ToolFamily-PostAssembly.ps1          # Step 4 sub-step: post-assembly validation
        ├── 5-Generate-SkillsRelevance-One.ps1            # Step 5: skills relevance report
        └── 6-Generate-HorizontalArticles-One.ps1         # Step 6: horizontal how-to articles
```

### Pipeline step model

- **Core pipeline:** Steps 1-4
- **Step 4 validation:** `Validate-ToolFamily-PostAssembly.ps1` runs automatically inside Step 4
- **Supplementary outputs:** Step 5 (skills relevance) and Step 6 (horizontal articles)

### Script details

| Script | Purpose |
|---|---|
| `preflight.ps1` | One-time setup: validates environment, builds the .NET solution, generates CLI metadata, and runs brand validation |
| `validate-env.ps1` | Checks `.env` has the Azure OpenAI credentials required for Steps 2, 3, 4, and 6 |
| `0-Validate-BrandMappings.ps1` | Validates `brand-to-server-mapping.json` consistency with CLI namespaces before the numbered steps run |
| `start-only.sh` | Worker script that processes a single namespace/family; called by `start.sh` in a loop |
| `Generate-ToolFamily.ps1` | Main per-namespace orchestrator running core Steps 1-4, then optional Steps 5-6 |
| `Invoke-CliAnalyzer.ps1` | Runs the CliAnalyzer .NET project to extract tool metadata from CLI JSON |
| `1-Generate-AnnotationsParametersRaw-One.ps1` | Step 1: generates annotation, parameter, and raw tool include files for one namespace/family |
| `2-Generate-ExamplePrompts-One.ps1` | Step 2: generates AI example prompts and validates them for one namespace/family |
| `3-Generate-ToolGenerationAndAIImprovements-One.ps1` | Step 3: runs ToolGeneration_Composed and ToolGeneration_Improved for one namespace/family |
| `4-Generate-ToolFamilyCleanup-One.ps1` | Step 4: assembles tool-family files with AI metadata for one namespace/family |
| `Validate-ToolFamily-PostAssembly.ps1` | Step 4 sub-step: validates the assembled tool-family article and report output |
| `5-Generate-SkillsRelevance-One.ps1` | Step 5: generates GitHub Copilot skills relevance reports for one namespace/family |
| `6-Generate-HorizontalArticles-One.ps1` | Step 6: generates horizontal how-to articles using AI for one namespace/family |

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