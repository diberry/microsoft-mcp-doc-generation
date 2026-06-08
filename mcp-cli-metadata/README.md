# mcp-cli-metadata

Version snapshots for Azure MCP CLI tool metadata. The `update-azure-mcp.yml` workflow creates a subdirectory here for each `@azure/mcp` release.

## Contents

- **Per-version directories** (e.g., `3.0.0-beta.10+.../tools-list.json`) — read-only historical snapshots
- **`tracked-version.txt`** — the currently tracked `@azure/mcp` version

## CLI Metadata Extraction

CLI metadata extraction is handled by [`mcp-tools/McpCliMetadata/`](../mcp-tools/McpCliMetadata/) — a .NET console app that invokes the `azmcp` binary via `Process.Start` and produces `cli-output.json`, `cli-namespace.json`, and `cli-version.json`.

```bash
dotnet run --project mcp-tools/McpCliMetadata -- ./generated
```

See [`mcp-tools/McpCliMetadata/README.md`](../mcp-tools/McpCliMetadata/README.md) for full details.
