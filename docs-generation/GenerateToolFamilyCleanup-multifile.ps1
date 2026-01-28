# Azure MCP Tool Family Cleanup Generator - Multi-Phase Mode
# Assembles tool family files from individual tool files using AI for metadata/related content

param(
    [string]$ToolsInputDir = "../generated/tools",
    [string]$MetadataOutputDir = "../generated/tool-family-metadata",
    [string]$RelatedOutputDir = "../generated/tool-family-related",
    [string]$FinalOutputDir = "../generated/tool-family-multifile",
    [switch]$Help
)

if ($Help) {
    Write-Host "Azure MCP Tool Family Cleanup Generator (Multi-Phase)"
    Write-Host "======================================================"
    Write-Host ""
    Write-Host "Usage: ./GenerateToolFamilyCleanup-multifile.ps1 [options]"
    Write-Host ""
    Write-Host "Options:"
    Write-Host "  -ToolsInputDir <path>       Input directory with complete tool files"
    Write-Host "                              Default: ../generated/tools"
    Write-Host ""
    Write-Host "  -MetadataOutputDir <path>   Output directory for AI-generated metadata"
    Write-Host "                              Default: ../generated/tool-family-metadata"
    Write-Host ""
    Write-Host "  -RelatedOutputDir <path>    Output directory for AI-generated related content"
    Write-Host "                              Default: ../generated/tool-family-related"
    Write-Host ""
    Write-Host "  -FinalOutputDir <path>      Output directory for final stitched files"
    Write-Host "                              Default: ../generated/tool-family-multifile"
    Write-Host ""
    Write-Host "  -Help                       Display this help message"
    Write-Host ""
    Write-Host "Multi-Phase Process:"
    Write-Host "  Phase 1: Read and group tools from ./generated/tools by family"
    Write-Host "  Phase 2: Generate metadata (frontmatter + H1) using AI"
    Write-Host "  Phase 3: Generate related content section using AI"
    Write-Host "  Phase 4: Stitch together (no AI, pure assembly)"
    Write-Host ""
    Write-Host "Advantages over single-phase:"
    Write-Host "  - No 16K token limit (foundry with 19 tools works perfectly)"
    Write-Host "  - 95% cost reduction (~115K tokens vs. 2M tokens)"
    Write-Host "  - All tools included (no truncation)"
    Write-Host "  - Intermediate files saved for debugging"
    exit 0
}

Write-Host "Azure MCP Tool Family Cleanup Generator (Multi-Phase)"
Write-Host "======================================================"
Write-Host ""

# Configuration
$ToolsInputDir = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot $ToolsInputDir))
$MetadataOutputDir = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot $MetadataOutputDir))
$RelatedOutputDir = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot $RelatedOutputDir))
$FinalOutputDir = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot $FinalOutputDir))

Write-Host "Configuration:"
Write-Host "  Tools Input:      $ToolsInputDir"
Write-Host "  Metadata Output:  $MetadataOutputDir"
Write-Host "  Related Output:   $RelatedOutputDir"
Write-Host "  Final Output:     $FinalOutputDir"
Write-Host ""

# Validate input directory
if (-not (Test-Path $ToolsInputDir)) {
    Write-Host "❌ Error: Tools input directory not found: $ToolsInputDir" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please run the base documentation generation first:"
    Write-Host "  pwsh ./Generate-MultiPageDocs.ps1"
    exit 1
}

# Create output directories
Write-Host "Creating output directories..."
@($MetadataOutputDir, $RelatedOutputDir, $FinalOutputDir) | ForEach-Object {
    if (-not (Test-Path $_)) {
        New-Item -ItemType Directory -Path $_ -Force | Out-Null
        Write-Host "  ✓ Created: $_"
    } else {
        Write-Host "  ✓ Exists: $_"
    }
}
Write-Host ""

# Build ToolFamilyCleanup
Write-Host "Building ToolFamilyCleanup..."
Push-Location "$PSScriptRoot/ToolFamilyCleanup"
try {
    $buildOutput = dotnet build --configuration Release 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Build failed:" -ForegroundColor Red
        Write-Host $buildOutput
        exit 1
    }
    Write-Host "✓ Build successful"
    Write-Host ""
}
finally {
    Pop-Location
}

# Run Tool Family Cleanup in multi-phase mode
Write-Host "Running Tool Family Cleanup (Multi-Phase)..."
$exePath = "$PSScriptRoot/ToolFamilyCleanup/bin/Release/net9.0/ToolFamilyCleanup.dll"

# Run from docs-generation directory so relative paths work correctly
Push-Location $PSScriptRoot
try {
    dotnet $exePath --multi-phase
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Tool Family Cleanup failed with exit code $LASTEXITCODE" -ForegroundColor Red
        exit 1
    }
}
finally {
    Pop-Location
}

Write-Host ""
Write-Host "✓ Multi-phase cleanup completed successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "Output locations:"
Write-Host "  Metadata:        $MetadataOutputDir"
Write-Host "  Related content: $RelatedOutputDir"
Write-Host "  Final files:     $FinalOutputDir"
