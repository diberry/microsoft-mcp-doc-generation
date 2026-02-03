#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Separate tool generation orchestration script - runs all 3 generators in sequence
    
.DESCRIPTION
    This script orchestrates the separated tool generation process:
    1. RawToolGenerator - Creates raw files with placeholders
    2. ComposedToolGenerator - Replaces placeholders with actual content
    3. ImprovedToolGenerator - Applies AI-based improvements
    
    Prerequisites:
    - CLI output must exist (./generated/cli/cli-output.json)
    - Annotations, parameters, and example prompts must be generated
    - Azure OpenAI credentials must be configured (for step 3)
    
.PARAMETER OutputPath
    Base output directory (default: ../generated)
    
.PARAMETER SkipRaw
    Skip RawToolGenerator (use existing raw files)
    
.PARAMETER SkipComposed
    Skip ComposedToolGenerator (use existing composed files)
    
.PARAMETER SkipImproved
    Skip ImprovedToolGenerator (skip AI improvements)
    
.PARAMETER MaxTokens
    Maximum tokens for AI improvements (default: 8000)
    
.EXAMPLE
    ./Generate-SeparateTools.ps1
    ./Generate-SeparateTools.ps1 -SkipRaw
    ./Generate-SeparateTools.ps1 -SkipImproved
    ./Generate-SeparateTools.ps1 -MaxTokens 12000
#>

param(
    [string]$OutputPath = "../generated",
    [switch]$SkipRaw = $false,
    [switch]$SkipComposed = $false,
    [switch]$SkipImproved = $false,
    [int]$MaxTokens = 8000
)

# Resolve output path
$currentDir = Get-Location
if ([System.IO.Path]::IsPathRooted($OutputPath)) {
    $outputDir = $OutputPath
} else {
    $outputDir = Join-Path $currentDir $OutputPath
}

# Normalize to absolute path
$resolvedOutput = Resolve-Path $outputDir -ErrorAction SilentlyContinue
if ($resolvedOutput) {
    $outputDir = $resolvedOutput.ProviderPath
}

# Ensure output directory exists
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

# Set up logging
$logDir = Join-Path $outputDir "logs"
if (-not (Test-Path $logDir)) {
    New-Item -ItemType Directory -Path $logDir -Force | Out-Null
}
$logFile = Join-Path $logDir "separate-tools-$(Get-Date -Format 'yyyyMMdd-HHmmss').log"
Start-Transcript -Path $logFile -Append

# Helper functions for colored output
function Write-Info { param([string]$Message) Write-Host "INFO: $Message" -ForegroundColor Cyan }
function Write-Success { param([string]$Message) Write-Host "SUCCESS: $Message" -ForegroundColor Green }
function Write-Warning { param([string]$Message) Write-Host "WARNING: $Message" -ForegroundColor Yellow }
function Write-Error { param([string]$Message) Write-Host "ERROR: $Message" -ForegroundColor Red }
function Write-Progress { param([string]$Message) Write-Host "PROGRESS: $Message" -ForegroundColor Magenta }
function Write-Section { 
    param([string]$Title) 
    Write-Host ""
    Write-Host "═══════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host "  $Title" -ForegroundColor Cyan
    Write-Host "═══════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host ""
}

# Display header
Write-Host ""
Write-Host "╔═══════════════════════════════════════════════════════════════════╗" -ForegroundColor Green
Write-Host "║       Separate Tool Generation Orchestration Script              ║" -ForegroundColor Green
Write-Host "╚═══════════════════════════════════════════════════════════════════╝" -ForegroundColor Green
Write-Host ""
Write-Host "Output Directory: $outputDir" -ForegroundColor White
Write-Host "Log File: $logFile" -ForegroundColor White
Write-Host ""

# Define paths
$cliOutputFile = Join-Path $outputDir "cli/cli-output.json"
$rawToolsDir = Join-Path $outputDir "tools-raw"
$composedToolsDir = Join-Path $outputDir "tools-composed"
$improvedToolsDir = Join-Path $outputDir "tools-ai-improved"
$annotationsDir = Join-Path $outputDir "multi-page/annotations"
$parametersDir = Join-Path $outputDir "multi-page/parameters"
$examplePromptsDir = Join-Path $outputDir "multi-page/example-prompts"

# Read MCP CLI version
$mcpCliVersion = "unknown"
$versionFile = Join-Path $outputDir "cli/cli-version.json"
if (Test-Path $versionFile) {
    try {
        $versionJson = Get-Content $versionFile -Raw | ConvertFrom-Json
        $mcpCliVersion = $versionJson.Version
        Write-Info "MCP CLI Version: $mcpCliVersion"
    }
    catch {
        Write-Warning "Could not read MCP CLI version: $_"
    }
}

# Validate prerequisites
Write-Section "Validating Prerequisites"

$hasErrors = $false

if (-not (Test-Path $cliOutputFile)) {
    Write-Error "CLI output file not found: $cliOutputFile"
    $hasErrors = $true
}
else {
    Write-Success "CLI output file exists"
}

if (-not $SkipComposed) {
    if (-not (Test-Path $annotationsDir)) {
        Write-Warning "Annotations directory not found: $annotationsDir"
    }
    else {
        $annotationCount = (Get-ChildItem $annotationsDir -Filter "*.md" -File).Count
        Write-Success "Annotations directory exists ($annotationCount files)"
    }

    if (-not (Test-Path $parametersDir)) {
        Write-Warning "Parameters directory not found: $parametersDir"
    }
    else {
        $paramCount = (Get-ChildItem $parametersDir -Filter "*.md" -File).Count
        Write-Success "Parameters directory exists ($paramCount files)"
    }

    if (-not (Test-Path $examplePromptsDir)) {
        Write-Warning "Example prompts directory not found: $examplePromptsDir"
    }
    else {
        $promptCount = (Get-ChildItem $examplePromptsDir -Filter "*.md" -File).Count
        Write-Success "Example prompts directory exists ($promptCount files)"
    }
}

