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
