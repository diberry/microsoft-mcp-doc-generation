#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Final validation orchestrator - verifies documentation quantity and completeness
    
.DESCRIPTION
    This script performs final validation checks on generated documentation:
    - Verify Quantity: Checks that all expected files were generated
    
    NOTE: Example prompt validation is performed during Step 3 (3-Generate-ExamplePrompts.ps1)
    
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

# Get the repo root
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

# Helper functions for colored output
function Write-Info { param([string]$Message) Write-Host "INFO: $Message" -ForegroundColor Cyan }
function Write-Success { param([string]$Message) Write-Host "SUCCESS: $Message" -ForegroundColor Green }
function Write-Warning { param([string]$Message) Write-Host "WARNING: $Message" -ForegroundColor Yellow }
function Write-Error { param([string]$Message) Write-Host "ERROR: $Message" -ForegroundColor Red }
function Write-Progress { param([string]$Message) Write-Host "PROGRESS: $Message" -ForegroundColor Magenta }

try {
    Write-Progress "Starting Final Validation"
    Write-Info "Starting at: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    Write-Info ""
    
    # Track validation results
    $validationResults = @{
        quantityCheck = $false
        errors = @()
    }
    
    Write-Info "NOTE: Example prompt validation is performed during Step 3 (3-Generate-ExamplePrompts.ps1)"
    Write-Info ""
    
    # Step 1: Run Verify Quantity Tool
    Write-Progress "Step 1: Verifying Documentation Quantity..."
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
    Write-Info "  • Quantity Verification: $(if ($validationResults.quantityCheck) { 'PASS' } else { 'SKIP/FAIL' })"
    
    if ($validationResults.errors.Count -gt 0) {
        Write-Info ""
        Write-Info "Validation Errors/Warnings:"
        foreach ($validationError in $validationResults.errors) {
            Write-Warning "  • $validationError"
        }
    }
    
    Write-Info ""
    Write-Success "Final validation completed at: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    Write-Info ""
    
    # Return validation results for caller to handle
    if ($validationResults.errors.Count -gt 0) {
        exit 1
    }
    
} catch {
    Write-Error "Validation failed: $($_.Exception.Message)"
    Write-Error "Error details: $($_.ScriptStackTrace)"
    exit 1
}

    
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
        foreach ($validationError in $validationResults.errors) {
            Write-Warning "  • $validationError"
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
