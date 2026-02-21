#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Runs the CLI Analyzer to generate Markdown reports of the CLI JSON data

.DESCRIPTION
    This script orchestrates the CliAnalyzer .NET application to analyze the Azure MCP
    CLI JSON data and generate both console output and Markdown reports.
    
    The analyzer groups tools by namespace, counts parameters (required vs optional),
    and provides detailed statistics and complexity metrics.

.PARAMETER OutputPath
    Path to the generated directory containing cli-output.json (default: ../generated)

.PARAMETER HtmlOutputPath
    Path and filename for Markdown report output (default: {OutputPath}/reports/cli-analysis-report.md)

.PARAMETER Namespace
    Optional: Analyze specific namespace only (e.g., "sql", "keyvault")

.PARAMETER Tool
    Optional: Show details for specific tool (use with --namespace)

.PARAMETER HtmlOnly
    Generate Markdown report only, skip console output (default: $false)

.PARAMETER SkipBuild
    Skip building the CliAnalyzer project (default: $false).
    Set to $true when the orchestrator has already built the solution.

.EXAMPLE
    ./Invoke-CliAnalyzer.ps1
    # Runs full analysis with console output and HTML report
    
    ./Invoke-CliAnalyzer.ps1 -HtmlOnly
    # Generates Markdown report without console output
    
    ./Invoke-CliAnalyzer.ps1 -Namespace sql
    # Analyzes SQL tools only
    
    ./Invoke-CliAnalyzer.ps1 -Namespace sql -Tool "create"
    # Shows details for the SQL create tool
#>

param(
    [string]$OutputPath = "../../generated",
    [string]$HtmlOutputPath = "",
    [string]$Namespace,
    [string]$Tool,
    [bool]$HtmlOnly = $false,
    [bool]$SkipBuild = $false
)

$ErrorActionPreference = "Stop"

function Write-Info { param([string]$Message) Write-Host "INFO: $Message" -ForegroundColor Cyan }
function Write-Success { param([string]$Message) Write-Host "SUCCESS: $Message" -ForegroundColor Green }
function Write-Warning { param([string]$Message) Write-Host "WARNING: $Message" -ForegroundColor Yellow }
function Write-Error { param([string]$Message) Write-Host "ERROR: $Message" -ForegroundColor Red }

try {
    # Resolve paths from script location
    $scriptDir = $PSScriptRoot                        # docs-generation/scripts/
    $docsGenDir = Split-Path -Parent $scriptDir       # docs-generation/
    $repoRoot = Split-Path -Parent $docsGenDir        # repo root
    
    # Resolve output path (relative to script directory)
    $outputDir = if ([System.IO.Path]::IsPathRooted($OutputPath)) { 
        $OutputPath 
    } else { 
        [System.IO.Path]::GetFullPath((Join-Path $scriptDir $OutputPath))
    }
    
    $cliJsonPath = Join-Path $outputDir "cli/cli-output.json"
    
    # Resolve HTML output path - if not provided, use reports directory under OutputPath
    if ([string]::IsNullOrWhiteSpace($HtmlOutputPath)) {
        $htmlReportPath = Join-Path $outputDir "reports/cli-analysis-report.md"
    } elseif ([System.IO.Path]::IsPathRooted($HtmlOutputPath)) {
        $htmlReportPath = $HtmlOutputPath
    } else {
        $htmlReportPath = [System.IO.Path]::GetFullPath((Join-Path $scriptDir $HtmlOutputPath))
    }
    
    # Ensure reports directory exists
    $reportsDir = Split-Path -Parent $htmlReportPath
    if (-not (Test-Path $reportsDir)) {
        New-Item -ItemType Directory -Path $reportsDir -Force | Out-Null
    }
    
    Write-Info "CLI Analyzer invocation started"
    Write-Info "Script directory: $scriptDir"
    Write-Info "Repo root: $repoRoot"
    Write-Info "Output directory: $outputDir"
    Write-Info "CLI JSON source: $cliJsonPath"
    Write-Info "Markdown output: $htmlReportPath"
    
    # Verify CLI JSON exists
    if (-not (Test-Path $cliJsonPath)) {
        Write-Warning "CLI JSON file not found: $cliJsonPath"
        Write-Info "Skipping analyzer - no CLI data available yet"
        return
    }
    
    # Build analyzer command arguments
    $analyzerArgs = @(
        "run",
        "--project",
        "docs-generation/CliAnalyzer",
        "--",
        "--file",
        $cliJsonPath,
        "--output",
        $htmlReportPath
    )
    
    # Add custom CLI arguments if provided
    if ($Namespace) {
        $analyzerArgs += @("--namespace", $Namespace)
    }
    
    if ($Tool) {
        $analyzerArgs += @("--tool", $Tool)
    }
    
    if ($HtmlOnly) {
        $analyzerArgs += @("--html-only")
    }
    
    Write-Info "Running CLI Analyzer..."
    Write-Info "Command: dotnet $($analyzerArgs -join ' ')"
    
    # Build the analyzer project first (unless skipped)
    if (-not $SkipBuild) {
        Write-Info "Building CLI Analyzer project..."
        Push-Location $repoRoot
        try {
            & dotnet build docs-generation/CliAnalyzer --configuration Release 2>&1 | ForEach-Object { Write-Info $_ }
            if ($LASTEXITCODE -ne 0) {
                Write-Warning "Build warnings/messages occurred (exit code: $LASTEXITCODE)"
            }
        } finally {
            Pop-Location
        }
    }
    
    Write-Info ""
    Write-Info "Running CLI Analyzer..."
    
    # Change to repo root for relative path resolution
    Push-Location $repoRoot
    try {
        & dotnet $analyzerArgs
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Analyzer exited with code: $LASTEXITCODE"
        } else {
            Write-Success "CLI Analyzer completed successfully"
        }
    } finally {
        Pop-Location
    }
    
    # Verify HTML was generated
    if (Test-Path $htmlReportPath) {
        $fileSize = (Get-Item $htmlReportPath).Length / 1KB
        Write-Success "Markdown report generated: $htmlReportPath ($([Math]::Round($fileSize, 2)) KB)"
    }
    
} catch {
    Write-Error "CLI Analyzer failed: $_"
    Write-Error "Stack trace: $($_.ScriptStackTrace)"
    throw
}
