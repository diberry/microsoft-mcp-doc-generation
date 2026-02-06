#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Generates annotations, parameters, and raw tool files

.DESCRIPTION
    Runs the base generation steps required for tool pipelines:
    1. Annotations
    2. Parameters
    3. ToolGeneration_Raw (via ToolGenerationAndAIImprovements with composed/improved skipped)

.PARAMETER OutputPath
    Path to the generated directory (default: ../../generated from script location)

.EXAMPLE
    ./Generate-AnnotationsParametersRaw.ps1
    ./Generate-AnnotationsParametersRaw.ps1 -OutputPath ../generated
#>

param(
    [string]$OutputPath = "../../generated"
)

$ErrorActionPreference = "Stop"

function Write-Info { param([string]$Message) Write-Host "INFO: $Message" -ForegroundColor Cyan }
function Write-Success { param([string]$Message) Write-Host "SUCCESS: $Message" -ForegroundColor Green }
function Write-Warning { param([string]$Message) Write-Host "WARNING: $Message" -ForegroundColor Yellow }
function Write-Error { param([string]$Message) Write-Host "ERROR: $Message" -ForegroundColor Red }
function Write-Progress { param([string]$Message) Write-Host "PROGRESS: $Message" -ForegroundColor Magenta }

try {
    Write-Progress "Annotations + Parameters + Raw Tools"
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

    $cliOutputFile = Join-Path $outputDir "cli/cli-output.json"
    $versionOutputFile = Join-Path $outputDir "cli/cli-version.json"

    if (-not (Test-Path $cliOutputFile)) {
        throw "CLI output file not found: $cliOutputFile"
    }

    if (-not (Test-Path $versionOutputFile)) {
        Write-Warning "CLI version file not found: $versionOutputFile"
    }

    $cliVersion = "unknown"
    if (Test-Path $versionOutputFile) {
        $versionContent = Get-Content $versionOutputFile -Raw
        if ($versionContent.Trim().StartsWith('{')) {
            $versionData = $versionContent | ConvertFrom-Json
            $cliVersion = $versionData.version ?? $versionData.Version ?? "unknown"
        } else {
            $cliVersion = $versionContent.Trim()
        }
    }

    Write-Info "CLI Version: $cliVersion"
    Write-Info ""

    Write-Progress "Step 1: Generating annotation include files..."
    & "$scriptDir\Generate-Annotations.ps1" -OutputPath $OutputPath -CliVersion $cliVersion
    if ($LASTEXITCODE -ne 0) {
        throw "Annotations generation failed"
    }

    Write-Progress "Step 2: Generating parameter include files..."
    & "$scriptDir\Generate-Parameters.ps1" -OutputPath $OutputPath -CliVersion $cliVersion
    if ($LASTEXITCODE -ne 0) {
        throw "Parameters generation failed"
    }

    Write-Progress "Step 3: Generating raw tool files..."
    & "$scriptDir\Generate-ToolGenerationAndAIImprovements.ps1" -OutputPath $OutputPath -SkipComposed -SkipImproved
    if ($LASTEXITCODE -ne 0) {
        throw "Raw tool generation failed"
    }

    Write-Success "Annotations, parameters, and raw tools generated successfully"
    Write-Info ""
    Write-Success "Completed at: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"

} catch {
    Write-Error "Generation failed: $($_.Exception.Message)"
    exit 1
}
