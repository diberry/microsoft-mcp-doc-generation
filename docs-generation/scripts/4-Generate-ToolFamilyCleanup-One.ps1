#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Generates tool family files for a single tool

.DESCRIPTION
    Similar to GenerateExamplePrompt-One.ps1 but for tool family generation.
    Generates and tests tool family metadata, related content, and final stitched files
    for a single Azure MCP tool. Useful for quick testing and debugging of the 
    tool family cleanup/assembly pipeline.
    
    Steps:
    1. Filters cli-output.json to include only the specified tool
    2. Generates metadata (frontmatter + H1) using AI
    3. Generates related content section using AI
    4. Stitches together final tool family file
    5. Shows all output files

.PARAMETER ToolCommand
    The tool command to test (e.g., "keyvault secret create", "storage account list")
    Can also be a tool family/namespace prefix to generate all tools in that family (e.g., "keyvault", "storage")

.PARAMETER OutputPath
    Path to the generated directory (default: ../generated from docs-generation root)

.PARAMETER SkipMetadata
    Skip metadata generation (use existing metadata files)

.PARAMETER SkipRelated
    Skip related content generation (use existing related content files)

.PARAMETER SkipStitch
    Skip final stitching (only generate metadata and related content)

.PARAMETER SkipValidation
    Skip the validation step (only generate files)

.EXAMPLE
    ./Generate-ToolFamilyCleanup-One.ps1 -ToolCommand "keyvault secret create"  # Single tool
    ./Generate-ToolFamilyCleanup-One.ps1 -ToolCommand "storage"                      # All storage tools
    ./Generate-ToolFamilyCleanup-One.ps1 -ToolCommand "acr registry list" -SkipRelated
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$ToolCommand,
    
    [string]$OutputPath = "../../generated",
    
    [switch]$SkipMetadata = $false,
    [switch]$SkipRelated = $false,
    [switch]$SkipStitch = $false,
    [switch]$SkipValidation
)

$ErrorActionPreference = "Stop"

function Write-Info { param([string]$Message) Write-Host "INFO: $Message" -ForegroundColor Cyan }
function Write-Success { param([string]$Message) Write-Host "SUCCESS: $Message" -ForegroundColor Green }
function Write-Warning { param([string]$Message) Write-Host "WARNING: $Message" -ForegroundColor Yellow }
function Write-Error { param([string]$Message) Write-Host "ERROR: $Message" -ForegroundColor Red }
function Write-Progress { param([string]$Message) Write-Host "PROGRESS: $Message" -ForegroundColor Magenta }
function Write-Divider { Write-Host ("═" * 80) -ForegroundColor DarkGray }

function Normalize-Command {
    param([string]$Command)

    if ([string]::IsNullOrWhiteSpace($Command)) {
        return $null
    }

    $normalized = ($Command -replace "\s+", " ").Trim().ToLowerInvariant()
    return $normalized
}

