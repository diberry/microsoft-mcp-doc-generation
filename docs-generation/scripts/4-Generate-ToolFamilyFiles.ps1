#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Generates final tool family files (complete tools, composed/improved tools, tool family cleanup)

.DESCRIPTION
    Runs all steps required to produce final tool family files:
    1. Complete tools
    2. Tool generation composed + improved (raw optional if already generated)
    3. Tool family cleanup/assembly

.PARAMETER OutputPath
    Path to the generated directory (default: ../../generated from script location)

.PARAMETER MaxTokens
    Maximum tokens for AI improvements (default: 8000)

.EXAMPLE
    ./Generate-ToolFamilyFiles.ps1
    ./Generate-ToolFamilyFiles.ps1 -OutputPath ../generated -MaxTokens 12000
#>

param(
    [string]$OutputPath = "../../generated",
    [int]$MaxTokens = 8000
)

$ErrorActionPreference = "Stop"

function Write-Info { param([string]$Message) Write-Host "INFO: $Message" -ForegroundColor Cyan }
function Write-Success { param([string]$Message) Write-Host "SUCCESS: $Message" -ForegroundColor Green }
function Write-Warning { param([string]$Message) Write-Host "WARNING: $Message" -ForegroundColor Yellow }
function Write-Error { param([string]$Message) Write-Host "ERROR: $Message" -ForegroundColor Red }
function Write-Progress { param([string]$Message) Write-Host "PROGRESS: $Message" -ForegroundColor Magenta }

try {
    Write-Progress "Tool Family Files Generation"
    Write-Info "Starting at: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    Write-Info ""

    $scriptDir = $PSScriptRoot
    $outputDir = if ([System.IO.Path]::IsPathRooted($OutputPath)) {
        $OutputPath
    } else {
        $absPath = Join-Path (Get-Location) $OutputPath
        [System.IO.Path]::GetFullPath($absPath)
    }

    Write-Info "Output directory: $outputDir"

    $rawToolsDir = Join-Path $outputDir "tools-raw"
    $skipRaw = $false
    if (Test-Path $rawToolsDir) {
        $rawCount = (Get-ChildItem $rawToolsDir -Filter "*.md" -File -ErrorAction SilentlyContinue).Count
        if ($rawCount -gt 0) {
            $skipRaw = $true
            Write-Info "Raw tools detected ($rawCount files). Skipping raw generation."
        }
    }

    Write-Progress "Step 1: Generating complete tools..."
    & "$scriptDir\Generate-CompleteTools.ps1" -OutputPath $OutputPath
    if ($LASTEXITCODE -ne 0) {
        throw "Complete tools generation failed"
    }

    Write-Progress "Step 2: Generating composed + improved tools..."
    if ($skipRaw) {
        & "$scriptDir\..\Generate-ToolGenerationAndAIImprovements.ps1" -OutputPath $OutputPath -SkipRaw -MaxTokens $MaxTokens
    } else {
        & "$scriptDir\..\Generate-ToolGenerationAndAIImprovements.ps1" -OutputPath $OutputPath -MaxTokens $MaxTokens
    }
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Tool generation and AI improvements reported issues"
    }

    Write-Progress "Step 3: Generating tool family files..."
    & "$scriptDir\GenerateToolFamilyCleanup-multifile.ps1" -OutputPath $OutputPath
    if ($LASTEXITCODE -ne 0) {
        throw "Tool family cleanup failed"
    }

    Write-Success "Tool family files generated successfully"
    Write-Info ""
    Write-Success "Completed at: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"

} catch {
    Write-Error "Tool family generation failed: $($_.Exception.Message)"
    exit 1
}
