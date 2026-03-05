#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Generates related-skills documentation for a single Azure service namespace

.DESCRIPTION
    Generates a related-skills markdown page linking the specified MCP namespace
    to Agent Skills from the skills-source directory. Uses the RelatedSkillsGenerator
    .NET project and skills-to-namespace-mapping.json configuration.

    This step is NON-FATAL if the skills-source directory does not exist — it prints
    a warning and exits 0 so the pipeline can continue without skills data.

.PARAMETER ServiceArea
    The service area/namespace to generate related skills for (e.g., "keyvault", "storage", "acr")

.PARAMETER OutputPath
    Path to the generated directory (default: ../../generated from scripts/ dir)

.PARAMETER SkipBuild
    Skip building the .NET solution (default: $false).
    Set when the orchestrator (start.sh) has already built the solution.

.EXAMPLE
    ./6-Generate-RelatedSkills-One.ps1 -ServiceArea "keyvault"
    ./6-Generate-RelatedSkills-One.ps1 -ServiceArea "storage" -OutputPath ../../generated -SkipBuild
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$ServiceArea,

    [string]$OutputPath = "../../generated",

    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

# Import shared logging and normalization helpers
. "$PSScriptRoot\Shared-Functions.ps1"

try {
    Write-Divider
    Write-Progress "Step 6: Related Skills Generation"
    Write-Info "Service: $ServiceArea"
    Write-Info "Started: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    Write-Divider
    Write-Host ""

    $scriptDir = $PSScriptRoot
    $docsGenDir = Split-Path -Parent $scriptDir
    $outputDir = Resolve-OutputDir $OutputPath

    Write-Info "Output directory: $outputDir"
    Write-Host ""

    # Check that skills-source/ directory with CATALOG.md exists (non-fatal if missing)
    $skillsSourceDir = Join-Path $docsGenDir "skills-source"
    if (-not (Test-Path (Join-Path $skillsSourceDir "CATALOG.md"))) {
        Write-Warning "CATALOG.md not found at: $skillsSourceDir"
        Write-Warning "Skipping related skills generation (non-fatal)"
        Write-Warning "Run sync-agent-skills.sh first to download catalog files"
        Write-Host ""
        exit 0
    }
    Write-Success "✓ Found CATALOG.md in: $skillsSourceDir"

    # Check that skills-to-namespace-mapping.json exists
    $mappingFile = Join-Path $docsGenDir "data/skills-to-namespace-mapping.json"
    if (-not (Test-Path $mappingFile)) {
        Write-Warning "skills-to-namespace-mapping.json not found at: $mappingFile"
        Write-Warning "Skipping related skills generation (non-fatal)"
        Write-Host ""
        exit 0
    }
    Write-Success "✓ Found skills-to-namespace-mapping.json"
    Write-Host ""

    # Build .NET packages (skip if already built by preflight)
    Invoke-DotnetBuild -SkipBuild:$SkipBuild

    # Run RelatedSkillsGenerator
    Write-Divider
    Write-Progress "Generating related skills..."
    Write-Divider
    Write-Host ""

    Push-Location $docsGenDir
    try {
        & dotnet run --project RelatedSkillsGenerator --configuration Release --no-build -- --single-service $ServiceArea --output-path $outputDir --skills-source $skillsSourceDir
        $exitCode = $LASTEXITCODE
    } finally {
        Pop-Location
    }

    if ($exitCode -ne 0) {
        throw "Related skills generation failed (exit code: $exitCode)"
    }

    Write-Host ""
    Write-Divider

    # Show generated file
    $relatedFile = Join-Path $outputDir "related-skills/related-skills-$ServiceArea.md"

    Write-Progress "Generated Files:"
    Write-Host ""

    if (Test-Path $relatedFile) {
        Write-Success "✓ Related skills: $relatedFile"
        $lineCount = (Get-Content $relatedFile | Measure-Object -Line).Lines
        $byteSize = (Get-Item $relatedFile).Length
        $sizeKB = [math]::Round($byteSize / 1KB, 1)
        Write-Info "  Lines: $lineCount"
        Write-Info "  Size: ${sizeKB}KB"
    } else {
        Write-Warning "✗ Related skills file not found: $relatedFile"
        Write-Warning "  (namespace may have no skills mapped)"
    }

    Write-Host ""
    Write-Divider
    Write-Success "Step 6 completed successfully"
    Write-Info "Completed: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    Write-Divider
    Write-Host ""

} catch {
    Write-Host ""
    Write-Divider
    Write-ErrorMessage "Step 6 failed: $($_.Exception.Message)"
    Write-ErrorMessage $_.ScriptStackTrace
    Write-Divider

    exit 1
}
