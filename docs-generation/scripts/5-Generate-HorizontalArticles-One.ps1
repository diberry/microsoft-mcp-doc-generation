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

function Write-Info { param([string]$Message) Write-Host "INFO: $Message" -ForegroundColor Cyan }
function Write-Success { param([string]$Message) Write-Host "SUCCESS: $Message" -ForegroundColor Green }
function Write-Warning { param([string]$Message) Write-Host "WARNING: $Message" -ForegroundColor Yellow }
function Write-Error { param([string]$Message) Write-Host "ERROR: $Message" -ForegroundColor Red }
function Write-Progress { param([string]$Message) Write-Host "PROGRESS: $Message" -ForegroundColor Magenta }
function Write-Divider { Write-Host ("═" * 80) -ForegroundColor DarkGray }

try {
    Write-Divider
    Write-Progress "Single Service Horizontal Article Generation"
    Write-Info "Service: $ServiceArea"
    Write-Info "Started: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    Write-Divider
    Write-Host ""

    $scriptDir = $PSScriptRoot
    $docsGenDir = Split-Path -Parent $scriptDir
    $outputDir = if ([System.IO.Path]::IsPathRooted($OutputPath)) {
        $OutputPath
    } else {
        $absPath = Join-Path $scriptDir $OutputPath
        [System.IO.Path]::GetFullPath($absPath)
    }

    Write-Info "Output directory: $outputDir"
    Write-Host ""

    # Step 1: Validate CLI output files
    Write-Progress "Step 1: Validating CLI output files..."
    
    $cliOutputPath = Join-Path $outputDir "cli/cli-output.json"
    $cliVersionPath = Join-Path $outputDir "cli/cli-version.json"
    
    if (-not $SkipValidation) {
        if (-not (Test-Path $cliOutputPath)) {
            throw "CLI output not found at: $cliOutputPath"
        }
        
        if (-not (Test-Path $cliVersionPath)) {
            throw "CLI version file not found at: $cliVersionPath"
        }
        
        Write-Success "✓ CLI output files validated"
    } else {
        Write-Warning "Skipping validation (--SkipValidation)"
    }
    Write-Host ""

    # Load CLI output and filter by service
    Write-Progress "Loading CLI output..."
    $cliOutput = Get-Content $cliOutputPath -Raw | ConvertFrom-Json
    $allTools = $cliOutput.results
    Write-Info "Total tools in CLI output: $($allTools.Count)"

    # Filter tools by service area
    $serviceTools = @($allTools | Where-Object { 
        $_.command -like "$ServiceArea *" 
    })
    
    if ($serviceTools.Count -eq 0) {
        Write-Error "No tools found for service: $ServiceArea"
        Write-Info "Available service areas (first 10):"
        $allTools | ForEach-Object { $_.command -split ' ' | Select-Object -First 1 } | Sort-Object -Unique | Select-Object -First 10 | ForEach-Object { Write-Info "  - $_" }
        exit 1
    }

    Write-Success "✓ Found $($serviceTools.Count) tools for service: $ServiceArea"
    Write-Host ""

    # Create a filtered CLI output with just these tools
    $tempDir = Join-Path $scriptDir "temp-horizontal"
    New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
    
    $filteredOutput = @{
        version = $cliOutput.version
        results = $serviceTools
    }
    
    $filteredOutputFile = Join-Path $tempDir "cli-output-filtered.json"
    $filteredOutput | ConvertTo-Json -Depth 10 | Set-Content $filteredOutputFile -Encoding UTF8
    Write-Info "Created filtered CLI output: $filteredOutputFile ($($serviceTools.Count) tools)"
    Write-Host ""

    # Step 2: Build the horizontal article generator (skip if already built by preflight)
    if (-not $SkipBuild) {
        Write-Progress "Step 2: Building horizontal article generator..."
        
        Push-Location $docsGenDir
        try {
            & dotnet build HorizontalArticleGenerator/HorizontalArticleGenerator.csproj --configuration Release --nologo --verbosity quiet
            if ($LASTEXITCODE -ne 0) {
                throw "Failed to build horizontal article generator (exit code: $LASTEXITCODE)"
            }
            Write-Success "✓ Build successful"
        } finally {
            Pop-Location
        }
        Write-Host ""
    } else {
        Write-Info "Skipping build (already built by preflight)"
    }

    # Step 3: Run the generator for the single service
    Write-Divider
    Write-Progress "Step 3: Generating horizontal article with AI content..."
    Write-Divider
    Write-Host ""
    
    Push-Location $docsGenDir
    try {
        # Run with single service flag
        $transformArg = if ($UseTextTransformation) { "--transform" } else { "" }
        & dotnet run --project HorizontalArticleGenerator/HorizontalArticleGenerator.csproj --configuration Release --no-build -- --single-service $ServiceArea $transformArg
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
    Remove-Item -Path $tempDir -Recurse -Force -ErrorAction SilentlyContinue

} catch {
    Write-Host ""
    Write-Divider
    Write-Error "Test failed: $($_.Exception.Message)"
    Write-Error $_.ScriptStackTrace
    Write-Divider
    
    # Cleanup temp directory
    $tempDir = Join-Path $scriptDir "temp-horizontal"
    Remove-Item -Path $tempDir -Recurse -Force -ErrorAction SilentlyContinue
    
    exit 1
}
