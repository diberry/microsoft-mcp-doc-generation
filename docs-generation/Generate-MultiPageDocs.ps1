#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Multi-page documentation generator for Azure MCP tools - simplified orchestration script
    
.DESCRIPTION
    This script runs the Azure MCP CLI to get tools data, then calls the C# generator
    to produce documentation using Handlebars templates.
    
.PARAMETER Format
    Output format for data files: 'json', 'yaml', or 'both' (default: both)
    
.PARAMETER CreateIndex
    Whether to create an index page (default: true)
    
.PARAMETER CreateCommon
    Whether to create a common tools page (default: true)
    
.PARAMETER CreateCommands
    Whether to create a commands page (default: true)
    
.PARAMETER CreateServiceOptions
    Whether to create a service start options page (default: true)
    
.EXAMPLE
    ./Generate-MultiPageDocs.ps1
    ./Generate-MultiPageDocs.ps1 -Format json
    ./Generate-MultiPageDocs.ps1 -CreateIndex $false
    ./Generate-MultiPageDocs.ps1 -CreateCommands $false
    ./Generate-MultiPageDocs.ps1 -CreateServiceOptions $false
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
    
    # Remove previous data files
    $dataFiles = @("generated/mcp-tools.json", "generated/mcp-tools.yaml", "mcp-tools.json", "mcp-tools.yaml")
    foreach ($file in $dataFiles) {
        if (Test-Path $file) {
            Remove-Item $file -Force
            Write-Info "Removed previous data file: $file"
        }
    }
    
    # Remove previous output directory
    $parentDir = "generated"
    if (Test-Path $parentDir) {
        Remove-Item $parentDir -Recurse -Force
        Write-Info "Removed previous generated directory"
    }
    
    New-Item -ItemType Directory -Path $parentDir -Force | Out-Null
    New-Item -ItemType Directory -Path "generated/multi-page" -Force | Out-Null
    Write-Info "Created output directories"
}

# Main execution
try {
    Write-Progress "Starting Azure MCP Multi-Page Documentation Generation..."
    
    # Step 0: Clean up previous output
    Clear-PreviousOutput
    
    # Step 1: Generate JSON data from MCP CLI
    Write-Progress "Step 1: Generating MCP tools data from CLI..."
    
    Push-Location "..\servers\Azure.Mcp.Server\src"
    
    Write-Progress "Running CLI tools list command..."
    $rawOutput = & dotnet run -- tools list
    if ($LASTEXITCODE -ne 0) { 
        throw "Failed to generate JSON data from CLI" 
    }
    
    # Save raw CLI output for the C# generator
    $cliOutputFile = "../../../docs-generation/generated/cli-output.json"
    $rawOutput | Out-File -FilePath $cliOutputFile -Encoding UTF8
    Write-Success "CLI output saved: $cliOutputFile"
    
    Pop-Location
    
    # Step 2: Build C# generator if needed
    Write-Progress "Step 2: Building C# generator..."
    Push-Location "CSharpGenerator"
    
    & dotnet build --configuration Release
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to build C# generator"
    }
    Write-Success "C# generator built successfully"
    
    Pop-Location
    
    # Step 3: Run C# generator to create documentation
    Write-Progress "Step 3: Generating documentation using C# generator..."
    
    $cliOutputPath = "../generated/cli-output.json"  # Relative to CSharpGenerator directory
    $outputDir = "../generated/multi-page"           # Relative to CSharpGenerator directory
    
    # Build arguments for C# generator
    $generatorArgs = @("generate-docs", $cliOutputPath, $outputDir)
    if ($CreateIndex) { $generatorArgs += "--index" }
    if ($CreateCommon) { $generatorArgs += "--common" }
    if ($CreateCommands) { $generatorArgs += "--commands" }
    if (-not $CreateServiceOptions) { $generatorArgs += "--no-service-options" }
    
    Push-Location "CSharpGenerator"
    & dotnet run --configuration Release -- $generatorArgs
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
    
    # Step 5: Summary
    Write-Progress "Step 5: Generation Summary"
    
    Write-Success "Multi-page documentation generation completed successfully!"
    Write-Info ""
    Write-Info "Generated files in 'generated/multi-page':"
    
    # List generated files
    $actualOutputDir = "generated/multi-page"
    if (Test-Path $actualOutputDir) {
        $files = Get-ChildItem $actualOutputDir -Name "*.md" | Sort-Object
        foreach ($file in $files) {
            $filePath = Join-Path $actualOutputDir $file
            $sizeKB = [math]::Round((Get-Item $filePath).Length / 1KB, 1)
            Write-Info "  📄 $file (${sizeKB}KB)"
        }
    }
    
    Write-Info ""
    Write-Info "Data files:"
    $actualCliOutputPath = "generated/cli-output.json"
    if (Test-Path $actualCliOutputPath) {
        $jsonSize = [math]::Round((Get-Item $actualCliOutputPath).Length / 1KB, 1)
        Write-Info "  📄 $actualCliOutputPath (${jsonSize}KB) - CLI output"
    }
    
    $totalPages = (Get-ChildItem $actualOutputDir -Name "*.md" | Measure-Object).Count
    Write-Success "Documentation generation complete: $totalPages pages created using C# generator with Handlebars templates"

} catch {
    Write-Error "Documentation generation failed: $($_.Exception.Message)"
    Write-Error "Error details: $($_.ScriptStackTrace)"
    exit 1
}
