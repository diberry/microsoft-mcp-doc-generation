#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Generates a GitHub Copilot skills relevance report for a single Azure service.

.DESCRIPTION
    Fetches GitHub Copilot skill files from three public repositories, analyzes
    their relevance to the specified Azure service/MCP namespace, and generates
    a markdown report in the skills-relevance/ output directory.

    Skill sources:
    - github/awesome-copilot         (skills/)
    - microsoft/skills               (root)
    - microsoft/GitHub-Copilot-for-Azure  (plugin/skills/)

.PARAMETER ServiceArea
    The service area/family to generate a skills report for (e.g., "keyvault", "storage", "aks")
    This is the tool family/namespace name, not a specific tool command.

.PARAMETER OutputPath
    Path to the generated directory (default: ../../generated from scripts/)

.PARAMETER MinScore
    Minimum relevance score (0.0–1.0) for including a skill (default: 0.1)

.PARAMETER SkipBuild
    Skip building the .NET solution (already built by preflight)

.EXAMPLE
    ./5-Generate-SkillsRelevance-One.ps1 -ServiceArea "keyvault"
    ./5-Generate-SkillsRelevance-One.ps1 -ServiceArea "aks" -OutputPath ../generated
    ./5-Generate-SkillsRelevance-One.ps1 -ServiceArea "storage" -MinScore 0.2
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$ServiceArea,

    [string]$OutputPath = "../../generated",

    [double]$MinScore = 0.1,

    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

# Import shared logging and normalization helpers
. "$PSScriptRoot\Shared-Functions.ps1"

try {
    Write-Divider
    Write-Progress "Skills Relevance Report Generation"
    Write-Info "Service: $ServiceArea"
    Write-Info "Started: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    Write-Divider
    Write-Host ""

    $scriptDir = $PSScriptRoot
    $docsGenDir = Split-Path -Parent $scriptDir
    $outputDir = Resolve-OutputDir $OutputPath
    $skillsOutputDir = Join-Path $outputDir "skills-relevance"

    Write-Info "Output directory: $skillsOutputDir"
    Write-Host ""

    # Build .NET packages (skip if already built by preflight)
    Invoke-DotnetBuild -SkipBuild:$SkipBuild

    # Warn if GITHUB_TOKEN is not set (unauthenticated requests are rate-limited to 60/hr)
    $githubToken = $env:GITHUB_TOKEN
    if (-not $githubToken) {
        Write-Warning "GITHUB_TOKEN not set. Unauthenticated GitHub API rate limits (60 req/hr) apply."
        Write-Warning "Set GITHUB_TOKEN to a personal access token for higher rate limits."
    } else {
        Write-Info "✓ GITHUB_TOKEN set"
    }
    Write-Host ""

    # Run SkillsRelevance generator
    Write-Divider
    Write-Progress "Fetching and analyzing GitHub Copilot skills..."
    Write-Divider
    Write-Host ""

    $skillsProject = Join-Path $docsGenDir "SkillsRelevance"
    $noBuildArg = if ($SkipBuild) { "--no-build" } else { "" }

    Push-Location $docsGenDir
    try {
        & dotnet run --project $skillsProject --configuration Release $noBuildArg -- `
            $ServiceArea `
            --output-path $skillsOutputDir `
            --min-score $MinScore
        $exitCode = $LASTEXITCODE
    } finally {
        Pop-Location
    }

    Write-Host ""
    Write-Divider

    if ($exitCode -ne 0) {
        # Non-fatal: skills relevance is supplementary, not required for the main pipeline
        Write-Warning "Skills relevance generation reported issues (exit code: $exitCode). Continuing pipeline..."
    } else {
        # Show generated files
        Write-Progress "Generated Files:"
        Write-Host ""

        $reportFile = Join-Path $skillsOutputDir "$($ServiceArea)-skills-relevance.md"
        if (Test-Path $reportFile) {
            $lineCount = (Get-Content $reportFile | Measure-Object -Line).Lines
            $sizeKB = [math]::Round((Get-Item $reportFile).Length / 1KB, 1)
            Write-Success "✓ Skills report: $reportFile"
            Write-Info "  Lines: $lineCount"
            Write-Info "  Size: ${sizeKB}KB"
        } else {
            Write-Warning "  Skills relevance report not found: $reportFile"
        }

        $indexFile = Join-Path $skillsOutputDir "index.md"
        if (Test-Path $indexFile) {
            Write-Success "✓ Index: $indexFile"
        }
    }

    Write-Host ""
    Write-Divider
    Write-Success "Skills relevance step completed"
    Write-Info "Completed: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    Write-Divider
    Write-Host ""

} catch {
    Write-Host ""
    Write-Divider
    # Non-fatal: log the error and return cleanly so the pipeline continues to Step 6
    Write-Warning "Skills relevance generation failed: $($_.Exception.Message)"
    Write-Warning $_.ScriptStackTrace
    Write-Divider
    Write-Host ""
    # Exit 0 to avoid halting the main pipeline (Step 6 horizontal articles must still run)
    exit 0
}
