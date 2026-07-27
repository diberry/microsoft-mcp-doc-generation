---
name: echo-content-impact
description: Validate the Azure MCP release-sync pipeline, analyze per-release content impact (including beta/pre-release versions), report deterministic work breakdown (full rewrites vs surgical fixes), and publish readiness for azure-dev-docs-pr.
domain: content-generation
status: active
triggers:
  - "Validate Azure MCP release sync"
  - "Check Echo publish readiness"
  - "Analyze Azure MCP content impact"
---

# Echo — Content Impact Skill

**Owner:** Echo (Azure MCP CLI Version Sync Specialist)  
**Step:** 3 of 3  
**Trigger:** Run after `echo-metadata-generation` resolves the version queue  
**Confidence:** High

---

## Summary

Analyze generated metadata against the published tools article set, classify namespace impact, produce a deterministic work breakdown (identifying new namespaces requiring full rewrites vs. surgical changes fixable via skills/prompts), and deliver a prioritized content handoff report.

> **Release cadence — beta is first-class.** Azure MCP Server ships pre-release/beta builds (roughly twice weekly, Tue/Thu) and the docs are maintained **on those betas** — the team does **not** wait for a GA/stable release. Every `3.0.0-beta.N` version is a documentable release: generate its metadata, diff it, and assess content impact the same as any stable version. Never gate content impact on "waiting for GA."
>
> **Parameter tables are auto-generated.** The `--auth-method`, `--tenant`, and `--retry-*` options are *common parameters* filtered out of published parameter tables, so their addition/removal is usually **zero visible doc impact**. Parameter tables live between `<!-- @mcpcli {command} -->` markers and regenerate from `tools-list.json` — most beta impact is a low-effort pipeline regen + diff review, not manual editing.
>
> **Trim release PRs to the real delta.** When reviewing a release PR, scope it to the beta.N-1 → beta.N reader-visible delta only. If a namespace's source changed but produced no reader-visible parameter, command, or description change, trim the PR to the real delta (or close it if the delta is empty) instead of advancing it. Example: beta.22 #9458 was trimmed to a single `--tags` row after removing non-customer-facing `--auth-method` noise.

---

## Input

- Version list from Step 2 JSON, or `-VersionList` provided explicitly
- Metadata repository clone at `repos/public-diberry-microsoft-mcp-doc-generation`
- Content repository clone at `repos/emu-microsoftdocs-azure-dev-docs-pr`

---

## Output

This skill emits structured output per `structured-output@1.0.0`.

- Envelope schema: `.github/skills/structured-output/schemas/spark-structured-output-envelope.schema.json`
- Domain result schema: `echo-content-impact@1.0.0`
- Producer: `echo-content-impact`
- JSON artifact: `projects/azure-ai-tools/status/echo-content-impact-{timestamp}.json`
- Result fields: `TIMESTAMP`, `FILE_TIMESTAMP`, `VERSION_LIST`, `VERSIONS_ANALYZED`, `ANALYSES`, `WORK_ITEMS_BY_VERSION`, `VALIDATION_RESULTS`, `H2_STRUCTURE_SUMMARY`, `PUBLISH_STATUS`, `NAMESPACE_COUNT`, `NEW_NAMESPACE_COUNT`, `CHANGED_NAMESPACE_COUNT`, `UNCHANGED_NAMESPACE_COUNT`, `NEW_NAMESPACES`, `CHANGED_NAMESPACES`, `UNCHANGED_NAMESPACES`, `IMPACT_MATRIX`, `REPORT_PATH`, `JSON_PATH`, `BACKFILL`, `DRY_RUN`, `ADO_ITEM_ID`, `ADO_TARGET_VERSION`, `ADO_UPDATE_STATUS`, `nextStep`
- `WORK_ITEMS_BY_VERSION[].workItems[]` fields: `namespace`, `status`, `priority`, `workType` (`full-rewrite` or `surgical-fix`), `description`, `toolsAdded`, `optionsAdded`, `descriptionChanges`, `affectedArticles`
- Correlation ID: `echo-azure-mcp-{version}` for the release chain.
- Supplements: rendered markdown report at `projects/azure-ai-tools/status/echo-content-impact-{timestamp}.md`, ADO-safe/paste-ready comment text, and work-breakdown prose are human-facing supplements only.
- Known ADO write/comment limitations are emitted as warning-severity `errors[]` entries when they affect the run.

