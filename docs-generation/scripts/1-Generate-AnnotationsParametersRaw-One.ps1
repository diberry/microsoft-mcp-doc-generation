#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Generates annotations, parameters, and raw tool files for a single tool

.DESCRIPTION
    Similar to GenerateExamplePrompt-One.ps1 but for the base generation pipeline.
    Generates and validates annotations, parameters, and raw tool files for a single Azure MCP tool.
    Useful for quick testing and debugging of the annotation/parameter/raw pipeline.
    
    Steps:
    1. Filters cli-output.json to include only the specified tool
    2. Generates annotations for that tool
    3. Generates parameters for that tool
    4. Generates raw tool file for that tool
    5. Shows all output files

.PARAMETER ToolCommand
    The tool command to test (e.g., "keyvault secret create", "storage account list")
    Can also be a tool family/namespace prefix to generate all tools in that family (e.g., "keyvault", "storage")

.PARAMETER OutputPath
    Path to the generated directory (default: ../generated from docs-generation root)

.PARAMETER SkipValidation
    Skip the validation step (only generate files)

.EXAMPLE
    ./Generate-AnnotationsParametersRaw-One.ps1 -ToolCommand "keyvault secret create"  # Single tool
    ./Generate-AnnotationsParametersRaw-One.ps1 -ToolCommand "storage"                      # All storage tools
    ./Generate-AnnotationsParametersRaw-One.ps1 -ToolCommand "acr registry list" -SkipValidation
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$ToolCommand,
    
    [string]$OutputPath = "../../generated",
    
    [switch]$SkipValidation,

    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

# Import shared logging and normalization helpers
. "$PSScriptRoot\Shared-Functions.ps1"

function Get-CommandFromFile {
    param([string]$FilePath)

    try {
        $lines = Get-Content -Path $FilePath -TotalCount 50 -ErrorAction Stop
    } catch {
        return $null
    }

    foreach ($line in $lines) {
        if ($line -match "^#\s*azmcp\s+(.+)$") {
            return $matches[1].Trim()
        }

        if ($line -match "^\s*<!--\s*@mcpcli\s+(.+?)\s*-->") {
            return $matches[1].Trim()
        }
    }

    return $null
}

