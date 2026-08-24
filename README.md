# Azure MCP Documentation Generator

Automated system for generating comprehensive markdown documentation for Microsoft Azure Model Context Protocol (MCP) server tools. A typed .NET pipeline (PipelineRunner) orchestrates 7 steps — from raw CLI metadata extraction through AI-enhanced article assembly — producing 800+ markdown files across 52 Azure service namespaces.

## Quick Start

### Running the Generator

Generate documentation for all Azure services (52 namespaces):

```bash
./start.sh
```

Generate for a single service (outputs to `./generated-advisor/`):

```bash
./start.sh advisor
```

Generate with specific steps only:

```bash
./start.sh 1,2,3          # All services, steps 1-3 (output: ./generated/)
./start.sh advisor 1,2    # advisor only, steps 1-2 (output: ./generated-advisor/)
```

Skip dependency validation for fast iteration on a single step:

```bash
./start.sh advisor 4 --skip-deps   # Run step 4 without requiring steps 1-3
```

**Note**: When a specific namespace is provided, output goes to `./generated-<namespace>/` instead of `./generated/`. This allows you to work on a single service without affecting the full documentation set.

### Generate all namespace family files from versioned metadata

Use `start.sh` as the primary, general-purpose entry point for the typed pipeline. To generate namespace families from the version named by `mcp-cli-metadata/tracked-version.txt`, use the root PowerShell script:

```powershell
# All namespaces (default)
pwsh -File ./start-with-logs.ps1

# Specific metadata namespaces or command-family roots (comma list)
pwsh -File ./start-with-logs.ps1 -NamespaceList "advisor,appservice,compute"

# Selectors from a text file (one per line, # comments supported)
pwsh -File ./start-with-logs.ps1 -NamespaceFile ./my-namespaces.txt
```

The same command works from PowerShell or Git Bash. Store AI settings in `.azure/<environment>/.env`. The script uses the environment named by `defaultEnvironment`; if no default resolves, it accepts one unambiguous nested environment. A root `.azure/.env` is the fallback when no nested environment file exists.

Before generation, the script fails if the tracked metadata snapshot is absent or unusable, then validates the required keyless settings: `FOUNDRY_ENDPOINT`, `FOUNDRY_MODEL_NAME`, `FOUNDRY_MODEL_API_VERSION`, and `FOUNDRY_USE_DEFAULT_CREDENTIAL=true`. Explicit `-NamespaceList` and `-NamespaceFile` selectors can be concrete metadata namespaces from `cli-namespace.json` or command-family roots discovered from `cli-output.json`. Without an explicit selector, the script dispatches every concrete namespace from `cli-namespace.json`. It calls `start.sh <selector> 1,2,3,4,5,6` for each selected entry. Later entries reuse the first run's build and CLI installation, output goes to `generated-<selector>/`, and `start.sh` progress streams directly.

To validate the real metadata and resolved environment without creating or changing `generated/`, run:

```powershell
pwsh -File ./start-with-logs.ps1 -PreflightOnly
```

This specialized script doesn't replace `start.sh`: it is intended for versioned namespace family generation and doesn't run Step 6.

### Parallel Execution (Fan-Out)

After preflight (Step 0) completes once, individual namespaces can run **in parallel** since each writes to its own isolated `generated-<namespace>/` directory:

```bash
# Run preflight once (builds solution, extracts CLI metadata)
./start.sh advisor 1    # Any namespace triggers preflight

# Then fan out multiple namespaces in parallel
./start.sh compute &
./start.sh storage &
./start.sh keyvault &
./start.sh cosmos &
wait  # Wait for all to complete
```

Or run specific steps in parallel:

```bash
# Fan out Step 5-6 for namespaces that already have Steps 1-4 on disk
./start.sh appservice 5,6 &
./start.sh compute 5,6 &
./start.sh cosmos 5,6 &
wait
```

**Safe because**: Each namespace writes to `generated-<namespace>/`, shared CLI metadata is read-only after preflight, and the C# pipeline runner uses instance-scoped state with no global locks.

### Pipeline Steps

`start.sh` now runs typed `BootstrapStep` (Step 0) once, then the per-namespace pipeline below:

