# Validation Runbook

Use these repo-local validators to check generated Azure MCP article output before Phase 2 pipeline integration lands.

## Prerequisites

- PowerShell 7+
- Pester 5+
- Generated output under `generated/` or `generated-<namespace>/`
- `tools-list.json` from `test-npm-azure-mcp/`

Install Pester if needed:

```powershell
Install-Module Pester -Scope CurrentUser -Force -SkipPublisherCheck
```

## Run the Pester suite

```powershell
Invoke-Pester -Path ./mcp-tools/validation/tests -Output Detailed
```

## Run article health validation

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

Use `-Strict` if warnings should fail the command.

## Run coverage audit

> **Before running**: Ensure the `validation/` output folder exists (see above).

```powershell
pwsh -File .\mcp-tools\validation\Scan-McpToolCoverage.ps1 `
  -ToolsJsonPath .\test-npm-azure-mcp\<version>\tools-list.json `
  -ArticlesDir .\generated-storage\tool-family `
  -Namespace storage `
  -OutputJson .\generated-storage\validation\coverage-audit.json
```

## Expected outputs

- `article-health.json` — per-file check results from `Test-ArticleHealth.ps1`
- `coverage-audit.json` — coverage summary and mismatch details from `Scan-McpToolCoverage.ps1`

## Troubleshooting

- `pwsh` not found: install PowerShell 7 and rerun.
- `No tools found for namespace`: verify the namespace prefix exists in `tools-list.json`.
- `Articles directory not found`: pass the generated `tool-family` directory, not repo root.
- Failing Pester integration tests on non-Windows shells: use `pwsh -File`, not `pwsh -Command`.

## Phase note

This runbook documents Phase 1 of PRD #574 only. The generation pipeline does not invoke these validators yet; that wrapper/gate work starts in later phases.
