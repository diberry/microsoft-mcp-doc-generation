# ADO User Story Template — Azure MCP Server Release

## How to Use This Template

This template is instantiated **once per release version**. Each Azure MCP Server release gets its own ADO User Story, ensuring complete traceability and self-contained work item context. Never combine multiple versions into a single User Story.

**Version-specific anchor generation:**
GitHub anchors are derived from the CHANGELOG header by lowercasing and removing dots:
- `## 3.0.0-beta.25` → `#300-beta25`
- `## 3.0.0-beta.24` → `#300-beta24`

---

## ADO Work Item Fields

| Field | Value |
|-------|-------|
| **Work Item Type** | User Story |
| **Project** | msft-skilling / Content |
| **State** | New |
| **Assigned To** | Dina Berry (diberry@microsoft.com) |
| **Tags** | azure, mcp-server, cli-metadata, content-generation |
| **Parent** | 576070 |
| **Area Path** | Content\Production\Core AI\Azure Dev Experiences\Tools\GitHub Copilot for Azure |

---

## Title

```
Azure MCP Server {VERSION} — CLI Metadata & Content
```

**Example:** `Azure MCP Server 3.0.0-beta.25 — CLI Metadata & Content`

---

## Description

```markdown
# Azure MCP Server {VERSION} Release

**Release Date:** {RELEASE_DATE}  
**Status:** Metadata & Content Generation  
**Upstream Repo:** https://github.com/microsoft/mcp

---

## Release Metadata

- **Version:** {VERSION}
- **Release Date:** {RELEASE_DATE}
- **CHANGELOG:** [Full CHANGELOG](https://github.com/microsoft/mcp/blob/main/servers/Azure.Mcp.Server/CHANGELOG.md)
- **Version-Specific Section:** [CHANGELOG #{VERSION_ANCHOR}](https://github.com/microsoft/mcp/blob/main/servers/Azure.Mcp.Server/CHANGELOG.md#{VERSION_ANCHOR})

---

## Links

- **Metadata Repository:** https://github.com/diberry/microsoft-mcp-doc-generation/tree/main/mcp-cli-metadata
- **Metadata Folder (This Version):** https://github.com/diberry/microsoft-mcp-doc-generation/tree/main/mcp-cli-metadata/{VERSION}
- **Content Repository:** https://github.com/MicrosoftDocs/azure-dev-docs-pr
- **Tools Directory:** https://github.com/MicrosoftDocs/azure-dev-docs-pr/tree/main/articles/azure-mcp-server/tools

---

## Underlying PRs from CHANGELOG

### Breaking Changes
{LIST_BREAKING_CHANGES_PRS}

### Features Added
{LIST_FEATURES_ADDED_PRS}

### Bugs Fixed
{LIST_BUGS_FIXED_PRS}

### Other Changes
{LIST_OTHER_CHANGES_PRS}

---

## Content Impact (Completed by Step 3: echo-content-impact)

**Note:** This section is populated automatically after Step 3 (Content Impact Analysis) completes.

### Namespaces Summary

- **NEW Namespaces:** {COUNT} — {LIST_NEW_NAMESPACES}
- **CHANGED Namespaces:** {COUNT} — {LIST_CHANGED_NAMESPACES}
- **UNCHANGED Namespaces:** {COUNT}

### Priority Breakdown

- **HIGH Priority:** {COUNT} namespaces — Breaking changes or new customer-facing features
- **MEDIUM Priority:** {COUNT} namespaces — Bug fixes or parameter changes
- **LOW Priority:** {COUNT} namespaces — Documentation or non-functional changes

### Detailed Impact Report

[Link to Step 3 Content Impact Report](file:///C:/project-dina-ai-dev-tools/projects/azure-ai-tools/status/echo-content-impact-{VERSION}-{TIMESTAMP}.md)

---

## Action Items

- [ ] **Step 1 Complete:** Release detected, ADO work item created
- [ ] **Step 2 Complete:** Metadata folder generated at `mcp-cli-metadata/{VERSION}/`
- [ ] **Step 2 Complete:** Metadata PR created and merged to main
- [ ] **Step 3 Complete:** Content impact analysis completed
- [ ] **Content Team:** New articles created for NEW namespaces
- [ ] **Content Team:** Existing articles updated for CHANGED namespaces
- [ ] **Content Team:** All PRs reviewed and merged
- [ ] **Validation:** All tool articles validated with `Scan-McpToolCoverage.ps1`
- [ ] **Validation:** All annotation tables migrated from legacy single-line format
- [ ] **Publish:** Changes live on Microsoft Learn

---

## Acceptance Criteria

- ✅ All metadata files generated for version {VERSION}
- ✅ All PRs from CHANGELOG are documented with upstream links
- ✅ Content impact analysis identifies all affected namespaces
- ✅ NEW namespaces have corresponding new tool articles created
- ✅ CHANGED namespaces have existing articles updated
- ✅ All tool articles pass `Scan-McpToolCoverage.ps1` validation
- ✅ No legacy single-line annotation format remains (all migrated to table format)
- ✅ All content PRs merged and published to Microsoft Learn

---

## Reports

| Step | Report | Purpose |
|------|--------|---------|
| 1 | [Release Detection](file:///C:/project-dina-ai-dev-tools/projects/azure-ai-tools/status/echo-release-detection-{TIMESTAMP}.md) | Version detection, CHANGELOG parsing |
| 2 | [Metadata Generation](file:///C:/project-dina-ai-dev-tools/projects/azure-ai-tools/status/echo-metadata-generation-{TIMESTAMP}.md) | Metadata folder generation, PR creation |
| 3 | [Content Impact](file:///C:/project-dina-ai-dev-tools/projects/azure-ai-tools/status/echo-content-impact-{VERSION}-{TIMESTAMP}.md) | Namespace analysis, priority assignment |

---

## Template Version

**Version:** 1.0  
**Created:** 2026-07-13  
**Owner:** Echo (Azure MCP CLI Version Sync Specialist)
```

