# docs-generation/scripts

Scripts and entry points for the Azure MCP documentation generation pipeline.

## Directory structure

```
scripts/
├── (root)          # Thin entry wrappers + manual/fallback step scripts
├── standalone/     # Individual generator scripts for direct execution
└── utilities/      # Dev, debug, testing, and CI helpers
```

## Standard execution path

The standard pipeline path is now:

```
start.sh
└── docs-generation/PipelineRunner/PipelineRunner.csproj
    ├── preflight.ps1                                # bootstrap: env, build, CLI metadata, brand validation
    └── typed C# namespace steps (Steps 1-6)
        ├── AnnotationsParametersRawStep             # Step 1
        ├── ExamplePromptsStep                       # Step 2
        ├── ToolGenerationStep                       # Step 3
        ├── ToolFamilyCleanupStep                    # Step 4
        ├── SkillsRelevanceStep                      # Step 5
        └── HorizontalArticlesStep                   # Step 6
```

All six standard steps are now typed C# classes under `docs-generation/PipelineRunner/Steps/Namespace/`.
The numbered `*-One.ps1` scripts remain in the repo as reference, fallback, and ad-hoc/manual execution helpers, but they are no longer in the standard `start.sh` execution path.

### Typed standard steps

| Step | Typed class | Generator(s) invoked | Failure policy | Primary outputs |
|---|---|---|---|---|
| 1 | `AnnotationsParametersRawStep` | `CSharpGenerator`, `ToolGeneration_Raw` | Fatal | `annotations/`, `parameters/`, `tools-raw/` |
| 2 | `ExamplePromptsStep` | `ExamplePromptGeneratorStandalone`, `ExamplePromptValidator` | Fatal | `example-prompts/`, `example-prompts-prompts/`, `example-prompts-raw-output/` |
| 3 | `ToolGenerationStep` | `ToolGeneration_Composed`, `ToolGeneration_Improved` | Fatal | `tools-composed/`, `tools/` |
| 4 | `ToolFamilyCleanupStep` | `ToolFamilyCleanup` + typed post-validator | Fatal | `tool-family-metadata/`, `tool-family-related/`, `tool-family/`, `reports/` |
| 5 | `SkillsRelevanceStep` | `SkillsRelevance` | Warn | `skills-relevance/` |
| 6 | `HorizontalArticlesStep` | `HorizontalArticleGenerator` | Fatal | `horizontal-articles/` |

Step 5 remains warning-only by design: skills relevance is supplementary and must not halt the main pipeline.

## PipelineRunner CLI

Run the typed runner from the repo root:

```bash
dotnet run --project docs-generation/PipelineRunner/PipelineRunner.csproj -- --namespace compute --steps 1,2,3,4,5,6 --output ./generated-compute
```

### Supported options

| Option | Meaning |
|---|---|
| `--namespace <name>` | Run a single namespace/service area. Omit to process all namespaces discovered from CLI metadata. |
| `--steps <csv>` | Comma-separated step list. Default: `1,2,3,4,5,6`. |
| `--output <path>` | Output directory. Defaults to `./generated` or `./generated-<namespace>`. |
| `--skip-build` | Reuse existing Release outputs and skip build work. |
| `--skip-validation` | Skip validation behavior that is modeled in the runner/steps. |
| `--dry-run` | Print the resolved plan, including step names, scopes, failure policies, and implementation type, without running bootstrap or generators. |

### Example commands

```bash
# Full typed plan for one namespace without running generators
dotnet run --project docs-generation/PipelineRunner/PipelineRunner.csproj -- --namespace compute --dry-run

# Run only the core documentation pipeline
dotnet run --project docs-generation/PipelineRunner/PipelineRunner.csproj -- --namespace compute --steps 1,2,3,4

# Run only supplementary outputs after core content already exists
dotnet run --project docs-generation/PipelineRunner/PipelineRunner.csproj -- --namespace compute --steps 5,6 --skip-build
```

## `start.sh` backward compatibility

`start.sh` remains the operator-facing shell entry point and preserves the existing positional workflow:

```bash
./start.sh                # all namespaces, all 6 steps
./start.sh compute        # one namespace, all 6 steps
./start.sh compute 1,2,3  # one namespace, selected steps
./start.sh 1,2,3          # all namespaces, selected steps
```

Compatibility notes:

- `start.sh` now delegates to `PipelineRunner` instead of `Generate-ToolFamily.ps1`.
- If the first argument starts with `-`, `start.sh` passes all arguments through directly to `PipelineRunner`.
- The numbered PowerShell wrappers stay available for manual runs, but the supported day-to-day path is `start.sh` → `PipelineRunner`.

## Root scripts

| Script | Current role |
|---|---|
| `preflight.ps1` | Still used by `PipelineRunner` bootstrap for environment checks, build coordination, CLI metadata, and brand validation |
| `validate-env.ps1` | Validates `.env` for Azure OpenAI-backed steps |
| `0-Validate-BrandMappings.ps1` | Validates `brand-to-server-mapping.json` consistency with CLI namespaces |
| `Generate-ToolFamily.ps1` | Legacy/manual PowerShell orchestrator retained for reference and fallback |
| `1-Generate-AnnotationsParametersRaw-One.ps1` | Legacy/manual Step 1 wrapper retained for reference and fallback |
| `2-Generate-ExamplePrompts-One.ps1` | Legacy/manual Step 2 wrapper retained for reference and fallback |
| `3-Generate-ToolGenerationAndAIImprovements-One.ps1` | Legacy/manual Step 3 wrapper retained for reference and fallback |
| `4-Generate-ToolFamilyCleanup-One.ps1` | Legacy/manual Step 4 wrapper retained for reference and fallback |
| `5-Generate-SkillsRelevance-One.ps1` | Legacy/manual Step 5 wrapper retained for reference and fallback |
| `6-Generate-HorizontalArticles-One.ps1` | Legacy/manual Step 6 wrapper retained for reference and fallback |
| `Invoke-CliAnalyzer.ps1` | Manual helper for CLI analyzer generation |

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
