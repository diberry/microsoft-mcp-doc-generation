#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Validates that all CLI namespaces have brand mappings before generation.

.DESCRIPTION
    Pre-pipeline validator that:
    1. Reads cli-output.json to extract all tool namespaces
    2. Compares against brand-to-server-mapping.json
    3. If new namespaces found, uses GenAI to suggest brand mapping entries
    4. Outputs suggestions and HALTS execution for human review

    This script should run BEFORE any generation steps (Steps 1-5).

.PARAMETER OutputPath
    Path to the generated directory (default: ../generated from script location)

.PARAMETER CliOutputPath
    Path to cli-output.json (default: {OutputPath}/cli/cli-output.json)

.PARAMETER BrandMappingPath
    Path to brand-to-server-mapping.json (default: data/brand-to-server-mapping.json)

.EXAMPLE
    ./0-Validate-BrandMappings.ps1
    ./0-Validate-BrandMappings.ps1 -OutputPath ../generated
#>

param(
    [string]$OutputPath = "../generated",
    [string]$CliOutputPath = "",
    [string]$BrandMappingPath = ""
)

$ErrorActionPreference = "Stop"

function Write-Info { param([string]$Message) Write-Host "INFO: $Message" -ForegroundColor Cyan }
function Write-Success { param([string]$Message) Write-Host "SUCCESS: $Message" -ForegroundColor Green }
function Write-Warn { param([string]$Message) Write-Host "WARNING: $Message" -ForegroundColor Yellow }
function Write-Err { param([string]$Message) Write-Host "ERROR: $Message" -ForegroundColor Red }

$scriptDir = $PSScriptRoot
if (-not $scriptDir) { $scriptDir = Get-Location }

# Resolve paths
$resolvedOutput = if ([System.IO.Path]::IsPathRooted($OutputPath)) { $OutputPath } else { Join-Path $scriptDir $OutputPath }

if ([string]::IsNullOrWhiteSpace($CliOutputPath)) {
    $CliOutputPath = Join-Path $resolvedOutput "cli" "cli-output.json"
}

if ([string]::IsNullOrWhiteSpace($BrandMappingPath)) {
    $BrandMappingPath = Join-Path $scriptDir "data" "brand-to-server-mapping.json"
}

Write-Host ""
Write-Host "╔══════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║         Step 0: Brand Mapping Validation                    ║" -ForegroundColor Cyan
Write-Host "╚══════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

# Verify CLI output exists
if (-not (Test-Path $CliOutputPath)) {
    Write-Err "CLI output not found: $CliOutputPath"
    Write-Err "Run CLI metadata generation first (npm run get:tools-json)"
    exit 1
}

# Verify brand mapping exists
if (-not (Test-Path $BrandMappingPath)) {
    Write-Err "Brand mapping file not found: $BrandMappingPath"
    exit 1
}

Write-Info "CLI output:    $CliOutputPath"
Write-Info "Brand mapping: $BrandMappingPath"
Write-Host ""

# Build the BrandMapperValidator project
$projectDir = Join-Path $scriptDir "BrandMapperValidator"
$projectFile = Join-Path $projectDir "BrandMapperValidator.csproj"

if (-not (Test-Path $projectFile)) {
    Write-Err "BrandMapperValidator project not found: $projectFile"
    exit 1
}

Write-Info "Building BrandMapperValidator..."
Push-Location $projectDir
try {
    & dotnet build --configuration Release --verbosity quiet
    if ($LASTEXITCODE -ne 0) {
        Write-Err "Failed to build BrandMapperValidator"
        exit 1
    }
    Write-Success "Build successful"
} finally {
    Pop-Location
}

Write-Host ""
Write-Info "Running brand mapping validation..."
Write-Host ""

# Run the validator
$suggestionsPath = Join-Path $resolvedOutput "reports" "brand-mapping-suggestions.json"

Push-Location $projectDir
try {
    & dotnet run --configuration Release --no-build -- `
        --cli-output "$CliOutputPath" `
        --brand-mapping "$BrandMappingPath" `
        --output "$suggestionsPath"
    $exitCode = $LASTEXITCODE
} finally {
    Pop-Location
}

Write-Host ""

switch ($exitCode) {
    0 {
        Write-Success "Brand mapping validation passed - all namespaces are mapped"
        exit 0
    }
    2 {
        Write-Err "Brand mapping validation FAILED - new namespaces need mappings"
        Write-Warn "Review suggestions at: $suggestionsPath"
        Write-Warn "Pipeline execution halted. Add mappings and re-run."
        exit 2
    }
    default {
        Write-Err "Brand mapping validation failed with error code: $exitCode"
        exit $exitCode
    }
}
