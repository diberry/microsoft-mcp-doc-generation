# Update @azure/mcp Workflow

## Overview

This workflow tracks the Azure MCP npm package version recorded in `mcp-cli-metadata/tracked-version.txt`, installs the latest matching `@azure/mcp` release globally, captures CLI snapshots into `mcp-cli-metadata/`, and commits non-breaking updates directly to `main`.

## Triggers

- **Scheduled**: nightly at 9:00 AM UTC
- **Manual**: `workflow_dispatch`

## What it does

1. Reads the current tracked version from `mcp-cli-metadata/tracked-version.txt`
2. Resolves the latest matching npm version (staying on the same prerelease channel when applicable)
3. Installs `@azure/mcp` globally with `npm install -g`
4. Verifies the CLI with `azmcp --version` and `azmcp --help`
5. Creates a new `mcp-cli-metadata/<version>/tools-list.json` snapshot when needed
6. Generates a diff against the previous snapshot and uploads artifacts
7. Runs the MCP coverage audit against the published articles repo when a new snapshot exists
8. Updates `tracked-version.txt`, commits the changes, and pushes them to `main`

## Related files

- **Workflow**: `.github/workflows/update-azure-mcp.yml`
- **Workflow smoke test**: `.github/workflows/test-azure-mcp-update.yml`
- **Tracked version file**: `mcp-cli-metadata/tracked-version.txt`
- **CLI snapshots**: `mcp-cli-metadata/<version>/tools-list.json`

## Notes

- Major version bumps are still blocked for manual review.
- The workflow no longer depends on `package.json`, `package-lock.json`, npm audit, Node-based tests, or generated CLI example scripts.
- Snapshot folders and comparison artifacts remain in `mcp-cli-metadata/` for historical analysis.