| Phase | Description | Typical output | AI Required |
|------|-------------|----------------|-------------|
| 0 | Typed bootstrap: validate Azure AI Services config when needed, clean/create output folders, build the solution, extract MCP CLI metadata, validate brand mappings, and run shared parsers | `cli/`, `e2e-test-prompts/`, build artifacts, brand validation output | No |
| 1 | Generate annotations, parameter files, and raw tool markdown | `annotations/`, `parameters/`, `tools-raw/` | No |
| 2 | Generate example prompts for each tool | `example-prompts/`, `example-prompts-prompts/` | Yes |
| 3 | Generate composed and AI-improved tool files | `tools/` | Yes |
| 4 | Assemble the tool-family article, generate related metadata, and run post-assembly validation | `tool-family/{namespace}.md`, `reports/tool-family-validation-{namespace}.txt` | Yes |
| 5 | Generate GitHub Copilot skills relevance reports (supplementary, non-fatal) | `skills-relevance/{namespace}-skills-relevance.md` | No |
| 6 | Generate horizontal articles | `horizontal-articles/horizontal-article-{namespace}.md` | Yes |

**Note**: Steps 2, 3, 4, and 6 require Azure AI Services (Foundry-compatible) keyless configuration in `mcp-tools/.env`. Use `FOUNDRY_USE_DEFAULT_CREDENTIAL=true` with endpoint, model name, and API version. `FOUNDRY_API_KEY` is not required or supported for repo generation workflows. See [mcp-tools/scripts/README.md](mcp-tools/scripts/README.md) for details.

**Live endpoint probe**: Phase 0/Bootstrap also makes one live Azure OpenAI call right after config presence is confirmed, to prove the configured endpoint actually works before Steps 2–6 run. A non-interactive or redirected-input run fails immediately (nonzero exit) if the probe fails. An interactive run is prompted to continue; declining fails the same way, while confirming Continue records a loud critical-failure entry, disables all further AI calls for the run, and proceeds with deterministic/verbatim work only — AI-required artifacts are marked incomplete, never reported as successful. See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md#live-ai-endpoint-probe--partial_explicit-offline-continuation-ad-042) for details.

### Verify keyless AI configuration

Run the keyless guard tests before changing any generative-AI entry point:

```bash
dotnet test mcp-doc-generation.sln --filter Category=Keyless
dotnet test skills-generation/skills-generation.slnx --filter Category=Keyless
```

Or run both with the helper:

```bash
./verify-keyless.sh
# or
pwsh -File ./verify-keyless.ps1
```

## Key Paths

- **Entry point:** `start.sh`
- **Worker/orchestration scripts:** `mcp-tools/scripts/`
- **C#/.NET generators:** `mcp-tools/DocGeneration.Steps.AnnotationsParametersRaw.Annotations/`, `mcp-tools/DocGeneration.Steps.ExamplePrompts.Generation/`, `mcp-tools/DocGeneration.Steps.ToolFamilyCleanup/`, `mcp-tools/DocGeneration.Steps.SkillsRelevance/`, `mcp-tools/DocGeneration.Steps.HorizontalArticles/`, `mcp-tools/DocGeneration.Core.GenerativeAI/`, `mcp-tools/DocGeneration.Core.TemplateEngine/`, `mcp-tools/DocGeneration.Utilities.ToolMetadataExtractor/`
- **Prompt templates:** `mcp-tools/prompts/`
- **Handlebars templates:** `mcp-tools/templates/`
- **Configuration data:** `mcp-tools/data/`
- **MCP CLI metadata extraction:** `mcp-cli-metadata/`
- **Generated output:** `generated/` or `generated-<namespace>/`

### Legacy naming notes

- `RelatedSkillsGenerator` and `SkillList` were superseded by the typed Step 5 package `mcp-tools/DocGeneration.Steps.SkillsRelevance/`, which now owns both the per-namespace skills relevance report and the skills index output.
- The live Step 4 compiled project is `mcp-tools/DocGeneration.Steps.ToolFamilyCleanup/`; `mcp-tools/ToolFamily/` remains planning/reference documentation, not a build project.
- `ToolMetadataEnricher` is not present on `squad/dotnet-naming-standards`; if it is restored, its naming-standard home is `DocGeneration.Steps.Bootstrap.ToolMetadataEnricher`.

