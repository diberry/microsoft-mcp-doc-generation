#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Generate parameter include files for each tool
    
.DESCRIPTION
    This script generates individual parameter documentation files for each tool using the C# generator.
    Parameters include all command-line options available for each tool.
    
.PARAMETER OutputPath
    Base output directory for generated files (relative to script location, default: ../generated)
    
.PARAMETER CliVersion
    CLI version to include in generated files (optional)

.EXAMPLE
    ./Generate-Parameters.ps1
    ./Generate-Parameters.ps1 -OutputPath ../generated -CliVersion "2.0.0-beta.17"
#>

param(
    [string]$OutputPath = "../generated",
    [string]$CliVersion = ""
)

$ErrorActionPreference = "Stop"

# Helper functions
function Write-Info { param([string]$Message) Write-Host "INFO: $Message" -ForegroundColor Cyan }
function Write-Success { param([string]$Message) Write-Host "SUCCESS: $Message" -ForegroundColor Green }
function Write-Error { param([string]$Message) Write-Host "ERROR: $Message" -ForegroundColor Red }
function Write-Progress { param([string]$Message) Write-Host "PROGRESS: $Message" -ForegroundColor Magenta }

try {
    Write-Progress "Generate Parameter Include Files"
    Write-Info "Starting at: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    
    # Resolve paths
    $scriptDir = $PSScriptRoot
    $outputDir = if ([System.IO.Path]::IsPathRooted($OutputPath)) {
        $OutputPath
    } else {
        Join-Path $scriptDir $OutputPath
    }
    
    # Ensure output directory exists
    if (-not (Test-Path $outputDir)) {
        New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
    }
    
    Write-Info "Script directory: $scriptDir"
    Write-Info "Output directory: $outputDir"
    
    # Check for CLI files
    $cliOutputFile = Join-Path $outputDir "cli" "cli-output.json"
    $cliVersionFile = Join-Path $outputDir "cli" "cli-version.json"
    
    if (-not (Test-Path $cliOutputFile)) {
        throw "CLI output file not found: $cliOutputFile"
    }
    
    Write-Success "✓ CLI files found"
    
    # Read version if not provided
    if ([string]::IsNullOrEmpty($CliVersion)) {
        if (Test-Path $cliVersionFile) {
            $CliVersion = (Get-Content $cliVersionFile -Raw).Trim()
            Write-Info "Read CLI version from file: $CliVersion"
        } else {
            $CliVersion = "unknown"
            Write-Info "Using default CLI version: unknown"
        }
    }
    
    # Create parameters directory
    $parametersDir = Join-Path $outputDir "parameters"
    if (-not (Test-Path $parametersDir)) {
        New-Item -ItemType Directory -Path $parametersDir -Force | Out-Null
    }
    
    Write-Info "Parameters output directory: $parametersDir"
    
    # Determine generator directory
    $generatorPath = if (Test-Path "CSharpGenerator/CSharpGenerator.csproj") {
        "CSharpGenerator"
    } elseif (Test-Path "docs-generation/CSharpGenerator/CSharpGenerator.csproj") {
        "docs-generation/CSharpGenerator"
    } else {
        throw "Cannot locate CSharpGenerator project"
    }
    
    Write-Info "Generator path: $generatorPath"
    
    # Build generator args
    $generatorArgs = @(
        "generate-docs",
        $cliOutputFile,
        $outputDir,
        "--parameters"
    )
    
    if ($CliVersion -and $CliVersion -ne "unknown") {
        $generatorArgs += "--version"
        $generatorArgs += $CliVersion
    }
    
    $commandString = "dotnet run --configuration Release -- " + ($generatorArgs -join " ")
    Write-Info "Running: $commandString"
    Write-Info ""
    
    # Execute generator with real-time output
    Push-Location $generatorPath
    try {
        & dotnet run --configuration Release -- $generatorArgs
        if ($LASTEXITCODE -ne 0) {
            throw "Generator failed with exit code: $LASTEXITCODE"
        }
    } finally {
        Pop-Location
    }
    
    Write-Info ""
    
    # Count generated files
    $parameterFiles = @(Get-ChildItem -Path $parametersDir -Filter "*.md" -File -ErrorAction SilentlyContinue)
    Write-Success "Parameters generation completed"
    Write-Info "Generated $($parameterFiles.Count) parameter files"
    Write-Info "Output directory: $parametersDir"
    
    Write-Info ""
    Write-Success "Completed at: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    
} catch {
    Write-Error "Parameters generation failed: $_"
    exit 1
}
