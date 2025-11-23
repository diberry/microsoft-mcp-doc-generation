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
    
.PARAMETER ExamplePrompts
    Whether to generate example prompts using Azure OpenAI (default: false)
    
.EXAMPLE
    ./Generate-MultiPageDocs.ps1
    ./Generate-MultiPageDocs.ps1 -Format json
    ./Generate-MultiPageDocs.ps1 -CreateIndex $false
    ./Generate-MultiPageDocs.ps1 -CreateCommands $false
    ./Generate-MultiPageDocs.ps1 -CreateServiceOptions $false
    ./Generate-MultiPageDocs.ps1 -ExamplePrompts $true
#>

param(
    [ValidateSet('json', 'yaml', 'both')]
    [string]$Format = 'both',
    [bool]$CreateIndex = $true,
    [bool]$CreateCommon = $true,
    [bool]$CreateCommands = $true,
    [bool]$CreateServiceOptions = $true,
    [bool]$ExamplePrompts = $true
)

# Set up logging
$logDir = "generated/logs"
if (-not (Test-Path $logDir)) {
    New-Item -ItemType Directory -Path $logDir -Force | Out-Null
}
$logFile = Join-Path $logDir "generation-$(Get-Date -Format 'yyyyMMdd-HHmmss').log"
Start-Transcript -Path $logFile -Append
Write-Host "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] Log file: $logFile" -ForegroundColor Cyan

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
    New-Item -ItemType Directory -Path "generated/cli" -Force | Out-Null
    New-Item -ItemType Directory -Path "generated/tools" -Force | Out-Null
    if ($ExamplePrompts) {
        New-Item -ItemType Directory -Path "generated/example-prompts" -Force | Out-Null
    }
    Write-Info "Created output directories"
}