**Example `.env` configuration**:

```ini
FOUNDRY_USE_DEFAULT_CREDENTIAL="true"
FOUNDRY_ENDPOINT="https://your-resource.openai.azure.com/"
FOUNDRY_MODEL_NAME="gpt-4o-mini"
FOUNDRY_MODEL_API_VERSION="2025-01-01-preview"
TOOL_FAMILY_CLEANUP_FOUNDRY_MODEL_NAME="gpt-4o"
TOOL_FAMILY_CLEANUP_FOUNDRY_MODEL_API_VERSION="2025-01-01-preview"
```

## Output Artifacts

Output location depends on how you run the pipeline:
- **Full catalog** (`./start.sh`): `./generated/`
- **Single namespace** (`./start.sh advisor`): `./generated-advisor/`

### 1. Tool family articles

Primary publishable output for each namespace:

```
./generated/tool-family/
├── acr.md
├── advisor.md
├── aks.md
├── storage.md
├── keyvault.md
└── ...
```

Each file assembles the namespace into one article with tool descriptions, parameter tables, example prompts, annotations, and related content.

### 2. Validation reports

Post-assembly validation now writes one report per namespace and blocks the pipeline on missing-tool or tool-count mismatches.

```
./generated/reports/
└── tool-family-validation-{namespace}.txt
```

Warning-only checks in the same report cover required-parameter coverage in example prompts, standard example headers, annotation marker counts, and basic branding drift.

### 3. Horizontal articles

Cross-cutting "how-to" guides for service-level scenarios:

```
./generated/horizontal-articles/
├── horizontal-article-acr.md
├── horizontal-article-storage.md
├── horizontal-article-keyvault.md
└── ...
```

### 4. Supporting artifacts for review/debugging

