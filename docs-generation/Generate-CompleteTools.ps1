#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Complete tools file generator - focused on debugging tool file generation
    
.DESCRIPTION
    This script runs the complete-tools generator using existing annotation and parameter files
    from the ./generated directory.
    
.EXAMPLE
    ./Generate-CompleteTools.ps1
#>

# Resolve output directory - always use 'generated' sibling to this script's location
$scriptDir = Split-Path -Parent $PSScriptRoot
$generatedDir = Join-Path $scriptDir "generated"

# Set up logging
$logDir = Join-Path $generatedDir "logs"
if (-not (Test-Path $logDir)) {
    New-Item -ItemType Directory -Path $logDir -Force | Out-Null
}
$logFile = Join-Path $logDir "complete-tools-$(Get-Date -Format 'yyyyMMdd-HHmmss').log"
Start-Transcript -Path $logFile -Append
Write-Host "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] Log file: $logFile" -ForegroundColor Cyan

# Helper functions for colored output
function Write-Info { param([string]$Message) Write-Host "INFO: $Message" -ForegroundColor Cyan }
function Write-Success { param([string]$Message) Write-Host "SUCCESS: $Message" -ForegroundColor Green }
function Write-Warning { param([string]$Message) Write-Host "WARNING: $Message" -ForegroundColor Yellow }
function Write-Error { param([string]$Message) Write-Host "ERROR: $Message" -ForegroundColor Red }
function Write-Progress { param([string]$Message) Write-Host "PROGRESS: $Message" -ForegroundColor Magenta }

# Main execution
try {
    Write-Progress "Starting Complete Tools Generation..."
    Write-Info "Using existing files from generated/"
    Write-Info "Generated directory: $generatedDir"
    
    # Verify CLI files exist
    $cliOutputFile = Join-Path $generatedDir "cli/cli-output.json"
    $versionOutputFile = Join-Path $generatedDir "cli/cli-version.json"
    
    if (-not (Test-Path $cliOutputFile)) {
        Write-Error "CLI output file not found: $cliOutputFile"
        throw "CLI files not found"
    }
    
    if (-not (Test-Path $versionOutputFile)) {
        Write-Error "Version file not found: $versionOutputFile"
        throw "Version file not found"
    }
    
    Write-Success "✓ CLI files verified"
    
    # Load version information (stored as plain text, not JSON)
    Write-Info "Reading version file: $versionOutputFile"
    $cliVersion = (Get-Content $versionOutputFile -Raw).Trim()
    Write-Info "CLI Version: $cliVersion"
    
    # Set output directory to generated (not generated/tools)
    $currentLocation = Get-Location
    $cliInputPath = Join-Path $generatedDir "cli/cli-output.json"
    $outputDir = $generatedDir
    
    Write-Info "Input: $cliInputPath"
    Write-Info "Output: $outputDir"
    
    # Verify annotations and parameters exist
    $annotationsDir = Join-Path $outputDir "annotations"
    $parametersDir = Join-Path $outputDir "parameters"
    
    if (Test-Path $annotationsDir) {
        $annotationCount = @(Get-ChildItem $annotationsDir -Name "*.md").Count
        Write-Info "✓ Found $annotationCount annotation files"
    } else {
        Write-Warning "Annotations directory not found: $annotationsDir"
    }
    
    if (Test-Path $parametersDir) {
        $parameterCount = @(Get-ChildItem $parametersDir -Name "*.md").Count
        Write-Info "✓ Found $parameterCount parameter files"
    } else {
        Write-Warning "Parameters directory not found: $parametersDir"
    }
    
    # Determine generator directory
    $generatorPath = if (Test-Path "CSharpGenerator/CSharpGenerator.csproj") {
        "CSharpGenerator"
    } elseif (Test-Path "docs-generation/CSharpGenerator/CSharpGenerator.csproj") {
        "docs-generation/CSharpGenerator"
    } else {
        throw "Cannot locate CSharpGenerator project"
    }
    
    Write-Progress "Running generator with --complete-tools flag..."
    
    # Build generator arguments
    $generatorArgs = @("generate-docs", $cliInputPath, $outputDir, "--complete-tools")
    if ($cliVersion -and $cliVersion -ne "unknown") {
        $generatorArgs += "--version"
        $generatorArgs += $cliVersion
    }
    
    $commandString = "dotnet run --configuration Release -- " + ($generatorArgs -join " ")
    Write-Info "Running: $commandString"
    
    # Execute generator
    Push-Location $generatorPath
    try {
        $generatorOutput = & dotnet run --configuration Release -- $generatorArgs 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Command failed with exit code: $LASTEXITCODE"
            Write-Error "Generator output: $($generatorOutput | Out-String)"
            throw "Failed to generate complete tools"
        }
        
        Write-Success "Generator completed successfully"
    } finally {
        Pop-Location
    }
    
    # Display output
    Write-Info ""
    Write-Info "Generator output:"
    foreach ($line in $generatorOutput) {
        Write-Info $line
    }
    
    # List generated tool files
    Write-Info ""
    Write-Info "Generated complete tool files:"
    
    $toolsDir = Join-Path $outputDir "tools"
    if (Test-Path $toolsDir) {
        $files = @(Get-ChildItem $toolsDir -Name "*.complete.md" | Sort-Object)
        Write-Info "✓ Found $($files.Count) complete tool files"
        
        if ($files.Count -gt 0) {
            Write-Info ""
            Write-Info "First 10 files:"
            $files | Select-Object -First 10 | ForEach-Object {
                $filePath = Join-Path $toolsDir $_
                $sizeKB = [math]::Round((Get-Item $filePath).Length / 1KB, 1)
                Write-Info "  📄 $_ (${sizeKB}KB)"
            }
        }
    } else {
        Write-Warning "Tools directory not found: $toolsDir"
    }
    
    Write-Success "Complete tools generation finished"
    
    # Generate per-service tool pages (optional)
    # Uncomment to enable tool pages generation
    Write-Progress "Generating per-service tool pages..."
    Write-Info "Debug: Per-service tool pages generation"
    Write-Info "  • Generator path: $generatorPath"
    Write-Info "  • CLI input: $cliInputPath"
    Write-Info "  • Output dir: $outputDir"
    Write-Info "  • CLI version: $cliVersion"
    
    Push-Location $generatorPath
    try {
        $toolPagesArgs = @("generate-docs", $cliInputPath, $outputDir, "--tool-pages")
        if ($cliVersion -and $cliVersion -ne "unknown") {
            $toolPagesArgs += "--version"
            $toolPagesArgs += $cliVersion
        }
        
        $toolPagesCommand = "dotnet run --configuration Release -- " + ($toolPagesArgs -join " ")
        Write-Info "Running: $toolPagesCommand"
        
        $toolPagesOutput = & dotnet run --configuration Release -- $toolPagesArgs 2>&1
        
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Tool pages generation failed with exit code: $LASTEXITCODE"
            Write-Error "Output: $($toolPagesOutput | Out-String)"
            throw "Failed to generate tool pages"
        }
        
        # Display output
        Write-Info ""
        Write-Info "Tool pages generation output:"
        foreach ($line in $toolPagesOutput) {
            Write-Info $line
        }
        
        Write-Success "Tool pages generated successfully"
    } catch {
        Write-Warning "Failed to generate tool pages: $($_.Exception.Message)"
    } finally {
        Pop-Location
    }

} catch {
    Write-Error "Generation failed: $($_.Exception.Message)"
    Write-Error "Error details: $($_.ScriptStackTrace)"
    Stop-Transcript
    exit 1
}

Write-Host "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] Complete" -ForegroundColor Green
Stop-Transcript
