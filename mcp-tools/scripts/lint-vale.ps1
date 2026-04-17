# lint-vale.ps1 — Run Vale prose linter on generated mcp-tools output
# Usage: .\mcp-tools\scripts\lint-vale.ps1 [-TargetDir ./generated/multi-page/]
param(
    [string]$TargetDir = ""
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$mcpToolsDir = Split-Path -Parent $scriptDir
$repoRoot = Split-Path -Parent $mcpToolsDir

# Default target: scan all generated-* directories and generated/multi-page
if (-not $TargetDir) {
    $targets = @()
    $multiPage = Join-Path $repoRoot "generated" "multi-page"
    if (Test-Path $multiPage) { $targets += $multiPage }
    Get-ChildItem -Path $repoRoot -Directory -Filter "generated-*" | ForEach-Object {
        $targets += $_.FullName
    }
    if ($targets.Count -eq 0) {
        Write-Host "⚠️ No generated output directories found. Run the pipeline first."
        exit 0
    }
} else {
    $targets = @($TargetDir)
}

# Find Vale binary
$valeExe = $null
if (Get-Command vale -ErrorAction SilentlyContinue) {
    $valeExe = "vale"
} elseif (Test-Path "$mcpToolsDir/tools/vale.exe") {
    $valeExe = "$mcpToolsDir/tools/vale.exe"
} elseif (Test-Path "$mcpToolsDir/tools/vale") {
    $valeExe = "$mcpToolsDir/tools/vale"
} elseif (Test-Path (Join-Path $repoRoot "vale_bin" "vale.exe")) {
    $valeExe = Join-Path $repoRoot "vale_bin" "vale.exe"
} else {
    Write-Error "❌ Vale not found. Install: https://vale.sh/docs/install/"
    exit 1
}

$valeConfig = Join-Path $mcpToolsDir ".vale.ini"
$overallExit = 0

foreach ($target in $targets) {
    Write-Host "Running Vale lint on $target..."
    & $valeExe --config $valeConfig $target
    if ($LASTEXITCODE -gt $overallExit) {
        $overallExit = $LASTEXITCODE
    }
}

if ($overallExit -eq 0) {
    Write-Host "✅ Vale: all checks passed"
} else {
    Write-Host "⚠️ Vale: issues found (exit code $overallExit)"
}

exit $overallExit
