#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Generates composed and AI-improved tool files for a single tool

.DESCRIPTION
    Similar to GenerateExamplePrompt-One.ps1 and Generate-AnnotationsParametersRaw-One.ps1
    but for tool generation and AI improvements.
    
    Generates and tests tool generation/composition and AI improvements for a single Azure MCP tool.
    Useful for quick testing and debugging of the tool composition/improvement pipeline.
    
    Steps:
    1. Filters cli-output.json to include only the specified tool
    2. Generates composed tool file (replaces placeholders with annotations/parameters/examples)
    3. Optionally generates AI-improved tool file
    4. Shows all output files

.PARAMETER ToolCommand
    The tool command to test (e.g., "keyvault secret create", "storage account list")
    Can also be a tool family/namespace prefix to generate all tools in that family (e.g., "keyvault", "storage")

.PARAMETER OutputPath
    Path to the generated directory (default: ../generated from docs-generation root)

.PARAMETER SkipComposed
    Skip composed tool generation (use existing composed files)

.PARAMETER SkipImproved
    Skip AI-improved tool generation (only generate composed)

.PARAMETER MaxTokens
    Maximum tokens for AI improvements (default: 8000)

.PARAMETER SkipValidation
    Skip the validation step (only generate files)

.EXAMPLE
    ./Generate-ToolGenerationAndAIImprovements-One.ps1 -ToolCommand "keyvault secret create"  # Single tool
    ./Generate-ToolGenerationAndAIImprovements-One.ps1 -ToolCommand "storage"                      # All storage tools
    ./Generate-ToolGenerationAndAIImprovements-One.ps1 -ToolCommand "acr registry list" -SkipImproved
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$ToolCommand,
    
    [string]$OutputPath = "../../generated",
    
    [switch]$SkipComposed = $false,
    [switch]$SkipImproved = $false,
    [int]$MaxTokens = 8000,
    [switch]$SkipValidation,

    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

# Import shared logging and normalization helpers
. "$PSScriptRoot\Shared-Functions.ps1"

