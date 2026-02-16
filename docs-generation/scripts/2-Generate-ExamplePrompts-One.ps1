#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Tests example prompt generation and validation for a single tool or family

.DESCRIPTION
    Generates and validates example prompts for a single Azure MCP tool or tool family.
    Useful for quick testing and debugging of the prompt generation pipeline.
    
    Steps:
    1. Filters cli-output.json to include only the specified tool
    2. Generates example prompts for that tool using ExamplePromptGeneratorStandalone
    3. Validates the generated prompts using ExamplePromptValidator
    4. Shows all output files (input prompt, raw output, example prompts, validation)

.PARAMETER ToolCommand
    The tool command to test (e.g., "keyvault secret create", "storage account list")
    Can also be a tool family/namespace prefix to generate all tools in that family (e.g., "keyvault", "storage")

.PARAMETER OutputPath
    Path to the generated directory (default: ../generated from docs-generation root)

.PARAMETER SkipValidation
    Skip the validation step (only generate prompts)

.EXAMPLE
    ./2-Generate-ExamplePrompts-One.ps1 -ToolCommand "keyvault secret create"  # Single tool
    ./2-Generate-ExamplePrompts-One.ps1 -ToolCommand "storage"                      # All storage tools
    ./2-Generate-ExamplePrompts-One.ps1 -ToolCommand "acr registry list" -SkipValidation
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$ToolCommand,
    
    [string]$OutputPath = "../../generated",
    
    [switch]$SkipValidation,

    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

function Write-Info { param([string]$Message) Write-Host "INFO: $Message" -ForegroundColor Cyan }
function Write-Success { param([string]$Message) Write-Host "SUCCESS: $Message" -ForegroundColor Green }
function Write-Warning { param([string]$Message) Write-Host "WARNING: $Message" -ForegroundColor Yellow }
function Write-Error { param([string]$Message) Write-Host "ERROR: $Message" -ForegroundColor Red }
function Write-Progress { param([string]$Message) Write-Host "PROGRESS: $Message" -ForegroundColor Magenta }
function Write-Divider { Write-Host ("═" * 80) -ForegroundColor DarkGray }

