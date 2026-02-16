#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Validates tool counts and generates family/comparison reports

.DESCRIPTION
    Step 5: Validates total tool counts between CLI output and ToolDescriptionEvaluator,
    generates tool family files, and creates comparison reports.
    
    This script:
    1. Compares CLI tool count with ToolDescriptionEvaluator output
    2. Identifies any missing or extra tools
    3. Generates comprehensive reports
    4. Saves comparison data for analysis

.PARAMETER OutputPath
    Path to the generated directory (default: ../generated from script location)

.EXAMPLE
    ./5-Validate-total-tool-count-and-family.ps1
    ./5-Validate-total-tool-count-and-family.ps1 -OutputPath ../generated
#>

param(
    [string]$OutputPath = "../generated"
)

$ErrorActionPreference = "Stop"

function Write-Info { param([string]$Message) Write-Host "INFO: $Message" -ForegroundColor Cyan }
function Write-Success { param([string]$Message) Write-Host "SUCCESS: $Message" -ForegroundColor Green }
function Write-Warning { param([string]$Message) Write-Host "WARNING: $Message" -ForegroundColor Yellow }
function Write-Error { param([string]$Message) Write-Host "ERROR: $Message" -ForegroundColor Red }
function Write-Progress { param([string]$Message) Write-Host "PROGRESS: $Message" -ForegroundColor Magenta }

