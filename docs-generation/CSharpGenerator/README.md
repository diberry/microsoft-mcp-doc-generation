# CSharpGenerator

.NET 9.0 console application that generates annotation and parameter markdown documentation for Azure MCP (Model Context Protocol) tools. It reads structured JSON output from the Azure MCP CLI, transforms it into a documentation model, and renders markdown files using Handlebars templates.

## What it does

CSharpGenerator is called during **Step 1** of the generation pipeline (`1-Generate-AnnotationsParametersRaw-One.ps1`). Both annotation and parameter files are generated unconditionally on every invocation — the `--annotations` flag controls only the tool-annotations summary output.

The pipeline calls it with:

1. **`generate-docs <cli-json> <output-dir> --annotations --version <v>`** — generates annotations, parameters, and the annotations summary
2. **`generate-docs <cli-json> <output-dir> --version <v>`** — generates annotations and parameters (summary skipped)

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
│   ├── ParameterSorting            Sorts params: required first, then alphabetical
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
| `MetadataValue` | Key-value metadata attached to tools |

## Configuration

Loaded from `docs-generation/data/config.json` at startup. Points to:

- `nl-parameters.json` — natural language parameter name mappings
- `static-text-replacement.json` — text replacement rules
- Required data files validated on startup

## Removed deprecated functionality

Several generators were previously in this codebase but have been deleted. They were superseded by dedicated packages:

| Removed generator | Superseded by | Details |
|---|---|---|
| `CompleteToolGenerator` | `ToolGeneration_Composed` | Single-file tool docs |
| `ExamplePromptGenerator` | `ExamplePromptGeneratorStandalone` | AI example prompts |
| `ToolFamilyPageGenerator` | `ToolFamilyCleanup` | Tool family assembly |
| `ParamAnnotationGenerator` | Separate annotation + parameter files | Combined includes |
| `ReportGenerator` | N/A (informational only) | Security reports |
| `ServiceOptionsDiscovery` | N/A (unused by pipeline) | Service options page |
| `ExamplePromptsResponse` | `ExamplePromptGeneratorStandalone` | AI response model |

See [docs/README.md](docs/README.md) for detailed component documentation.

## Usage

```bash
# Annotations + parameters (called by Step 1)
dotnet run --project CSharpGenerator --configuration Release -- \
  generate-docs cli-output.json ../generated --annotations --version 2.0.0

# Template mode (standalone)
dotnet run --project CSharpGenerator --configuration Release -- \
  template templates/my-template.hbs data.json output.md

# Run all tests
dotnet test docs-generation.sln
```
