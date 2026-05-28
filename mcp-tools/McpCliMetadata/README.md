# DocGeneration.McpCliMetadata

Console app that extracts Azure MCP CLI tool metadata by invoking the `azmcp` binary.

## Purpose

Replaces the Node.js-based `mcp-cli-metadata/` scripts with a typed .NET console app.
Writes three JSON files to `generated/cli/`:

| File | Command |
|------|---------|
| `cli-version.json` | `azmcp --version` |
| `cli-output.json` | `azmcp tools list` |
| `cli-namespace.json` | `azmcp tools list --namespace-mode` |

## Usage

```bash
dotnet run --project mcp-tools/McpCliMetadata -- ./generated
```

The `azmcp` binary must be available on `PATH` as a prerequisite.

## Architecture

- **`AzmcpRunner`** — wraps `IProcessRunner` to invoke `azmcp` commands and return output
- **`IProcessRunner`** — abstraction over `Process.Start` (enables unit testing with fakes)
- **`Program.cs`** — entry point; accepts output directory as argument

## Testing

```bash
dotnet test mcp-doc-generation.sln --filter "McpCliMetadata"
```

Tests use `FakeProcessRunner` with fixture data — no real `azmcp` binary required.
