#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Generate annotation include files for each tool
    
.DESCRIPTION
    This script generates individual annotation files for each tool using the C# generator.
    Annotations are metadata about tool behavior (destructive, idempotent, etc.)
    
.PARAMETER OutputPath
    Base output directory for generated files (relative to script location, default: ../generated)
    
.PARAMETER CliVersion
    CLI version to include in generated files (optional)

.EXAMPLE
    ./Generate-Annotations.ps1
    ./Generate-Annotations.ps1 -OutputPath ../generated -CliVersion "2.0.0-beta.17"
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
    Write-Progress "Generate Annotation Include Files"
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
    
    # Create annotations directory
    $annotationsDir = Join-Path $outputDir "annotations"
    if (-not (Test-Path $annotationsDir)) {
        New-Item -ItemType Directory -Path $annotationsDir -Force | Out-Null
    }
    
    Write-Info "Annotations output directory: $annotationsDir"
    
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
        "--annotations"
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
    $annotationFiles = @(Get-ChildItem -Path $annotationsDir -Filter "*.md" -File -ErrorAction SilentlyContinue)
    Write-Success "Annotations generation completed"
    Write-Info "Generated $($annotationFiles.Count) annotation files"
    Write-Info "Output directory: $annotationsDir"
    
    Write-Info ""
    Write-Success "Completed at: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    
} catch {
    Write-Error "Annotations generation failed: $_"
    exit 1
}
