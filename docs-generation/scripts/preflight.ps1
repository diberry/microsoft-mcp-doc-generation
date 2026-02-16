#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Preflight setup for documentation generation pipeline

.DESCRIPTION
    Performs all one-time setup steps before namespace processing:
    - Validates .env file exists with required AI credentials
    - Cleans ./generated directory
    - Creates output directory structure
    - Builds .NET solution (all generator projects)
    - Generates MCP CLI metadata (cli-output.json, cli-namespace.json, cli-version.json)
    - Validates brand mappings (Step 0) - STOPS if missing branding
    
    This script is designed to be called once by the orchestrator (start.sh) before
    processing any namespaces. It should NOT be run by worker scripts (start-only.sh).

.PARAMETER OutputPath
    Path to the generated directory (default: ../../generated from script location)

.EXAMPLE
    # From repository root
    pwsh ./docs-generation/scripts/preflight.ps1

.EXAMPLE
    # With custom output path
    pwsh ./docs-generation/scripts/preflight.ps1 -OutputPath "./custom-output"
#>

param(
    [string]$OutputPath = ""
)

$ErrorActionPreference = "Stop"

# Calculate paths relative to script location
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path (Join-Path $scriptDir "../..")
$docsGenDir = Join-Path $repoRoot "docs-generation"
$testNpmDir = Join-Path $repoRoot "test-npm-azure-mcp"

# Determine output directory
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $repoRoot "generated"
} elseif (-not [System.IO.Path]::IsPathRooted($OutputPath)) {
    $OutputPath = Join-Path $repoRoot $OutputPath
}
$OutputPath = [System.IO.Path]::GetFullPath($OutputPath)

Write-Host "===================================================================" -ForegroundColor Cyan
Write-Host "PREFLIGHT: Global Setup" -ForegroundColor Cyan
Write-Host "===================================================================" -ForegroundColor Cyan
Write-Host ""

# Step 0: Validate .env file and AI configuration
$validateEnvScript = Join-Path $scriptDir "validate-env.ps1"
& $validateEnvScript -DocsGenDir $docsGenDir

if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) {
    Write-Host "⛔ PIPELINE HALTED: .env validation failed" -ForegroundColor Red
    exit 1
}
Write-Host ""

# Step 1: Clean up last run
Write-Host "Cleaning previous run..." -ForegroundColor Yellow
if (Test-Path $OutputPath) {
    Remove-Item -Path $OutputPath -Recurse -Force
}
Write-Host "✓ Cleaned: $OutputPath" -ForegroundColor Green
Write-Host ""

# Step 2: Create output directories
Write-Host "Creating output directories..." -ForegroundColor Yellow
New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $OutputPath "cli") -Force | Out-Null
Write-Host "✓ Created base directories" -ForegroundColor Green
Write-Host ""

# Step 3: Build .NET solution
Write-Host "Building .NET solution..." -ForegroundColor Yellow
$solutionFile = Join-Path $repoRoot "docs-generation.sln"
if (Test-Path $solutionFile) {
    # Clean stale obj/bin artifacts to prevent NuGet target conflicts
    Write-Host "  Cleaning previous build artifacts..." -ForegroundColor Gray
    & dotnet clean $solutionFile --configuration Release --verbosity quiet 2>&1 | Out-Null
    & dotnet build $solutionFile --configuration Release --verbosity quiet
    if ($LASTEXITCODE -ne 0) {
        Write-Host "⛔ PIPELINE HALTED: .NET build failed" -ForegroundColor Red
        exit 1
    }
    Write-Host "✓ .NET build succeeded" -ForegroundColor Green
} else {
    Write-Host "⚠ WARNING: Solution file not found: $solutionFile" -ForegroundColor Yellow
}
Write-Host ""

# Step 4: Generate tool metadata from MCP CLI
Write-Host "Generating CLI tool metadata..." -ForegroundColor Yellow
Push-Location $testNpmDir
try {
    Write-Host "  Installing npm dependencies..." -ForegroundColor Gray
    npm install --silent 2>&1 | Out-Null
    
    Write-Host "  Extracting CLI version..." -ForegroundColor Gray
    $versionFile = Join-Path $OutputPath "cli/cli-version.json"
    npm run --silent get:version | Out-File -FilePath $versionFile -Encoding utf8 -NoNewline
    
    Write-Host "  Extracting tool metadata..." -ForegroundColor Gray
    $outputFile = Join-Path $OutputPath "cli/cli-output.json"
    npm run --silent get:tools-json | Out-File -FilePath $outputFile -Encoding utf8 -NoNewline
    
    Write-Host "  Extracting namespace metadata..." -ForegroundColor Gray
    $namespaceFile = Join-Path $OutputPath "cli/cli-namespace.json"
    npm run --silent get:tools-namespace | Out-File -FilePath $namespaceFile -Encoding utf8 -NoNewline
    
    Write-Host "✓ Generated CLI tool metadata" -ForegroundColor Green
} finally {
    Pop-Location
}
Write-Host ""

# Step 5: Validate brand mappings (CRITICAL - stops pipeline if validation fails)
Write-Host "Running brand mapping validation..." -ForegroundColor Yellow
Push-Location $docsGenDir
try {
    $validationScript = Join-Path $scriptDir "0-Validate-BrandMappings.ps1"
    & $validationScript -OutputPath $OutputPath -SkipBuild
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host ""
        Write-Host "⛔ PIPELINE HALTED: Brand mapping validation failed (exit code: $LASTEXITCODE)" -ForegroundColor Red
        Write-Host "   Review suggestions at: $OutputPath/reports/brand-mapping-suggestions.json" -ForegroundColor Yellow
        Write-Host "   Add missing mappings to: $docsGenDir/data/brand-to-server-mapping.json" -ForegroundColor Yellow
        Write-Host "   Then re-run this script." -ForegroundColor Yellow
        exit $LASTEXITCODE
    }
    Write-Host "✓ Brand mapping validation passed" -ForegroundColor Green
} finally {
    Pop-Location
}
Write-Host ""

# Step 6: Create additional output directories
Write-Host "Creating generation output directories..." -ForegroundColor Yellow
$directories = @(
    "common-general",
    "tools",
    "example-prompts",
    "annotations",
    "logs",
    "tool-family"
)

foreach ($dir in $directories) {
    New-Item -ItemType Directory -Path (Join-Path $OutputPath $dir) -Force | Out-Null
}
Write-Host "✓ Created generation directories" -ForegroundColor Green
Write-Host ""

# Summary
Write-Host "===================================================================" -ForegroundColor Green
Write-Host "✓ PREFLIGHT COMPLETE" -ForegroundColor Green
Write-Host "===================================================================" -ForegroundColor Green
Write-Host "Output directory: $OutputPath" -ForegroundColor Cyan
Write-Host ""
