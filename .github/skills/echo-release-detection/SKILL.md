---
name: echo-release-detection
description: Detect upstream Azure MCP releases, compare them with local metadata coverage, and emit release-context reports with version, date, and commit SHA.
domain: content-generation
status: active
triggers:
  - "Echo, sync Azure MCP releases"
  - "Detect Azure MCP releases"
  - "Check Azure MCP release context"
---

# Echo — Release Detection Skill

**Owner:** Echo (Azure MCP CLI Version Sync Specialist)  
**Step:** 1 of 3  
**Trigger:** `Echo, sync Azure MCP releases`  
**Confidence:** High

---

## Summary

Detect upstream Azure MCP Server 3.x releases, compare them to local metadata coverage, and emit structured output plus a rendered report for downstream steps. Supports explicit version specification for handling pre-release or missing changelog versions.

---

## Destination Contract & Guardrails

**Hard NEVER rules for Azure MCP release sync:**

- **NEVER** write release artifacts, CLI metadata snapshots, downloaded packages, or extracted binaries anywhere in this hub repo, including `projects/**/plans/**`.
- **NEVER** create a `plans/echo-release-probe*` folder or any probe/scratch folder for release artifacts.
- **NEVER** commit `.nupkg`, `.zip`, or unzipped CLI binaries anywhere in this hub repo.
- CLI metadata's **ONLY** destination is the doc-generation clone: `repos/public-diberry-microsoft-mcp-doc-generation/mcp-cli-metadata/{version}+{sha}/`, with exactly these four files: `cli-namespace.json`, `cli-output.json`, `cli-version.json`, and `namespace-mapping.json`.
- `{sha}` is resolved deterministically from the extracted release binary by running `azmcp --version`; do not guess it from changelog text, package names, tags, or PRs.
- If the required capture path is unclear or the SHA cannot be resolved, **STOP and report**. Do not improvise a download, extraction, scratch, or alternate-output flow.

Run the shared Echo+Finn release-artifact guard before committing:

```powershell
pwsh .github/skills/echo-release-detection/scripts/validate-release-artifacts.ps1
```

```bash
bash .github/skills/echo-release-detection/scripts/validate-release-artifacts.sh
```

Optional local pre-commit hook install:

```powershell
Set-Content -Path .git/hooks/pre-commit -Value 'pwsh .github/skills/echo-release-detection/scripts/validate-release-artifacts.ps1' -Encoding ascii
```

```bash
printf '%s\n' 'bash .github/skills/echo-release-detection/scripts/validate-release-artifacts.sh' > .git/hooks/pre-commit && chmod +x .git/hooks/pre-commit
```

> **Release cadence — beta is first-class.** Azure MCP Server ships pre-release/beta builds (roughly twice weekly, Tue/Thu) and the docs are maintained **on those betas** — the team does **not** wait for GA. A newly detected `3.0.0-beta.N` is a real release to document, not something to defer. Downstream steps (2 & 3) treat betas exactly like stable versions.

---

## Input

- Upstream Azure MCP changelog (`CHANGELOG.md` raw URL by default)
- Metadata repository clone at `repos/public-diberry-microsoft-mcp-doc-generation`
- Optional `-Version` parameter to explicitly specify a version (e.g., `-Version 3.0.0-beta.99`)
- Optional overrides for changelog path, output directory, and ADO creation

---

## Output

This skill emits structured output per `structured-output@1.0.0`.

- Envelope schema: `.github/skills/structured-output/schemas/spark-structured-output-envelope.schema.json`
- Domain result schema: `echo-release-detection@1.0.0`
- Producer: `echo-release-detection`
- JSON artifact: `projects/azure-ai-tools/status/echo-release-detection-{timestamp}.json`
- Result fields: `TIMESTAMP`, `FILE_TIMESTAMP`, `NEW_VERSIONS`, `NEW_VERSIONS_COUNT`, `ADO_WORK_ITEM_ID`, `VERSION_DETAILS`, `RELEASE_CONTEXT`, `CONTEXT_VERSIONS`, `LATEST_UPSTREAM_VERSION`, `VERSION_STATUS`, `LATEST_METADATA_VERSION`, `DETECTED_VERSION_COUNT`, `METADATA_VERSIONS`, `REPORT_PATH`, `JSON_PATH`, `nextStep`
- Correlation ID: `echo-azure-mcp-{version}` across the release-detection → metadata-generation → content-impact chain.
- Supplements: rendered markdown report at `projects/azure-ai-tools/status/echo-release-detection-{timestamp}.md` and the report's **Version Status** section are human-facing supplements only.

---

## Execution

### Run the script

```powershell
pwsh .github/skills/echo-release-detection/scripts/echo-release-detection.ps1
```

```bash
bash .github/skills/echo-release-detection/scripts/echo-release-detection.sh
```

### Script → template flow

1. The script fetches or loads the changelog.
2. It computes new versions by comparing changelog headers to `mcp-cli-metadata/` folders.
3. If `-Version` is specified, it looks for that version instead (or creates missing entry if not found).
4. It writes structured JSON output.
5. It renders `templates/report-template.md` by replacing `{{VAR_NAME}}` placeholders.
6. It writes the final report file to `projects/azure-ai-tools/status/`.