try {
    Write-Divider
    Write-Progress "Single Tool Generation & AI Improvements Test"
    Write-Info "Tool: $ToolCommand"
    Write-Info "Started: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    Write-Divider
    Write-Host ""

    $scriptDir = $PSScriptRoot
    $docsGenDir = Split-Path -Parent $scriptDir
    $outputDir = Resolve-OutputDir $OutputPath

    Write-Info "Output directory: $outputDir"
    Write-Host ""

    # Get CLI version
    $cliVersion = Get-CliVersion $outputDir
    Write-Info "CLI Version: $cliVersion"
    Write-Host ""

    # Load full CLI output
    $cli = Get-CliOutput $outputDir

    # Normalize and find matching tools
    $ToolCommand = Normalize-ToolCommand $ToolCommand
    $matchingTools = Find-MatchingTools $cli.AllTools $ToolCommand
    Write-Host ""

    # Create filtered CLI file in temp directory
    $temp = New-FilteredCliFile $cli.CliOutput $matchingTools
    $tempDir = $temp.TempDir
    $filteredOutputFile = $temp.FilteredFile
    Write-Host ""

    # Build .NET packages (skip if already built by preflight)
    Invoke-DotnetBuild -SkipBuild:$SkipBuild

    # Run Composed tool generation
    if (-not $SkipComposed) {
        Write-Divider
        Write-Progress "Generating composed tool file..."
        Write-Divider
        Write-Host ""
        
        $rawToolsDir = Join-Path $outputDir "tools-raw"
        $composedToolsDir = Join-Path $outputDir "tools-composed"
        $annotationsDir = Join-Path $outputDir "annotations"
        $parametersDir = Join-Path $outputDir "parameters"
        $examplePromptsDir = Join-Path $outputDir "example-prompts"

        # Check for missing prerequisite directories and warn
        $missingDirs = @()
        if (-not (Test-Path $annotationsDir)) {
            $missingDirs += "annotations (run Step 1)"
        }
        if (-not (Test-Path $parametersDir)) {
            $missingDirs += "parameters (run Step 1)"
        }
        if (-not (Test-Path $examplePromptsDir)) {
            $missingDirs += "example-prompts (run Step 2)"
        }
        
        if ($missingDirs.Count -gt 0) {
            Write-Warning "Missing prerequisite files from earlier steps:"
            foreach ($dir in $missingDirs) {
                Write-Host "  - $dir"
            }
            Write-Host "  Composition will complete but content files will be missing from the output."
            Write-Host ""
        }

        Push-Location $docsGenDir
        try {
            $composedArgs = @(
                "--project", "ToolGeneration_Composed",
                "--configuration", "Release"
            )
            if ($SkipBuild) { $composedArgs += "--no-build" }
            $composedArgs += @(
                "--",
                $rawToolsDir,
                $composedToolsDir,
                $annotationsDir,
                $parametersDir,
                $examplePromptsDir
            )
            
            & dotnet run @composedArgs
            if ($LASTEXITCODE -ne 0) {
                throw "Composed tool generation failed"
            }
        } finally {
            Pop-Location
        }

        Write-Host ""
        Write-Divider
    } else {
        Write-Warning "Skipped composed tool generation"
    }

    # Run AI-improved tool generation
    if (-not $SkipImproved) {
        Write-Divider
        Write-Progress "Generating AI-improved tool file..."
        Write-Divider
        Write-Host ""
        
        $composedToolsDir = Join-Path $outputDir "tools-composed"
        $improvedToolsDir = Join-Path $outputDir "tools"

        # Check for missing composed tools directory
        if (-not (Test-Path $composedToolsDir)) {
            Write-Warning "Composed tools directory not found: $composedToolsDir"
            Write-Host "  Cannot run AI improvement without composed tools."
            Write-Host "  Run Step 3 without -SkipComposed to generate composed tools first."
            Write-Host ""
        } else {
            Push-Location $docsGenDir
            try {
                $improvedArgs = @(
                    "--project", "ToolGeneration_Improved",
                    "--configuration", "Release"
                )
                if ($SkipBuild) { $improvedArgs += "--no-build" }
                $improvedArgs += @(
                    "--",
                    $composedToolsDir,
                    $improvedToolsDir,
                    $MaxTokens
                )
                
                & dotnet run @improvedArgs
                if ($LASTEXITCODE -ne 0) {
                    throw "AI-improved tool generation failed"
                }
            } finally {
                Pop-Location
            }
        }

        Write-Host ""
        Write-Divider
    } else {
        Write-Warning "Skipped AI-improved tool generation"
    }

    # Show generated files
    Write-Progress "Generated Files:"
    Write-Host ""
    
    if ($matchingTools.Count -eq 1) {
        # Single tool
        $singleToolCommand = $matchingTools[0].command
        $baseFileName = Get-ToolBaseFileName $singleToolCommand
        
        $composedToolFile = Join-Path $outputDir "tools-composed/$baseFileName.md"
        $improvedToolFile = Join-Path $outputDir "tools/$baseFileName.md"
        
        if (Test-Path $composedToolFile) {
            Write-Success "✓ Composed tool: $composedToolFile"
            $lineCount = (Get-Content $composedToolFile).Count
            Write-Info "  Lines: $lineCount"
        } else {
            Write-Warning "✗ Composed tool not found: $composedToolFile"
        }
        
        if (Test-Path $improvedToolFile) {
            Write-Success "✓ AI-improved tool: $improvedToolFile"
            $lineCount = (Get-Content $improvedToolFile).Count
            Write-Info "  Lines: $lineCount"
        } else {
            Write-Warning "✗ AI-improved tool not found: $improvedToolFile"
        }
    } else {
        # Multiple tools - count files in directories
        $composedToolsDir = Join-Path $outputDir "tools-composed"
        $improvedToolsDir = Join-Path $outputDir "tools"
        
        $composedCount = if (Test-Path $composedToolsDir) { (Get-ChildItem $composedToolsDir -Filter "*.md" | Measure-Object).Count } else { 0 }
        $improvedCount = if (Test-Path $improvedToolsDir) { (Get-ChildItem $improvedToolsDir -Filter "*.md" | Measure-Object).Count } else { 0 }
        
        if ($composedCount -gt 0) {
            Write-Success "✓ Composed tools: $composedCount files in $composedToolsDir"
        } else {
            Write-Warning "✗ No composed tools found"
        }
        
        if (-not $SkipImproved -and $improvedCount -gt 0) {
            Write-Success "✓ AI-improved tools: $improvedCount files in $improvedToolsDir"
        } elseif (-not $SkipImproved) {
            Write-Warning "✗ No AI-improved tools found"
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
            
            if (-not $SkipComposed) {
                if (-not (Test-Path $composedToolFile)) {
                    Write-Warning "✗ Composed tool file not found"
                    $allFound = $false
                } else {
                    Write-Success "✓ Composed tool file exists"
                }
            }
            
            if (-not $SkipImproved) {
                if (-not (Test-Path $improvedToolFile)) {
                    Write-Warning "✗ AI-improved tool file not found"
                    $allFound = $false
                } else {
                    Write-Success "✓ AI-improved tool file exists"
                }
            }
            
            if ($allFound) {
                Write-Success "✓ All files generated successfully"
            } else {
                Write-Warning "Some files were not generated"
            }
        } else {
            # Multiple tools validation
            Write-Success "✓ Generated $($matchingTools.Count) composed and improved tool files"
            Write-Info "  Verify files in:"
            Write-Info "    - $composedToolsDir"
            if (-not $SkipImproved) {
                Write-Info "    - $improvedToolsDir"
            }
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
    
    # Cleanup temp directory
    Remove-TempDir $tempDir

} catch {
    Write-Host ""
    Write-Divider
    Write-ErrorMessage "Test failed: $($_.Exception.Message)"
    Write-ErrorMessage $_.ScriptStackTrace
    Write-Divider
    
    # Cleanup temp directory
    Remove-TempDir (Join-Path $PSScriptRoot "temp")
    
    exit 1
}
