# Validation Scripts

Repo-local deterministic validators for Azure MCP generated articles.

## Contents

- `Test-ArticleHealth.ps1` — publish-readiness checks for generated markdown
- `Scan-McpToolCoverage.ps1` — coverage audit against `tools-list.json`
- `tests/` — Pester suites and fixtures for both scripts

## Prerequisites

- PowerShell 7+
- Pester 5+
- Generated article output to validate (`generated/` or `generated-<namespace>/`)
- `tools-list.json` from `mcp-cli-metadata/` when running coverage audit

## Running the tests

```powershell
Invoke-Pester -Path ./mcp-tools/validation/tests -Output Detailed
```

## Running the validators manually

Article health:

> **Before running**: Create the `validation/` output folder if it does not already exist.
>
> ```powershell
> New-Item -ItemType Directory -Force -Path .\generated-storage\validation
> ```

```powershell
pwsh -File .\mcp-tools\validation\Test-ArticleHealth.ps1 `
  -ArticlesDir .\generated-storage\tool-family `
  -OutputJson .\generated-storage\validation\article-health.json
```

Coverage audit:

> **Before running**: Ensure the `validation/` output folder exists (see above).

```powershell
pwsh -File .\mcp-tools\validation\Scan-McpToolCoverage.ps1 `
  -ToolsJsonPath .\mcp-cli-metadata\<version>\tools-list.json `
  -ArticlesDir .\generated-storage\tool-family `
  -Namespace storage `
  -OutputJson .\generated-storage\validation\coverage-audit.json
```

## Phase 1 scope

This directory is the Phase 1 relocation for PRD #574. The scripts remain manually invocable in this phase; typed pipeline wrappers and gate evaluation land in later phases.

For a contributor-focused walkthrough, see `docs/VALIDATION-RUNBOOK.md`.
