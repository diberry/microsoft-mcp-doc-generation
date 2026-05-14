# Get-McpOpenPRs.ps1 — Auto-discover open PRs for Azure MCP Server tool articles
# Queries GitHub via `gh` CLI for open PRs by the current user on MicrosoftDocs/azure-dev-docs-pr
# that touch articles/azure-mcp-server/tools/, then generates a JSON file for use by Scan-McpToolCoverage.ps1.
#
# Usage:
#   .\Get-McpOpenPRs.ps1 -OutputDir <path> [-Repo <owner/repo>] [-Author <github-username>]
#
# Output:
#   Writes open-prs-{yyyy-MM-dd-HHmmss}.json to OutputDir

param(
    [Parameter(Mandatory)]
    [string]$OutputDir,

    [string]$Repo = "MicrosoftDocs/azure-dev-docs-pr",

    # GitHub username to filter PRs. Defaults to current authenticated user.
    [string]$Author = "@me"
)

$ErrorActionPreference = "Stop"

# ─── Verify gh CLI ────────────────────────────────────────────────────────────
$ghVersion = & gh --version 2>$null
if ($LASTEXITCODE -ne 0) {
    Write-Error "GitHub CLI (gh) is not installed or not in PATH. Install from https://cli.github.com/"
    exit 1
}

# ─── Namespace-to-file mapping (mirrors Scan-McpToolCoverage.ps1) ─────────────
$namespaceToFile = @{
    'acr'               = 'azure-container-registry.md'
    'advisor'           = 'azure-advisor.md'
    'aks'               = 'azure-kubernetes-service.md'
    'appservice'        = 'azure-app-service.md'
    'azurebackup'       = 'azure-backup.md'
    'azuremonitor'      = 'azure-monitor.md'
    'azureterraform'    = 'azure-terraform.md'
    'azureterraformbestpractices' = 'azure-terraform-best-practices.md'
    'changeanalysis'    = 'azure-change-analysis.md'
    'compute'           = 'azure-compute.md'
    'containerapp'      = 'azure-container-apps.md'
    'cosmos'            = 'azure-cosmos-db.md'
    'deploy'            = 'azure-deploy.md'
    'deviceregistry'    = 'azure-device-registry.md'
    'extension'         = 'azure-extensions.md'
    'fileshares'        = 'azure-file-shares.md'
    'functions'         = 'azure-functions.md'
    'keyvault'          = 'azure-key-vault.md'
    'kubernetes'        = 'azure-kubernetes.md'
    'marketplace'       = 'azure-marketplace.md'
    'monitor'           = 'azure-monitor.md'
    'mysql'             = 'azure-database-mysql.md'
    'network'           = 'azure-network.md'
    'postgres'          = 'azure-database-postgresql.md'
    'redis'             = 'azure-redis.md'
    'resourcemanager'   = 'azure-resource-manager.md'
    'server'            = 'azure-server.md'
    'servicebus'        = 'azure-service-bus.md'
    'sql'               = 'azure-sql.md'
    'staticwebapps'     = 'azure-static-web-apps.md'
    'storage'           = 'azure-storage.md'
    'subscription'      = 'azure-subscription.md'
    'virtualdesktop'    = 'azure-virtual-desktop.md'
    'wellarchitectedframework' = 'azure-well-architected-framework.md'
    'workbooks'         = 'azure-workbooks.md'
}

# ─── Query GitHub for open PRs ────────────────────────────────────────────────
Write-Host "Querying open PRs on $Repo by $Author..." -ForegroundColor Cyan

$prJson = & gh pr list --repo $Repo --author $Author --state open `
    --json number,headRefName,title,files `
    --limit 50 2>&1

if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to query PRs: $prJson"
    exit 1
}

$prs = $prJson | ConvertFrom-Json

# ─── Filter to PRs that touch azure-mcp-server/tools/ ─────────────────────────
$toolsPath = "articles/azure-mcp-server/tools/"
$results = @()

foreach ($pr in $prs) {
    # Check if any file in the PR is under the tools path
    $toolFiles = @($pr.files | Where-Object { $_.path -like "${toolsPath}*" })
    if ($toolFiles.Count -eq 0) { continue }

    foreach ($file in $toolFiles) {
        $fileName = Split-Path $file.path -Leaf
        # Determine namespace from filename
        $ns = $null
        foreach ($key in $namespaceToFile.Keys) {
            if ($namespaceToFile[$key] -eq $fileName) {
                $ns = $key
                break
            }
        }
        # Fallback: derive namespace from filename pattern azure-{name}.md
        if (-not $ns -and $fileName -match '^azure-(.+)\.md$') {
            $ns = $Matches[1] -replace '-', ''
        }

        if ($ns) {
            $results += @{
                pr        = $pr.number
                namespace = $ns
                branch    = $pr.headRefName
                file      = $fileName
            }
        }
    }
}

# Deduplicate (same PR + namespace)
$unique = @{}
foreach ($r in $results) {
    $key = "$($r.pr)-$($r.namespace)"
    if (-not $unique.ContainsKey($key)) {
        $unique[$key] = $r
    }
}
$results = @($unique.Values | Sort-Object { $_.pr })

# ─── Write output ─────────────────────────────────────────────────────────────
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

$timestamp = Get-Date -Format "yyyy-MM-dd-HHmmss"
$outputFile = Join-Path $OutputDir "open-prs-$timestamp.json"

$results | ConvertTo-Json -Depth 5 | Set-Content -Path $outputFile -Encoding UTF8

Write-Host ""
Write-Host "Found $($results.Count) tool-related entries across $($results | ForEach-Object { $_.pr } | Sort-Object -Unique | Measure-Object | Select-Object -ExpandProperty Count) open PRs" -ForegroundColor Green
Write-Host "Output: $outputFile" -ForegroundColor Green

# Output the path for piping to other scripts
Write-Output $outputFile
