#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Simple test script to debug example prompts generation
.DESCRIPTION
    Runs just the example prompts generation step with full output logging
#>

param(
    [string]$OutputPath = "../../generated"
)

$ErrorActionPreference = "Stop"

# Color output functions
function Write-Info($msg) { Write-Host "INFO: $msg" -ForegroundColor Cyan }
function Write-Success($msg) { Write-Host "SUCCESS: $msg" -ForegroundColor Green }
function Write-Error($msg) { Write-Host "ERROR: $msg" -ForegroundColor Red }
function Write-Progress($msg) { Write-Host "PROGRESS: $msg" -ForegroundColor Yellow }

try {
    Write-Progress "Test: Example Prompts Generation"
    Write-Info "Starting at: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    
    # Resolve paths
    $scriptDir = $PSScriptRoot
    $docsGenDir = Split-Path -Parent $scriptDir
    $outputDir = Join-Path $scriptDir $OutputPath | Resolve-Path
    
    Write-Info "Script directory: $scriptDir"
    Write-Info "Output directory: $outputDir"
    
    # Check for CLI files
    $cliOutputFile = Join-Path $outputDir "cli" "cli-output.json"
    $cliVersionFile = Join-Path $outputDir "cli" "cli-version.json"
    
    if (-not (Test-Path $cliOutputFile)) {
        throw "CLI output file not found: $cliOutputFile"
    }
    if (-not (Test-Path $cliVersionFile)) {
        throw "CLI version file not found: $cliVersionFile"
    }
    
    Write-Success "✓ CLI files found"
    
    # Read version
    $cliVersion = (Get-Content $cliVersionFile -Raw).Trim()
    Write-Info "CLI Version: $cliVersion"
    
    # Check for example-prompts directory
    $examplePromptsDir = Join-Path $outputDir "example-prompts"
    if (-not (Test-Path $examplePromptsDir)) {
        Write-Info "Creating example-prompts directory"
        New-Item -ItemType Directory -Path $examplePromptsDir -Force | Out-Null
    }
    
    # Count existing files
    $existingFiles = @(Get-ChildItem -Path $examplePromptsDir -Filter "*.md" -File)
    Write-Info "Existing example prompt files: $($existingFiles.Count)"
    
    # Navigate to CSharpGenerator
    $generatorDir = Join-Path $docsGenDir "CSharpGenerator"
    if (-not (Test-Path $generatorDir)) {
        throw "Generator directory not found: $generatorDir"
    }
    
    Write-Info "Changing to generator directory: $generatorDir"
    Push-Location $generatorDir
    
    try {
        # Build the command
        $args = @(
            "run",
            "--configuration", "Release",
            "--",
            "generate-docs",
            $cliOutputFile,
            $outputDir,
            "--example-prompts",
            "--version", $cliVersion
        )
        
        $commandString = "dotnet " + ($args -join " ")
        Write-Info "Running command:"
        Write-Info "  $commandString"
        Write-Info ""
        
        Write-Progress "Starting example prompts generation..."
        Write-Info "Output will appear below:"
        Write-Host "========================================" -ForegroundColor Magenta
        
        # Run the command directly without capturing (so output appears in real-time)
        & dotnet @args
        $exitCode = $LASTEXITCODE
        
        Write-Host "========================================" -ForegroundColor Magenta
        Write-Info "Exit code: $exitCode"
        
        if ($exitCode -ne 0) {
            throw "Generator failed with exit code: $exitCode"
        }
        
        Write-Success "✓ Generator completed successfully"
        
    } finally {
        Pop-Location
    }
    
    # Count files after generation
    $newFiles = @(Get-ChildItem -Path $examplePromptsDir -Filter "*.md" -File)
    Write-Info ""
    Write-Success "Final example prompt files: $($newFiles.Count)"
    Write-Info "New files created: $($newFiles.Count - $existingFiles.Count)"
    
    # Show first few files as sample
    if ($newFiles.Count -gt 0) {
        Write-Info ""
        Write-Info "Sample files (first 10):"
        $newFiles | Select-Object -First 10 | ForEach-Object {
            Write-Info "  - $($_.Name)"
        }
    }
    
    Write-Info ""
    Write-Success "Test completed at: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    
} catch {
    Write-Error "Test failed: $_"
    Write-Error $_.ScriptStackTrace
    exit 1
}
