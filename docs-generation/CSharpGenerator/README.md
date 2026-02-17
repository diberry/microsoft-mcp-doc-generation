# CSharpGenerator

.NET 9.0 console application that generates annotation and parameter markdown documentation for Azure MCP (Model Context Protocol) tools. It reads structured JSON output from the Azure MCP CLI, transforms it into a documentation model, and renders markdown files using Handlebars templates.

## What it does

CSharpGenerator is called during **Step 1** of the generation pipeline (`1-Generate-AnnotationsParametersRaw-One.ps1`) with two distinct invocations:

1. **`generate-docs <cli-json> <output-dir> --annotations`** — generates per-tool annotation include files (metadata: destructive, idempotent, read-only, secret, local-consent)
2. **`generate-docs <cli-json> <output-dir> --parameters`** — generates per-tool parameter include files (CLI options in a markdown table)

Both invocations also produce security and metadata summary reports.

### Secondary modes (rarely used directly)

The `generate-docs` command also supports `--index`, `--common`, and `--commands` flags for generating aggregate pages. These are not currently invoked by the pipeline.

A standalone `template` mode renders any Handlebars template with arbitrary JSON data:

```
CSharpGenerator template <template-file> <data-file> <output-file>
```

## Architecture

```
Program.cs                          CLI entry point, argument parsing
├── Config.cs                       Loads config.json, initializes TextCleanup
├── DocumentationGenerator.cs       Orchestrates generation across all tools
│   ├── AnnotationGenerator         Per-tool annotation .md files
│   ├── ParameterGenerator          Per-tool parameter table .md files
│   ├── PageGenerator               Area/index/commands/common pages
│   └── FrontmatterUtility          YAML frontmatter generation
└── OptionsDiscovery.cs             Discovers common params from MCP source

External:
└── TemplateEngine                  Handlebars compilation + custom helpers (shared library)
```

### Data flow

```
cli-output.json
    │
    ▼
DocumentationGenerator.TransformCliOutput()
    │  Parses JSON → CliOutput → TransformedData
    │  Groups tools by service area
    │  Loads common parameters from common-parameters.json
    ▼
┌─────────────────────────────────┐
│ AnnotationGenerator             │──▶ generated/annotations/*.md
│ ParameterGenerator              │──▶ generated/parameters/*.md
│ PageGenerator                   │──▶ generated/common-general/*.md
└─────────────────────────────────┘
```

### Key behaviors

- **Common parameter filtering**: Parameters listed in `common-parameters.json` (e.g., `--tenant`, `--subscription`, `--resource-group`) are excluded from parameter tables unless they are marked `Required` for a specific tool.
- **Three-tier filename resolution**: Include filenames are resolved through `brand-to-server-mapping.json` → `compound-words.json` → original area name (see `Shared/ToolFileNameBuilder`).
- **Text cleanup**: Parameter names and descriptions pass through `NaturalLanguageGenerator.TextCleanup` for normalization (e.g., `--resource-group` → "Resource group"), static text replacement, and period normalization.
- **Frontmatter**: Every generated file is prepended with YAML frontmatter containing `ms.topic`, `ms.date`, and version metadata.

## Dependencies

| Package/Project | Purpose |
|---|---|
| `TemplateEngine` | Handlebars template rendering and custom helpers |
| `Shared` | File naming utilities, brand mapping loader, logging |
| `NaturalLanguageGenerator` | Text cleanup and normalization |
| `GenerativeAI` | Azure OpenAI client (referenced but only used by deprecated generators) |
| `ExamplePromptValidator` | Prompt validation (referenced but only used by deprecated code paths) |

## Models

| Model | Purpose |
|---|---|
| `CliOutput` | Root JSON deserialization: `{ results: Tool[] }` |
| `Tool` | Single MCP tool: command, description, parameters, metadata |
| `Option` | Single CLI parameter: name, type, required, description |
| `TransformedData` | Processed data container: tools list, areas map, common params |
| `ToolMetadata` | Annotation flags: destructive, idempotent, readOnly, secret, localRequired |
| `CommonParameter` | A shared parameter filtered from tool-specific tables |
| `AreaData` | Tools grouped under a service area |

## Configuration

Loaded from `docs-generation/data/config.json` at startup. Points to:

- `nl-parameters.json` — natural language parameter name mappings
- `static-text-replacement.json` — text replacement rules
- Required data files validated on startup

## Deprecated functionality

Several generators remain in the codebase but are no longer called by the pipeline. They have been superseded by dedicated packages:

| Deprecated generator | Superseded by | Details |
|---|---|---|
| `CompleteToolGenerator` | `ToolGeneration_Composed` | Single-file tool docs |
| `ExamplePromptGenerator` | `ExamplePromptGeneratorStandalone` | AI example prompts |
| `ToolFamilyPageGenerator` | `ToolFamilyCleanup` | Tool family assembly |
| `ParamAnnotationGenerator` | Separate annotation + parameter files | Combined includes |

See [docs/UNUSED-FUNCTIONALITY.md](docs/UNUSED-FUNCTIONALITY.md) for full analysis.

## Usage

```bash
# Annotations (called by Step 1)
dotnet run --project CSharpGenerator --configuration Release -- \
  generate-docs cli-output.json ../generated --annotations --version 2.0.0

# Parameters (called by Step 1)
dotnet run --project CSharpGenerator --configuration Release -- \
  generate-docs cli-output.json ../generated --parameters --version 2.0.0

# Template mode (standalone)
dotnet run --project CSharpGenerator --configuration Release -- \
  template templates/my-template.hbs data.json output.md
```
