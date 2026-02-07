#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Generates example prompts for tools using Azure OpenAI

.DESCRIPTION
    Top-level orchestrator for example prompt generation using standalone .NET packages.
    1. Reads CLI version (if available)
    2. Calls ExamplePromptGeneratorStandalone (.NET package at ./ExamplePromptGeneratorStandalone/) 
       to generate prompts via Azure OpenAI for each tool
    3. Validates generated prompts using ExamplePromptValidator (.NET package at ./ExamplePromptValidator/)
       to ensure all required parameters are present

.PARAMETER OutputPath
    Path to the generated directory (default: ./generated from docs-generation root)

.EXAMPLE
    ./3-Generate-ExamplePrompts.ps1
    ./3-Generate-ExamplePrompts.ps1 -OutputPath ../generated
#>

param(
    [string]$OutputPath = "./generated"
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

    # Check if example prompts already exist
    $examplePromptsDir = Join-Path $outputDir "example-prompts"
    $existingPrompts = if (Test-Path $examplePromptsDir) {
        @(Get-ChildItem -Path $examplePromptsDir -Filter "*.md" -File -ErrorAction SilentlyContinue).Count
    } else {
        0
    }

    if ($existingPrompts -gt 0) {
        Write-Success "✓ Example prompts already exist ($existingPrompts files)"
        Write-Info "Skipping generation, proceeding to validation..."
        Write-Info ""
    } else {
        Write-Info "No existing example prompts found, will generate..."
    }

    Write-Info ""

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

    # Only generate if prompts don't already exist
    if ($existingPrompts -eq 0) {
        # Build .NET packages before running
        Write-Progress "Building .NET packages..."
        $solutionFile = Join-Path (Split-Path $scriptDir -Parent) "docs-generation.sln"
        if (Test-Path $solutionFile) {
            & dotnet build $solutionFile --configuration Release
            if ($LASTEXITCODE -ne 0) {
                throw ".NET build failed with exit code: $LASTEXITCODE"
            }
            Write-Success "✓ .NET packages built successfully"
        } else {
            Write-Warning "Solution file not found: $solutionFile. Skipping build."
        }
        Write-Info ""

        # Run ExamplePromptGeneratorStandalone (.NET package)
        # Location: ./ExamplePromptGeneratorStandalone/
        # Purpose: Generates 5 natural language example prompts per tool using Azure OpenAI
        Write-Progress "Generating example prompts via ExamplePromptGeneratorStandalone..."
        Write-Info ""
        
        $cliOutputFile = Join-Path $outputDir "cli/cli-output.json"
        if (-not (Test-Path $cliOutputFile)) {
            throw "CLI output file not found: $cliOutputFile"
        }

        $generatorProject = Join-Path $scriptDir "ExamplePromptGeneratorStandalone"
        & dotnet run --project $generatorProject --configuration Release -- $cliOutputFile $outputDir $cliVersion
        
        if ($LASTEXITCODE -ne 0) {
            throw "Example prompts generation failed"
        }

        Write-Info ""
    }
    else {
        Write-Success "Skipping example prompt generation (files already exist)"
        Write-Info ""
    }

    # Count generated files
    $examplePromptsDir = Join-Path $outputDir "example-prompts"
    $examplePromptFiles = @(Get-ChildItem -Path $examplePromptsDir -Filter "*.md" -File -ErrorAction SilentlyContinue)
    
    Write-Info "Final count"
    Write-Info "  Example prompts: $($examplePromptFiles.Count)"
    
    # Run ExamplePromptValidator (.NET package with PowerShell wrapper)
    # Location: ./ExamplePromptValidator/
    # Purpose: Uses Azure OpenAI to validate that all required parameters are present in generated prompts
    Write-Info ""
    Write-Progress "Validating example prompts (required params only)..."
    & "$scriptDir\ExamplePromptValidator\scripts\Validate-ExamplePrompts-RequiredParams.ps1" -OutputPath $OutputPath

    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Example prompt validation completed with issues"
    } else {
        Write-Success "✓ Example prompt validation completed successfully"
    }

    Write-Success "Example prompts generated successfully"
    Write-Info ""
    Write-Success "Completed at: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"

} catch {
    Write-Error "Example prompts generation failed: $($_.Exception.Message)"
    exit 1
}
