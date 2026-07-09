#!/usr/bin/env pwsh
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

.PARAMETER ModelDeploymentName
    Model deployment name. Should match your bicep/AI Services deployment. Defaults to "gpt-5-mini".

.PARAMETER ApiVersion
    Azure OpenAI API version. Should match your bicep/AI Services deployment. Defaults to "2025-03-01-preview".

.EXAMPLE
    pwsh -File infra/populate-env.ps1 -ResourceGroup rg-mcpdocs

.EXAMPLE
    pwsh -File infra/populate-env.ps1 -EnvironmentName mcpdocs

.EXAMPLE
    pwsh -File infra/populate-env.ps1 -ResourceGroup rg-mcpdocs -UseDefaultCredential
#>

param(
    [string]$ResourceGroup,
    [string]$EnvironmentName,
    [switch]$UseDefaultCredential,
    [string]$OutputPath,
    # Model deployment name — should match your bicep/AI Services deployment
    [string]$ModelDeploymentName = "gpt-5-mini",
    # Azure OpenAI API version — should match your bicep/AI Services deployment
    [string]$ApiVersion = "2025-03-01-preview"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ── Resolve paths ────────────────────────────────────────────────────────────────
$scriptDir = $PSScriptRoot
$repoRoot = Split-Path -Parent $scriptDir

if (-not $OutputPath) {
    $OutputPath = Join-Path $repoRoot 'mcp-tools' '.env'
}

# ── Derive missing parameters ────────────────────────────────────────────────────
if ($ResourceGroup -and -not $EnvironmentName) {
    # Strip 'rg-' prefix to derive environment name
    if ($ResourceGroup -match '^rg-(.+)$') {
        $EnvironmentName = $Matches[1]
        Write-Host "  Derived EnvironmentName '$EnvironmentName' from ResourceGroup '$ResourceGroup'" -ForegroundColor DarkGray
    }
    else {
        Write-Error "Cannot derive EnvironmentName from ResourceGroup '$ResourceGroup' (expected 'rg-{name}' pattern). Please provide -EnvironmentName explicitly."
        exit 1
    }
}
elseif (-not $ResourceGroup -and -not $EnvironmentName) {
    Write-Error "Provide at least -ResourceGroup or -EnvironmentName."
    exit 1
}
elseif (-not $ResourceGroup) {
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

# Use parameter values (externalized as script params with defaults)
$modelDeploymentName = $ModelDeploymentName
$apiVersion = $ApiVersion

# ── Query Azure AI Services endpoint ────────────────────────────────────────────
Write-Host "Querying AI Services endpoint ($aiServicesName)..." -ForegroundColor Yellow
$endpoint = az cognitiveservices account show `
    --name $aiServicesName `
    --resource-group $ResourceGroup `
    --query "properties.endpoint" `
    --output tsv 2>&1

if ($LASTEXITCODE -ne 0) {
    # Fallback: discover AI Services resources in the resource group
    Write-Host "  '$aiServicesName' not found. Discovering AI Services resources in '$ResourceGroup'..." -ForegroundColor Yellow
    $discovered = az cognitiveservices account list `
        --resource-group $ResourceGroup `
        --query "[].{name:name, endpoint:properties.endpoint, kind:kind}" `
        --output json 2>&1

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to list AI Services in '$ResourceGroup'. Error: $discovered"
        exit 1
    }

    $resources = $discovered | ConvertFrom-Json
    if ($resources.Count -eq 0) {
        Write-Error "No AI Services resources found in '$ResourceGroup'."
        exit 1
    }

    if ($resources.Count -gt 1) {
        Write-Host "  ⚠️ Multiple AI Services found in resource group:" -ForegroundColor Yellow
        $resources | ForEach-Object { Write-Host "    - $($_.name) ($($_.kind))" -ForegroundColor Yellow }
    }

    # Pick the primary (non-secondary) resource
    $primary = $resources | Where-Object { $_.name -notmatch '-sec$' } | Select-Object -First 1
    if (-not $primary) {
        $primary = $resources | Select-Object -First 1
    }

    if ($resources.Count -gt 1) {
        Write-Host "  ⚠️ Using '$($primary.name)'. Pass -EnvironmentName to target a specific resource." -ForegroundColor Yellow
    }

    $aiServicesName = $primary.name
    $endpoint = $primary.endpoint
    # Derive key vault name from discovered AI Services name
    $derivedKvName = $aiServicesName -replace '^oai-', 'kv-'
    if ($derivedKvName -eq $aiServicesName) {
        Write-Host "  ⚠️ Could not derive Key Vault name from '$aiServicesName' (no 'oai-' prefix). KV lookup will be skipped." -ForegroundColor Yellow
        $keyVaultName = $null
    }
    else {
        $keyVaultName = $derivedKvName
    }
    Write-Host "  Discovered: $aiServicesName (endpoint: $endpoint)" -ForegroundColor Green
    if ($keyVaultName) {
        Write-Host "  Derived Key Vault: $keyVaultName" -ForegroundColor Green
    }
}
else {
    $endpoint = $endpoint.Trim()
    Write-Host "  Endpoint: $endpoint" -ForegroundColor Green
}

# ── Query API key from Key Vault (unless using default credential) ───────────────
$apiKey = ""
if (-not $UseDefaultCredential) {
    if (-not $keyVaultName) {
        Write-Host "  Key Vault name not available. Falling back to AI Services keys..." -ForegroundColor Yellow
        $apiKey = az cognitiveservices account keys list `
            --name $aiServicesName `
            --resource-group $ResourceGroup `
            --query "key1" `
            --output tsv 2>&1

        if ($LASTEXITCODE -ne 0) {
            Write-Error "Failed to fetch API key from AI Services. Error: $apiKey"
            exit 1
        }
    }
    else {
        Write-Host "Fetching API key from Key Vault ($keyVaultName/$secretName)..." -ForegroundColor Yellow
        $apiKey = az keyvault secret show `
            --vault-name $keyVaultName `
            --name $secretName `
            --query "value" `
            --output tsv 2>&1

        if ($LASTEXITCODE -ne 0) {
            Write-Host "  Key Vault not available. Falling back to AI Services keys..." -ForegroundColor Yellow
            $apiKey = az cognitiveservices account keys list `
                --name $aiServicesName `
                --resource-group $ResourceGroup `
                --query "key1" `
                --output tsv 2>&1

            if ($LASTEXITCODE -ne 0) {
                Write-Error "Failed to fetch API key from both Key Vault and AI Services. Error: $apiKey"
                Write-Host "  Hint: Ensure you have 'Key Vault Secrets User' or 'Cognitive Services Contributor' role." -ForegroundColor Yellow
                exit 1
            }
        }
    }
    $apiKey = $apiKey.Trim()
    Write-Host "  API Key: ****$(if ($apiKey.Length -gt 4) { $apiKey.Substring($apiKey.Length - 4) } else { '????' })" -ForegroundColor Green
}
else {
    Write-Host "  ✅ Using DefaultAzureCredential (managed identity / az login) — keyless is the intended, fully-supported auth path for this pipeline. FOUNDRY_API_KEY is deliberately omitted from .env; the whole pipeline (bootstrap probe, Step 4 tool-family reducer, and Step 6 horizontal articles included) resolves keyless config from .env. Ensure the signed-in identity has the 'Cognitive Services OpenAI User' role on the Foundry endpoint." -ForegroundColor Green
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