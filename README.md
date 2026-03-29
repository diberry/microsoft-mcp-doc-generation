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
| 0 | Typed bootstrap: validate Azure OpenAI config when needed, clean/create output folders, build the solution, extract MCP CLI metadata, validate brand mappings, and run shared parsers | `cli/`, `e2e-test-prompts/`, build artifacts, brand validation output | No |
| 1 | Generate annotations, parameter files, and raw tool markdown | `annotations/`, `parameters/`, `tools-raw/` | No |
| 2 | Generate example prompts for each tool | `example-prompts/`, `example-prompts-prompts/` | Yes |
| 3 | Generate composed and AI-improved tool files | `tools/` | Yes |
| 4 | Assemble the tool-family article, generate related metadata, and run post-assembly validation | `tool-family/{namespace}.md`, `reports/tool-family-validation-{namespace}.txt` | Yes |
| 5 | Generate GitHub Copilot skills relevance reports (supplementary, non-fatal) | `skills-relevance/{namespace}-skills-relevance.md` | No |
| 6 | Generate horizontal articles | `horizontal-articles/horizontal-article-{namespace}.md` | Yes |

**Note**: Steps 2, 3, 4, and 6 require Azure OpenAI credentials configured in `docs-generation/.env`. See [docs-generation/scripts/README.md](docs-generation/scripts/README.md) for details.

## Key Paths

- **Entry point:** `start.sh`
- **Worker/orchestration scripts:** `docs-generation/scripts/`
- **C#/.NET generators:** `docs-generation/DocGeneration.Steps.AnnotationsParametersRaw.Annotations/`, `docs-generation/DocGeneration.Steps.ExamplePrompts.Generation/`, `docs-generation/DocGeneration.Steps.ToolFamilyCleanup/`, `docs-generation/DocGeneration.Steps.SkillsRelevance/`, `docs-generation/DocGeneration.Steps.HorizontalArticles/`, `docs-generation/DocGeneration.Core.GenerativeAI/`, `docs-generation/DocGeneration.Core.TemplateEngine/`, `docs-generation/DocGeneration.Utilities.ToolMetadataExtractor/`
- **Prompt templates:** `docs-generation/prompts/`
- **Handlebars templates:** `docs-generation/templates/`
- **Configuration data:** `docs-generation/data/`
- **MCP CLI metadata extraction:** `test-npm-azure-mcp/`
- **Generated output:** `generated/` or `generated-<namespace>/`

### Legacy naming notes

- `RelatedSkillsGenerator` and `SkillList` were superseded by the typed Step 5 package `docs-generation/DocGeneration.Steps.SkillsRelevance/`, which now owns both the per-namespace skills relevance report and the skills index output.
- The live Step 4 compiled project is `docs-generation/DocGeneration.Steps.ToolFamilyCleanup/`; `docs-generation/ToolFamily/` remains planning/reference documentation, not a build project.
- `ToolMetadataEnricher` is not present on `squad/dotnet-naming-standards`; if it is restored, its naming-standard home is `DocGeneration.Steps.Bootstrap.ToolMetadataEnricher`.

**Example `.env` configuration**:

