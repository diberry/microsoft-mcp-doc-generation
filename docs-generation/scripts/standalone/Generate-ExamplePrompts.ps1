#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Generates example prompts for tools using Azure OpenAI

.DESCRIPTION
    Runs the example prompt generation step using existing CLI outputs.

.PARAMETER OutputPath
    Path to the generated directory (default: ../../generated from script location)

.EXAMPLE
    ./Generate-ExamplePrompts.ps1
    ./Generate-ExamplePrompts.ps1 -OutputPath ../generated
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
    Write-Progress "Example Prompts Generation"
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

    $versionOutputFile = Join-Path $outputDir "cli/cli-version.json"
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

    & "$scriptDir\Generate-ExamplePromptsAI.ps1" -OutputPath $OutputPath -CliVersion $cliVersion
    if ($LASTEXITCODE -ne 0) {
        throw "Example prompts generation failed"
    }

    Write-Success "Example prompts generated successfully"
    Write-Info ""
    Write-Success "Completed at: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"

} catch {
    Write-Error "Example prompts generation failed: $($_.Exception.Message)"
    exit 1
}
