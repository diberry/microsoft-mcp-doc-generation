# mcp-cli-metadata

Azure MCP CLI metadata extractor — the single integration point between the `azmcp` binary and the C# documentation generation pipeline.

## Purpose

This directory contains two things:

1. **Version snapshots** — per-version directories (e.g., `3.0.0-beta.10+.../tools-list.json`) preserving tool metadata at each `@azure/mcp` release. These are read-only historical artifacts created automatically by the `update-azure-mcp.yml` workflow.
2. **Legacy reference** — historical Node.js scripts (now removed). CLI metadata extraction is handled exclusively by the .NET tool described below.

**All 52 namespace documentation generations depend on the CLI metadata produced by `mcp-tools/McpCliMetadata/`.**

## How CLI Metadata Is Extracted

CLI metadata is extracted by `mcp-tools/McpCliMetadata/`, a .NET 10 console app that invokes the `azmcp` binary directly via `Process.Start`. It replaces the former Node.js npm scripts.

### Run the extractor

```bash
dotnet run --project mcp-tools/McpCliMetadata -- ./generated
```

This produces three files in `./generated/cli/`:

| Output file | Contents |
|-------------|---------|
| `cli-output.json` | All tools as structured JSON |
| `cli-namespace.json` | Tools grouped by namespace |
| `cli-version.json` | `azmcp` version string |

### Pipeline integration

```
start.sh → PipelineRunner → BootstrapStep (Step 0)
  ├─ dotnet run mcp-tools/McpCliMetadata   → cli/cli-version.json
  │                                        → cli/cli-output.json
  │                                        → cli/cli-namespace.json
  ↓
  Steps 1–6 consume cli-output.json for all generation
```

`preflight.ps1` calls `dotnet run --project mcp-tools/McpCliMetadata` instead of any npm scripts.

### .NET tool location

| Component | Path |
|-----------|------|
| Project file | `mcp-tools/McpCliMetadata/McpCliMetadata.csproj` |
| Runner | `mcp-tools/McpCliMetadata/AzmcpRunner.cs` |
| Entry point | `mcp-tools/McpCliMetadata/Program.cs` |
| Tests | `mcp-tools/McpCliMetadata.Tests/` |

## Version Snapshots

Each `@azure/mcp` release has a snapshot directory here (e.g., `3.0.0-beta.10+7287903f.../tools-list.json`) preserving the tool metadata at that version. The GitHub workflow `update-azure-mcp.yml` creates these automatically on version updates. Do not edit snapshot directories — they are historical artifacts.
