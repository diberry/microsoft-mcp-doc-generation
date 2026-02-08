#!/usr/bin/env pwsh
<#!
.SYNOPSIS
    Fast validation for required parameters in example prompts

.DESCRIPTION
    Validates that each example prompt file contains all required parameters
    from CLI output using lightweight regex matching (no LLM).

.PARAMETER OutputPath
    Path to the generated directory (default: ../generated from script location)

.PARAMETER CliOutputFile
    Optional CLI output JSON path (default: <OutputPath>/cli/cli-output.json)

.PARAMETER ExamplePromptsDir
    Optional example prompts directory (default: <OutputPath>/example-prompts)

.PARAMETER MaxMissingDetails
    Max number of missing parameter lines to print (default: 50)

.EXAMPLE
    ./Validate-ExamplePrompts-RequiredParams.ps1
    ./Validate-ExamplePrompts-RequiredParams.ps1 -OutputPath ../generated
#>

param(
    [string]$OutputPath = "../generated",
    [string]$CliOutputFile = "",
    [string]$ExamplePromptsDir = "",
    [int]$MaxMissingDetails = 50,
    [string]$ToolCommand = ""
)

$ErrorActionPreference = "Stop"

function Write-Info { param([string]$Message) Write-Host "INFO: $Message" -ForegroundColor Cyan }
function Write-Success { param([string]$Message) Write-Host "SUCCESS: $Message" -ForegroundColor Green }
function Write-Warning { param([string]$Message) Write-Host "WARNING: $Message" -ForegroundColor Yellow }
function Write-Error { param([string]$Message) Write-Host "ERROR: $Message" -ForegroundColor Red }

function Normalize-Command {
    param([string]$Command)

    if ([string]::IsNullOrWhiteSpace($Command)) {
        return $null
    }

    return ($Command -replace "\s+", " ").Trim().ToLowerInvariant()
}

function Get-CommandFromFile {
    param([string]$FilePath)

    try {
        $lines = Get-Content -Path $FilePath -TotalCount 50 -ErrorAction Stop
    } catch {
        return $null
    }

    foreach ($line in $lines) {
        if ($line -match "^#\s*azmcp\s+(.+)$") {
            return $matches[1].Trim()
        }

        if ($line -match "^\s*<!--\s*@mcpcli\s+(.+?)\s*-->") {
            return $matches[1].Trim()
        }
    }

    return $null
}

<# COMMENTED OUT: Using ExamplePromptValidator .NET tool instead of regex
function Build-ParamRegex {
    param([string]$ParamName)

    $clean = ($ParamName -replace "^--", "").Trim().ToLowerInvariant()
    if ([string]::IsNullOrWhiteSpace($clean)) {
        return $null
    }

    # Split on hyphens and underscores to get component words
    $tokens = $clean -split "[-_]+" | Where-Object { $_ -ne "" }
    if ($tokens.Count -eq 0) {
        return $null
    }

    # For single-word parameters, match just the word
    if ($tokens.Count -eq 1) {
        $escapedToken = [regex]::Escape($tokens[0])
        # Match: --param, -param, or as standalone word
        $pattern = "(?i)(?:--?$escapedToken\b|\\b$escapedToken(?:\\s+|'|\"|$))"
        return [regex]::new($pattern, [System.Text.RegularExpressions.RegexOptions]::Compiled)
    }

    # For multi-word parameters like "resource-group", "database-type", etc.
    # Match: --resource-group, resource group, resourcegroup, etc.
    $escapedTokens = $tokens | ForEach-Object { [regex]::Escape($_) }
    
    # Build patterns:
    # 1. --resource-group (with dashes)
    # 2. resource group (with spaces)
    # 3. resourcegroup (concatenated)
    $hyphenated = $escapedTokens -join "-"
    $spaced = $escapedTokens -join "\\s+"
    $concatenated = $escapedTokens -join ""
    
    $pattern = "(?i)(?:--$hyphenated\b|$spaced(?:\\s+|'|\"|$)|$concatenated)"
    return [regex]::new($pattern, [System.Text.RegularExpressions.RegexOptions]::Compiled)
}
#>

try {
    $scriptDir = $PSScriptRoot
    $outputDir = if ([System.IO.Path]::IsPathRooted($OutputPath)) {
        $OutputPath
    } else {
        $absPath = Join-Path (Get-Location) $OutputPath
        [System.IO.Path]::GetFullPath($absPath)
    }

    if ([string]::IsNullOrWhiteSpace($CliOutputFile)) {
        $CliOutputFile = Join-Path $outputDir "cli/cli-output.json"
    }

    if ([string]::IsNullOrWhiteSpace($ExamplePromptsDir)) {
        $ExamplePromptsDir = Join-Path $outputDir "example-prompts"
    }

    Write-Info "Output directory: $outputDir"
    Write-Info "CLI output: $CliOutputFile"
    Write-Info "Example prompts: $ExamplePromptsDir"
    if (-not [string]::IsNullOrWhiteSpace($ToolCommand)) {
        Write-Info "Filtering to tool: $ToolCommand"
    }

    if (-not (Test-Path $CliOutputFile)) {
        throw "CLI output file not found: $CliOutputFile"
    }

    if (-not (Test-Path $ExamplePromptsDir)) {
        throw "Example prompts directory not found: $ExamplePromptsDir"
    }

    # Build the ExamplePromptValidator project
    Write-Info ""
    Write-Info "Building ExamplePromptValidator..."
    $scriptDir = $PSScriptRoot
    $validatorProject = Split-Path -Parent $scriptDir
    
    if (-not (Test-Path $validatorProject)) {
        throw "ExamplePromptValidator project not found at: $validatorProject"
    }

    # Stream output in real-time (don't capture/buffer)
    & dotnet build $validatorProject --configuration Release
    if ($LASTEXITCODE -ne 0) {
        throw "ExamplePromptValidator build failed"
    }
    Write-Success "✓ ExamplePromptValidator built successfully"
    Write-Info ""

    # Use ExamplePromptValidator .NET tool for validation instead of regex
    Write-Info "Running ExamplePromptValidator..."
    Write-Info ""
    
    # Build arguments for named parameters
    $validatorArgs = @(
        "--generated", $outputDir
        "--example-prompts-dir", $ExamplePromptsDir
    )
    
    if (-not [string]::IsNullOrWhiteSpace($ToolCommand)) {
        $validatorArgs += "--tool-command"
        $validatorArgs += $ToolCommand
    }
    
    # Stream validator output in real-time (don't capture/buffer)
    & dotnet run --project $validatorProject --configuration Release -- @validatorArgs
    
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0 -and $exitCode -ne 1) {
        throw "ExamplePromptValidator failed with exit code: $exitCode"
    }

    # Return the same exit code as the validator
    exit $exitCode
}
catch {
    Write-Error "Validation failed: $($_.Exception.Message)"
    exit 1
}