- `tools/` - composed tool markdown used to assemble the final family article
- `tools-raw/` - raw tool markdown from initial extraction
- `annotations/` and `parameters/` - reusable partial content
- `example-prompts/` and `example-prompts-prompts/` - generated prompts plus the exact AI input used to create them
- `skills-relevance/` - supplementary GitHub Copilot skills relevance reports
- `cli/` - MCP CLI metadata snapshots (`cli-output.json`, `cli-namespace.json`, `cli-version.json`, `azmcp-commands.json`)
- `run-accounting.json` - per-run six-category summary (successful/root-failed/warning-only/suppressed namespaces plus frozen-baseline reconciliation); see [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md#runtime-dependency-suppression-ad-029)
- `logs/` - run logs and diagnostics

> When a `Fatal` step does not cleanly succeed — a nonzero exit, **or** a recorded *blocking* artifact failure even when the step itself reports success — the steps that transitively depend on it are **suppressed** (skipped) rather than aborting the whole run. A suppressed step writes a `step-result.json` marked `suppressed: true` and produces no other output; independent steps and later namespaces still run. A Step 2 required-parameter/example-prompt validation failure that survives automatic retries is recorded and still shown as a warning, but is non-blocking and does **not** trigger suppression on its own. See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md#runtime-dependency-suppression-ad-029).

## Folder Organization

```
microsoft-mcp-doc-generation/
├── start.sh                     # Entry point (bash wrapper → PipelineRunner)
├── mcp-doc-generation.sln          # .NET 9 solution
│
├── docs/                        # Documentation
│   ├── QUICK-START.md           # 5-minute guide
│   ├── START-SCRIPTS.md         # Detailed start.sh options
│   ├── ARCHITECTURE.md          # System architecture + data flow
│   ├── GENERATION-SCRIPTS.md    # Script execution order
│   ├── PROJECT-GUIDE.md         # Full developer guide
│   └── pipeline-regression-runbook.md  # CI regression gate contributor guide
│
├── mcp-tools/             # Generation system
│   ├── DocGeneration.PipelineRunner/          # Typed orchestrator (Steps 0-6)
│   │   ├── Program.cs                        # CLI entry (System.CommandLine)
│   │   ├── PipelineRunner.cs                 # Core runner loop
│   │   ├── Registry/StepRegistry.cs          # Step registration
│   │   ├── Steps/Bootstrap/                  # Step 0: env, build, CLI
│   │   ├── Steps/Namespace/                  # Steps 1-6
│   │   └── Validation/                       # Post-assembly validators
│   ├── DocGeneration.Steps.*/                # Generator projects (one per step)
│   ├── DocGeneration.Core.*/                 # Shared libraries
│   ├── scripts/                 # Legacy PowerShell (fallback only)
│   ├── data/                    # Configuration files (JSON)
│   ├── prompts/                 # AI prompt templates
│   └── templates/               # Handlebars templates
│
├── generated/                   # Output directory (created during generation)
│   ├── tool-family/             # Main output: service documentation
│   ├── horizontal-articles/     # Service-level how-to guides
│   ├── tools/                   # Composed/AI-improved tool files
│   ├── tools-raw/               # Raw tool files from step 1
│   ├── annotations/             # Tool annotation includes
│   ├── parameters/              # Parameter documentation
│   ├── example-prompts/         # AI-generated examples
│   ├── example-prompts-prompts/ # Prompt captures for example generation
│   ├── skills-relevance/        # GitHub Copilot skills reports
│   ├── reports/                 # Validation reports
│   ├── run-accounting.json      # Per-run six-category outcome summary
│   └── logs/                    # Generation logs
│
├── mcp-cli-metadata/          # MCP CLI metadata extractor
└── start.sh                     # Main entry point
```

## Prompt Dependency System

**Critical**: Documentation generation is heavily dependent on AI prompts that guide content quality and structure.

### Prompt Locations

Prompts are distributed across generator projects based on their purpose:

#### 1. Example Prompts (`mcp-tools/DocGeneration.Steps.ExamplePrompts.Generation/prompts/`)
```
prompts/
├── system-prompt.txt                    # AI behavior for example generation
└── user-prompt.txt                      # Template for tool-specific prompts
```
**Purpose**: Generates 5 natural language example prompts per tool  
**Output**: `./generated/example-prompts/{tool}-example-prompts.md`

#### 2. Tool Family Cleanup (`mcp-tools/DocGeneration.Steps.ToolFamilyCleanup/prompts/`)
```
prompts/
├── tool-family-cleanup-system-prompt.txt   # Style guide and formatting rules
├── tool-family-cleanup-user-prompt.txt     # Tool-specific instructions
├── h2-heading-user-prompt.txt              # Heading generation
├── family-metadata-system-prompt.txt       # Family metadata
├── related-content-system-prompt.txt       # Related content
└── related-content-user-prompt.txt
```
**Purpose**: AI-based formatting, structure improvements, metadata generation  
**Output**: Improved `./generated/tool-family/{namespace}.md` files

#### 3. Horizontal Articles (`mcp-tools/DocGeneration.Steps.HorizontalArticles/prompts/`)
```
prompts/
├── horizontal-article-system-prompt.txt    # How-to article format
└── horizontal-article-user-prompt.txt      # Service-specific guide template
```
**Purpose**: Generates service-specific how-to guides  
**Output**: `./generated/horizontal-articles/horizontal-article-{service}.md`

#### 4. Tool Description Analysis (`mcp-tools/prompts/`)
```
prompts/
├── tool-description-analyzer-prompt.md     # Description quality analysis
├── system-prompt-example-prompt.txt        # Example prompt system behavior
└── user-prompt-example-prompt.txt          # Example prompt user template
```
**Purpose**: Analyzes and improves tool descriptions  
**Output**: Various analysis and improvement files

### Reviewing Generated Prompts

All AI prompts sent to Azure OpenAI are saved for review and debugging:

```
./generated/
├── example-prompts-prompts/           # Prompts sent for example generation
│   └── {tool}-input-prompt.md
├── horizontal-article-prompts/        # Prompts sent for horizontal articles
│   └── horizontal-article-{service}-prompt.md
└── logs/                              # Detailed generation logs
    └── debug-{timestamp}.log
```

**Why saved?**  
- Debug AI responses that don't match expectations
- Iterate on prompt improvements
- Understand what context was provided to the AI
- Validate prompt template rendering

### Customizing Prompts

To modify AI-generated content quality or style:

1. **Edit the prompt files** in their respective `prompts/` directories
2. **Regenerate documentation** for the affected step:
   ```bash
   ./start.sh advisor 2      # Regenerate example prompts only
   ./start.sh advisor 4      # Regenerate tool family cleanup
   ./start.sh advisor 5      # Regenerate horizontal articles
   ```
3. **Review generated prompts** in `./generated/` to verify changes
4. **Iterate** until desired output quality is achieved

## Documentation

### Getting Started

| Document | Description |
|----------|-------------|
| [CHANGELOG.md](CHANGELOG.md) | All notable changes to the project |
| [docs/QUICK-START.md](docs/QUICK-START.md) | 5-minute setup guide |
| [docs/PROJECT-GUIDE.md](docs/PROJECT-GUIDE.md) | Full developer guide — extending, testing, troubleshooting |

### Architecture & Design

| Document | Description |
|----------|-------------|
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | System architecture, data flow, pipeline step details |
| [docs/configuration-registry.md](docs/configuration-registry.md) | Configuration files inventory, load order, schemas, duplication analysis |
| [docs/PRD-PipelineRunner.md](docs/PRD-PipelineRunner.md) | Product requirements for the typed .NET pipeline |
| [mcp-tools/README.md](mcp-tools/README.md) | Generator implementation details |

### Pipeline & Scripts

| Document | Description |
|----------|-------------|
| [docs/START-SCRIPTS.md](docs/START-SCRIPTS.md) | Complete `start.sh` usage with all options |
| [docs/GENERATION-SCRIPTS.md](docs/GENERATION-SCRIPTS.md) | Script execution order and dependencies |
| [docs/GET-MCP-VERSION.md](docs/GET-MCP-VERSION.md) | Retrieving MCP version information |
| [docs/ci-integration.md](docs/ci-integration.md) | CI pipeline structure, local dev commands, test inventory, debugging guide |
| [docs/VALIDATION-RUNBOOK.md](docs/VALIDATION-RUNBOOK.md) | Manual validation workflow for repo-local article-health and coverage scripts |

### Quality & Testing

| Document | Description |
|----------|-------------|
| [docs/test-strategy.md](docs/test-strategy.md) | Test strategy for the documentation pipeline |
| [docs/beta34-baseline-freeze.md](docs/beta34-baseline-freeze.md) | Frozen beta.34 critical-failure evidence baseline (#813) — immutability, regeneration, classification, sanitization |
| [docs/FINGERPRINTING.md](docs/FINGERPRINTING.md) | Baseline fingerprinting tool — snapshot and diff generated output |
| [docs/acrolinx-compliance-strategy.md](docs/acrolinx-compliance-strategy.md) | Acrolinx compliance strategy for tool-family articles |
| [mcp-tools/validation/README.md](mcp-tools/validation/README.md) | Repo-local validation scripts, test fixtures, and manual execution commands |
| [mcp-tools/DocGeneration.PromptRegression.Tests/README.md](mcp-tools/DocGeneration.PromptRegression.Tests/README.md) | Prompt regression testing — baselines, metrics, comparison |
| [docs/prompt-versioning.md](docs/prompt-versioning.md) | Prompt versioning — SHA256 hashing, `PromptSnapshot`, `StepResultFile` v2 schema |

### AI & Content Generation

| Document | Description |
|----------|-------------|
| [docs/tool-generation-and-ai-improvements.md](docs/tool-generation-and-ai-improvements.md) | Tool generation and AI enhancement strategies |
| [mcp-tools/DocGeneration.Steps.ExamplePrompts.Generation/README.md](mcp-tools/DocGeneration.Steps.ExamplePrompts.Generation/README.md) | Example prompt generation (Step 2) |
| [mcp-tools/DocGeneration.Steps.HorizontalArticles/README.md](mcp-tools/DocGeneration.Steps.HorizontalArticles/README.md) | Horizontal article generation (Step 6) |

### Planning & Decisions

| Document | Description |
|----------|-------------|
| [.squad/decisions.md](.squad/decisions.md) | Architectural decisions log (AD-001 through AD-025) |
| [docs/plans/HORIZONTAL-ARTICLE-IMPROVEMENT-PLAN.md](docs/plans/HORIZONTAL-ARTICLE-IMPROVEMENT-PLAN.md) | Plan for horizontal article improvements |
| [docs/plans/TEMPLATE-ENGINE-EXTRACTION.md](docs/plans/TEMPLATE-ENGINE-EXTRACTION.md) | Plan for template engine extraction |
| [docs/text-transformation-migration-plan.md](docs/text-transformation-migration-plan.md) | NaturalLanguage → TextTransformation migration plan (#351) |

### Utilities

| Document | Description |
|----------|-------------|
| [mcp-cli-metadata/README.md](mcp-cli-metadata/README.md) | MCP CLI metadata extractor |

### Repository Configuration

| Document | Description |
|----------|-------------|
| [.github/scripts/README.md](.github/scripts/README.md) | Repository configuration scripts |
| [.github/how-to/REBRANCH-ON-MAIN.md](.github/how-to/REBRANCH-ON-MAIN.md) | How to rebranch on main |

## Prerequisites

### Required
- **.NET SDK** — For C# generator projects (projects use .NET 9.0) and CLI metadata extraction (`mcp-tools/McpCliMetadata/`, .NET 10)
- **PowerShell (pwsh)** — For orchestration scripts (`preflight.ps1`, etc.)

### Optional (for AI-enhanced steps)
- **Azure OpenAI** - For steps 2, 3, 4, and 6 (example prompts, improvements, assembly cleanup, horizontal articles)
- **GitHub CLI (`gh`)** - For step 5 (skills relevance). Must be authenticated (`gh auth login`). The `GITHUB_TOKEN` env var is used for GitHub API calls; without it, unauthenticated rate limits (60 req/hr) apply.

### Configuration

For AI-enhanced generation (steps 2, 3, 4, and 6), configure Azure OpenAI credentials:

```bash
# Copy sample environment file
cp mcp-tools/sample.env mcp-tools/.env

# Edit .env with your credentials
FOUNDRY_API_KEY="your-api-key"
FOUNDRY_ENDPOINT="https://your-resource.openai.azure.com/"
FOUNDRY_MODEL_NAME="gpt-4o-mini"

# For step 5 (skills relevance) — set GitHub token for higher rate limits
# Generate from authenticated gh CLI:
#   export GITHUB_TOKEN=$(gh auth token)    # bash
#   $env:GITHUB_TOKEN = (gh auth token)     # PowerShell
GITHUB_TOKEN="your-github-token"
```

## Output Structure

```
fingerprint-baseline.json        # Known-good output snapshot (see docs/FINGERPRINTING.md)

generated/
├── cli/                         # MCP CLI metadata (shared by all)
│   ├── cli-version.json
│   ├── cli-output.json
│   ├── cli-namespace.json
│   └── azmcp-commands.json
│
├── tool-family/                 # ⭐ Main output: service documentation
│   └── {namespace}.md
│
├── horizontal-articles/         # ⭐ Service-level how-to guides
│   └── horizontal-article-{namespace}.md
│
├── tools/                       # Composed and AI-improved tool markdown
├── tools-raw/                   # Raw extracted tool markdown
├── annotations/                 # Tool annotation includes
├── parameters/                  # Parameter documentation
├── example-prompts/             # AI-generated example prompts
├── example-prompts-prompts/     # Prompts sent to AI (for review)
├── horizontal-article-prompts/  # Prompts sent to AI (for review)
├── skills-relevance/            # GitHub Copilot skills relevance reports
├── reports/                     # Validation and analysis reports
│   └── tool-family-validation-{namespace}.txt
└── logs/                        # Generation logs
```

## Performance

| Configuration | Duration | Notes |
|--------------|----------|-------|
| Single service (Step 1 only) | ~1 min | No AI calls |
| Single service (all steps) | ~25-30 min | Full pipeline with AI |
| All services (Step 1 only) | ~52 min | Fast, no AI |
| All services (all steps) | ~22-26 hours | Sequential AI processing |

**Note**: Times assume sequential processing. Step 1 can be run quickly without AI credentials for basic documentation.

## License

[MIT License](LICENSE)

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for contribution guidelines.

**Note**: This repository uses GitHub issue templates. All new issues must use the [bug](/.github/ISSUE_TEMPLATE/bug.yml) or [feature](/.github/ISSUE_TEMPLATE/feature.yml) template.

---

**Last Updated**: March 2026  
**Maintained By**: @diberry
