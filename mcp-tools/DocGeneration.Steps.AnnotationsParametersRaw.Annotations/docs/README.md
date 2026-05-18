# DocGeneration.Steps.AnnotationsParametersRaw.Annotations - Detailed Component Reference

Companion to the [root README](../README.md). This file documents the internal structure in detail.

## Generators (`Generators/`)

| File | Purpose |
|---|---|
| `AnnotationGenerator.cs` | Generates per-tool annotation include files (destructive, idempotent, read-only, secret, local-consent flags) |
| `ParameterGenerator.cs` | Generates per-tool parameter table include files with common-parameter filtering |
| `PageGenerator.cs` | Generates area index pages, commands page, and common tools page |
| `FrontmatterUtility.cs` | Produces YAML frontmatter (`ms.topic`, `ms.date`, version) prepended to every generated file |
| `ParameterSorting.cs` | Sorts parameters: required first, then alphabetical; used by ParameterGenerator |

## Models (`Models/`)

| Model | Purpose |
|---|---|
| `CliOutput.cs` | Root JSON deserialization: `{ results: Tool[] }` |
| `Tool.cs` | Single MCP tool: command, description, parameters, metadata |
| `Option.cs` | Single CLI parameter: name, type, required, description |
| `TransformedData.cs` | Processed data: tools list, areas map, common params |
| `ToolMetadata.cs` | Annotation flags: destructive, idempotent, readOnly, secret, localRequired |
| `CommonParameter.cs` | A shared parameter filtered from tool-specific tables |
| `AreaData.cs` | Tools grouped under a service area |
| `MetadataValue.cs` | Key-value metadata attached to tools |

## Core Files

| File | Purpose |
|---|---|
| `Program.cs` | CLI entry point, argument parsing, two modes: `generate-docs` and `template` |
| `DocumentationGenerator.cs` | Orchestrates generation: parses CLI JSON, groups tools by area, invokes generators |
| `Config.cs` | Loads `config.json`, initializes TextCleanup with NL-parameter and static-text-replacement data |
| `OptionsDiscovery.cs` | Discovers common parameters from MCP source files |

## Common Parameter Filtering

Parameters in `data/common-parameters.json` (`--tenant`, `--subscription`, `--auth-method`, `--resource-group`, retry params) are excluded from parameter tables **unless** they are `Required` for a specific tool.

> **⚠️ IMPORTANT — Resource-group convention:** This filtering rule is frequently misunderstood during manual reviews. Reviewers and AI agents sometimes incorrectly add `resource-group` rows to parameter tables when the source marks them optional. The rule is: if `required` is empty/blank in `tools-list.json`, the parameter MUST NOT appear in the published table. This applies to ALL common parameters, but `resource-group` is the one most commonly added incorrectly because it "feels" required.

Filtering is implemented in `ParameterFilterHelper.cs`:

```csharp
// ParameterFilterHelper.cs — the authoritative inclusion logic
public static bool ShouldInclude(Option opt, HashSet<string> commonParameterNames)
{
    return !string.IsNullOrEmpty(opt.Name)
        && (!commonParameterNames.Contains(opt.Name) || opt.Required);
}
```

### Source JSON Structure (tools-list.json)

The `tools-list.json` file is the source of truth for parameter verification:

```json
{
  "status": "success",
  "message": "...",
  "results": [
    {
      "command": "compute vm get",       // namespace = first word ("compute")
      "option": [
        {
          "name": "--resource-group",
          "required": "True",            // "True" = Required; "" (blank) = Optional
          "description": "..."
        }
      ]
    }
  ]
}
```

**Key fields for verification:**
- `results[].command` — Full tool command. Namespace = first word.
- `results[].option[].name` — CLI parameter name (e.g., `--resource-group`)
- `results[].option[].required` — String `"True"` if required, empty string `""` if optional

## Configuration Files

All in `mcp-tools/data/`:

| File | Purpose |
|---|---|
| `config.json` | Main config — points to NL-parameter and static-text-replacement files |
| `common-parameters.json` | Parameters filtered from tool-specific tables |
| `brand-to-server-mapping.json` | Brand name → server name → filename (highest priority) |
| `compound-words.json` | Word transformations for filename generation (medium priority) |
| `stop-words.json` | Words removed from include filenames |
| `nl-parameters.json` | Natural language parameter name mappings |
| `static-text-replacement.json` | Text replacements for descriptions |

## Output Structure

```
generated/
├── annotations/        # Per-tool annotation include files
├── parameters/         # Per-tool parameter table include files
├── common-general/     # Index, commands, and common tools pages
├── reports/            # Security and metadata summary reports
└── logs/               # Generation debug logs
```

## Related Documentation

- [Root README](../README.md) — architecture, data flow, CLI usage, dependencies
- [System Overview](../../docs/SYSTEM-OVERVIEW.md) — full pipeline architecture
- [Copilot Instructions](../../.github/copilot-instructions.md) — development guidelines
