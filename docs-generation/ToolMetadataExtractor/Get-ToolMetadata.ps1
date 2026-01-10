#!/usr/bin/env pwsh

param (
    [string]$ProjectPath = "/workspaces/new-mcp",
    [string]$OutputFile = "tool-metadata.json",
    [switch]$SkipExtraction = $false,
    [switch]$SkipToolList = $false,
    [string]$ToolListFile = "tool-list.txt"
)

# Set working directory to the project root
Set-Location $ProjectPath

# Configure paths
$serverProject = "servers/Azure.Mcp.Server/src/Azure.Mcp.Server.csproj"
$extractorProject = "docs-generation/ToolMetadataExtractor/ToolMetadataExtractor.csproj"
$toolListPath = Join-Path $ProjectPath $ToolListFile

# Step 1: Get tool list from MCP CLI if not skipping
if (-not $SkipToolList) {
    Write-Host "Getting tool list from MCP CLI..."
    
    # Run the tools list command and extract just the command paths
    $toolListResult = dotnet run --project $serverProject -- tools list 
    $toolListJson = $toolListResult | ConvertFrom-Json
    
    # Extract all command paths from the results
    $toolCommands = @()
    foreach ($tool in $toolListJson.results) {
        # The command format is "azmcp service resource operation"
        if ($tool.command -and $tool.command.StartsWith("azmcp ")) {
            $toolCommands += $tool.command.Substring(6) # Remove "azmcp " prefix
        }
    }
    
    # Write the command paths to file
    $toolCommands | Out-File -FilePath $toolListPath
    Write-Host "Found $($toolCommands.Count) tools, saved to $toolListPath"
}
else {
    Write-Host "Skipping tool list extraction, using existing file: $toolListPath"
    if (-not (Test-Path $toolListPath)) {
        Write-Error "Tool list file does not exist: $toolListPath"
        exit 1
    }
}

# Step 2: Run the extractor if not skipping
if (-not $SkipExtraction) {
    Write-Host "Running metadata extractor..."
    
    # Build the project first
    dotnet build $extractorProject
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to build the extractor project"
        exit 1
    }
    
    # Run the extractor with the tool list file
    $outputPath = Join-Path $ProjectPath $OutputFile
    dotnet run --project $extractorProject -- --tools-file $toolListPath --output $outputPath
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Metadata extraction failed"
        exit 1
    }
    
    Write-Host "Metadata extraction complete, results saved to $outputPath"
}
else {
    Write-Host "Skipping metadata extraction"
}

Write-Host "Script execution complete!"