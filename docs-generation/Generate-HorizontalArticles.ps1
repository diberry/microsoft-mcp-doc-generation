#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Standalone script for generating horizontal Azure service articles using AI
    
.DESCRIPTION
    Generates horizontal how-to articles for Azure services that explain how to use
    Azure MCP Server with each service. Uses AI to generate content that fills a
    Handlebars template.
    
    This script is completely independent of Generate-MultiPageDocs.ps1 and does not
    modify any existing documentation generation.
    
.PARAMETER SkipValidation
    Skip validation of CLI output files (not recommended)
    
.EXAMPLE
    ./Generate-HorizontalArticles.ps1
#>

param(
    [switch]$SkipValidation = $false
)
# Set up logging
$logDir = "../generated/logs"
if (-not (Test-Path $logDir)) {
    New-Item -ItemType Directory -Path $logDir -Force | Out-Null
}
$logFile = Join-Path $logDir "horizontal-articles-$(Get-Date -Format 'yyyyMMdd-HHmmss').log"
Start-Transcript -Path $logFile -Append

# Helper functions for colored output
function Write-Info { param([string]$Message) Write-Host "INFO: $Message" -ForegroundColor Cyan }
function Write-Success { param([string]$Message) Write-Host "SUCCESS: $Message" -ForegroundColor Green }
function Write-Warning { param([string]$Message) Write-Host "WARNING: $Message" -ForegroundColor Yellow }
function Write-Error { param([string]$Message) Write-Host "ERROR: $Message" -ForegroundColor Red }
function Write-Progress { param([string]$Message) Write-Host "PROGRESS: $Message" -ForegroundColor Magenta }

try {
    Write-Host ""
    Write-Host "======================================" -ForegroundColor Cyan
    Write-Host "Azure MCP Horizontal Article Generator" -ForegroundColor Cyan
    Write-Host "======================================" -ForegroundColor Cyan
    Write-Host ""
    
    # Step 1: Validate CLI output files...
    Write-Progress "Step 1: Validating CLI output files..."
    
    $cliOutputPath = "../generated/cli/cli-output.json"
    $cliVersionPath = "../generated/cli/cli-version.json"
    
    if (-not $SkipValidation) {
        if (-not (Test-Path $cliOutputPath)) {
            Write-Error "CLI output not found at: $cliOutputPath"
            Write-Error ""
            Write-Error "Please run one of the following first:"
            Write-Error "  ./run-mcp-cli-output.sh"
            Write-Error "  pwsh ./docs-generation/Get-McpCliOutput.ps1"
            throw "CLI output files not found"
        }
        
        if (-not (Test-Path $cliVersionPath)) {
            Write-Error "CLI version file not found at: $cliVersionPath"
            throw "CLI version file not found"
        }
        
        Write-Success "CLI output files validated"
    } else {
        Write-Warning "Skipping validation (--SkipValidation)"
    }
    Write-Host ""
    
    
    # Step 2: Build the horizontal article generator
    Write-Progress "Step 2: Building horizontal article generator..."
    
    & dotnet build HorizontalArticleGenerator/HorizontalArticleGenerator.csproj --configuration Release --nologo --verbosity quiet
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to build horizontal article generator (exit code: $LASTEXITCODE)"
    }
    
    Write-Success "Build successful"
    Write-Host ""
    
    # Step 3: Run the generator
    Write-Progress "Step 3: Generating horizontal articles with AI content..."
    Write-Host ""
    
    Push-Location $PSScriptRoot
    & dotnet run --project HorizontalArticleGenerator/HorizontalArticleGenerator.csproj --configuration Release --no-build
    $exitCode = $LASTEXITCODE
    Pop-Location
    
    if ($exitCode -ne 0) {
        throw "Failed to generate horizontal articles (exit code: $exitCode)"
    }
    
    Write-Host ""
    Write-Success "Horizontal article generation completed successfully!"
    Write-Host ""
    
    # Step 4: Summary
    Write-Progress "Step 4: Generation Summary"
    
    $outputDir = "../generated/horizontal-articles"
    if (Test-Path $outputDir) {
        $files = Get-ChildItem $outputDir -Filter "*.md" | Sort-Object Name
        $totalFiles = $files.Count
        $totalSize = ($files | Measure-Object -Property Length -Sum).Sum
        $totalSizeKB = [math]::Round($totalSize / 1KB, 1)
        
        Write-Info ""
        Write-Info "Generated files:"
        Write-Info "  📁 Directory: $outputDir"
        Write-Info "  📄 Files: $totalFiles articles"
        Write-Info "  💾 Total size: ${totalSizeKB}KB"
        Write-Info ""
        
        if ($totalFiles -le 10) {
            Write-Info "Files created:"
            foreach ($file in $files) {
                $sizeKB = [math]::Round($file.Length / 1KB, 1)
                Write-Info "  📄 $($file.Name) (${sizeKB}KB)"
            }
        } else {
            Write-Info "Sample files created:"
            foreach ($file in $files | Select-Object -First 5) {
                $sizeKB = [math]::Round($file.Length / 1KB, 1)
                Write-Info "  📄 $($file.Name) (${sizeKB}KB)"
            }
            Write-Info "  ... and $($totalFiles - 5) more"
        }
        
        # Check for error files
        $errorFiles = Get-ChildItem $outputDir -Filter "error-*.txt" -ErrorAction SilentlyContinue
        if ($errorFiles) {
            Write-Warning ""
            Write-Warning "⚠ Errors occurred for some services:"
            foreach ($errorFile in $errorFiles) {
                Write-Warning "  📄 $($errorFile.Name)"
            }
        }
    } else {
        Write-Warning "Output directory not found: $outputDir"
    }
    
    Write-Host ""
    Write-Success "All done! ✓"
    
} catch {
    Write-Error "Horizontal article generation failed: $($_.Exception.Message)"
    Write-Error "Error details: $($_.ScriptStackTrace)"
    Stop-Transcript
    exit 1
}

Write-Host ""
Write-Host "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] Generation complete" -ForegroundColor Green
Stop-Transcript