### Example runs

**Detect latest version (default):**
```powershell
pwsh .github/skills/echo-release-detection/scripts/echo-release-detection.ps1
```

**Process specific version (including missing ones):**
```powershell
pwsh .github/skills/echo-release-detection/scripts/echo-release-detection.ps1 -Version 3.0.0-beta.99
```

**Custom changelog path:**
```powershell
pwsh .github/skills/echo-release-detection/scripts/echo-release-detection.ps1 `
  -ChangelogPath tests/fixtures/echo-release-sync/sample-changelog.md `
  -SkipAdo
```

### Handling missing versions

When a version is not found in the changelog:
- The script creates an ADO item (just like normal)
- The report includes a **⚠️ Version Status** section
- Status shows "Missing from changelog" with link to upstream changelog
- Downstream steps (Steps 2 & 3) can proceed with content generation as needed
- Once the version appears in the changelog, re-running without `-Version` will detect it normally

---

## Next Step

### Structural conformance check

Before finalizing Azure MCP tool-doc changes:
- Validate the entire modified tool section against the canonical template, not only the requested edit lines.
- Treat any content between the tool annotation-hints table and the next `##` heading as a non-template artifact to flag/remove, never edit.
- Cross-check sibling sections in the same file; elements that appear in only some sections, such as trailing bare `Examples` blocks, are suspect artifacts.
- Diff the file against `origin/main` and adopt the current main format; flag stale branches for rebase when formats have moved on.

Pass `NEW_VERSIONS` to `echo-metadata-generation`, then follow the **Capture Procedure** in `.github/skills/echo-metadata-generation/SKILL.md#capture-procedure`. That procedure is the only approved path for producing CLI metadata snapshots.

---

## ADO Integration

**Critical:** Each new version MUST get its own ADO User Story. Never combine multiple versions into a single work item.

**Template Location:** `.github/skills/echo-release-detection/templates/ado-user-story-template.md`

**Work Item Fields (per version):**
- **Work Item Type:** User Story
- **Project:** msft-skilling / Content
- **State:** New
- **Assigned To:** Dina Berry (diberry@microsoft.com)
- **Iteration Path:** derive the current active fiscal iteration at run time; do not leave it at `Content`.
  1. Query Content project iterations and choose the deepest leaf whose `startDate <= now < finishDate` and whose path follows the fiscal pattern `Content\FY*\Q*\NN Mon`.
  2. If the project iteration API is unavailable or stale, query recent comparable Dina-owned Azure MCP User Stories and use the modal iteration path.
  3. As of 2026-07-16, recent comparable stories use `Content\FY27\Q1\07 Jul`.
- **Tags:** `azure-mcp-server; mcp-server`
- **Story Points:** default `3` for Azure MCP release-sync stories because content impact is not known at creation time. Use `2` only for confirmed metadata-only/simple maintenance work, and `5` for larger cross-repo or multi-article work.
- **Parent:** 576070
- **Area Path:** Content\Production\Core AI\Azure Dev Experiences\AI apps and tools\Azure MCP Server

**ADO create/update command rules:**
- Use single-line ASCII field values. `az boards work-item update --fields` strips `&`, non-ASCII characters, and stray `<`/`>` and truncates on newlines.
- Include iteration, tags, and story points at creation time:

```powershell
az boards work-item create `
  --org 'https://dev.azure.com/msft-skilling' `
  --project 'Content' `
  --type 'User Story' `
  --title "Azure MCP Server {VERSION} - CLI Metadata & Content" `
  --description $singleLineDescription `
  --parent 576070 `
  --area-path 'Content\Production\Core AI\Azure Dev Experiences\AI apps and tools\Azure MCP Server' `
  --iteration $iterationPath `
  --assigned-to 'Dina Berry <diberry@microsoft.com>' `
  --fields 'System.Tags=azure-mcp-server; mcp-server' 'Microsoft.VSTS.Scheduling.StoryPoints=3'
```

**Title Format:** `Azure MCP Server {VERSION} — CLI Metadata & Content` in reports/templates; use ASCII hyphen (`-`) in `az boards` commands.
- Example: "Azure MCP Server 3.0.0-beta.25 — CLI Metadata & Content"
- **NEVER** use version ranges like "3.0.0-beta.24..25"

**Description Requirements (per version):**
- Version-specific CHANGELOG anchor (e.g., `#300-beta25` for `3.0.0-beta.25`)
- ALL PRs from CHANGELOG for THIS version only, grouped by category:
  - Breaking Changes
  - Features Added
  - Bugs Fixed
  - Other Changes
- Content Impact section (populated by Step 3)
- Action Items checklist
- Acceptance Criteria

**Workflow:**
1. Step 1 (Release Detection) creates ONE User Story per detected version using the template
2. Step 2 (Metadata Generation) generates the new namespaces/tools for each version
3. Step 3 (Content Impact) analyzes impact and MUST update EACH User Story with:
   - Summary of new tools/namespaces for that version
   - Affected existing tools for that version
   - Estimated effort for content generation
   - Links to the Step 2 and Step 3 reports