---

## Execution

### Run the script

```powershell
pwsh .github/skills/echo-content-impact/scripts/echo-content-impact.ps1 -RunValidation
```

### Script → template flow

1. The script resolves `VERSION_LIST` from arguments or the latest Step 2 JSON artifact.
2. It loads `cli-namespace.json` and `cli-output.json` for each version.
3. It compares namespace/tool state to the previous metadata version and checks article coverage in `articles/azure-mcp-server/tools/`.
4. For each changed/new namespace, it extracts detailed changes (tools added/removed, options changed, descriptions updated) and classifies the work type:
   - **full-rewrite:** No existing content OR major tool-set changes (commands added/removed) → requires new article
   - **surgical-fix:** Option or description changes only → can be fixed with skill updates or prompt refinement
5. It emits structured JSON analysis with `WORK_ITEMS_BY_VERSION` for downstream scripting.
6. It renders `templates/report-template.md` with a "Work Breakdown" section organized by work type.
7. It writes the final report to `projects/azure-ai-tools/status/`.

### Example custom run

```powershell
pwsh .github/skills/echo-content-impact/scripts/echo-content-impact.ps1 `
  -Version 3.0.0-beta.19 `
  -ContentRepoPath repos/emu-microsoftdocs-azure-dev-docs-pr
```

### Backfill an existing ADO item for a specific version

```powershell
pwsh .github/skills/echo-content-impact/scripts/echo-content-impact.ps1 `
  -Version 3.0.0-beta.20 `
  -Backfill `
  -AdoItemId 558376 `
  -DryRun
```

---

## Report Interpretation

**Section: Work Breakdown by Version**

- **Full Rewrites (HIGH priority):** These namespaces need completely new articles in the content repo. Must generate full tool documentation from the metadata.
- **Surgical Fixes (MEDIUM/LOW priority):** These namespaces have existing articles but need targeted updates:
  - **Options Added/Removed:** Update the command reference section with new/obsolete options
  - **Parameter Table Names:** When generating or patching Azure MCP tool-article parameter tables, code-cell parameter names use the backticked display name without the leading `--` (for example, `agent`, not `--agent`); CLI examples keep the raw `--` switch. Order parameters required-first and then optional, consistently in both the CLI example (console code block) and the parameter table, with matching order between the two. Verify this on every PR; generated output should comply after doc-generation PR #741, but confirm during review.
  - **Description Changes:** Replace old descriptions with new ones (useful for skills that patch descriptions via prompt)

### Why fewer PRs than changelog namespaces

The changelog counts SOURCE changes; PRs count READER-VISIBLE doc changes, and these differ. Every changelog namespace with no PR falls into exactly one bucket:

- **covered-elsewhere:** An existing or other PR already carries the doc change.
- **no-reader-impact:** Source changed but nothing reader-visible changed, such as only common parameters (`--auth-method`, `--tenant`, `--retry-*`) that are filtered out of published parameter tables.

Report each no-PR namespace with its bucket so the gap is self-explaining. Reuse the format in `projects/azure-ai-tools/plans/plan-2026-07-16-1426-content-impact-reporting.md`.

Use the `WORK_ITEMS_BY_VERSION` JSON structure to:
1. Route full-rewrite work to content creation agents
2. Route surgical-fix work to prompt/skill update agents (skills can automate these updates)
3. Prioritize by `priority` field (HIGH → MEDIUM → LOW)

---

## Next Step