try {
    Write-Divider
    Write-Progress "Single Tool Family Generation Test"
    Write-Info "Tool: $ToolCommand"
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

    # Load full CLI output
    $cliOutputFile = Join-Path $outputDir "cli/cli-output.json"
    if (-not (Test-Path $cliOutputFile)) {
        throw "CLI output file not found: $cliOutputFile"
    }

    Write-Progress "Loading CLI output..."
    $cliOutput = Get-Content $cliOutputFile -Raw | ConvertFrom-Json
    $allTools = $cliOutput.results
    Write-Info "Total tools in CLI output: $($allTools.Count)"

    # Find tool(s) - either exact command match or family prefix match
    $matchingTools = @($allTools | Where-Object { 
        $_.command -eq $ToolCommand -or $_.command -like "$ToolCommand *"
    })
    
    if ($matchingTools.Count -eq 0) {
        Write-Error "No tools found matching: $ToolCommand"
        Write-Info "Available tools (first 10):"
        $allTools | Select-Object -First 10 -ExpandProperty command | ForEach-Object { Write-Info "  - $_" }
        exit 1
    }

    if ($matchingTools.Count -eq 1) {
        # Single tool match
        $tool = $matchingTools[0]
        Write-Success "✓ Found tool: $($tool.name)"
        Write-Info "  Command: $($tool.command)"
        Write-Info "  Description: $($tool.description)"
    } else {
        # Family match - multiple tools
        Write-Success "✓ Found $($matchingTools.Count) tools in family: $ToolCommand"
        foreach ($t in $matchingTools | Select-Object -First 5) {
            Write-Info "  - $($t.command)"
        }
        if ($matchingTools.Count -gt 5) {
            Write-Info "  ... and $($matchingTools.Count - 5) more"
        }
    }
    Write-Host ""

    # Extract family name from first matching tool
    $familyName = ($matchingTools[0].command -split ' ')[0]
    
    $tempDir = Join-Path $scriptDir "temp-family"
    New-Item -ItemType Directory -Path $tempDir -Force | Out-Null

    $filteredOutput = @{
        version = $cliOutput.version
        results = @($matchingTools)
    }
    
    $filteredOutputFile = Join-Path $tempDir "cli-output-single-tool.json"
    $filteredOutput | ConvertTo-Json -Depth 10 | Set-Content $filteredOutputFile -Encoding UTF8
    Write-Info "Created filtered CLI output: $filteredOutputFile"
    Write-Host ""

    # Build .NET packages
    Write-Progress "Building .NET packages..."
    $solutionFile = Join-Path (Split-Path $docsGenDir -Parent) "docs-generation.sln"
    if (Test-Path $solutionFile) {
        & dotnet build $solutionFile --configuration Release --verbosity quiet
        if ($LASTEXITCODE -ne 0) {
            throw ".NET build failed"
        }
        Write-Success "✓ Build succeeded"
    }
    Write-Host ""

    # Setup directories
    $toolsInputDir = Join-Path $outputDir "tools"
    $metadataOutputDir = Join-Path $outputDir "tool-family-metadata"
    $relatedOutputDir = Join-Path $outputDir "tool-family-related"
    $finalOutputDir = Join-Path $outputDir "tool-family"

    @($metadataOutputDir, $relatedOutputDir, $finalOutputDir) | ForEach-Object {
        if (-not (Test-Path $_)) {
            New-Item -ItemType Directory -Path $_ -Force | Out-Null
        }
    }

    # Check if tools directory exists and has files
    if (-not (Test-Path $toolsInputDir)) {
        Write-Error "Tools directory not found: $toolsInputDir"
        Write-Host "  Run Step 3 to generate tool files first."
        throw "Tools directory not found"
    }
    
    $toolFiles = Get-ChildItem $toolsInputDir -Filter "*.md" -ErrorAction SilentlyContinue
    if ($toolFiles.Count -eq 0) {
        Write-Error "No tool files found in: $toolsInputDir"
        Write-Host "  Run Step 3 to generate tool files first."
        throw "No tool files found"
    }
    
    Write-Info "Using tools from: $toolsInputDir ($($toolFiles.Count) files)"

    # Build ToolFamilyCleanup
    Write-Progress "Building ToolFamilyCleanup..."
    $toolFamilyDir = Join-Path $docsGenDir "ToolFamilyCleanup"
    Push-Location $toolFamilyDir
    try {
        & dotnet build --configuration Release --verbosity quiet
        if ($LASTEXITCODE -ne 0) {
            throw "ToolFamilyCleanup build failed"
        }
        Write-Success "✓ Build succeeded"
    } finally {
        Pop-Location
    }
    Write-Host ""

    # Run Tool Family Cleanup (multi-phase) in a temporary workspace to limit to this family
    Write-Divider
    Write-Progress "Running Tool Family Cleanup (single family)..."
    Write-Divider
    Write-Host ""

    $exePath = "$toolFamilyDir/bin/Release/net9.0/ToolFamilyCleanup.dll"
    $tempRoot = Join-Path $scriptDir "temp-family-run"
    $tempDocs = Join-Path $tempRoot "docs-generation"
    $tempGenerated = Join-Path $tempRoot "generated"
    $tempTools = Join-Path $tempGenerated "tools"

    $tempCli = Join-Path $tempGenerated "cli"
    @($tempDocs, $tempTools, $tempCli) | ForEach-Object {
        if (-not (Test-Path $_)) {
            New-Item -ItemType Directory -Path $_ -Force | Out-Null
        }
    }

    # Copy cli-version.json so CliVersionReader can find it in the temp workspace
    $cliVersionFile = Join-Path $outputDir "cli/cli-version.json"
    if (Test-Path $cliVersionFile) {
        Copy-Item -Path $cliVersionFile -Destination $tempCli -Force
    } else {
        Write-Warning "cli-version.json not found at $cliVersionFile - mcp-cli.version will be 'unknown'"
    }

    $brandMappingPath = Join-Path $docsGenDir "data/brand-to-server-mapping.json"
    if (Test-Path $brandMappingPath) {
        Copy-Item -Path $brandMappingPath -Destination $tempDocs -Force
    }

    function Get-ToolNamespaceFromFile {
        param([string]$FilePath)

        $match = Select-String -Path $FilePath -Pattern "@mcpcli\s+([^\r\n]+)" -AllMatches -ErrorAction SilentlyContinue | Select-Object -First 1
        if (-not $match) {
            return $null
        }

        $commandText = $match.Matches[0].Groups[1].Value.Trim()
        if (-not $commandText) {
            return $null
        }

        return ($commandText.Split(' ')[0]).ToLower()
    }

    # Copy only tools for this family into temp tools directory
    $familyNameLower = $familyName.ToLower()

    # Primary: match by tool namespace inside file content
    $familyToolFiles = Get-ChildItem -Path $toolsInputDir -Filter "*.md" -File -ErrorAction SilentlyContinue | Where-Object {
        (Get-ToolNamespaceFromFile $_.FullName) -eq $familyNameLower
    }

    # Fallback: match by filename prefixes (brand mappings)
    $prefixes = New-Object System.Collections.Generic.List[string]
    $prefixes.Add($familyNameLower)

    # Try to load brand-to-server mapping for alternate filename prefixes
    $brandMappingPath = Join-Path $docsGenDir "data/brand-to-server-mapping.json"
    if (Test-Path $brandMappingPath) {
        try {
            $brandMappings = Get-Content $brandMappingPath -Raw | ConvertFrom-Json
            $mapping = $brandMappings | Where-Object { $_.mcpServerName -eq $familyNameLower } | Select-Object -First 1
            if ($mapping -and $mapping.fileName) {
                $prefixes.Add($mapping.fileName.ToLower())
            }
        } catch {
            Write-Warning "Could not read data/brand-to-server-mapping.json: $($_.Exception.Message)"
        }
    }

    # Common fallbacks for AI services
    if (-not $prefixes.Contains("ai-$familyNameLower")) {
        $prefixes.Add("ai-$familyNameLower")
    }
    if (-not $prefixes.Contains("azure-$familyNameLower")) {
        $prefixes.Add("azure-$familyNameLower")
    }

    if (-not $familyToolFiles -or $familyToolFiles.Count -eq 0) {
        $familyToolFiles = @()
        foreach ($prefix in $prefixes) {
            $matches = Get-ChildItem -Path $toolsInputDir -Filter "$prefix-*.md" -File -ErrorAction SilentlyContinue
            if ($matches) {
                $familyToolFiles += $matches
            }
        }

        $familyToolFiles = $familyToolFiles | Sort-Object FullName -Unique
        if ($familyToolFiles.Count -eq 0) {
            $searched = ($prefixes | Sort-Object -Unique) -join ", "
            throw "No tool files found for family '$familyName' in $toolsInputDir (checked prefixes: $searched)"
        }
    }

    foreach ($file in $familyToolFiles) {
        Copy-Item -Path $file.FullName -Destination $tempTools -Force
    }

    Push-Location $tempDocs
    try {
        & dotnet $exePath --multi-phase
        if ($LASTEXITCODE -ne 0) {
            throw "Tool Family Cleanup failed"
        }
    } finally {
        Pop-Location
    }

    # Copy generated files back to main output directories
    $tempMetadata = Join-Path $tempGenerated "tool-family-metadata"
    $tempRelated = Join-Path $tempGenerated "tool-family-related"
    $tempFinal = Join-Path $tempGenerated "tool-family"

    if (Test-Path $tempMetadata) {
        Copy-Item -Path (Join-Path $tempMetadata "*.md") -Destination $metadataOutputDir -Force -ErrorAction SilentlyContinue
    }
    if (Test-Path $tempRelated) {
        Copy-Item -Path (Join-Path $tempRelated "*.md") -Destination $relatedOutputDir -Force -ErrorAction SilentlyContinue
    }
    if (Test-Path $tempFinal) {
        Copy-Item -Path (Join-Path $tempFinal "*.md") -Destination $finalOutputDir -Force -ErrorAction SilentlyContinue
    }

    Write-Host ""
    Write-Divider

    # Show generated files
    Write-Progress "Generated Files:"
    Write-Host ""
    
    if ($matchingTools.Count -eq 1) {
        # Single tool - files are named by FAMILY, not by tool
        $metadataFile = Join-Path $metadataOutputDir "$familyName-metadata.md"
        $relatedFile = Join-Path $relatedOutputDir "$familyName-related.md"
        $finalFile = Join-Path $finalOutputDir "$familyName.md"
        
        if (Test-Path $metadataFile) {
            Write-Success "✓ Metadata: $metadataFile"
            $lineCount = (Get-Content $metadataFile).Count
            Write-Info "  Lines: $lineCount"
        } else {
            Write-Warning "✗ Metadata not found: $metadataFile"
        }
        
        if (Test-Path $relatedFile) {
            Write-Success "✓ Related content: $relatedFile"
            $lineCount = (Get-Content $relatedFile).Count
            Write-Info "  Lines: $lineCount"
        } else {
            Write-Warning "✗ Related content not found: $relatedFile"
        }
        
        if (Test-Path $finalFile) {
            Write-Success "✓ Final file: $finalFile"
            $lineCount = (Get-Content $finalFile).Count
            Write-Info "  Lines: $lineCount"
        } else {
            Write-Warning "✗ Final file not found: $finalFile"
        }
    } else {
        # Multiple tools - count files in directories
        $metadataCount = if (Test-Path $metadataOutputDir) { (Get-ChildItem $metadataOutputDir -Filter "*.md" | Measure-Object).Count } else { 0 }
        $relatedCount = if (Test-Path $relatedOutputDir) { (Get-ChildItem $relatedOutputDir -Filter "*.md" | Measure-Object).Count } else { 0 }
        $finalCount = if (Test-Path $finalOutputDir) { (Get-ChildItem $finalOutputDir -Filter "*.md" | Measure-Object).Count } else { 0 }
        
        if ($metadataCount -gt 0) {
            Write-Success "✓ Metadata files: $metadataCount in $metadataOutputDir"
        } else {
            Write-Warning "✗ No metadata files found"
        }
        
        if ($relatedCount -gt 0) {
            Write-Success "✓ Related content files: $relatedCount in $relatedOutputDir"
        } else {
            Write-Warning "✗ No related content files found"
        }
        
        if ($finalCount -gt 0) {
            Write-Success "✓ Final family files: $finalCount in $finalOutputDir"
        } else {
            Write-Warning "✗ No final files found"
        }
    }
    
    Write-Host ""

    # Validate files
    if (-not $SkipValidation) {
        Write-Divider
        Write-Progress "Validating generated files..."
        Write-Divider
        Write-Host ""
        
        if ($matchingTools.Count -eq 1) {
            # Single tool validation
            $allFound = $true
            
            if (-not (Test-Path $metadataFile)) {
                Write-Warning "✗ Metadata file not found"
                $allFound = $false
            } else {
                Write-Success "✓ Metadata file exists"
            }
            
            if (-not (Test-Path $relatedFile)) {
                Write-Warning "✗ Related content file not found"
                $allFound = $false
            } else {
                Write-Success "✓ Related content file exists"
            }
            
            if (-not (Test-Path $finalFile)) {
                Write-Warning "✗ Final file not found"
                $allFound = $false
            } else {
                Write-Success "✓ Final file exists"
            }
            
            if ($allFound) {
                Write-Success "✓ All files generated successfully"
            } else {
                Write-Warning "Some files were not generated"
            }
        } else {
            # Multiple tools validation (single family)
            $metadataCount = if (Test-Path $metadataOutputDir) { (Get-ChildItem $metadataOutputDir -Filter "*.md" | Measure-Object).Count } else { 0 }
            $relatedCount = if (Test-Path $relatedOutputDir) { (Get-ChildItem $relatedOutputDir -Filter "*.md" | Measure-Object).Count } else { 0 }
            $finalCount = if (Test-Path $finalOutputDir) { (Get-ChildItem $finalOutputDir -Filter "*.md" | Measure-Object).Count } else { 0 }

            Write-Success "✓ Processed $($matchingTools.Count) tools"
            Write-Success "✓ Generated $finalCount family files"
            Write-Info "  Verify files in:"
            Write-Info "    - $metadataOutputDir ($metadataCount files)"
            Write-Info "    - $relatedOutputDir ($relatedCount files)"
            Write-Info "    - $finalOutputDir ($finalCount files)"
        }
        
        Write-Host ""
    } else {
        Write-Warning "Skipped validation (use without -SkipValidation to validate)"
    }

    Write-Host ""
    Write-Divider
    Write-Success "Test completed successfully"
    Write-Info "Completed: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    Write-Divider
    Write-Host ""
    
    # Cleanup temp directories
    Remove-Item -Path $tempDir -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -Path $tempRoot -Recurse -Force -ErrorAction SilentlyContinue

} catch {
    Write-Host ""
    Write-Divider
    Write-Error "Test failed: $($_.Exception.Message)"
    Write-Error $_.ScriptStackTrace
    Write-Divider
    
    # Cleanup temp directories
    $tempDir = Join-Path $scriptDir "temp-family"
    $tempRoot = Join-Path $scriptDir "temp-family-run"
    Remove-Item -Path $tempDir -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -Path $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
    
    exit 1
}
