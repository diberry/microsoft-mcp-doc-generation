# Tool Metadata Enricher

A .NET 9.0 console application that enriches Azure MCP CLI output with metadata extracted from azmcp-commands.json, augmenting tool parameters with defaults, allowed values, placeholders, and conditional parameter groups.

## Purpose

The Tool Metadata Enricher bridges the gap between two data sources:
- **CLI Output** (`cli-output.json`) — Raw command metadata from the Azure MCP CLI
- **Azmcp Commands** (`azmcp-commands.json`) — Detailed parameter metadata from the npm azmcp package

It produces an enriched output file (`cli-output-enriched.json`) that contains the original tool data plus a new `enrichment` object for each tool, which includes:

1. **Tool Matching** — Whether the CLI tool was matched to an azmcp command
2. **Conditional Parameter Groups** — Parameters that appear in tool descriptions as conditional sets
3. **Parameter Enhancements** — Defaults, allowed values, placeholders for each parameter
4. **Examples** — Raw code examples extracted from azmcp command blocks

## Input/Output Files

### Input Files

- **`cli-output.json`** — Raw CLI output from Azure MCP server containing all tools with their descriptions and parameters
- **`azmcp-commands.json`** — Structured parameter metadata from the npm azmcp package, including global options and command-specific enhancements

### Output Files

- **`cli-output-enriched.json`** — Enriched CLI output with added `enrichment` object on each tool plus `enrichmentMetadata` at document root

## CLI Arguments

```bash
dotnet run -- --cli-output <path> --azmcp-commands <path> --output <path>
```

| Argument | Required | Description |
|----------|----------|-------------|
| `--cli-output` | Yes | Path to cli-output.json |
| `--azmcp-commands` | Yes | Path to azmcp-commands.json |
| `--output` | Yes | Path where cli-output-enriched.json will be written |

### Example

```bash
dotnet run -- \
  --cli-output generated/cli/cli-output.json \
  --azmcp-commands azmcp-commands.json \
  --output generated/cli/cli-output-enriched.json
```

## Enrichment Categories

### 1. Tool Matching

Each tool in the enriched output includes a `matched` boolean indicating whether it was successfully matched to an azmcp command entry. Unmatched tools receive no parameter enhancements.

### 2. Conditional Parameter Groups

Parameters that are logically grouped or conditionally used together are extracted from tool descriptions. Each conditional group captures:
- **Type** — The grouping category (e.g., "required if", "mutually exclusive")
- **Parameters** — List of parameter names in the group
- **Source** — Where the group was identified (e.g., tool description)
- **Description** — Optional explanation of the condition

### 3. Parameter Defaults

Default values for parameters, extracted from azmcp command definitions and global options. Normalizes special cases like:
- Environment variable references (`Environment variable X` → `X environment variable`)
- Quoted values (strips surrounding quotes)
- Backticks (removed)
- Dash literals (`-` treated as no default)

### 4. Allowed Values

Discrete set of valid values for a parameter, de-duplicated and extracted from azmcp parameter definitions. Example:
```json
"status": {
  "allowedValues": ["queued", "in_progress", "completed"]
}
```

### 5. Value Placeholders

Human-readable hints for parameter values (e.g., `<resource-group>`, `<subscription-id>`), used in documentation and examples.

## Pipeline Position

The Tool Metadata Enricher runs as **Step 2** of the BootstrapStep, after the CommandParser:

1. ✅ **CommandParser** — Extracts azmcp-commands.json structure
2. ⏳ **Tool Metadata Enricher** ← You are here
3. → Namespace-specific steps use enriched output for parameter descriptions

The enricher is **non-blocking** — if enrichment fails, the pipeline continues with unenriched data (graceful degradation).

## Design

### Additive-Only Sidecar Enrichment

The enricher uses a **sidecar enrichment object** pattern:
- Original CLI tool data remains unchanged
- All enrichments are added under a new `enrichment` property
- Tools can be used with or without enrichment

### Non-Blocking Failure Mode

- Missing azmcp-commands.json file → logs error but returns exit code 1
- Unmatched tools → marked as `matched: false`, processed without enhancements
- Partial enrichments → applied incrementally, null/empty enrichments omitted from output

### Service Architecture

**Services** (`Services/`):
- **ToolMatcher** — Cross-references CLI tools with azmcp commands
- **ConditionalParamExtractor** — Parses tool descriptions for conditional parameter groups
- **ParameterEnricher** — Maps azmcp parameter metadata to CLI parameters
- **EnrichmentOrchestrator** — Coordinates all enrichers, produces final enriched document

**Models** (`Models/`):
- **CliOutputModel** — Deserialization of cli-output.json
- **AzmcpCommandsModel** — Deserialization of azmcp-commands.json
- **EnrichedOutput** — Output data structures including `ToolEnrichment` and `EnrichmentMetadata`

## Prerequisites

- .NET 9.0 SDK or later
- CLI output JSON from Azure MCP server
- Azmcp commands JSON from npm azmcp package

## Installation & Usage

### Build

```bash
cd docs-generation/DocGeneration.Steps.Bootstrap.ToolMetadataEnricher
dotnet build
```

### Run

```bash
dotnet run -- \
  --cli-output ../generated/cli/cli-output.json \
  --azmcp-commands ../azmcp-commands.json \
  --output ../generated/cli/cli-output-enriched.json
```

### Output

The enricher outputs enrichment statistics to console:

```
Total tools: 208
Matched tools: 185
Unmatched tools: 23
Conditional groups found: 156
Output: /path/to/cli-output-enriched.json
```

## Error Handling

| Condition | Behavior | Exit Code |
|-----------|----------|-----------|
| Missing cli-output.json | Error logged, process exits | 1 |
| Missing azmcp-commands.json | Error logged, process exits | 1 |
| Invalid JSON | Parse error logged, process exits | 1 |
| General exception | Exception message logged, process exits | 1 |
| Success | Statistics printed, enriched JSON written | 0 |

## License

Part of the Azure MCP Documentation Generator project.
