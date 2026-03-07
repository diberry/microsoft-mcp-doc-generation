#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Generates a horizontal article (tool family page) for a single Azure service

.DESCRIPTION
    Similar to Generate-HorizontalArticles.ps1 but for a single Azure service.
    Generates a horizontal how-to article that explains how to use Azure MCP Server
    with a specific service. Uses AI to generate content that fills a Handlebars template.
    
    Steps:
    1. Validates CLI output files
    2. Builds the horizontal article generator
    3. Filters tools to the specified service
    4. Generates the article with AI content
    5. Shows the generated file

.PARAMETER ServiceArea
    The service area/family to generate an article for (e.g., "keyvault", "storage", "acr")
    This is the tool family/namespace name, not a specific tool command.
    
.PARAMETER OutputPath
    Path to the generated directory (default: ../generated from docs-generation root)

.PARAMETER SkipValidation
    Skip validation of CLI output files

.PARAMETER UseTextTransformation
    Apply text transformations to AI-generated content (default: $true)

.EXAMPLE
    ./Generate-HorizontalArticles-One.ps1 -ServiceArea "keyvault"
    ./Generate-HorizontalArticles-One.ps1 -ServiceArea "storage" -OutputPath ../generated
    ./Generate-HorizontalArticles-One.ps1 -ServiceArea "acr" -SkipValidation
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$ServiceArea,
    
    [string]$OutputPath = "../../generated",
    
    [switch]$SkipValidation = $false,

    [bool]$UseTextTransformation = $true,

    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

# Import shared logging and normalization helpers
. "$PSScriptRoot\Shared-Functions.ps1"

try {
    Write-Divider
    Write-Progress "Single Service Horizontal Article Generation"
    Write-Info "Service: $ServiceArea"
    Write-Info "Started: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    Write-Divider
    Write-Host ""

    $scriptDir = $PSScriptRoot
    $docsGenDir = Split-Path -Parent $scriptDir
    $outputDir = Resolve-OutputDir $OutputPath

    Write-Info "Output directory: $outputDir"
    Write-Host ""

    # Validate CLI version file exists
    if (-not $SkipValidation) {
        $cliVersionPath = Join-Path $outputDir "cli/cli-version.json"
        if (-not (Test-Path $cliVersionPath)) {
            throw "CLI version file not found at: $cliVersionPath"
        }
        Write-Success "✓ CLI output files validated"
    } else {
        Write-Warning "Skipping validation (--SkipValidation)"
    }
    Write-Host ""

    # Load full CLI output
    $cli = Get-CliOutput $outputDir

    # Normalize and find matching tools
    $ServiceArea = Normalize-ToolCommand $ServiceArea
    $matchingTools = Find-MatchingTools $cli.AllTools $ServiceArea
    Write-Host ""

    # Create filtered CLI file in temp directory
    $temp = New-FilteredCliFile $cli.CliOutput $matchingTools "temp-horizontal"
    $tempDir = $temp.TempDir
    Write-Host ""

    # Build .NET packages (skip if already built by preflight)
    Invoke-DotnetBuild -SkipBuild:$SkipBuild

    # Step 3: Run the generator for the single service
    Write-Divider
    Write-Progress "Step 3: Generating horizontal article with AI content..."
    Write-Divider
    Write-Host ""
    
    Push-Location $docsGenDir
    try {
        # Run with single service flag and output path
        $transformArg = if ($UseTextTransformation) { "--transform" } else { "" }
        & dotnet run --project HorizontalArticleGenerator/HorizontalArticleGenerator.csproj --configuration Release --no-build -- --single-service $ServiceArea --output-path $outputDir $transformArg
        $exitCode = $LASTEXITCODE
    } finally {
        Pop-Location
    }
    
    if ($exitCode -ne 0) {
        throw "Failed to generate horizontal article (exit code: $exitCode)"
    }

    Write-Host ""
    Write-Divider

    # Step 4: Show generated file
    $articleFile = Join-Path $outputDir "horizontal-articles/horizontal-article-$ServiceArea.md"
    $errorFile = Join-Path $outputDir "horizontal-articles/error-$ServiceArea.txt"
    
    Write-Progress "Generated Files:"
    Write-Host ""
    
    if (Test-Path $articleFile) {
        Write-Success "✓ Article: $articleFile"
        $lineCount = (Get-Content $articleFile | Measure-Object -Line).Lines
        $byteSize = (Get-Item $articleFile).Length
        $sizeKB = [math]::Round($byteSize / 1KB, 1)
        Write-Info "  Lines: $lineCount"
        Write-Info "  Size: ${sizeKB}KB"
    } elseif (Test-Path $errorFile) {
        Write-Warning "✗ Article generation failed"
        Write-Info "  Error file: $errorFile"
        Write-Info "  Error details:"
        Get-Content $errorFile | ForEach-Object { Write-Info "    $_" }
    } else {
        Write-Warning "✗ Article not found: $articleFile"
    }
    
    Write-Host ""
    Write-Divider
    Write-Success "Test completed successfully"
    Write-Info "Completed: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    Write-Divider
    Write-Host ""
    
    # Cleanup temp directory
    Remove-TempDir $tempDir

} catch {
    Write-Host ""
    Write-Divider
    Write-ErrorMessage "Test failed: $($_.Exception.Message)"
    Write-ErrorMessage $_.ScriptStackTrace
    Write-Divider
    
    # Cleanup temp directory
    Remove-TempDir (Join-Path $PSScriptRoot "temp-horizontal")
    
    exit 1
}
