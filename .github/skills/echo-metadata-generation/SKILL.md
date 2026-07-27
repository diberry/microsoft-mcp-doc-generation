---
name: echo-metadata-generation
description: Generate the Azure MCP release-context handoff by verifying metadata snapshots, refreshing the skill catalog, and inventorying markdown plus script assets.
domain: content-generation
status: active
triggers:
  - "Generate Azure MCP metadata handoff"
  - "Refresh Echo skill catalog"
  - "Inventory Echo release sync assets"
---

# Echo — Metadata Generation Skill

**Owner:** Echo (Azure MCP CLI Version Sync Specialist)  
**Step:** 2 of 3  
**Trigger:** Run after `echo-release-detection` produces a version queue  
**Confidence:** High

---

## Summary

Resolve the version queue from Step 1, verify or generate metadata coverage, and emit structured PR state plus a rendered handoff report.

---

## Destination Contract & Guardrails

- CLI metadata's **ONLY** destination is `repos/public-diberry-microsoft-mcp-doc-generation/mcp-cli-metadata/{version}+{sha}/`.
- Each snapshot directory contains exactly four files: `cli-namespace.json`, `cli-output.json`, `cli-version.json`, and `namespace-mapping.json`.
- **NEVER** write release artifacts, CLI metadata, downloaded packages, extracted binaries, or probe/scratch output anywhere in the hub repo, including `projects/**/plans/**`.
- **NEVER** create `plans/echo-release-probe*` or any other probe/scratch folder for release artifacts.
- **NEVER** commit `.nupkg`, `.zip`, or unzipped CLI binaries.
- If the capture path is unclear or `azmcp --version` does not resolve the SHA, **STOP and report** instead of improvising.

> **Release cadence — beta is first-class.** Azure MCP Server ships pre-release/beta builds (roughly twice weekly, Tue/Thu) and the docs are maintained **on those betas** — the team does **not** wait for GA. Generate metadata for every `3.0.0-beta.N` version; never mark a beta as "waiting-for-ga" or skip it because it is pre-release. To install a specific pre-release CLI use `dotnet tool update azure.mcp --global --version 3.0.0-beta.N` (an explicit `--version` implies pre-release; do **not** combine `--prerelease` with `--version`).

---

## Input

- Version list from Step 1 JSON, or `-VersionList` provided explicitly
- Metadata repository clone at `repos/public-diberry-microsoft-mcp-doc-generation`
- Optional plan-only execution for validation and dry runs

---

## Output

This skill emits structured output per `structured-output@1.0.0`.

- Envelope schema: `.github/skills/structured-output/schemas/spark-structured-output-envelope.schema.json`
- Domain result schema: `echo-metadata-generation@1.0.0`
- Producer: `echo-metadata-generation`
- JSON artifact: `projects/azure-ai-tools/status/echo-metadata-generation-{timestamp}.json`
- Result fields: `TIMESTAMP`, `FILE_TIMESTAMP`, `VERSION_LIST`, `PR_DETAILS`, `MERGED_COUNT`, `PENDING_COUNT`, `GENERATED_COUNT`, `ASSET_INVENTORY`, `MARKDOWN_ASSET_COUNT`, `SCRIPT_ASSET_COUNT`, `CATALOG_STATUS`, `CATALOG_SKILLS_PRESENT`, `CATALOG_SKILLS_ADDED`, `README_PATH`, `CATALOG_PATH`, `GENERATION_SCRIPT`, `REPORT_PATH`, `JSON_PATH`, `nextStep`
- Correlation ID: preserves incoming `echo-azure-mcp-{version}` from release detection, or creates it from the first processed version.
- Supplements: rendered markdown report at `projects/azure-ai-tools/status/echo-metadata-generation-{timestamp}.md` and validation notes are human-facing supplements only.

Validation note: Generated metadata may contain CLI-syntax names such as `--foo`; downstream Azure MCP tool-article parameter-table display names must strip the leading `--`.

---

## Execution

### Run the script

```powershell
pwsh .github/skills/echo-metadata-generation/scripts/echo-metadata-generation.ps1
```

```bash
bash .github/skills/echo-metadata-generation/scripts/echo-metadata-generation.sh
```

### Script → template flow

1. The script resolves `VERSION_LIST` from arguments or the latest Step 1 JSON artifact.
2. It checks `mcp-cli-metadata/` for each requested version.
3. It emits structured PR/merge state as JSON.
4. It renders `templates/report-template.md` with that state.
5. It writes the final report file to `projects/azure-ai-tools/status/`.

### Capture Procedure

Use this procedure when Step 2 finds a requested version missing from `mcp-cli-metadata/`. This is the only approved deterministic capture path.

1. Install the exact Azure MCP CLI beta globally; an explicit version implies pre-release, so do not add `--prerelease`:

   ```powershell
   dotnet tool update azure.mcp --global --version 3.0.0-beta.N
   ```

2. Run the installed release binary to resolve the source commit SHA:

   ```powershell
   azmcp --version
   ```

   Use the `microsoft/mcp` commit SHA reported by that command. Do not infer the SHA from package metadata, changelog text, tags, or PRs. If the SHA is missing or ambiguous, stop and report the blocker.

3. Generate exactly these four CLI metadata files from that installed CLI: `cli-namespace.json`, `cli-output.json`, `cli-version.json`, and `namespace-mapping.json`.

4. Write the snapshot only to the doc-generation clone:

   ```text
   repos/public-diberry-microsoft-mcp-doc-generation/mcp-cli-metadata/{version}+{sha}/
   ```

5. Confirm the snapshot directory contains exactly those four files and no packages, zips, extracted binaries, or scratch output. Keep Step 1 and Step 2 reports in `projects/azure-ai-tools/status/`; do not move reports into the clone and do not write metadata into the hub.

### Example dry run

```powershell
pwsh .squad/skills/echo-metadata-generation/scripts/echo-metadata-generation.ps1 `
  -VersionList 3.0.0-beta.18,3.0.0-beta.19 `
  -PlanOnly
```

---

## Next Step

Pass `VERSION_LIST` to `echo-content-impact`.

### New namespace reviewer metadata

For a new Azure MCP namespace article, resolve `ms.reviewer` before reviewer pings:

1. Find the namespace owner GitHub handle or handles in `microsoft/mcp` CODEOWNERS for `/tools/Azure.Mcp.Tools.<Namespace>/`, and use the source PR author as supporting evidence.
2. Resolve each GitHub handle to a Microsoft alias with the `github-ms-alias-bidirectional-lookup` skill and OSPO lookup.
3. Put the alias values in article metadata as `ms.reviewer`, comma-delimited for multiple aliases. Use aliases only, never display names.

Worked example from beta.26: Insights ownership came from CODEOWNERS `/tools/Azure.Mcp.Tools.Insights/` = `@micha31r` and `@arunrab`; source PR `microsoft/mcp#2711` was authored by `@micha31r`; resolved aliases were `mren, arunrab`.
