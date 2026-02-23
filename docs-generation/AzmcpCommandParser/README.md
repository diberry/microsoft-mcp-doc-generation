# AzmcpCommandParser

A .NET 9.0 console application that parses the `azmcp-commands.md` markdown file into structured JSON.

## Purpose

Converts the Azure MCP CLI command reference (2,800+ line markdown document) into a typed C# model and serializes it to JSON. This enables programmatic access to:

- 51 service sections (Storage, Key Vault, ACR, etc.)
- 277 commands (256 definitions + 21 examples)
- 8 global options
- Tool metadata flags (destructive, idempotent, read-only, etc.)
- Command parameters with types, defaults, and allowed values

## Usage

### Via Preflight (recommended)

The parser runs automatically as Step 7 of the preflight script. Output goes to `generated/cli/azmcp-commands.json`:

```bash
# Full pipeline (preflight runs parser automatically)
./start.sh

# Single namespace (preflight still runs parser)
./start.sh advisor
```

### Standalone

```bash
# Output JSON to file
dotnet run --project docs-generation/AzmcpCommandParser -- \
  --file docs-generation/azure-mcp/azmcp-commands.md \
  --output azmcp-commands.json

# Output JSON to stdout
dotnet run --project docs-generation/AzmcpCommandParser -- \
  --file docs-generation/azure-mcp/azmcp-commands.md
```

### CLI Options

| Option     | Required | Description                              |
|------------|----------|------------------------------------------|
| `--file`   | Yes      | Path to `azmcp-commands.md`              |
| `--output` | No       | Output JSON file path (default: stdout)  |

## Architecture

```
AzmcpCommandParser/
├── Program.cs                          # CLI entry point (System.CommandLine)
├── Models/
│   └── CommandDocument.cs              # 14 model classes
├── Parsing/
│   └── MarkdownCommandParser.cs        # Markdown → model parser
└── Serialization/
    └── CommandDocumentSerializer.cs     # Model ↔ JSON serialization
```

### Models (`Models/CommandDocument.cs`)

| Class                | Description                                      |
|----------------------|--------------------------------------------------|
| `CommandDocument`    | Root: title, intro, global options, sections      |
| `GlobalOption`       | Global CLI option (name, required, default, desc) |
| `ServerOperations`   | Server modes and start options                    |
| `ServiceSection`     | One H3 service area (e.g., "Azure Storage")       |
| `SubSection`         | H4 sub-section within a service                   |
| `Command`            | A parsed command with syntax, params, metadata    |
| `ToolMetadata`       | 6 boolean flags from code block comments          |
| `CommandParameter`   | Parameter with name, required, flag, allowed vals  |
| `ParameterTable`     | Tabular parameter listing                         |
| `ResponseFormat`     | Response structure documentation                  |

### Parser (`Parsing/MarkdownCommandParser.cs`)

- Line-by-line state machine parsing
- Compiled regex patterns via `[GeneratedRegex]`
- Handles: H1–H4 headings, code fences, metadata comments, tables, multi-line commands (`\` continuation)
- Distinguishes definition commands from example commands
- Derives area names from 37 known heading→area mappings

### Serializer (`Serialization/CommandDocumentSerializer.cs`)

- camelCase property naming
- Null values omitted
- Pretty-printed JSON output

## Tests

```bash
dotnet test docs-generation/AzmcpCommandParser.Tests
```

30 unit tests covering: title/intro extraction, global options, service sections, metadata flags, multi-line commands, parameter parsing (required/optional/flag/allowed values), sub-sections, example vs definition distinction, area name derivation, serialization roundtrip.

## Dependencies

- `System.Text.Json` — JSON serialization
- `System.CommandLine` — CLI argument parsing

Versions managed via Central Package Management (`Directory.Packages.props`).
