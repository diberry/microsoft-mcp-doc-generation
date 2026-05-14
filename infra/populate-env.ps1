<#
.SYNOPSIS
    Populates mcp-tools/.env from deployed Azure resources.

.DESCRIPTION
    Reads the bicep outputs from infra/main.bicep to determine .env variable names,
    queries deployed Azure resources via az CLI, and writes mcp-tools/.env.

    Requires:
      - az login (authenticated)
      - RBAC: Cognitive Services Reader on the AI Services resource
      - RBAC: Key Vault Secrets User on the Key Vault (unless -UseDefaultCredential)

.PARAMETER ResourceGroup
    Azure resource group name. If omitted, derived from EnvironmentName as "rg-{EnvironmentName}".

.PARAMETER EnvironmentName
    Environment name used in bicep deployment (e.g., "mcpdocs").
    Used to derive resource names: oai-{env}, kv-{env}, rg-{env}.

.PARAMETER UseDefaultCredential
    If set, skips Key Vault API key fetch and sets FOUNDRY_USE_DEFAULT_CREDENTIAL=true.
    Omits FOUNDRY_API_KEY from .env.

.PARAMETER OutputPath
    Path to write the .env file. Defaults to mcp-tools/.env relative to repo root.

.EXAMPLE
    pwsh -File infra/populate-env.ps1 -EnvironmentName mcpdocs

.EXAMPLE
    pwsh -File infra/populate-env.ps1 -ResourceGroup rg-mcpdocs -EnvironmentName mcpdocs -UseDefaultCredential
#>

param(
    [string]$ResourceGroup,
    [Parameter(Mandatory = $true)]
    [string]$EnvironmentName,
    [switch]$UseDefaultCredential,
    [string]$OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ── Resolve paths ────────────────────────────────────────────────────────────────
$scriptDir = $PSScriptRoot
$repoRoot = Split-Path -Parent $scriptDir

if (-not $OutputPath) {
    $OutputPath = Join-Path $repoRoot 'mcp-tools' '.env'
}

# ── Derive resource group if not provided ────────────────────────────────────────
if (-not $ResourceGroup) {
    $ResourceGroup = "rg-$EnvironmentName"
}

Write-Host "╔══════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║  populate-env.ps1 — Derive .env from deployed Azure resources ║" -ForegroundColor Cyan
Write-Host "╚══════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Environment:    $EnvironmentName"
Write-Host "  Resource Group: $ResourceGroup"
Write-Host "  Output:         $OutputPath"
Write-Host "  Auth Mode:      $(if ($UseDefaultCredential) { 'DefaultAzureCredential' } else { 'API Key (from Key Vault)' })"
Write-Host ""

# ── Verify az CLI is available and logged in ─────────────────────────────────────
Write-Host "Checking az CLI authentication..." -ForegroundColor Yellow
try {
    $account = az account show --output json 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Error "az CLI not authenticated. Run 'az login' first."
        exit 1
    }
    $accountObj = $account | ConvertFrom-Json
    Write-Host "  Logged in as: $($accountObj.user.name) (subscription: $($accountObj.name))" -ForegroundColor Green
}
catch {
    Write-Error "az CLI not found or not authenticated. Install Azure CLI and run 'az login'."
    exit 1
}

# ── Resource naming (matches infra/main.bicep) ───────────────────────────────────
$aiServicesName = "oai-$EnvironmentName"
$keyVaultName = "kv-$EnvironmentName"
$secretName = "foundry-api-key"

# Bicep param defaults
$modelDeploymentName = "gpt-5-mini"
$apiVersion = "2025-03-01-preview"

# ── Query Azure AI Services endpoint ────────────────────────────────────────────
Write-Host "Querying AI Services endpoint ($aiServicesName)..." -ForegroundColor Yellow
$endpoint = az cognitiveservices account show `
    --name $aiServicesName `
    --resource-group $ResourceGroup `
    --query "properties.endpoint" `
    --output tsv 2>&1

if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to query AI Services resource '$aiServicesName' in '$ResourceGroup'. Error: $endpoint"
    exit 1
}
$endpoint = $endpoint.Trim()
Write-Host "  Endpoint: $endpoint" -ForegroundColor Green

# ── Query API key from Key Vault (unless using default credential) ───────────────
$apiKey = ""
if (-not $UseDefaultCredential) {
    Write-Host "Fetching API key from Key Vault ($keyVaultName/$secretName)..." -ForegroundColor Yellow
    $apiKey = az keyvault secret show `
        --vault-name $keyVaultName `
        --name $secretName `
        --query "value" `
        --output tsv 2>&1

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to fetch secret '$secretName' from Key Vault '$keyVaultName'. Error: $apiKey"
        Write-Host "  Hint: Ensure you have 'Key Vault Secrets User' role on the vault." -ForegroundColor Yellow
        exit 1
    }
    $apiKey = $apiKey.Trim()
    Write-Host "  API Key: ****$(if ($apiKey.Length -gt 4) { $apiKey.Substring($apiKey.Length - 4) } else { '????' })" -ForegroundColor Green
}

# ── Build .env content ───────────────────────────────────────────────────────────
Write-Host "Writing .env file..." -ForegroundColor Yellow

$envLines = @()

if ($UseDefaultCredential) {
    $envLines += "FOUNDRY_USE_DEFAULT_CREDENTIAL=true"
}
else {
    $envLines += "FOUNDRY_API_KEY=`"$apiKey`""
}

$envLines += "FOUNDRY_ENDPOINT=`"$endpoint`""
$envLines += "FOUNDRY_INSTANCE=`"$aiServicesName`""
$envLines += "FOUNDRY_MODEL_NAME=`"$modelDeploymentName`""
$envLines += "FOUNDRY_MODEL_API_VERSION=`"$apiVersion`""
$envLines += "TOOL_FAMILY_CLEANUP_FOUNDRY_MODEL_NAME=`"$modelDeploymentName`""
$envLines += "TOOL_FAMILY_CLEANUP_FOUNDRY_MODEL_API_VERSION=`"$apiVersion`""
$envLines += "ENDPOINT=`"$endpoint`""

# Ensure output directory exists
$outputDir = Split-Path -Parent $OutputPath
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

$envLines | Set-Content -Path $OutputPath -Encoding utf8NoBOM

Write-Host ""
Write-Host "✅ .env written to: $OutputPath" -ForegroundColor Green
Write-Host ""
Write-Host "Contents:" -ForegroundColor Cyan
Get-Content $OutputPath | ForEach-Object {
    if ($_ -match 'API_KEY=') {
        Write-Host "  $($_ -replace '=.*', '=****')"
    }
    else {
        Write-Host "  $_"
    }
}
Write-Host ""
Write-Host "Done. You can now run: ./start.sh" -ForegroundColor Green
