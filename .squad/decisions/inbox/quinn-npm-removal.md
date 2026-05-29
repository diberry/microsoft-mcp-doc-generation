# Decision: Remove npm artifacts from mcp-cli-metadata (PRD #627 Phases 4-6)

**Date**: 2026-05-29  
**Author**: Quinn (DevOps / Scripts Engineer)  
**Branch**: squad/627-finish-npm-to-dotnet-migration  
**Related PRs**: #628, #631

---

## What Was Done

### Phase 4 — Removed npm artifacts from `mcp-cli-metadata/`

**Deleted files:**
- `package.json` — npm package manifest (no longer needed; .NET tool is the extractor)
- `package-lock.json` — npm lockfile
- `check-npm-versions.js` — npm version utility
- `generate-report.js` — Node.js report generator
- `generate-cli-examples.js` — Node.js CLI example generator
- `validate-cli-output.js` — Node.js JSON validator
- `chat-completion.sh` — npm-era shell script
- `samples.env` — npm environment config
- `compare-2x-3x.js`, `detailed-compare.js`, `detailed-diff.js`, `diff-versions.js`, `final-comparison.js` — investigation/comparison utilities from the npm migration era
- `test/` directory — Node.js test files and fixtures (generate-cli-examples.test.js, generate-report.test.js, fixtures/)
- Old investigation artifacts: `2x-beta40-vs-3x-beta3.json`, `2x-vs-3x-*.json`, `31-namespace.json`, `39.json`, `40.json`, `cli-beta*.json`, `cli-output-2x-beta38.json`, `cli-examples.md`, `detailed-comparison-output.txt`, `diff-versions.json`, `detailed-diff.json`, `generation-report*.md`

**Kept:**
- All versioned snapshot directories (`2.0.0-beta.*/`, `3.0.0-beta.*/`) — historical artifacts, read-only
- `tracked-version.txt` — used by both CI workflows
- `README.md` — already updated in PR #631 to document the .NET tool

### Phase 5 — preflight.ps1

**No changes required.** `preflight.ps1` was already updated in PR #631 to call `dotnet run --project mcp-tools/McpCliMetadata/McpCliMetadata.csproj` (Step 4 in that script). The npm `Push-Location` / `npm install` / `npm run` pattern was already replaced.

### Phase 6 — CI workflow updates

**`update-azure-mcp.yml`:**
1. Replaced `node -e "JSON.parse(...)"` JSON validation with `python3 -c "import json,sys; json.load(...)"` — Python 3 is always available on ubuntu-latest runners, no Node.js dependency
2. Replaced `node -p "JSON.parse(...).results?.length ?? 0"` tool count with `python3 -c "import json; d=json.load(open(...))"` equivalent
3. **Removed** "Generate CLI examples" step — `generate-cli-examples.js` deleted; this step called the npm script
4. **Removed** "Run npm audit" step — there is no longer an npm package to audit in this repository

**`npm install -g @azure/mcp@$VERSION` was kept** in both `update-azure-mcp.yml` and `test-azure-mcp-update.yml`. The `.NET` tool (`AzmcpRunner.cs`) locates the `azmcp` binary by scanning `$PATH` for `azmcp.cmd` (Windows) or `azmcp` (Linux/macOS). The npm global install is the mechanism that puts the binary on PATH in CI — so this install step is still required.

**`build-and-test.yml`:** No changes needed. Path trigger already uses `mcp-cli-metadata/**` (the renamed folder).

**`test-azure-mcp-update.yml`:** No changes needed. The workflow installs the tracked `@azure/mcp` version via npm to get the binary — still correct after the migration.

---

## Why

The `.NET` `McpCliMetadata` project (PRs #628, #631) is a full replacement for the npm-based workflow. It calls `azmcp` directly via `Process.Start`, produces the same three output files (`cli-output.json`, `cli-namespace.json`, `cli-version.json`), and is already integrated into `preflight.ps1` and the pipeline. Keeping the npm scripts alongside the .NET tool created ambiguity, dead code, and a false security surface (npm audit failures on a package we no longer execute).

The `npm install -g @azure/mcp` step in CI is infrastructure — it installs the `azmcp` binary onto PATH so the .NET tool can invoke it. This is distinct from the Node.js application code that was removed.