Hand off the prioritized work breakdown (full rewrites + surgical fixes) to the content team. Route surgical fixes to skill engineers for automated prompt/script-based updates. For Azure MCP tool-article fixes, preserve display parameter names without leading `--` in parameter tables while retaining `--` in CLI examples.

---

## Reviewer Metadata (ms.reviewer)

For every Azure MCP release-doc PR, set the article `ms.reviewer` metadata field before requesting reviewer pings.

- Derive owners from the namespace ownership in `repos/microsoft-mcp/.github/CODEOWNERS` (the `microsoft/mcp` changelog ownership for that namespace).
- Resolve each owner's GitHub handle to the internal Microsoft alias via the OSPO lookup (`github-ms-alias-bidirectional-lookup`). Set `ms.reviewer` to the alias or aliases, comma-delimited when more than one.
- On the Teams reminder message, use the person's name (not the alias), following Clifford's convention.
- Example mapping from beta.22: `appconfig` → `conniey,joncarde` (Connie Yau, Jonathan Cardenas); `azurebackup` → `shrja` (Shraddha Jain); `storagesync` → `ankushb,kszobi` (Ankush Bindlish, Kristian Szobi); `foundryextensions` → `zhoujay,xiangyan` (Jay Zhou, Xiang Yan); `sreagent` → `dbandaru` (Dheeraj Bandaru).

---

## ADO Integration

**Critical (standing rule — on-item verdict + trace):** Every Echo release work item MUST carry an explicit verdict and a self-contained, auditable trace **on the item body itself** — not only in a discussion comment. Step 3 writes this to **both** `System.Description` and `Microsoft.VSTS.Common.AcceptanceCriteria` for **both** content-impact and no-impact releases.

A reader must be able to verify the reasoning end-to-end from the work item body alone. The description and acceptance criteria MUST include:

- **VERDICT:** explicit `CONTENT IMPACT` or `NO CONTENT IMPACT` at the top.
- **Source + version:** `microsoft/mcp`, the Azure MCP Server version, and how the version was determined.
- **Release PRs:** every release PR as a clickable link.
- **Files:** files changed per release PR, when available from GitHub.
- **Article mapping:** how each release change maps (or does not map) to a content article: `NEW`, `CHANGED`, `UNCHANGED`/no reader-visible impact, with the target article path.
- **No-impact path:** the no-impact contract (decision, reason code(s), evidence, unblock) and the explicit list of PRs/files inspected with why none affect docs.

The script enforces this automatically:

1. Resolve the ADO item created in Step 1 or passed by `-Backfill -AdoItemId`.
2. **Attach the impact report** (`.md`) to the work item as an `AttachedFile` relation (`Add-AdoReportAttachment`).
3. Build single-line ASCII-only verdict + trace HTML (`New-AdoVerdictDescriptionHtml`, `New-AdoVerdictAcceptanceHtml`). The description references the **attached filename**, not just a local path.
4. Write the HTML to `System.Description` and `Microsoft.VSTS.Common.AcceptanceCriteria` (`Update-AdoWorkItemFields`).
5. Post the impact matrix as a supplementary discussion comment when ADO permits it. If comment posting is blocked, Echo emits paste-ready single-line ASCII HTML and reports that it was not posted.

**Delivery guarantee:** Every Echo run yields (1) an ADO story with full content impact plus reasoning on the item body, and (2) a docs PR containing the customer-facing changes. The only permitted exception to "PR contains the change" is when the generation pipeline must produce content but Azure resources are unavailable or `azd up` fails. In that exception, the ADO item MUST record the blocker, the reason no PR exists, and what is needed to unblock. A genuine `NO CONTENT IMPACT` release (for example, only common parameters such as `--auth-method`, `--tenant`, or `--retry-*` that are filtered out of published parameter tables) is a legitimate no-PR case and must carry the no-impact contract on the item body.

