#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Generate example prompts using Azure OpenAI
    
.DESCRIPTION
    This script generates natural language example prompts for each tool using Azure OpenAI.
    Requires Azure OpenAI credentials configured in .env file.
    
.PARAMETER OutputPath
    Base output directory for generated files (relative to script location, default: ../generated)
    
.PARAMETER CliVersion
    CLI version to include in generated files (optional)

.EXAMPLE
    ./Generate-ExamplePromptsAI.ps1
    ./Generate-ExamplePromptsAI.ps1 -OutputPath ../generated -CliVersion "2.0.0-beta.17"
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
    Write-Progress "Generate Example Prompts with Azure OpenAI"
    Write-Info "Starting at: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    
    # Resolve paths
    $scriptDir = $PSScriptRoot
    $outputDir = if ([System.IO.Path]::IsPathRooted($OutputPath)) {
        $OutputPath
    } else {
        $absPath = Join-Path (Get-Location) $OutputPath
        [System.IO.Path]::GetFullPath($absPath)
    }
    
    Write-Info "Output directory: $outputDir"
    
    # Check for CLI files
    $cliOutputFile = Join-Path $outputDir "cli" "cli-output.json"
    if (-not (Test-Path $cliOutputFile)) {
        throw "CLI output file not found: $cliOutputFile"
    }
    
    Write-Success "✓ CLI files found"
    
    # Read version if not provided
    if ([string]::IsNullOrEmpty($CliVersion)) {
        $cliVersionFile = Join-Path $outputDir "cli" "cli-version.json"
        if (Test-Path $cliVersionFile) {
            $CliVersion = (Get-Content $cliVersionFile -Raw).Trim()
        } else {
            $CliVersion = "unknown"
        }
    }
    
    # Determine generator directory
    $generatorPath = if (Test-Path "CSharpGenerator/CSharpGenerator.csproj") {
        "CSharpGenerator"
    } elseif (Test-Path "docs-generation/CSharpGenerator/CSharpGenerator.csproj") {
        "docs-generation/CSharpGenerator"
    } else {
        throw "Cannot locate CSharpGenerator project"
    }
    
    # Build generator args
    $generatorArgs = @(
        "generate-docs",
        $cliOutputFile,
        $outputDir,
        "--example-prompts"
    )
    
    if ($CliVersion -and $CliVersion -ne "unknown") {
        $generatorArgs += "--version"
        $generatorArgs += $CliVersion
    }
    
    $commandString = "dotnet run --configuration Release -- " + ($generatorArgs -join " ")
    Write-Info "Running: $commandString"
    Write-Info ""
    Write-Info "Output will stream in real-time below:"
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
    $examplePromptsDir = Join-Path $outputDir "example-prompts"
    $examplePromptFiles = @(Get-ChildItem -Path $examplePromptsDir -Filter "*.md" -File -ErrorAction SilentlyContinue)
    Write-Success "Example prompts generation completed"
    Write-Info "Generated $($examplePromptFiles.Count) example prompt files"
    Write-Info "Output directory: $examplePromptsDir"
    
    Write-Info ""
    Write-Success "Completed at: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    
} catch {
    Write-Error "Example prompts generation failed: $_"
    exit 1
}
