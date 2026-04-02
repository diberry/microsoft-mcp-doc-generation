# lint-vale.ps1 — Run Vale prose linter on generated skill pages
# Usage: .\skills-generation\scripts\lint-vale.ps1 [-TargetDir ./generated-skills/]
param(
    [string]$TargetDir = "./generated-skills/"
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$skillsDir = Split-Path -Parent $scriptDir

# Find Vale binary
$valeExe = "vale"
if (Get-Command $valeExe -ErrorAction SilentlyContinue) {
    # System vale
} elseif (Test-Path "$skillsDir/tools/vale.exe") {
    $valeExe = "$skillsDir/tools/vale.exe"
} else {
    Write-Error "❌ Vale not found. Install: https://vale.sh/docs/install/"
    exit 1
}

Write-Host "Running Vale lint on $TargetDir..."
& $valeExe --config "$skillsDir/.vale.ini" $TargetDir
$exitCode = $LASTEXITCODE

if ($exitCode -eq 0) {
    Write-Host "✅ Vale: all checks passed"
} else {
    Write-Host "⚠️ Vale: issues found (exit code $exitCode)"
}

exit $exitCode
