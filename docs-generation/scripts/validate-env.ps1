#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Validates .env file configuration for AI generation

.DESCRIPTION
    Checks that the .env file exists in docs-generation directory and contains
    all required Azure OpenAI credentials for AI-based generation steps (2, 3, 4, 5).
    
    Required variables:
    - FOUNDRY_API_KEY: Azure OpenAI API key
    - FOUNDRY_ENDPOINT: Azure OpenAI endpoint URL
    - FOUNDRY_MODEL_NAME: Azure OpenAI model deployment name
    
    Optional but recommended:
    - FOUNDRY_MODEL_API_VERSION: API version
    - TOOL_FAMILY_CLEANUP_FOUNDRY_MODEL_NAME: Model for cleanup step

.PARAMETER DocsGenDir
    Path to the docs-generation directory (default: ../.. from script location)

.PARAMETER WarnOnly
    If set, emit warnings instead of errors for missing/empty values

.EXAMPLE
    # From repository root
    pwsh ./docs-generation/scripts/validate-env.ps1

.EXAMPLE
    # Warn only (don't fail)
    pwsh ./docs-generation/scripts/validate-env.ps1 -WarnOnly
#>

param(
    [string]$DocsGenDir = "",
    [switch]$WarnOnly = $false
)

$ErrorActionPreference = "Stop"

# Calculate paths
if ([string]::IsNullOrWhiteSpace($DocsGenDir)) {
    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
    $DocsGenDir = Resolve-Path (Join-Path $scriptDir "..")
}

$envFile = Join-Path $DocsGenDir ".env"
$sampleEnvFile = Join-Path $DocsGenDir "sample.env"
$envIssues = @()

Write-Host "Validating .env configuration..." -ForegroundColor Yellow

# Check if .env file exists
if (-not (Test-Path $envFile)) {
    $envIssues += "❌ .env file not found at: $envFile"
    $envIssues += "   Copy sample.env to .env and configure your values:"
    $envIssues += "   cp $sampleEnvFile $envFile"
} else {
    # Load .env file
    $envContent = Get-Content $envFile -Raw
    
    # Define required variables
    $requiredVars = @{
        'FOUNDRY_API_KEY' = 'Azure OpenAI API key (required for AI steps 2, 3, 4, 5)'
        'FOUNDRY_ENDPOINT' = 'Azure OpenAI endpoint URL'
        'FOUNDRY_MODEL_NAME' = 'Azure OpenAI model deployment name'
    }
    
    # Define optional variables
    $optionalVars = @{
        'FOUNDRY_MODEL_API_VERSION' = 'Azure OpenAI API version (recommended)'
        'TOOL_FAMILY_CLEANUP_FOUNDRY_MODEL_NAME' = 'Model for tool cleanup step'
        'TOOL_FAMILY_CLEANUP_FOUNDRY_MODEL_API_VERSION' = 'API version for cleanup model'
    }
    
    # Check required variables
    foreach ($varName in $requiredVars.Keys) {
        $found = $false
        $isEmpty = $true
        
        # Try matching with quotes
        if ($envContent -match "$varName=`"(.+)`"") {
            $found = $true
            $value = $matches[1]
            if (-not [string]::IsNullOrWhiteSpace($value)) {
                $isEmpty = $false
            }
        }
        # Try matching without quotes
        elseif ($envContent -match "$varName=(.+)") {
            $found = $true
            $value = $matches[1].Trim()
            if (-not [string]::IsNullOrWhiteSpace($value)) {
                $isEmpty = $false
            }
        }
        
        if (-not $found) {
            $envIssues += "❌ $varName not found in .env - $($requiredVars[$varName])"
        } elseif ($isEmpty) {
            $envIssues += "❌ $varName is empty - $($requiredVars[$varName])"
        }
    }
    
    # Check optional variables (warnings only)
    foreach ($varName in $optionalVars.Keys) {
        if ($envContent -notmatch "$varName=") {
            Write-Host "⚠ WARNING: $varName not found - $($optionalVars[$varName])" -ForegroundColor Yellow
        }
    }
}

# Report results
if ($envIssues.Count -gt 0) {
    Write-Host ""
    if ($WarnOnly) {
        Write-Host "⚠ CONFIGURATION WARNINGS:" -ForegroundColor Yellow
        foreach ($issue in $envIssues) {
            Write-Host "   $issue" -ForegroundColor Yellow
        }
        Write-Host ""
        Write-Host "   AI generation steps (2, 3, 4, 5) require Azure OpenAI credentials." -ForegroundColor Yellow
        Write-Host "   You can still run Step 1 (non-AI) if you only need basic generation." -ForegroundColor Yellow
        Write-Host ""
    } else {
        Write-Host "⛔ CONFIGURATION ISSUES DETECTED:" -ForegroundColor Red
        foreach ($issue in $envIssues) {
            Write-Host "   $issue" -ForegroundColor Red
        }
        Write-Host ""
        Write-Host "   AI generation steps (2, 3, 4, 5) require Azure OpenAI credentials." -ForegroundColor Yellow
        Write-Host "   You can still run Step 1 (non-AI) if you only need basic generation." -ForegroundColor Yellow
        Write-Host ""
        Write-Host "⛔ VALIDATION FAILED: Fix .env configuration and re-run." -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "✓ .env configuration validated" -ForegroundColor Green
    exit 0
}