**Critical:** After this report is generated, update the corresponding ADO work item (created in Step 1) with:

1. **Add to work item description or comment:**
   - New namespaces identified (those requiring full-rewrite)
   - Changed tools in existing namespaces (surgical fixes)
   - Estimated effort breakdown (full rewrites vs. surgical fixes)

2. **Link the reports:**
   - Step 2 metadata-generation report (list of tools/namespaces)
   - Step 3 content-impact report (impact breakdown and priority)

3. **Update acceptance criteria:**
   - List specific new namespaces that need content generation
   - List tools/options in existing namespaces that need updates
   - Specify if surgical fixes can be done via skills or require full documentation

This ensures the content team has all context needed to pick up work and accurately estimate effort.

**ADO write-auth reality:** Agents cannot write ADO work-item comments via REST; it returns 403 errors such as "operation not allowed" or "identity not materialized." Only `az boards work-item update --fields` works, and it strips `&` entities, non-ASCII characters, stray `<`/`>`, and truncates on newlines, so field and tag writes must be single-line ASCII-only.

Comment add/delete is blocked for agents. When comment changes are needed, Echo produces (a) the paste-ready single-line ASCII-safe HTML comment body and (b) the list of comment IDs to delete, and Dina applies them manually in the ADO UI. Do not claim a comment was posted when it was only drafted.

### Required ADO field conventions

Every Echo-created Azure MCP release User Story must carry the current Content iteration, standard tags, and story points:

- **Iteration Path:** derive at run time. Prefer the deepest active Content project iteration whose dates contain the current date and whose path matches `Content\FY*\Q*\NN Mon`. If the iteration API is unavailable or stale, query recent comparable Dina-owned Azure MCP User Stories and use the modal iteration. Recent evidence on 2026-07-16: #597300, #596477, #595467, #593536, #596972, #593979, and #597301 all used `Content\FY27\Q1\07 Jul`.
- **Tags:** `azure-mcp-server; mcp-server`.
- **Story Points:** default `3` for release-sync work. Use `2` for confirmed metadata-only/simple maintenance work, and `5` for larger cross-repo or multi-article work.

If Step 3 determines that an Echo-created User Story has **zero content work** (`NEW=0`, `CHANGED=0`, total actions `0`), append the **`noop`** tag. Do not add `noop` when content work exists, even if some changes are pre-existing coverage gaps.

Use a single-line ASCII tag update and preserve existing tags:

```powershell
$workItem = az boards work-item show --id $WorkItemId --org 'https://dev.azure.com/msft-skilling' -o json | ConvertFrom-Json
$tags = [string]$workItem.fields.'System.Tags'
if (($tags -split ';\s*') -notcontains 'noop') {
  $newTags = if ([string]::IsNullOrWhiteSpace($tags)) { 'noop' } else { "$tags; noop" }
  az boards work-item update --id $WorkItemId --org 'https://dev.azure.com/msft-skilling' --fields "System.Tags=$newTags"
}
```

### ADO comment format (verdict-first)

Each ADO work-item comment Echo posts opens with a single-line, ASCII-only **verdict banner** so the final answer is unambiguous even when multiple runs stack as separate comments on the same item:

- `[FINAL: NO DOC IMPACT]` — zero NEW/CHANGED namespaces and the no-impact contract resolved to `NO_CONTENT_CHANGE` (not `_PENDING`).
- `[ACTION NEEDED: {n} new / {n} changed]` — at least one namespace needs an article or update.
- `[NO DELTA DETECTED - needs review]` — zero counts but no resolved contract (baseline/pipeline needs a look).

The banner is followed by a **Scope** line naming the comparison that produced the counts — `release delta (beta.N-1 -> beta.N)` versus `single-version snapshot` — so a release-delta "no impact" is never confused with a standing "tools exist but undocumented" coverage gap. A **Note** line flags that the comment is the latest Echo run and that earlier comments on the item are prior runs (comments are appended, not edited in place).
