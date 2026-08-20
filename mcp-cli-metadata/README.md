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

## Create a version snapshot

From this directory, run:

```bash
npm run snapshot
```

The command invokes the .NET metadata extractor, reads the full CLI version from
`cli-version.json`, and creates a directory with that version as its name. The
directory contains `cli-version.json`, `cli-output.json`, `cli-namespace.json`,
and `namespace-mapping.json`. It also updates `tracked-version.txt` with the
release version without the build SHA suffix (for example,
`3.0.0-beta.35`). The command fails instead of replacing an existing version
snapshot.
