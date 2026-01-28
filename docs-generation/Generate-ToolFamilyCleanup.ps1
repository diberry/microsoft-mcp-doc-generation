#!/usr/bin/env pwsh
# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

<#
.SYNOPSIS
    Runs the Tool Family Cleanup generator to apply Microsoft style guide standards to tool family documentation.

.DESCRIPTION
    This script builds and runs the ToolFamilyCleanup project to process tool family markdown files
    and apply LLM-based cleanup with Microsoft style guide standards.

.PARAMETER InputDir
    Input directory containing tool family markdown files. Default: ./generated/multi-page

.PARAMETER PromptsDir
    Directory to save generated prompts. Default: ./generated/tool-family-cleanup-prompts

.PARAMETER OutputDir
    Directory to save cleaned markdown files. Default: ./generated/tool-family-cleanup

.EXAMPLE
    .\Generate-ToolFamilyCleanup.ps1
    
    Runs with default paths

.EXAMPLE
    .\Generate-ToolFamilyCleanup.ps1 -InputDir "./generated/multi-page" -OutputDir "./generated/cleaned"
    
    Runs with custom output directory
#>

param(
    [string]$InputDir = "",
    [string]$PromptsDir = "",
    [string]$OutputDir = ""
)

$ErrorActionPreference = "Stop"

Write-Host "Azure MCP Tool Family Cleanup Generator" -ForegroundColor Cyan
Write-Host "=======================================" -ForegroundColor Cyan
Write-Host ""

try {
    # Get the script's directory (docs-generation folder)
    $scriptDir = $PSScriptRoot

    # Set default values if not provided
    if (-not $InputDir) { $InputDir = "../generated/tool-family" }
    if (-not $PromptsDir) { $PromptsDir = "../generated/tool-family-cleanup-prompts" }
    if (-not $OutputDir) { $OutputDir = "../generated/tool-family-cleaned" }

    # Convert to absolute paths relative to script directory
    $InputDir = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($scriptDir, $InputDir))
    $PromptsDir = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($scriptDir, $PromptsDir))
    $OutputDir = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($scriptDir, $OutputDir))

    # Display configuration
    Write-Host "Configuration:" -ForegroundColor Yellow
    Write-Host "  Input Dir:   $InputDir"
    Write-Host "  Prompts Dir: $PromptsDir"
    Write-Host "  Output Dir:  $OutputDir"
    Write-Host ""

    # Create output directories
    Write-Host "Creating output directories..." -ForegroundColor Yellow
    $directoriesToCreate = @($PromptsDir, $OutputDir)
    foreach ($dir in $directoriesToCreate) {
        if (-not (Test-Path $dir)) {
            New-Item -ItemType Directory -Path $dir -Force | Out-Null
            Write-Host "  ✓ Created: $dir"
        } else {
            Write-Host "  ✓ Exists: $dir"
        }
    }
    Write-Host ""

    # Navigate to project directory
    $projectDir = Join-Path $PSScriptRoot "ToolFamilyCleanup"
    
    if (-not (Test-Path $projectDir)) {
        Write-Error "ToolFamilyCleanup project directory not found: $projectDir"
        exit 1
    }

    Push-Location $projectDir

    try {
        # Build the project
        Write-Host "Building ToolFamilyCleanup..." -ForegroundColor Yellow
        dotnet build --configuration Release
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Build failed with exit code $LASTEXITCODE"
            exit $LASTEXITCODE
        }
        Write-Host "✓ Build successful" -ForegroundColor Green
        Write-Host ""

        # Build arguments
        $runArgs = @()
        if ($InputDir) {
            $runArgs += "--input-dir"
            $runArgs += $InputDir
        }
        if ($PromptsDir) {
            $runArgs += "--prompts-dir"
            $runArgs += $PromptsDir
        }
        if ($OutputDir) {
            $runArgs += "--output-dir"
            $runArgs += $OutputDir
        }

        # Run the tool
        Write-Host "Running Tool Family Cleanup..." -ForegroundColor Yellow
        if ($runArgs.Count -gt 0) {
            dotnet run --configuration Release --no-build -- $runArgs
        } else {
            dotnet run --configuration Release --no-build
        }

        if ($LASTEXITCODE -ne 0) {
            Write-Error "Tool Family Cleanup failed with exit code $LASTEXITCODE"
            exit $LASTEXITCODE
        }

        Write-Host ""
        Write-Host "✓ Tool Family Cleanup completed successfully" -ForegroundColor Green
    }
    finally {
        Pop-Location
    }
}
catch {
    Write-Error "An error occurred: $_"
    exit 1
}
