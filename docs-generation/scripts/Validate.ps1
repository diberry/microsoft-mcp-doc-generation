#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Validation orchestrator - runs all validation checks on generated documentation
    
.DESCRIPTION
    This script orchestrates validation of generated documentation:
    1. ExamplePromptValidator - Validates example prompts include required parameters
    2. Verify Quantity - Checks that all expected files were generated
    
    Generates validation reports and summarizes results.
    
.PARAMETER OutputPath
    Path to the generated directory (default: ../../generated from script location)
    
.EXAMPLE
    ./Validate.ps1
    ./Validate.ps1 -OutputPath ../generated
#>

param(
    [string]$OutputPath = "../../generated"
)

# Resolve output directory
$generatedDir = if ([System.IO.Path]::IsPathRooted($OutputPath)) {
    $OutputPath
} else {
    $absPath = Join-Path (Get-Location) $OutputPath
    [System.IO.Path]::GetFullPath($absPath)
}

# Get the docs-generation directory (parent of scripts/)
$docsGenDir = Split-Path -Parent $PSScriptRoot
$repoRoot = Split-Path -Parent $docsGenDir

# Helper functions for colored output
function Write-Info { param([string]$Message) Write-Host "INFO: $Message" -ForegroundColor Cyan }
function Write-Success { param([string]$Message) Write-Host "SUCCESS: $Message" -ForegroundColor Green }
function Write-Warning { param([string]$Message) Write-Host "WARNING: $Message" -ForegroundColor Yellow }
function Write-Error { param([string]$Message) Write-Host "ERROR: $Message" -ForegroundColor Red }
function Write-Progress { param([string]$Message) Write-Host "PROGRESS: $Message" -ForegroundColor Magenta }

try {
    Write-Progress "Starting Validation Orchestration"
    Write-Info "Starting at: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    Write-Info ""
    
    # Track validation results
    $validationResults = @{
        examplePrompts = $false
        quantityCheck = $false
        errors = @()
    }
    
    # Step 1: Run Example Prompt Validator
    Write-Progress "Step 1: Validating Example Prompts..."
    Write-Info ""
    
    $examplePromptValidatorProject = Join-Path $docsGenDir "ExamplePromptValidator"
    if (Test-Path $examplePromptValidatorProject) {
        Write-Info "Running ExamplePromptValidator..."
        try {
            Push-Location $docsGenDir
            $validatorOutput = & dotnet run --project ExamplePromptValidator --configuration Release 2>&1
            Pop-Location
            
            # Write validator output
            $validatorOutput | ForEach-Object { Write-Host $_ }
            
            if ($LASTEXITCODE -eq 0) {
                Write-Success "✓ Example Prompt Validation completed successfully"
                $validationResults.examplePrompts = $true
            } else {
                Write-Warning "Example Prompt Validation reported issues (exit code: $LASTEXITCODE)"
                $validationResults.examplePrompts = $false
                $validationResults.errors += "ExamplePromptValidator failed with exit code $LASTEXITCODE"
            }
        } catch {
            Write-Warning "Example Prompt Validator failed: $($_.Exception.Message)"
            $validationResults.errors += "ExamplePromptValidator error: $($_.Exception.Message)"
        }
    } else {
        Write-Info "ExamplePromptValidator not found at: $examplePromptValidatorProject"
        Write-Info "Skipping example prompt validation"
    }
    Write-Info ""
    
    # Step 2: Run Verify Quantity Tool
    Write-Progress "Step 2: Verifying Documentation Quantity..."
    Write-Info ""
    
    $verifyQuantityProject = Join-Path $repoRoot "verify-quantity"
    if (Test-Path $verifyQuantityProject) {
        Write-Info "Running verify-quantity tool..."
        try {
            Push-Location $verifyQuantityProject
            
            # Check if node_modules exists, if not run npm install
            if (-not (Test-Path "node_modules")) {
                Write-Info "Installing npm dependencies..."
                & npm install 2>&1 | Out-Null
                if ($LASTEXITCODE -ne 0) {
                    throw "Failed to install npm dependencies"
                }
            }
            
            $quantityOutput = & node index.js 2>&1
            Pop-Location
            
            # Write quantity output
            $quantityOutput | ForEach-Object { Write-Host $_ }
            
            if ($LASTEXITCODE -eq 0) {
                Write-Success "✓ Documentation Quantity Verification completed successfully"
                $validationResults.quantityCheck = $true
                
                # Check if missing-tools.md was generated
                $missingToolsReport = Join-Path $repoRoot "missing-tools.md"
                if (Test-Path $missingToolsReport) {
                    $reportSize = [math]::Round((Get-Item $missingToolsReport).Length / 1KB, 1)
                    Write-Success "Missing tools report generated: $missingToolsReport (${reportSize}KB)"
                }
            } else {
                Write-Warning "Verify Quantity tool reported issues (exit code: $LASTEXITCODE)"
                $validationResults.quantityCheck = $false
                $validationResults.errors += "Verify Quantity tool failed with exit code $LASTEXITCODE"
            }
        } catch {
            Write-Warning "Verify Quantity tool failed: $($_.Exception.Message)"
            $validationResults.errors += "Verify Quantity tool error: $($_.Exception.Message)"
        }
    } else {
        Write-Info "Verify Quantity tool not found at: $verifyQuantityProject"
        Write-Info "Skipping quantity verification"
    }
    Write-Info ""
    
    # Summary
    Write-Progress "Validation Summary"
    Write-Info ""
    Write-Info "Validation Results:"
    Write-Info "  • Example Prompt Validation: $(if ($validationResults.examplePrompts) { 'PASS' } else { 'SKIP/FAIL' })"
    Write-Info "  • Quantity Verification: $(if ($validationResults.quantityCheck) { 'PASS' } else { 'SKIP/FAIL' })"
    
    if ($validationResults.errors.Count -gt 0) {
        Write-Info ""
        Write-Info "Validation Errors/Warnings:"
        foreach ($error in $validationResults.errors) {
            Write-Warning "  • $error"
        }
    }
    
    Write-Info ""
    Write-Success "Validation orchestration completed at: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    Write-Info ""
    
    # Return validation results for caller to handle
    if ($validationResults.errors.Count -gt 0) {
        exit 1
    }
    
} catch {
    Write-Error "Validation orchestration failed: $($_.Exception.Message)"
    Write-Error "Error details: $($_.ScriptStackTrace)"
    exit 1
}
