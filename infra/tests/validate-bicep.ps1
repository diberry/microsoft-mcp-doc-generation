#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Validates all Bicep files compile without errors.
.DESCRIPTION
    Runs 'az bicep build' on each .bicep file under infra/ to catch syntax
    and reference errors before deployment.
#>

$ErrorActionPreference = 'Stop'
$infraDir = Split-Path -Parent $PSScriptRoot
$bicepFiles = Get-ChildItem -Path $infraDir -Filter '*.bicep' -Recurse | Where-Object { $_.Name -notlike '*.test.bicep' }

$failed = @()
$passed = 0

foreach ($file in $bicepFiles) {
    Write-Host "Validating: $($file.FullName)" -ForegroundColor Cyan
    try {
        $output = az bicep build --file $file.FullName 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Host "  FAILED" -ForegroundColor Red
            Write-Host "  $output"
            $failed += $file.FullName
        } else {
            Write-Host "  OK" -ForegroundColor Green
            $passed++
            # Clean up generated ARM template
            $armFile = [System.IO.Path]::ChangeExtension($file.FullName, '.json')
            if (Test-Path $armFile) { Remove-Item $armFile -Force }
        }
    } catch {
        Write-Host "  ERROR: $_" -ForegroundColor Red
        $failed += $file.FullName
    }
}

Write-Host "`n--- Results ---"
Write-Host "Passed: $passed" -ForegroundColor Green
if ($failed.Count -gt 0) {
    Write-Host "Failed: $($failed.Count)" -ForegroundColor Red
    $failed | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
    exit 1
}
Write-Host "All Bicep files validated successfully." -ForegroundColor Green