```ini
FOUNDRY_API_KEY="your-api-key-here"
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
- `logs/` - run logs and diagnostics

## Folder Organization

```
microsoft-mcp-doc-generation/
├── start.sh                     # Entry point (bash wrapper → PipelineRunner)
├── docs-generation.sln          # .NET 9 solution
│
├── docs/                        # Documentation
│   ├── QUICK-START.md           # 5-minute guide
│   ├── START-SCRIPTS.md         # Detailed start.sh options
│   ├── ARCHITECTURE.md          # System architecture + data flow
│   ├── GENERATION-SCRIPTS.md    # Script execution order
│   └── PROJECT-GUIDE.md         # Full developer guide
│
├── docs-generation/             # Generation system
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
│   └── logs/                    # Generation logs
│
├── test-npm-azure-mcp/          # MCP CLI metadata extractor
└── start.sh                     # Main entry point
```

## Prompt Dependency System

**Critical**: Documentation generation is heavily dependent on AI prompts that guide content quality and structure.

### Prompt Locations

Prompts are distributed across generator projects based on their purpose:

#### 1. Example Prompts (`docs-generation/DocGeneration.Steps.ExamplePrompts.Generation/prompts/`)
```
prompts/
├── system-prompt.txt                    # AI behavior for example generation
└── user-prompt.txt                      # Template for tool-specific prompts
```
**Purpose**: Generates 5 natural language example prompts per tool  
**Output**: `./generated/example-prompts/{tool}-example-prompts.md`

#### 2. Tool Family Cleanup (`docs-generation/DocGeneration.Steps.ToolFamilyCleanup/prompts/`)
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

#### 3. Horizontal Articles (`docs-generation/DocGeneration.Steps.HorizontalArticles/prompts/`)
```
prompts/
├── horizontal-article-system-prompt.txt    # How-to article format
└── horizontal-article-user-prompt.txt      # Service-specific guide template
```
**Purpose**: Generates service-specific how-to guides  
**Output**: `./generated/horizontal-articles/horizontal-article-{service}.md`

#### 4. Tool Description Analysis (`docs-generation/prompts/`)
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
| [docs/PRD-PipelineRunner.md](docs/PRD-PipelineRunner.md) | Product requirements for the typed .NET pipeline |
| [docs-generation/README.md](docs-generation/README.md) | Generator implementation details |

### Pipeline & Scripts

| Document | Description |
|----------|-------------|
| [docs/START-SCRIPTS.md](docs/START-SCRIPTS.md) | Complete `start.sh` usage with all options |
| [docs/GENERATION-SCRIPTS.md](docs/GENERATION-SCRIPTS.md) | Script execution order and dependencies |
| [docs/GET-MCP-VERSION.md](docs/GET-MCP-VERSION.md) | Retrieving MCP version information |

### Quality & Testing

| Document | Description |
|----------|-------------|
| [docs/test-strategy.md](docs/test-strategy.md) | Test strategy for the documentation pipeline |
| [docs/FINGERPRINTING.md](docs/FINGERPRINTING.md) | Baseline fingerprinting tool — snapshot and diff generated output |
| [docs/acrolinx-compliance-strategy.md](docs/acrolinx-compliance-strategy.md) | Acrolinx compliance strategy for tool-family articles |

### AI & Content Generation

| Document | Description |
|----------|-------------|
| [docs/tool-generation-and-ai-improvements.md](docs/tool-generation-and-ai-improvements.md) | Tool generation and AI enhancement strategies |
| [docs-generation/DocGeneration.Steps.ExamplePrompts.Generation/README.md](docs-generation/DocGeneration.Steps.ExamplePrompts.Generation/README.md) | Example prompt generation (Step 2) |
| [docs-generation/DocGeneration.Steps.HorizontalArticles/README.md](docs-generation/DocGeneration.Steps.HorizontalArticles/README.md) | Horizontal article generation (Step 6) |

### Planning & Decisions

| Document | Description |
|----------|-------------|
| [.squad/decisions.md](.squad/decisions.md) | Architectural decisions log (AD-001 through AD-025) |
| [docs/plans/HORIZONTAL-ARTICLE-IMPROVEMENT-PLAN.md](docs/plans/HORIZONTAL-ARTICLE-IMPROVEMENT-PLAN.md) | Plan for horizontal article improvements |
| [docs/plans/TEMPLATE-ENGINE-EXTRACTION.md](docs/plans/TEMPLATE-ENGINE-EXTRACTION.md) | Plan for template engine extraction |

### Utilities

| Document | Description |
|----------|-------------|
| [verify-quantity/README.md](verify-quantity/README.md) | Generated file verification tool |
| [summary-generator/README.md](summary-generator/README.md) | Documentation summary generator |
| [test-npm-azure-mcp/README.md](test-npm-azure-mcp/README.md) | MCP CLI metadata extractor |

### Repository Configuration

| Document | Description |
|----------|-------------|
| [.github/scripts/README.md](.github/scripts/README.md) | Repository configuration scripts |
| [.github/how-to/REBRANCH-ON-MAIN.md](.github/how-to/REBRANCH-ON-MAIN.md) | How to rebranch on main |

## Prerequisites

### Required
- **Node.js + npm** - For MCP CLI metadata extraction
- **PowerShell (pwsh)** - Optional for legacy/manual fallback scripts; the standard `start.sh` path no longer depends on PowerShell
- **.NET SDK** - For C# generator projects (projects use .NET 9.0)

### Optional (for AI-enhanced steps)
- **Azure OpenAI** - For steps 2, 3, 4, and 6 (example prompts, improvements, assembly cleanup, horizontal articles)
- **GitHub CLI (`gh`)** - For step 5 (skills relevance). Must be authenticated (`gh auth login`). The `GITHUB_TOKEN` env var is used for GitHub API calls; without it, unauthenticated rate limits (60 req/hr) apply.

### Configuration

For AI-enhanced generation (steps 2, 3, 4, and 6), configure Azure OpenAI credentials:

```bash
# Copy sample environment file
cp docs-generation/sample.env docs-generation/.env

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
