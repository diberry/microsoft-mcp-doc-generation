#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Generates raw tool files from CLI output - isolated, independent generation

.DESCRIPTION
    This script generates raw tool files (.md) from the Azure MCP CLI output.
    It has NO dependencies on other generation steps and can be run independently.
    
    Raw tool files are the foundation for all downstream processing:
    - ToolGeneration_Composed reads raw files and adds content
    - ToolGeneration_Improved reads composed files for AI enhancement
    
    Prerequisites:
    - CLI output must exist (./generated/cli/cli-output.json)
    - CLI version must exist (./generated/cli/cli-version.json)

.PARAMETER OutputPath
    Path to the generated directory (default: ../generated from script location)

.EXAMPLE
    ./Generate-RawTools.ps1
    ./Generate-RawTools.ps1 -OutputPath ../generated
#>

param(
    [string]$OutputPath = "../generated"
)

$ErrorActionPreference = "Stop"

function Write-Info { param([string]$Message) Write-Host "INFO: $Message" -ForegroundColor Cyan }
function Write-Success { param([string]$Message) Write-Host "SUCCESS: $Message" -ForegroundColor Green }
function Write-Warning { param([string]$Message) Write-Host "WARNING: $Message" -ForegroundColor Yellow }
function Write-Error { param([string]$Message) Write-Host "ERROR: $Message" -ForegroundColor Red }
function Write-Progress { param([string]$Message) Write-Host "PROGRESS: $Message" -ForegroundColor Magenta }

try {
    Write-Progress "Raw Tool Files Generation"
    Write-Info "Starting at: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    Write-Info ""

    $scriptDir = Split-Path -Parent $PSScriptRoot
    $outputDir = if ([System.IO.Path]::IsPathRooted($OutputPath)) {
        $OutputPath
    } else {
        $absPath = Join-Path (Get-Location) $OutputPath
        [System.IO.Path]::GetFullPath($absPath)
    }

    Write-Info "Output directory: $outputDir"
    Write-Info ""

    # Validate prerequisites
    Write-Progress "Validating prerequisites..."
    
    $cliOutputFile = Join-Path $outputDir "cli/cli-output.json"
    if (-not (Test-Path $cliOutputFile)) {
        throw "CLI output file not found: $cliOutputFile"
    }
    Write-Success "✓ CLI output exists"

    $versionOutputFile = Join-Path $outputDir "cli/cli-version.json"
    if (-not (Test-Path $versionOutputFile)) {
        throw "CLI version file not found: $versionOutputFile"
    }
    Write-Success "✓ CLI version exists"

    # Read CLI version
    $cliVersion = "unknown"
    try {
        $versionContent = Get-Content $versionOutputFile -Raw
        if ($versionContent.Trim().StartsWith('{')) {
            $versionData = $versionContent | ConvertFrom-Json
            $cliVersion = $versionData.version ?? $versionData.Version ?? "unknown"
        } else {
            $cliVersion = $versionContent.Trim()
        }
    } catch {
        Write-Warning "Could not read CLI version: $_"
    }

    Write-Info "CLI Version: $cliVersion"
    Write-Info ""

    # Run ToolGeneration_Raw
    Write-Progress "Generating raw tool files via ToolGeneration_Raw..."
    Write-Info ""
    
    $rawToolsDir = Join-Path $outputDir "tools-raw"
    $docsGenDir = $scriptDir
    
    Push-Location $docsGenDir
    try {
        $rawArgs = @(
            "--project", "ToolGeneration_Raw",
            "--configuration", "Release",
            "--",
            $cliOutputFile,
            $rawToolsDir,
            $cliVersion
        )
        
        Write-Info "Command: dotnet run $($rawArgs -join ' ')"
        dotnet run @rawArgs
        
        if ($LASTEXITCODE -ne 0) {
            throw "ToolGeneration_Raw failed with exit code $LASTEXITCODE"
        }
    } finally {
        Pop-Location
    }

    Write-Info ""

    # Count generated files
    $rawCount = if (Test-Path $rawToolsDir) { 
        (Get-ChildItem $rawToolsDir -Filter "*.md" -File -ErrorAction SilentlyContinue).Count 
    } else { 
        0 
    }
    
    Write-Success "Raw tool files: $rawCount"
    Write-Info ""
    Write-Success "Raw tool files generated successfully"
    Write-Info ""
    Write-Success "Completed at: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"

} catch {
    Write-Error "Raw tool generation failed: $($_.Exception.Message)"
    exit 1
}