---

## Fill-In Placeholders Reference

| Placeholder | Description | Example |
|-------------|-------------|---------|
| `{VERSION}` | Full semantic version | `3.0.0-beta.25` |
| `{RELEASE_DATE}` | ISO 8601 date format | `2026-07-10` |
| `{VERSION_ANCHOR}` | GitHub anchor slug (lowercase, no dots) | `#300-beta25` |
| `{LIST_BREAKING_CHANGES_PRS}` | All Breaking Changes PRs with descriptions | `- [#2979](https://github.com/microsoft/mcp/pull/2979) Replaced monitor healthmodels entity get` |
| `{LIST_FEATURES_ADDED_PRS}` | All Features Added PRs with descriptions | `- [#2948](https://github.com/microsoft/mcp/pull/2948) Added resilience toolset` |
| `{LIST_BUGS_FIXED_PRS}` | All Bugs Fixed PRs with descriptions | `- [#2907](https://github.com/microsoft/mcp/pull/2907) Fixed case-sensitive tenant comparison` |
| `{LIST_OTHER_CHANGES_PRS}` | All Other Changes PRs with descriptions | `- [#3003](https://github.com/microsoft/mcp/pull/3003) Added Claude Code install docs` |
| `{COUNT}` | Numeric count | `5` |
| `{LIST_NEW_NAMESPACES}` | Comma-separated list | `resilience, monitor.healthmodels` |
| `{LIST_CHANGED_NAMESPACES}` | Comma-separated list | `authorization, storage` |
| `{TIMESTAMP}` | ISO 8601 timestamp for reports | `2026-07-13-0842` |

---

## Notes for ADO Creation via `az boards`

**⚠️ Important:** When using `az boards work-item update --fields` to set field VALUES:

- The command **strips '&', non-ASCII characters, and stray '<'/'>' symbols**
- It **truncates on newlines** when used for field updates
- For **single-line fields** (Title, Tags): Use ASCII-safe values, replace arrows with ' / '
- For **rich multi-line HTML descriptions**: Use the appropriate `az boards` mechanism that preserves HTML/markdown, or attach full detail via markdown report + Hyperlink relation

**Recommendation:** Create the work item with minimal safe fields first, then update with rich description using the REST API or attach the full report as a file attachment.