# Main execution
try {
    Write-Progress "Starting Azure MCP Multi-Page Documentation Generation..."
    
    # Step 0: Clean up previous output (except CLI files)
    Write-Progress "Cleaning up previous output..."
    
    # Remove previous data files (but preserve CLI output)
    $dataFiles = @("generated/mcp-tools.json", "generated/mcp-tools.yaml", "mcp-tools.json", "mcp-tools.yaml")
    foreach ($file in $dataFiles) {
        if (Test-Path $file) {
            Remove-Item $file -Force
            Write-Info "Removed: $file"
        }
    }
    
    # Remove previous output directory (except CLI subdirectory)
    $parentDir = "generated"
    if (Test-Path $parentDir) {
        Get-ChildItem $parentDir -Exclude "cli" | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
        Write-Info "Removed previous generated files (preserved CLI output)"
    }
    
    New-Item -ItemType Directory -Path $parentDir -Force | Out-Null
    New-Item -ItemType Directory -Path "generated/logs" -Force | Out-Null
    New-Item -ItemType Directory -Path "generated/tools" -Force | Out-Null
    if ($ExamplePrompts) {
        New-Item -ItemType Directory -Path "generated/example-prompts" -Force | Out-Null
    }
    Write-Info "Created output directories"
    
    # Step 1: Validate CLI output files
    Write-Progress "Step 1: Validating CLI output files..."
    
    # Determine CLI output path (container vs local)
    $cliOutputPath = if ($env:MCP_SERVER_PATH) { 
        "/output/cli"  # Container path
    } else { 
        "generated/cli"  # Local path
    }
    Pop-Location
    
    $cliOutputFile = Join-Path $cliOutputPath "cli-output.json"
    $namespaceOutputFile = Join-Path $cliOutputPath "cli-namespace.json"
    $versionOutputFile = Join-Path $cliOutputPath "cli-version.json"
    
    Write-Info "CLI output path: $cliOutputPath"
    
    # Validate all three files exist and are valid
    $filesValid = $true
    $missingFiles = @()
    
    foreach ($file in @($cliOutputFile, $namespaceOutputFile, $versionOutputFile)) {
        if (-not (Test-Path $file)) {
            $filesValid = $false
            $missingFiles += $file
            Write-Warning "Missing: $file"
        } else {
            $fileSize = (Get-Item $file).Length
            if ($fileSize -eq 0) {
                $filesValid = $false
                Write-Warning "Empty file: $file"
            } else {
                try {
                    $content = Get-Content $file -Raw | ConvertFrom-Json
                    # Validate structure for CLI output files
                    if ($file -like "*cli-output.json" -or $file -like "*cli-namespace.json") {
                        if (-not $content.status -or -not $content.results) {
                            $filesValid = $false
                            Write-Warning "Invalid structure in: $file"
                        }
                    }
                    # Validate structure for version file
                    if ($file -like "*cli-version.json") {
                        if (-not $content.version) {
                            $filesValid = $false
                            Write-Warning "Invalid version file: $file"
                        }
                    }
                } catch {
                    $filesValid = $false
                    Write-Warning "Invalid JSON in: $file - $($_.Exception.Message)"
                }
            }
        }
    }
    
    if (-not $filesValid) {
        Write-Error "CLI output files are missing or invalid"
        Write-Error ""
        Write-Error "Required files:"
        Write-Error "  • $cliOutputFile"
        Write-Error "  • $namespaceOutputFile"
        Write-Error "  • $versionOutputFile"
        Write-Error ""
        Write-Error "Run the following to generate CLI output files:"
        Write-Error "  ./run-mcp-cli-output.sh"
        Write-Error "Or:"
        Write-Error "  pwsh ./docs-generation/Get-McpCliOutput.ps1"
        throw "CLI output files validation failed"
    }
    
    Write-Success "CLI output files validated successfully"
    
    # Load version information from cli-version.json
    $versionData = Get-Content $versionOutputFile -Raw | ConvertFrom-Json
    $cliVersion = $versionData.version
    Write-Info "CLI Version: $cliVersion"
    Write-Info "MCP Branch: $($versionData.mcpBranch)"
    Write-Info "Generated: $($versionData.timestamp)"
    
    # Read namespace data for CSV generation
    $jsonNamespaceOutput = Get-Content $namespaceOutputFile -Raw
    
    # Generate namespaces CSV with alphabetically sorted names
    Write-Progress "Generating namespaces CSV..."
    try {
        $namespaceData = $jsonNamespaceOutput | ConvertFrom-Json
        if ($namespaceData.results) {
            # Sort by name alphabetically and create CSV content with Name and Command columns
            $sortedNamespaces = $namespaceData.results | Sort-Object name
            $csvContent = "Name,Command`n"
            foreach ($ns in $sortedNamespaces) {
                $csvContent += "$($ns.name),               $($ns.command)`n"
            }
            
            # Save CSV file
            $csvOutputFile = "generated/namespaces.csv"
            $csvContent | Out-File -FilePath $csvOutputFile -Encoding UTF8 -NoNewline
            Write-Success "Namespaces CSV saved: $csvOutputFile"
        } else {
            Write-Warning "No namespace results found in CLI output"
        }
    } catch {
        Write-Warning "Failed to generate namespaces CSV: $($_.Exception.Message)"
    }
    
    # Step 2: Build C# generator and all dependencies via solution file
    Write-Progress "Step 2: Building C# generator and dependencies..."
    
    & dotnet build docs-generation.sln --configuration Release --nologo --verbosity quiet
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to build documentation solution (exit code: $LASTEXITCODE)"
    }
    Write-Success "C# generator and dependencies built successfully"
    
    # Step 3: Run C# generator to create documentation
    Write-Progress "Step 3: Generating documentation using C# generator..."
    
    # Determine CLI output path relative to CSharpGenerator directory
    $cliInputPath = if ($env:MCP_SERVER_PATH) {
        "/output/cli/cli-output.json"  # Container path
    } else {
        "../generated/cli/cli-output.json"  # Local path
    }
    $outputDir = if ($env:MCP_SERVER_PATH) {
        "/output/tools"  # Container path
    } else {
        "../generated/tools"  # Local path
    }
    
    # Build arguments for C# generator
    $generatorArgs = @("generate-docs", $cliInputPath, $outputDir)
    if ($CreateIndex) { $generatorArgs += "--index" }
    if ($CreateCommon) { $generatorArgs += "--common" }
    if ($CreateCommands) { $generatorArgs += "--commands" }
    $generatorArgs += "--annotations"  # Always generate annotation files
    if ($ExamplePrompts) { $generatorArgs += "--example-prompts" }  # Generate example prompts using Azure OpenAI
    if (-not $CreateServiceOptions) { $generatorArgs += "--no-service-options" }
    if ($cliVersion -and $cliVersion -ne "unknown") { 
        $generatorArgs += "--version"
        $generatorArgs += $cliVersion
    }
    
    Push-Location "CSharpGenerator"
    
    # Echo the exact command being run for debugging
    $commandString = "dotnet run --configuration Release --no-build -- " + ($generatorArgs -join " ")
    Write-Info "Running: $commandString"
    
    $generatorOutput = & dotnet run --configuration Release --no-build -- $generatorArgs 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Command failed with exit code: $LASTEXITCODE"
        Write-Error "Generator output: $($generatorOutput | Out-String)"
        throw "Failed to generate documentation with C# generator"
    }
    
    # Parse tool count information from generator output
    $totalTools = 0
    $totalAreas = 0
    $toolCountsByArea = @{}
    $captureToolList = $false
    $toolListOutput = @()
    
    foreach ($line in $generatorOutput) {
        if ($line -match "Total tools: (\d+)") {
            $totalTools = [int]$matches[1]
        }
        elseif ($line -match "Total service areas: (\d+)") {
            $totalAreas = [int]$matches[1]
        }
        elseif ($line -match "^\s*(\w+):\s*(\d+)\s*tools") {
            $areaName = $matches[1]
            $areaCount = [int]$matches[2]
            $toolCountsByArea[$areaName] = $areaCount
        }
        elseif ($line -match "^Tool List by Service Area:") {
            $captureToolList = $true
        }
        elseif ($captureToolList) {
            $toolListOutput += $line
        }
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
    
    # Step 5.1: Generate tools.json using ToolDescriptionEvaluator and compare tool counts
    Write-Progress "Step 5.1: Generating tools.json for comparison..."
    
    $toolDescriptionEvaluatorScript = "..\eng\tools\ToolDescriptionEvaluator\Update-ToolsJson.ps1"
    $toolDescriptionEvaluatorPath = "..\eng\tools\ToolDescriptionEvaluator\tools.json"
    $localToolDescPath = "generated/ToolDescriptionEvaluator.json"
    
    try {
        # Run the ToolDescriptionEvaluator script to generate tools.json
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
            
            # Compare tool counts between CLI output and ToolDescriptionEvaluator output
            Write-Progress "Comparing tool counts between CLI output and ToolDescriptionEvaluator..."
            
            try {
                # Parse CLI output
                $cliData = Get-Content $cliOutputFile -Raw | ConvertFrom-Json
                $cliToolCount = if ($cliData.results) { $cliData.results.Count } else { 0 }
                
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
                        timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
                        cliToolCount = $cliToolCount
                        toolDescToolCount = $toolDescToolCount
                        difference = [Math]::Abs($cliToolCount - $toolDescToolCount)
                        missingInToolDesc = $missingInToolDesc
                        missingInCli = $missingInCli
                    }
                    
                    $comparisonReportPath = "generated/tool-count-comparison.json"
                    $comparisonReport | ConvertTo-Json -Depth 3 | Out-File -FilePath $comparisonReportPath -Encoding UTF8
                    Write-Info "    📄 Tool count comparison report saved: $comparisonReportPath"
                }
                
            } catch {
                Write-Warning "Failed to compare tool counts: $($_.Exception.Message)"
            }
        }
    } catch {
        Write-Warning "Error running ToolDescriptionEvaluator: $($_.Exception.Message)"
    }
    
    Write-Success "Multi-page documentation generation completed successfully!"
    Write-Info ""
    Write-Info "Generated files in 'generated/tools':"
    
    # List generated files
    $actualOutputDir = "generated/tools"
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
    $actualCliOutputPath = "generated/cli/cli-output.json"
    if (Test-Path $actualCliOutputPath) {
        $jsonSize = [math]::Round((Get-Item $actualCliOutputPath).Length / 1KB, 1)
        Write-Info "  📄 $actualCliOutputPath (${jsonSize}KB) - CLI output"
    }
    
    $actualNamespaceOutputPath = "generated/cli/cli-namespace.json"
    if (Test-Path $actualNamespaceOutputPath) {
        $namespaceSize = [math]::Round((Get-Item $actualNamespaceOutputPath).Length / 1KB, 1)
        Write-Info "  📄 $actualNamespaceOutputPath (${namespaceSize}KB) - CLI namespace output"
    }
    
    $actualCsvOutputPath = "generated/namespaces.csv"
    if (Test-Path $actualCsvOutputPath) {
        $csvSize = [math]::Round((Get-Item $actualCsvOutputPath).Length / 1KB, 1)
        Write-Info "  📄 $actualCsvOutputPath (${csvSize}KB) - Alphabetically sorted namespaces CSV"
    }
    
    $comparisonReportPath = "generated/tool-count-comparison.json"
    if (Test-Path $comparisonReportPath) {
        $reportSize = [math]::Round((Get-Item $comparisonReportPath).Length / 1KB, 1)
        Write-Info "  📄 $comparisonReportPath (${reportSize}KB) - Tool count comparison report"
    }
    
    $toolDescOutputPath = "..\eng\tools\ToolDescriptionEvaluator\tools.json"
    if (Test-Path $toolDescOutputPath) {
        $toolDescSize = [math]::Round((Get-Item $toolDescOutputPath).Length / 1KB, 1)
        Write-Info "  📄 $toolDescOutputPath (${toolDescSize}KB) - ToolDescriptionEvaluator tools.json"
    }
    
    $localToolDescPath = "generated/ToolDescriptionEvaluator.json"
    if (Test-Path $localToolDescPath) {
        $localToolDescSize = [math]::Round((Get-Item $localToolDescPath).Length / 1KB, 1)
        Write-Info "  📄 $localToolDescPath (${localToolDescSize}KB) - ToolDescriptionEvaluator tools.json (local copy)"
    }
    
    $totalPages = (Get-ChildItem $actualOutputDir -Name "*.md" | Measure-Object).Count
    Write-Success "Documentation generation complete: $totalPages pages created using C# generator with Handlebars templates"
    
    # Build comprehensive summary for file output
    $summaryLines = @()
    $summaryLines += "# Azure MCP Documentation Generation Summary"
    $summaryLines += ""
    $summaryLines += "**Generated:** $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss UTC')"
    $summaryLines += "**Generation Method:** C# generator with Handlebars templates"
    $summaryLines += "**Total Pages Created:** $totalPages"
    $summaryLines += ""
    
    # Generated files section
    $summaryLines += "## Generated Documentation Files"
    $summaryLines += ""
    if (Test-Path $actualOutputDir) {
        $files = Get-ChildItem $actualOutputDir -Name "*.md" | Sort-Object
        foreach ($file in $files) {
            $filePath = Join-Path $actualOutputDir $file
            $sizeKB = [math]::Round((Get-Item $filePath).Length / 1KB, 1)
            $summaryLines += "- 📄 $file (${sizeKB}KB)"
        }
    }
    
    # Data files section
    $summaryLines += ""
    $summaryLines += "## Data Files"
    $summaryLines += ""
    
    if (Test-Path $actualCliOutputPath) {
        $jsonSize = [math]::Round((Get-Item $actualCliOutputPath).Length / 1KB, 1)
        $summaryLines += "- 📄 $actualCliOutputPath (${jsonSize}KB) - CLI output"
    }
    
    if (Test-Path $actualNamespaceOutputPath) {
        $namespaceSize = [math]::Round((Get-Item $actualNamespaceOutputPath).Length / 1KB, 1)
        $summaryLines += "- 📄 $actualNamespaceOutputPath (${namespaceSize}KB) - CLI namespace output"
    }
    
    if (Test-Path $actualCsvOutputPath) {
        $csvSize = [math]::Round((Get-Item $actualCsvOutputPath).Length / 1KB, 1)
        $summaryLines += "- 📄 $actualCsvOutputPath (${csvSize}KB) - Alphabetically sorted namespaces CSV"
    }
    
    if (Test-Path $comparisonReportPath) {
        $reportSize = [math]::Round((Get-Item $comparisonReportPath).Length / 1KB, 1)
        $summaryLines += "- 📄 $comparisonReportPath (${reportSize}KB) - Tool count comparison report"
    }
    
    if (Test-Path $toolDescOutputPath) {
        $toolDescSize = [math]::Round((Get-Item $toolDescOutputPath).Length / 1KB, 1)
        $summaryLines += "- 📄 $toolDescOutputPath (${toolDescSize}KB) - ToolDescriptionEvaluator tools.json"
    }
    
    # Display tool count statistics
    if ($totalTools -gt 0) {
        $summaryLines += ""
        $summaryLines += "## Tool Statistics"
        $summaryLines += ""
        $summaryLines += "- **Total tools:** $totalTools"
        $summaryLines += "- **Total service areas:** $totalAreas"
        
        if ($toolCountsByArea.Count -gt 0) {
            $summaryLines += ""
            $summaryLines += "### Tools by Service Area"
            $summaryLines += ""
            foreach ($area in ($toolCountsByArea.Keys | Sort-Object)) {
                $count = $toolCountsByArea[$area]
                $summaryLines += "- **${area}:** $count tools"
            }
        }
        
        # Add the complete tool list
        if ($toolListOutput.Count -gt 0) {
            $summaryLines += ""
            $summaryLines += "## Complete Tool List"
            $summaryLines += ""
            $inToolGroup = $false
            foreach ($line in $toolListOutput) {
                if ($line.Trim() -ne "") {
                    # Convert the console output format to markdown
                    if ($line -match "^(.+) \((\d+) tools\):$") {
                        # Add 2 empty lines before next group (except first group)
                        if ($inToolGroup) {
                            $summaryLines += ""
                            $summaryLines += ""
                        }
                        $areaName = $matches[1]
                        $toolCount = $matches[2]
                        $summaryLines += "### $areaName ($toolCount tools)"
                        $summaryLines += ""
                        $inToolGroup = $true
                    }
                    elseif ($line -match "^[-]+$") {
                        # Skip separator lines
                        continue
                    }
                    elseif ($line -match "^\s*•\s*(.+)$") {
                        $toolInfo = $matches[1]
                        $summaryLines += "- $toolInfo"
                    }
                    else {
                        $summaryLines += $line
                    }
                }
            }
        }
    }
    
    # Save summary to file
    $summaryFilePath = "generated/generation-summary.md"
    try {
        $summaryContent = $summaryLines -join "`n"
        $summaryContent | Out-File -FilePath $summaryFilePath -Encoding UTF8
        $summaryFileSize = [math]::Round((Get-Item $summaryFilePath).Length / 1KB, 1)
        Write-Info "  📄 $summaryFilePath (${summaryFileSize}KB) - Generation summary"
    }
    catch {
        Write-Warning "Failed to save generation summary to file: $($_.Exception.Message)"
    }
    
    Write-Success "Documentation generation complete: $totalPages pages created using C# generator with Handlebars templates"
    
    # Display console summary (detailed summary saved to generation-summary.md)
    if ($totalTools -gt 0) {
        Write-Info ""
        Write-Info "Tool Statistics:"
        Write-Info "  📊 Total tools: $totalTools"
        Write-Info "  📊 Total service areas: $totalAreas"
        
        if ($toolCountsByArea.Count -gt 0) {
            Write-Info "  📊 Tools by service area:"
            foreach ($area in ($toolCountsByArea.Keys | Sort-Object)) {
                $count = $toolCountsByArea[$area]
                Write-Info "     • ${area}: $count tools"
            }
        }
        
        # Display the complete tool list
        if ($toolListOutput.Count -gt 0) {
            Write-Info ""
            Write-Info "Complete Tool List:"
            foreach ($line in $toolListOutput) {
                if ($line.Trim() -ne "") {
                    Write-Info $line
                }
            }
        }
    }

} catch {
    Write-Error "Documentation generation failed: $($_.Exception.Message)"
    Write-Error "Error details: $($_.ScriptStackTrace)"
    Stop-Transcript
    exit 1
}

Write-Host "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] Generation complete" -ForegroundColor Green
Stop-Transcript
