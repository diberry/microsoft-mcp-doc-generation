#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Debug version of the Multi-page documentation generator
    
.DESCRIPTION
    This script is a modified version of Generate-MultiPageDocs.ps1 that enables
    debugging of the C# generator by using the VS Code debugger.
#>

param(
    [ValidateSet('json', 'yaml', 'both')]
    [string]$Format = 'both',
    [bool]$CreateIndex = $true,
    [bool]$CreateCommon = $true,
    [bool]$CreateCommands = $true,
    [bool]$CreateServiceOptions = $true
)

# Helper functions for colored output
function Write-Info { param([string]$Message) Write-Host "INFO: $Message" -ForegroundColor Cyan }
function Write-Success { param([string]$Message) Write-Host "SUCCESS: $Message" -ForegroundColor Green }
function Write-Warning { param([string]$Message) Write-Host "WARNING: $Message" -ForegroundColor Yellow }
function Write-Error { param([string]$Message) Write-Host "ERROR: $Message" -ForegroundColor Red }
function Write-Progress { param([string]$Message) Write-Host "PROGRESS: $Message" -ForegroundColor Magenta }

function Clear-PreviousOutput {
    Write-Progress "Cleaning up previous output..."
    
    # Remove previous output directory if it exists
    if (Test-Path $outputDir) {
        Remove-Item -Path $outputDir -Recurse -Force
        Write-Info "Removed previous generated directory"
    }
    
    # Create output directories
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $outputDir "multi-page") -Force | Out-Null
    Write-Info "Created output directories"
}

# Set paths and variables
$docsGenDir = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$rootDir = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$cliOutputPath = Join-Path $rootDir "generated/cli/cli-output.json"
$outputDir = Join-Path $rootDir "generated"

Write-Progress "Starting Azure MCP Multi-Page Documentation Generation..."

# Clean up previous output
Clear-PreviousOutput

# Step 1: Generate MCP tools data from CLI
Write-Progress "Step 1: Generating MCP tools data from CLI..."
Write-Progress "Running CLI tools list command..."

# Use the existing CLI output if available (for faster debugging)
if (Test-Path $cliOutputPath) {
    Write-Info "Using existing CLI output: $cliOutputPath"
} else {
    # If the file doesn't exist, generate it
    Push-Location "$rootDir/servers/Azure.Mcp.Server/src"
    $rawOutput = & dotnet run -- tools list
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to run 'tools list' command"
    }
    Pop-Location

    # Save raw CLI output to file
    $rawOutput | Out-File -FilePath $cliOutputPath -Encoding utf8
    Write-Success "CLI output saved: $cliOutputPath"
}

# Step 2: Build C# generator
Write-Progress "Step 2: Building C# generator..."

Push-Location (Join-Path $docsGenDir "CSharpGenerator")
& dotnet build --configuration Debug
if ($LASTEXITCODE -ne 0) {
    throw "Failed to build C# generator"
}
Pop-Location

Write-Success "C# generator built successfully"

# Step 3: Generate documentation using C# generator
Write-Progress "Step 3: Generating documentation using C# generator..."

# Build arguments for C# generator
$generatorArgs = @("generate-docs", $cliOutputPath, $outputDir)
if ($CreateIndex) { $generatorArgs += "--index" }
if ($CreateCommon) { $generatorArgs += "--common" }
if ($CreateCommands) { $generatorArgs += "--commands" }
if (-not $CreateServiceOptions) { $generatorArgs += "--no-service-options" }

# This is where we would normally run the generator, but we'll stop here
# so that the user can attach the debugger
Write-Host ""
Write-Host "===============================================" -ForegroundColor Yellow
Write-Host "READY FOR DEBUGGING" -ForegroundColor Yellow
Write-Host "===============================================" -ForegroundColor Yellow
Write-Host ""
Write-Host "To debug the CSharpGenerator:" -ForegroundColor White
Write-Host "1. Set breakpoints in the CSharpGenerator code" -ForegroundColor White
Write-Host "2. Run the 'Debug Docs-Generation CSharpGenerator' launch configuration" -ForegroundColor White
Write-Host "3. The arguments that will be used are:" -ForegroundColor White
Write-Host "   $generatorArgs" -ForegroundColor Gray
Write-Host ""
Write-Host "Press Enter to continue without debugging..." -ForegroundColor White
$null = Read-Host

# Continue with normal execution if the user didn't choose to debug
Push-Location "CSharpGenerator"
& dotnet run --configuration Debug -- $generatorArgs
if ($LASTEXITCODE -ne 0) {
    throw "Failed to generate documentation with C# generator"
}
Pop-Location

# Step 4: Generate additional data formats if requested
if ($Format -eq 'yaml' -or $Format -eq 'both') {
    Write-Progress "Step 4: Converting to YAML format..."
    # For now, focus on JSON since that's what works with tools list
    Write-Warning "YAML format conversion not implemented yet"
}

# Step 5: Print summary
Write-Progress "Step 5: Generation Summary"
Write-Success "Multi-page documentation generation completed successfully!"

# List generated files
Write-Info ""
Write-Info "Generated files in 'generated/multi-page':"
Get-ChildItem -Path (Join-Path $outputDir "multi-page") -File | ForEach-Object {
    $fileSizeKB = [Math]::Round($_.Length / 1KB, 1)
    Write-Info "  📄 $($_.Name) (${fileSizeKB}KB)"
}

Write-Info ""
Write-Info "Data files:"
$cliOutputFileSizeKB = [Math]::Round((Get-Item $cliOutputPath).Length / 1KB, 1)
Write-Info "  📄 $cliOutputPath (${cliOutputFileSizeKB}KB) - CLI output"

Write-Success "Documentation generation complete: $((Get-ChildItem -Path (Join-Path $outputDir "multi-page") -File).Count) pages created using C# generator with Handlebars templates"