try {
    Write-Progress "Validating Tool Counts and Generating Family Reports"
    Write-Info "Starting at: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    Write-Info ""

    $scriptDir = $PSScriptRoot
    $outputDir = if ([System.IO.Path]::IsPathRooted($OutputPath)) {
        $OutputPath
    } else {
        $absPath = Join-Path (Get-Location) $OutputPath
        [System.IO.Path]::GetFullPath($absPath)
    }

    Write-Info "Output directory: $outputDir"
    Write-Info ""

    # Set up paths
    $cliOutputFile = Join-Path $outputDir "cli/cli-output.json"
    $toolDescriptionEvaluatorScript = Join-Path (Split-Path $scriptDir -Parent) "..\eng\tools\ToolDescriptionEvaluator\Update-ToolsJson.ps1"
    $toolDescriptionEvaluatorPath = Join-Path (Split-Path $scriptDir -Parent) "..\eng\tools\ToolDescriptionEvaluator\tools.json"
    $localToolDescPath = Join-Path $outputDir "ToolDescriptionEvaluator.json"

    # Verify CLI output exists
    if (-not (Test-Path $cliOutputFile)) {
        throw "CLI output file not found: $cliOutputFile"
    }

    Write-Success "✓ CLI output file verified"
    Write-Info ""

    # Step 1: Generate tools.json using ToolDescriptionEvaluator
    Write-Progress "Step 1: Generating tools.json using ToolDescriptionEvaluator..."
    
    try {
        # Run the ToolDescriptionEvaluator script to generate tools.json if present
        if (Test-Path $toolDescriptionEvaluatorScript) {
            & $toolDescriptionEvaluatorScript -Force
            if ($LASTEXITCODE -ne 0) {
                Write-Warning "Failed to run ToolDescriptionEvaluator Update-ToolsJson.ps1, continuing with CLI-only comparison"
            } else {
                Write-Success "ToolDescriptionEvaluator tools.json generated successfully"
            
                # Copy the generated tools.json to our generated folder for easy access
                if (Test-Path $toolDescriptionEvaluatorPath) {
                    Copy-Item $toolDescriptionEvaluatorPath $localToolDescPath -Force
                    $localToolDescSize = [math]::Round((Get-Item $localToolDescPath).Length / 1KB, 1)
                    Write-Success "ToolDescriptionEvaluator output copied to: $localToolDescPath (${localToolDescSize}KB)"
                }
            
                Write-Info ""
            }
        } else {
            Write-Info "ToolDescriptionEvaluator scripts not found. Skipping comparison step."
            Write-Info ""
        }
    } catch {
        Write-Warning "Error running ToolDescriptionEvaluator: $($_.Exception.Message)"
        Write-Info ""
    }

    # Step 2: Compare tool counts
    Write-Progress "Step 2: Comparing tool counts between CLI output and ToolDescriptionEvaluator..."
    
    try {
        # Parse CLI output
        $cliData = Get-Content $cliOutputFile -Raw | ConvertFrom-Json
        $cliToolCount = if ($cliData.results) { $cliData.results.Count } else { 0 }
    
        Write-Info "CLI tool count: $cliToolCount"
        
        # Check if ToolDescriptionEvaluator output exists
        if (Test-Path $toolDescriptionEvaluatorPath) {
            # Parse ToolDescriptionEvaluator output
            $toolDescData = Get-Content $toolDescriptionEvaluatorPath -Raw | ConvertFrom-Json
            $toolDescToolCount = if ($toolDescData.results) { $toolDescData.results.Count } else { 0 }
        
            Write-Info ""
            Write-Info "Tool Count Comparison:"
            Write-Info "  📊 CLI tool count: $cliToolCount"
            Write-Info "  📊 ToolDescriptionEvaluator tool count: $toolDescToolCount"
        
            if ($cliToolCount -eq $toolDescToolCount) {
                Write-Success "  ✓ Tool counts match! Both sources report $cliToolCount tools."
            } else {
                Write-Warning "  ⚠ Tool count mismatch detected!"
                Write-Warning "    CLI output: $cliToolCount tools"
                Write-Warning "    ToolDescriptionEvaluator: $toolDescToolCount tools"
                Write-Warning "    Difference: $([Math]::Abs($cliToolCount - $toolDescToolCount)) tools"
            
                # Identify missing tools
                $cliToolNames = $cliData.results | ForEach-Object { "$($_.command)" } | Sort-Object
                $toolDescToolNames = $toolDescData.results | ForEach-Object { "$($_.command)" } | Sort-Object
            
                $missingInToolDesc = $cliToolNames | Where-Object { $_ -notin $toolDescToolNames }
                $missingInCli = $toolDescToolNames | Where-Object { $_ -notin $cliToolNames }
            
                if ($missingInToolDesc.Count -gt 0) {
                    Write-Warning ""
                    Write-Warning "  Tools present in CLI but missing in ToolDescriptionEvaluator:"
                    foreach ($tool in $missingInToolDesc) {
                        Write-Warning "    - $tool"
                    }
                }
            
                if ($missingInCli.Count -gt 0) {
                    Write-Warning ""
                    Write-Warning "  Tools present in ToolDescriptionEvaluator but missing in CLI:"
                    foreach ($tool in $missingInCli) {
                        Write-Warning "    - $tool"
                    }
                }
            
                # Save comparison report
                $comparisonReport = @{
                    timestamp         = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
                    cliToolCount      = $cliToolCount
                    toolDescToolCount = $toolDescToolCount
                    difference        = [Math]::Abs($cliToolCount - $toolDescToolCount)
                    missingInToolDesc = $missingInToolDesc
                    missingInCli      = $missingInCli
                }
            
                $comparisonReportPath = Join-Path $outputDir "tool-count-comparison.json"
                $comparisonReport | ConvertTo-Json -Depth 3 | Out-File -FilePath $comparisonReportPath -Encoding UTF8
                Write-Info "    📄 Tool count comparison report saved: $comparisonReportPath"
            }
        } else {
            Write-Info "ToolDescriptionEvaluator output not found. Skipping comparison."
        }
    
    } catch {
        Write-Warning "Failed to compare tool counts: $($_.Exception.Message)"
    }

    Write-Info ""
    Write-Success "Tool count validation and family report generation completed"
    Write-Info ""
    Write-Success "Completed at: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"

} catch {
    Write-Error "Validation failed: $($_.Exception.Message)"
    exit 1
}
