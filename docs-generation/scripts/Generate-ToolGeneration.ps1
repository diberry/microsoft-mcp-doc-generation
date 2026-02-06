#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Tool generation orchestrator - runs all tool generation phases
    
.DESCRIPTION
    This script orchestrates the complete tool generation pipeline:
    1. Generate-CompleteTools - Creates complete tool documentation files
    2. Generate-ToolGenerationAndAIImprovements - Runs 3-phase tool generation with AI improvements
    3. GenerateToolFamilyCleanup - Assembles tool family files with AI-generated metadata
    
    Prerequisites:
    - Base documentation must be generated first (annotations, parameters, example prompts)
    - CLI output must exist (./generated/cli/cli-output.json)
    
.PARAMETER OutputPath
    Base output directory (default: ../generated)
    
.PARAMETER SkipCompleteTools
    Skip complete tools generation
    
.PARAMETER SkipToolGeneration
    Skip tool generation and AI improvements
    
.PARAMETER SkipToolFamily
    Skip tool family cleanup and assembly
    
.PARAMETER MaxTokens
    Maximum tokens for AI improvements (default: 8000)

.EXAMPLE
    ./Generate-ToolGeneration.ps1
    ./Generate-ToolGeneration.ps1 -SkipToolGeneration
    ./Generate-ToolGeneration.ps1 -SkipToolFamily -MaxTokens 12000
#>

param(
    [string]$OutputPath = "../../generated",
    [switch]$SkipCompleteTools = $false,
    [switch]$SkipToolGeneration = $false,
    [switch]$SkipToolFamily = $false,
    [int]$MaxTokens = 8000
)

$ErrorActionPreference = "Stop"

# Helper functions
function Write-Info { param([string]$Message) Write-Host "INFO: $Message" -ForegroundColor Cyan }
function Write-Success { param([string]$Message) Write-Host "SUCCESS: $Message" -ForegroundColor Green }
function Write-Error { param([string]$Message) Write-Host "ERROR: $Message" -ForegroundColor Red }
function Write-Progress { param([string]$Message) Write-Host "PROGRESS: $Message" -ForegroundColor Magenta }

try {
    Write-Progress "Tool Generation Orchestration"
    Write-Info "Starting at: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    Write-Info ""
    
    # Resolve paths (relative to current working directory, not script location)
    $scriptDir = $PSScriptRoot
    $outputDir = if ([System.IO.Path]::IsPathRooted($OutputPath)) {
        $OutputPath
    } else {
        # Resolve relative to current working directory, then convert to absolute path
        $absPath = Join-Path (Get-Location) $OutputPath
        [System.IO.Path]::GetFullPath($absPath)
    }
    
    Write-Info "Output directory: $outputDir"
    
    # Check for CLI files (warn if missing, but allow scripts to run)
    $cliOutputFile = Join-Path $outputDir "cli" "cli-output.json"
    if (-not (Test-Path $cliOutputFile)) {
        Write-Warning "CLI output file not found: $cliOutputFile"
        Write-Warning "The tool generation scripts may still work if annotations and parameters exist"
        Write-Warning "For full pipeline with CLI data, run: pwsh ./Generate-MultiPageDocs.ps1"
    } else {
        Write-Success "✓ CLI files found"
    }
    Write-Info ""
    
    # Phase 1: Generate complete tools
    if (-not $SkipCompleteTools) {
        Write-Progress "Phase 1: Generating Complete Tools Documentation..."
        Write-Info ""
        & "$scriptDir\Generate-CompleteTools.ps1" -OutputPath $OutputPath
        if ($LASTEXITCODE -ne 0) {
            throw "Complete tools generation failed"
        }
        Write-Info ""
        Write-Success "✓ Phase 1 completed"
        Write-Info ""
    } else {
        Write-Info "⊘ Skipping Phase 1: Complete Tools"
        Write-Info ""
    }
    
    # Phase 2: Generate tool generation and AI improvements
    if (-not $SkipToolGeneration) {
        Write-Progress "Phase 2: Generating Tool Generation and AI Improvements..."
        Write-Info ""
        & "$scriptDir\Generate-ToolGenerationAndAIImprovements.ps1" -OutputPath $OutputPath -MaxTokens $MaxTokens
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Tool generation and AI improvements failed or was skipped"
        }
        Write-Info ""
        Write-Success "✓ Phase 2 completed"
        Write-Info ""
    } else {
        Write-Info "⊘ Skipping Phase 2: Tool Generation and AI Improvements"
        Write-Info ""
    }
    
    # Phase 3: Generate tool family cleanup
    if (-not $SkipToolFamily) {
        Write-Progress "Phase 3: Generating Tool Family Cleanup and Assembly..."
        Write-Info ""
        & "$scriptDir\GenerateToolFamilyCleanup-multifile.ps1" -OutputPath $OutputPath
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Tool family cleanup failed or was skipped"
        }
        Write-Info ""
        Write-Success "✓ Phase 3 completed"
        Write-Info ""
    } else {
        Write-Info "⊘ Skipping Phase 3: Tool Family Cleanup"
        Write-Info ""
    }
    
    Write-Success "Tool generation orchestration completed"
    Write-Info ""
    Write-Success "Completed at: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    
} catch {
    Write-Error "Tool generation orchestration failed: $_"
    exit 1
}