try {
    Write-Divider
    Write-Progress "Single Tool Annotations/Parameters/Raw Generation Test"
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

    # Run Annotations generation
    Write-Divider
    Write-Progress "Generating annotations..."
    Write-Divider
    Write-Host ""
    
    $csharpGeneratorDir = Join-Path $docsGenDir "CSharpGenerator"
    Push-Location $csharpGeneratorDir
    try {
        $noBuildArg = if ($SkipBuild) { "--no-build" } else { "" }
        & dotnet run --configuration Release $noBuildArg -- generate-docs $filteredOutputFile $outputDir --annotations --version $cliVersion
        if ($LASTEXITCODE -ne 0) {
            throw "Annotations generation failed"
        }
    } finally {
        Pop-Location
    }

    Write-Host ""
    Write-Divider

    # Run Parameters generation
    Write-Divider
    Write-Progress "Generating parameters..."
    Write-Divider
    Write-Host ""
    
    Push-Location $csharpGeneratorDir
    try {
        $noBuildArg = if ($SkipBuild) { "--no-build" } else { "" }
        & dotnet run --configuration Release $noBuildArg -- generate-docs $filteredOutputFile $outputDir --parameters --version $cliVersion
        if ($LASTEXITCODE -ne 0) {
            throw "Parameters generation failed"
        }
    } finally {
        Pop-Location
    }

    Write-Host ""
    Write-Divider

    # Run Raw tool generation (filtered CLI output)
    Write-Divider
    Write-Progress "Generating raw tool files..."
    Write-Divider
    Write-Host ""

    $rawToolsDir = Join-Path $outputDir "tools-raw"
    Push-Location $docsGenDir
    try {
        $rawArgs = @(
            "--project", "ToolGeneration_Raw",
            "--configuration", "Release"
        )
        if ($SkipBuild) { $rawArgs += "--no-build" }
        $rawArgs += @(
            "--",
            $filteredOutputFile,
            $rawToolsDir,
            $cliVersion
        )

        Write-Info "Command: dotnet run $($rawArgs -join ' ')"
        dotnet run @rawArgs
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Raw tool generation reported issues (may be expected for some tools)"
        }
    } finally {
        Pop-Location
    }

    Write-Host ""
    Write-Divider

    # Show generated files
    if ($matchingTools.Count -eq 1) {
        # Single tool
        $singleToolCommand = $matchingTools[0].command
        $baseFileName = Get-ToolBaseFileName $singleToolCommand
        
        $annotationsFile = Join-Path $outputDir "annotations/$baseFileName-annotations.md"
        $parametersFile = Join-Path $outputDir "parameters/$baseFileName-parameters.md"
        $rawToolFile = Join-Path $outputDir "tools-raw/$baseFileName.md"
    } else {
        # Multiple tools in family - show first few
        $annotationsFile = "(multiple files)"
        $parametersFile = "(multiple files)"
        $rawToolFile = "(multiple files)"
    }
    
    Write-Progress "Generated Files:"
    Write-Host ""
    
    if ($matchingTools.Count -eq 1) {
        # Single tool - validate exact files
        if (Test-Path $annotationsFile) {
            Write-Success "✓ Annotations: $annotationsFile"
            $lineCount = (Get-Content $annotationsFile).Count
            Write-Info "  Lines: $lineCount"
        } else {
            Write-Warning "✗ Annotations not found: $annotationsFile"
        }
        
        if (Test-Path $parametersFile) {
            Write-Success "✓ Parameters: $parametersFile"
            $lineCount = (Get-Content $parametersFile).Count
            Write-Info "  Lines: $lineCount"
        } else {
            Write-Warning "✗ Parameters not found: $parametersFile"
        }
        
        if (Test-Path $rawToolFile) {
            Write-Success "✓ Raw tool: $rawToolFile"
            $lineCount = (Get-Content $rawToolFile).Count
            Write-Info "  Lines: $lineCount"
        } else {
            Write-Warning "✗ Raw tool not found: $rawToolFile"
        }
    } else {
        # Multiple tools - count files in directories
        $annotationsDir = Join-Path $outputDir "annotations"
        $parametersDir = Join-Path $outputDir "parameters"
        $rawToolsDir = Join-Path $outputDir "tools-raw"
        
        $annotationsCount = if (Test-Path $annotationsDir) { (Get-ChildItem $annotationsDir -Filter "*.md" | Measure-Object).Count } else { 0 }
        $parametersCount = if (Test-Path $parametersDir) { (Get-ChildItem $parametersDir -Filter "*.md" | Measure-Object).Count } else { 0 }
        $rawToolCount = if (Test-Path $rawToolsDir) { (Get-ChildItem $rawToolsDir -Filter "*.md" | Measure-Object).Count } else { 0 }
        
        if ($annotationsCount -gt 0) {
            Write-Success "✓ Annotations: $annotationsCount files in $annotationsDir"
        } else {
            Write-Warning "✗ No annotations found"
        }
        
        if ($parametersCount -gt 0) {
            Write-Success "✓ Parameters: $parametersCount files in $parametersDir"
        } else {
            Write-Warning "✗ No parameters found"
        }
        
        if ($rawToolCount -gt 0) {
            Write-Success "✓ Raw tools: $rawToolCount files in $rawToolsDir"
        } else {
            Write-Warning "✗ No raw tools found"
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
            if (-not (Test-Path $annotationsFile)) {
                Write-Warning "✗ Annotations file not found"
                $allFound = $false
            } else {
                Write-Success "✓ Annotations file exists"
            }
            
            if (-not (Test-Path $parametersFile)) {
                Write-Warning "✗ Parameters file not found"
                $allFound = $false
            } else {
                Write-Success "✓ Parameters file exists"
            }
            
            if (-not (Test-Path $rawToolFile)) {
                Write-Warning "✗ Raw tool file not found"
                $allFound = $false
            } else {
                Write-Success "✓ Raw tool file exists"
            }
            
            if ($allFound) {
                Write-Success "✓ All files generated successfully"
            } else {
                Write-Warning "Some files were not generated"
            }
        } else {
            # Multiple tools validation
            Write-Success "✓ Generated $($matchingTools.Count) annotation, parameter, and raw tool files"
            Write-Info "  Verify files in:"
            Write-Info "    - $annotationsDir"
            Write-Info "    - $parametersDir"
            Write-Info "    - $rawToolsDir"
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