if ($hasErrors) {
    Write-Error "Prerequisites validation failed. Exiting."
    Stop-Transcript
    exit 1
}

# Phase 1: RawToolGenerator
if (-not $SkipRaw) {
    Write-Section "Phase 1: Generating Raw Tool Files"
    
    Write-Progress "Running RawToolGenerator..."
    Push-Location $currentDir
    
    try {
        $rawArgs = @(
            "--project", "RawToolGenerator",
            "--", 
            $cliOutputFile,
            $rawToolsDir,
            $mcpCliVersion
        )
        
        Write-Info "Command: dotnet run $($rawArgs -join ' ')"
        dotnet run @rawArgs
        
        if ($LASTEXITCODE -ne 0) {
            throw "RawToolGenerator failed with exit code $LASTEXITCODE"
        }
        
        $rawCount = (Get-ChildItem $rawToolsDir -Filter "*.md" -File -ErrorAction SilentlyContinue).Count
        Write-Success "Phase 1 completed - Generated $rawCount raw tool files"
    }
    catch {
        Write-Error "Phase 1 failed: $_"
        Pop-Location
        Stop-Transcript
        exit 1
    }
    finally {
        Pop-Location
    }
}
else {
    Write-Warning "Skipping Phase 1 (RawToolGenerator) - using existing files"
}

# Phase 2: ComposedToolGenerator
if (-not $SkipComposed) {
    Write-Section "Phase 2: Composing Tool Files"
    
    Write-Progress "Running ComposedToolGenerator..."
    Push-Location $currentDir
    
    try {
        $composedArgs = @(
            "--project", "ComposedToolGenerator",
            "--",
            $rawToolsDir,
            $composedToolsDir,
            $annotationsDir,
            $parametersDir,
            $examplePromptsDir
        )
        
        Write-Info "Command: dotnet run $($composedArgs -join ' ')"
        dotnet run @composedArgs
        
        if ($LASTEXITCODE -ne 0) {
            throw "ComposedToolGenerator failed with exit code $LASTEXITCODE"
        }
        
        $composedCount = (Get-ChildItem $composedToolsDir -Filter "*.md" -File -ErrorAction SilentlyContinue).Count
        Write-Success "Phase 2 completed - Generated $composedCount composed tool files"
    }
    catch {
        Write-Error "Phase 2 failed: $_"
        Pop-Location
        Stop-Transcript
        exit 1
    }
    finally {
        Pop-Location
    }
}
else {
    Write-Warning "Skipping Phase 2 (ComposedToolGenerator) - using existing files"
}

# Phase 3: ImprovedToolGenerator
if (-not $SkipImproved) {
    Write-Section "Phase 3: Applying AI Improvements"
    
    # Check for Azure OpenAI credentials
    $hasCredentials = $false
    if ($env:FOUNDRY_API_KEY -and $env:FOUNDRY_ENDPOINT -and $env:FOUNDRY_MODEL_NAME) {
        $hasCredentials = $true
        Write-Success "Azure OpenAI credentials found in environment"
    }
    elseif (Test-Path ".env") {
        Write-Info "Azure OpenAI credentials will be loaded from .env file"
        $hasCredentials = $true
    }
    else {
        Write-Warning "Azure OpenAI credentials not found. Phase 3 will be skipped."
        Write-Info "To enable AI improvements, set these environment variables:"
        Write-Info "  - FOUNDRY_API_KEY"
        Write-Info "  - FOUNDRY_ENDPOINT"
        Write-Info "  - FOUNDRY_MODEL_NAME"
        $hasCredentials = $false
    }
    
    if ($hasCredentials) {
        Write-Progress "Running ImprovedToolGenerator..."
        Push-Location $currentDir
        
        try {
            $improvedArgs = @(
                "--project", "ImprovedToolGenerator",
                "--",
                $composedToolsDir,
                $improvedToolsDir,
                $MaxTokens
            )
            
            Write-Info "Command: dotnet run $($improvedArgs -join ' ')"
            dotnet run @improvedArgs
            
            if ($LASTEXITCODE -ne 0) {
                throw "ImprovedToolGenerator failed with exit code $LASTEXITCODE"
            }
            
            $improvedCount = (Get-ChildItem $improvedToolsDir -Filter "*.md" -File -ErrorAction SilentlyContinue).Count
            Write-Success "Phase 3 completed - Generated $improvedCount improved tool files"
        }
        catch {
            Write-Error "Phase 3 failed: $_"
            Pop-Location
            Stop-Transcript
            exit 1
        }
        finally {
            Pop-Location
        }
    }
}
else {
    Write-Warning "Skipping Phase 3 (ImprovedToolGenerator) - AI improvements disabled"
}

# Summary
Write-Section "Generation Summary"

$summary = @()
$summary += "Raw tool files:      $(if (Test-Path $rawToolsDir) { (Get-ChildItem $rawToolsDir -Filter '*.md' -File).Count } else { 0 })"
$summary += "Composed tool files: $(if (Test-Path $composedToolsDir) { (Get-ChildItem $composedToolsDir -Filter '*.md' -File).Count } else { 0 })"
$summary += "Improved tool files: $(if (Test-Path $improvedToolsDir) { (Get-ChildItem $improvedToolsDir -Filter '*.md' -File).Count } else { 0 })"

foreach ($line in $summary) {
    Write-Host "  $line" -ForegroundColor White
}

Write-Host ""
Write-Success "All phases completed successfully!"
Write-Info "Log file: $logFile"
Write-Host ""

Stop-Transcript
