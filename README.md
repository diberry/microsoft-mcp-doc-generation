# Azure MCP Documentation Generator

Automated system for generating comprehensive markdown documentation for Microsoft Azure Model Context Protocol (MCP) server tools.

## Quick Start

### Running the Generator

Generate documentation for all Azure services (52 namespaces):

```bash
./start.sh
```

Generate for a single service:

```bash
./start.sh advisor
```

Generate with specific steps only:

```bash
./start.sh 1,2,3          # All services, steps 1-3
./start.sh advisor 1,2    # advisor only, steps 1-2
```

### Generation Steps for entire tool set

| Step | Description | Duration | AI Required |
|------|-------------|----------|-------------|
| 1 | Annotations, parameters, raw tools | ~1 min | No |
| 2 | Example prompts (AI-generated) | ~10-15 min | Yes |
| 3 | Composed and AI-improved tools | ~5-10 min | Yes |
| 4 | Tool family metadata and assembly | ~3-5 min | Yes |
| 5 | Horizontal articles (how-to guides) | ~5-10 min | Yes |

**Note**: Steps 2-5 require Azure OpenAI credentials configured in `docs-generation/.env`. See [docs/START-SCRIPTS.md](docs/START-SCRIPTS.md) for details.

**Example `.env` configuration**:

```ini
FOUNDRY_API_KEY="your-api-key-here"
FOUNDRY_ENDPOINT="https://your-resource.openai.azure.com/"
FOUNDRY_MODEL_NAME="gpt-4o-mini"
FOUNDRY_MODEL_API_VERSION="2025-01-01-preview"
TOOL_FAMILY_CLEANUP_FOUNDRY_MODEL_NAME="gpt-4o"
TOOL_FAMILY_CLEANUP_FOUNDRY_MODEL_API_VERSION="2025-01-01-preview"
```

## Critical Outputs

The generator produces two main types of documentation:

### 1. Tool Family Files (`./generated/tool-family/`)

Service-specific documentation files (52 total) - one per Azure namespace. These appear in 1P docs under the tools node.

```
./generated/tool-family/
├── acr.md              # Azure Container Registry
├── advisor.md          # Azure Advisor
├── aks.md              # Azure Kubernetes Service
├── storage.md          # Azure Storage
├── keyvault.md         # Azure Key Vault
└── ... (47 more)
```

Each file contains complete documentation for all tools in that service family, including:
- Tool commands and descriptions
- Parameters with natural language names
- Code examples and usage patterns
- Annotations and best practices

### 2. Horizontal Articles (`./generated/horizontal-articles/`)

Cross-cutting "how-to" guides that explain how to use Azure MCP with specific services. These appear in 1P docs under the services node.

```
./generated/horizontal-articles/
├── horizontal-article-acr.md
├── horizontal-article-storage.md
├── horizontal-article-keyvault.md
└── ... (one per service with tools)
```

These articles provide:
- Getting started guides
- Common scenarios and workflows
- Integration patterns
- Best practices

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
│   ├── scripts/                 # PowerShell/bash orchestration
│   │   ├── standalone/          # Individual generator scripts and validation tools
│   │   ├── batch/               # All-namespace orchestrators
│   │   ├── utilities/           # Dev, debug, testing, and CI helpers
│   │   └── legacy/              # Superseded scripts
│   ├── data/                    # Configuration files (JSON)
│   ├── prompts/                 # AI prompt templates (see below)
│   ├── templates/               # Handlebars templates
│   │
│   ├── CSharpGenerator/         # Core generator (.NET 9.0)
│   ├── ExamplePromptGeneratorStandalone/  # AI prompt generator
│   ├── HorizontalArticleGenerator/        # How-to article generator
│   ├── ToolFamilyCleanup/       # AI-based formatting
│   ├── GenerativeAI/            # Azure OpenAI client
│   └── Shared/                  # Shared utilities
│
├── generated/                   # Output directory (created during generation)
│   ├── tool-family/             # Main output: service documentation
│   ├── horizontal-articles/     # Main output: how-to guides
│   ├── tools/                   # Individual tool files
│   ├── annotations/             # Tool annotation includes
│   ├── parameters/              # Parameter documentation
│   ├── example-prompts/         # AI-generated examples
│   ├── reports/                 # Validation and analysis reports
│   └── logs/                    # Generation logs
│
├── test-npm-azure-mcp/          # MCP CLI metadata extractor
└── start.sh                     # Main entry point
```

## Prompt Dependency System

**Critical**: Documentation generation is heavily dependent on AI prompts that guide content quality and structure.

### Prompt Locations

Prompts are distributed across generator projects based on their purpose:

#### 1. Example Prompts (`docs-generation/ExamplePromptGeneratorStandalone/prompts/`)
```
prompts/
├── system-prompt.txt                    # AI behavior for example generation
└── user-prompt.txt                      # Template for tool-specific prompts
```
**Purpose**: Generates 5 natural language example prompts per tool  
**Output**: `./generated/example-prompts/{tool}-example-prompts.md`

#### 2. Tool Family Cleanup (`docs-generation/ToolFamilyCleanup/prompts/`)
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

#### 3. Horizontal Articles (`docs-generation/HorizontalArticleGenerator/prompts/`)
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
- **PowerShell (pwsh)** - For orchestration scripts
- **.NET SDK** - For C# generator projects (projects use .NET 9.0)

### Optional (for AI-enhanced steps)
- **Azure OpenAI** - For steps 2-5 (example prompts, improvements, horizontal articles)

### Configuration

For AI-enhanced generation (steps 2-5), configure Azure OpenAI credentials:

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
│   └── cli-namespace.json
│
├── tool-family/                 # ⭐ Main output: service documentation
│   └── {namespace}.md (52 files)
│
├── horizontal-articles/         # ⭐ Main output: how-to guides  
│   └── horizontal-article-{service}.md
│
├── tools/                       # Individual tool documentation
├── annotations/                 # Tool annotation includes
├── parameters/                  # Parameter documentation
├── example-prompts/             # AI-generated example prompts
├── example-prompts-prompts/     # Prompts sent to AI (for review)
├── horizontal-article-prompts/  # Prompts sent to AI (for review)
├── reports/                     # Validation and analysis reports
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

---

**Last Updated**: February 2026  
**Maintained By**: @diberry