try {
    Write-Divider
    Write-Progress "Single Tool Prompt Generation & Validation Test"
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

    # Get CLI version
    $versionOutputFile = Join-Path $outputDir "cli/cli-version.json"
    $cliVersion = "unknown"
    if (Test-Path $versionOutputFile) {
        $versionContent = Get-Content $versionOutputFile -Raw
        if ($versionContent.Trim().StartsWith('{')) {
            $versionData = $versionContent | ConvertFrom-Json
            $cliVersion = $versionData.version ?? $versionData.Version ?? "unknown"
        } else {
            $cliVersion = $versionContent.Trim()
        }
    }
    Write-Info "CLI Version: $cliVersion"
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
        $requiredParams = @($tool.option | Where-Object { $_.required })
        Write-Info "  Required parameters: $($requiredParams.Count)"
        foreach ($param in $requiredParams) {
            Write-Info "    - $($param.name)"
        }
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

    # Create a filtered CLI output with just this tool
    $tempDir = Join-Path $scriptDir "temp"
    New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
    
    $filteredOutput = @{
        version = $cliOutput.version
        results = @($matchingTools)
    }
    
    $filteredOutputFile = Join-Path $tempDir "cli-output-single-tool.json"
    $filteredOutput | ConvertTo-Json -Depth 10 | Set-Content $filteredOutputFile -Encoding UTF8
    Write-Info "Created filtered CLI output: $filteredOutputFile"
    Write-Host ""

    # Build .NET packages (skip if already built by preflight)
    if (-not $SkipBuild) {
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
    } else {
        Write-Info "Skipping build (already built by preflight)"
    }

    # Run ExamplePromptGeneratorStandalone
    Write-Divider
    Write-Progress "Generating example prompts..."
    Write-Divider
    Write-Host ""
    
    $generatorProject = Join-Path $docsGenDir "ExamplePromptGeneratorStandalone"
    $noBuildArg = if ($SkipBuild) { "--no-build" } else { "" }
    & dotnet run --project $generatorProject --configuration Release $noBuildArg -- $filteredOutputFile $outputDir $cliVersion
    
    if ($LASTEXITCODE -ne 0) {
        throw "Example prompts generation failed"
    }

    Write-Host ""
    Write-Divider

    # Show generated files
    if ($matchingTools.Count -eq 1) {
        $singleToolCommand = $matchingTools[0].command
        $commandSegments = $singleToolCommand -split ' '
        $baseFileName = $commandSegments -join '-'
        
        $inputPromptFile = Join-Path $outputDir "example-prompts-prompts/$baseFileName-input-prompt.md"
        $rawOutputFile = Join-Path $outputDir "example-prompts-raw-output/$baseFileName-raw-output.txt"
        $examplePromptsFile = Join-Path $outputDir "example-prompts/$baseFileName-example-prompts.md"
    }
    
    Write-Progress "Generated Files:"
    Write-Host ""
    
    if ($matchingTools.Count -eq 1) {
        if (Test-Path $inputPromptFile) {
            Write-Success "✓ Input prompt: $inputPromptFile"
        } else {
            Write-Warning "✗ Input prompt not found: $inputPromptFile"
        }
        
        if (Test-Path $rawOutputFile) {
            Write-Success "✓ Raw AI output: $rawOutputFile"
            Write-Info "  Showing first 20 lines:"
            Get-Content $rawOutputFile | Select-Object -First 20 | ForEach-Object { Write-Host "    $_" -ForegroundColor DarkGray }
            $lineCount = (Get-Content $rawOutputFile).Count
            if ($lineCount -gt 20) {
                Write-Info "    ... ($($lineCount - 20) more lines)"
            }
        } else {
            Write-Warning "✗ Raw output not found: $rawOutputFile"
        }
        
        Write-Host ""
        
        if (Test-Path $examplePromptsFile) {
            Write-Success "✓ Example prompts: $examplePromptsFile"
            Write-Info "  Content:"
            Get-Content $examplePromptsFile | ForEach-Object { Write-Host "    $_" -ForegroundColor DarkGray }
        } else {
            Write-Warning "✗ Example prompts not found: $examplePromptsFile"
        }
    } else {
        $inputDir = Join-Path $outputDir "example-prompts-prompts"
        $rawDir = Join-Path $outputDir "example-prompts-raw-output"
        $promptsDir = Join-Path $outputDir "example-prompts"
        
        $inputCount = if (Test-Path $inputDir) { (Get-ChildItem $inputDir -Filter "*.md" | Measure-Object).Count } else { 0 }
        $rawCount = if (Test-Path $rawDir) { (Get-ChildItem $rawDir -Filter "*.txt" | Measure-Object).Count } else { 0 }
        $promptCount = if (Test-Path $promptsDir) { (Get-ChildItem $promptsDir -Filter "*.md" | Measure-Object).Count } else { 0 }
        
        Write-Success "✓ Input prompts: $inputCount files in $inputDir"
        Write-Success "✓ Raw outputs: $rawCount files in $rawDir"
        Write-Success "✓ Example prompts: $promptCount files in $promptsDir"
    }
    
    Write-Host ""

    # Run validation if not skipped
    if (-not $SkipValidation) {
        Write-Divider
        Write-Progress "Validating example prompts..."
        Write-Divider
        Write-Host ""
        
        if ($matchingTools.Count -eq 1) {
            $validatorScript = Join-Path $docsGenDir "ExamplePromptValidator/scripts/Validate-ExamplePrompts-RequiredParams.ps1"
            & $validatorScript -OutputPath $OutputPath -ToolCommand $singleToolCommand
            
            if ($LASTEXITCODE -ne 0) {
                Write-Warning "Validation completed with issues"
            } else {
                Write-Success "✓ Validation completed successfully"
            }
            
            Write-Host ""
            
            # Show validation file
            $validationFile = Join-Path $outputDir "example-prompts-validation/$baseFileName-validation.md"
            if (Test-Path $validationFile) {
                Write-Success "✓ Validation report: $validationFile"
                Write-Info "  Content:"
                Get-Content $validationFile | ForEach-Object { Write-Host "    $_" -ForegroundColor DarkGray }
            } else {
                Write-Warning "✗ Validation report not found: $validationFile"
            }
        } else {
            Write-Warning "Skipping validation for tool families (use single tool for validation)"
        }
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
    Remove-Item -Path $tempDir -Recurse -Force -ErrorAction SilentlyContinue

} catch {
    Write-Host ""
    Write-Divider
    Write-Error "Test failed: $($_.Exception.Message)"
    Write-Divider
    
    # Cleanup temp directory
    $tempDir = Join-Path $scriptDir "temp"
    Remove-Item -Path $tempDir -Recurse -Force -ErrorAction SilentlyContinue
    
    exit 1
}
