# check-coverage.ps1 — Verify test coverage meets minimum threshold
# Usage: .\skills-generation\scripts\check-coverage.ps1 [-MinLineCoverage 80] [-MinBranchCoverage 70]
param(
    [int]$MinLineCoverage = 80,
    [int]$MinBranchCoverage = 70,
    [string]$ResultsDir = "./TestResults"
)

$ErrorActionPreference = "Stop"

# Find latest coverage file
$coverageFile = Get-ChildItem $ResultsDir -Recurse -Filter "coverage.cobertura.xml" | 
    Sort-Object LastWriteTime -Descending | 
    Select-Object -First 1

if (-not $coverageFile) {
    Write-Error "❌ No coverage file found in $ResultsDir. Run tests with --collect:'XPlat Code Coverage' first."
    exit 1
}

[xml]$xml = Get-Content $coverageFile.FullName
$lineRate = [math]::Round([double]$xml.coverage.'line-rate' * 100, 1)
$branchRate = [math]::Round([double]$xml.coverage.'branch-rate' * 100, 1)

Write-Host "Coverage: Line=$lineRate% (min: $MinLineCoverage%) | Branch=$branchRate% (min: $MinBranchCoverage%)"

$passed = $true
if ($lineRate -lt $MinLineCoverage) {
    Write-Host "❌ Line coverage $lineRate% is below minimum $MinLineCoverage%"
    $passed = $false
}
if ($branchRate -lt $MinBranchCoverage) {
    Write-Host "❌ Branch coverage $branchRate% is below minimum $MinBranchCoverage%"
    $passed = $false
}

if ($passed) {
    Write-Host "✅ Coverage thresholds met"
    exit 0
} else {
    exit 1
}
