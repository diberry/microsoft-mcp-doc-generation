<#
.SYNOPSIS
    Verifies that all skills in the inventory have published Learn pages.
.DESCRIPTION
    Reads skills-inventory.json, constructs the Learn URL for each skill
    using slug (or name as fallback), makes an HTTP HEAD request, and
    reports pass/fail status.
.PARAMETER InventoryPath
    Path to skills-inventory.json. Defaults to ../data/skills-inventory.json
    relative to this script.
#>
param(
    [string]$InventoryPath
)

$ErrorActionPreference = 'Stop'
$scriptDir = $PSScriptRoot
$docsGenDir = Split-Path -Parent $scriptDir

if (-not $InventoryPath) {
    $InventoryPath = Join-Path $docsGenDir "data/skills-inventory.json"
}

if (-not (Test-Path $InventoryPath)) {
    Write-Error "Inventory file not found: $InventoryPath"
    exit 1
}

$baseUrl = "https://learn.microsoft.com/en-us/azure/developer/azure-skills/skills"

$inventory = Get-Content -Raw $InventoryPath | ConvertFrom-Json
$skills = $inventory.skills

$totalCount = $skills.Count
$passCount = 0
$failCount = 0
$results = @()

Write-Host ""
Write-Host "Verifying $totalCount skills against published Learn pages..." -ForegroundColor Cyan
Write-Host ("-" * 90)
Write-Host ("{0,-35} {1,-50} {2,-6} {3}" -f "Skill", "URL Slug", "Status", "Result")
Write-Host ("-" * 90)

foreach ($skill in $skills) {
    $name = $skill.name
    $slug = if ($skill.slug) { $skill.slug } else { $name }
    $url = "$baseUrl/$slug"

    try {
        $response = Invoke-WebRequest -Uri $url -Method Head -UseBasicParsing -ErrorAction Stop
        $statusCode = $response.StatusCode
    }
    catch {
        if ($_.Exception.Response) {
            $statusCode = [int]$_.Exception.Response.StatusCode
        }
        else {
            $statusCode = 0
        }
    }

    if ($statusCode -eq 200) {
        $result = "PASS"
        $color = "Green"
        $passCount++
    }
    else {
        $result = "FAIL"
        $color = "Red"
        $failCount++
    }

    Write-Host ("{0,-35} {1,-50} {2,-6} " -f $name, $slug, $statusCode) -NoNewline
    Write-Host $result -ForegroundColor $color

    $results += [PSCustomObject]@{
        Name       = $name
        Slug       = $slug
        URL        = $url
        StatusCode = $statusCode
        Result     = $result
    }
}

Write-Host ("-" * 90)
Write-Host ""
Write-Host "Summary" -ForegroundColor Cyan
Write-Host "  Total:  $totalCount"
Write-Host "  Passed: $passCount" -ForegroundColor Green
if ($failCount -gt 0) {
    Write-Host "  Failed: $failCount" -ForegroundColor Red
    Write-Host ""
    Write-Host "Failed skills:" -ForegroundColor Red
    foreach ($r in ($results | Where-Object { $_.Result -eq "FAIL" })) {
        Write-Host "  - $($r.Name) => $($r.URL) (HTTP $($r.StatusCode))" -ForegroundColor Red
    }
}
else {
    Write-Host "  Failed: 0" -ForegroundColor Green
}
Write-Host ""

if ($failCount -gt 0) {
    exit 1
}
exit 0
