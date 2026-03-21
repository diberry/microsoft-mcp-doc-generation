# Azure MCP Documentation Generator

Automated system for generating comprehensive markdown documentation for Microsoft Azure Model Context Protocol (MCP) server tools.

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
├── docs/                        # Documentation
│   ├── QUICK-START.md           # 5-minute guide
│   ├── START-SCRIPTS.md         # Detailed start.sh documentation
│   ├── GENERATION-SCRIPTS.md    # Script execution order
│   └── ARCHITECTURE.md          # System architecture
│
├── docs-generation/             # Generation system
│   ├── DocGeneration.PipelineRunner/          # Typed orchestrator including BootstrapStep + Steps 1-6
│   ├── scripts/                 # Legacy/fallback PowerShell/bash orchestration helpers
│   │   ├── preflight.ps1        # Legacy bootstrap fallback; standard path uses BootstrapStep
│   │   ├── Generate-ToolFamily.ps1
│   │   ├── 1-Generate-AnnotationsParametersRaw-One.ps1
│   │   ├── 2-Generate-ExamplePrompts-One.ps1
│   │   ├── 3-Generate-ToolGenerationAndAIImprovements-One.ps1
│   │   ├── 4-Generate-DocGeneration.Steps.ToolFamilyCleanup-One.ps1
│   │   ├── Validate-ToolFamily-PostAssembly.ps1
│   │   ├── 5-Generate-DocGeneration.Steps.SkillsRelevance-One.ps1
│   │   ├── 6-Generate-HorizontalArticles-One.ps1
│   │   ├── standalone/          # Supporting validation/dev scripts
│   │   └── utilities/           # DocGeneration.Core.Shared helpers and utilities
│   ├── data/                    # Configuration files (JSON)
│   ├── prompts/                 # AI prompt templates (see below)
│   ├── templates/               # Handlebars templates
│   │
│   ├── DocGeneration.Steps.AnnotationsParametersRaw.Annotations/         # Core generator (.NET 9.0)
│   ├── DocGeneration.Steps.ExamplePrompts.Generation/  # AI prompt generator
│   ├── DocGeneration.Steps.HorizontalArticles/        # How-to article generator
│   ├── DocGeneration.Steps.ToolFamilyCleanup/       # AI-based formatting
│   ├── DocGeneration.Core.GenerativeAI/            # Azure OpenAI client
│   └── DocGeneration.Core.Shared/                  # DocGeneration.Core.Shared utilities
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

## Additional Documentation

- **[docs/QUICK-START.md](docs/QUICK-START.md)** - Docker-based 5-minute setup guide
- **[docs/START-SCRIPTS.md](docs/START-SCRIPTS.md)** - Complete start.sh documentation with all options
- **[docs/GENERATION-SCRIPTS.md](docs/GENERATION-SCRIPTS.md)** - Detailed script execution order and dependencies
- **[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)** - System architecture and design decisions
- **[docs-generation/README.md](docs-generation/README.md)** - Generator implementation details

## Prerequisites

### Required
- **Node.js + npm** - For MCP CLI metadata extraction
- **PowerShell (pwsh)** - Optional for legacy/manual fallback scripts; the standard `start.sh` path no longer depends on PowerShell
- **.NET SDK** - For C# generator projects (projects use .NET 9.0)

### Optional (for AI-enhanced steps)
- **Azure OpenAI** - For steps 2, 3, 4, and 6 (example prompts, improvements, assembly cleanup, horizontal articles)

### Configuration

For AI-enhanced generation (steps 2, 3, 4, and 6), configure Azure OpenAI credentials:

```bash
# Copy sample environment file
cp docs-generation/sample.env docs-generation/.env

# Edit .env with your credentials
FOUNDRY_API_KEY="your-api-key"
FOUNDRY_ENDPOINT="https://your-resource.openai.azure.com/"
FOUNDRY_MODEL_NAME="gpt-4o-mini"
```

## Output Structure

```
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
