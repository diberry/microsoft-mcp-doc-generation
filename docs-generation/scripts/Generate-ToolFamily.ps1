#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Complete tool family (namespace) documentation generator - single service/family

.DESCRIPTION
    Orchestrates all generation steps to produce a complete tool family file.
    This script guides you through the entire pipeline for a single tool family:
    
    1. Generates annotations, parameters, and raw tool files
    2. Generates example prompts for each tool
    3. Generates composed and AI-improved tool files
    4. Generates tool family metadata, related content, and final file
    5. Optionally generates horizontal article
    
    The result is a complete, standalone documentation file for the tool family
    with all content integrated and AI-enhanced.
    
    IMPORTANT - Step Dependencies:
    =============================
    The steps have sequential dependencies. Each step requires outputs from previous steps:
    
    - Step 1 (Annotations/Parameters/Raw): No dependencies. Creates base files.
                                           Output: annotations/, parameters/, tools-raw/
    
    - Step 2 (Example Prompts): Independent, can run after Step 1.
                                Output: multi-page/example-prompts/
    
    - Step 3 (Composed/Improved): REQUIRES Step 1 (annotations, parameters) + Step 2 (prompts)
                                  Output: tools-composed/, tools-ai-improved/
    
    - Step 4 (Family Assembly): REQUIRES Step 3 outputs
                                Output: tool-family/
    
    - Step 5 (Horizontal Articles): Can run independently but should follow other steps
                                    Output: horizontal-articles/
    
    To get complete output, run at minimum: Steps 1, 2, 3
    To get everything: Steps 1, 2, 3, 4 (and optionally 5)
    
    Running Step 3 alone will produce files with placeholder content for missing annotations/parameters/prompts.

.PARAMETER ToolFamily
    The tool family/namespace to generate (e.g., "keyvault", "storage", "acr")
    This will generate all tools in that family.

.PARAMETER OutputPath
    Path to the generated directory (default: ../generated from docs-generation root)

.PARAMETER MaxTokens
    Maximum tokens for AI improvements (default: 8000)

.PARAMETER GenerateHorizontalArticle
    Also generate a horizontal article (how-to guide) for the service (default: $true)

.PARAMETER SkipExamplePrompts
    Skip example prompt generation (default: $false)

.PARAMETER SkipAIImprovements
    Skip AI improvements, only generate composed versions (default: $false)

.PARAMETER SkipValidation
    Skip file validation steps (default: $false)

.PARAMETER UseTextTransformation
    Apply text transformations to horizontal article output (default: $true)

.PARAMETER SkipBuild
    Skip building the .NET solution (default: $false). 
    Set to $true when the orchestrator (start.sh) has already built the solution.

.PARAMETER Steps
    Array of step numbers to run (default: @(1,2,3,4,5) - all steps)
    Example: -Steps @(1,3) to run only steps 1 and 3
    Example: -Steps @(1) to run only step 1
    Example: -Steps @(1,2,3) to run steps 1 through 3
    
    WARNING: Running steps out of order or without dependencies will produce incomplete output.
    Recommended combinations:
      @(1) - Fast base generation (~1 min)
      @(1,2) - Add example prompts (~10-15 min)
      @(1,2,3) - Add composed/improved tools (~15-20 min) - RECOMMENDED minimum
      @(1,2,3,4) - Add family file (~20-25 min)
      @(1,2,3,4,5) - Add everything (~25-30 min) - FULL PIPELINE

.PARAMETER Step1Only
    (Legacy) Run only Step 1 - use -Steps @(1) instead

.PARAMETER Step1And2
    (Legacy) Run only Steps 1-2 - use -Steps @(1,2) instead

.PARAMETER Step1To3
    (Legacy) Run only Steps 1-3 - use -Steps @(1,2,3) instead

.PARAMETER Step1To4
    (Legacy) Run only Steps 1-4 - use -Steps @(1,2,3,4) instead

.EXAMPLE
    # PowerShell: Full pipeline (all 5 steps, default)
    ./Generate-ToolFamily.ps1 -ToolFamily "keyvault"
    
    # PowerShell: Full pipeline with custom token limit
    ./Generate-ToolFamily.ps1 -ToolFamily "storage" -MaxTokens 12000
    
    # PowerShell: Run only Step 1 (fast, ~1 minute)
    ./Generate-ToolFamily.ps1 -ToolFamily "advisor" -Steps @(1)
    
    # PowerShell: Run Steps 1-2 (adds example prompts, ~10-15 minutes)
    ./Generate-ToolFamily.ps1 -ToolFamily "advisor" -Steps @(1,2)
    
    # PowerShell: Run Steps 1-3 (adds composed/improved tools, ~15-20 minutes) - RECOMMENDED MINIMUM
    ./Generate-ToolFamily.ps1 -ToolFamily "advisor" -Steps @(1,2,3)
    
    # PowerShell: Run Steps 1-4 (adds family assembly, ~20-25 minutes)
    ./Generate-ToolFamily.ps1 -ToolFamily "advisor" -Steps @(1,2,3,4)
    
    # PowerShell: Run all steps (all 5, ~25-30 minutes)
    ./Generate-ToolFamily.ps1 -ToolFamily "advisor" -Steps @(1,2,3,4,5)
    
    # PowerShell: Run specific steps (e.g., only steps 1 and 4) - Will have incomplete output!
    ./Generate-ToolFamily.ps1 -ToolFamily "advisor" -Steps @(1,4)
    
    # PowerShell: Skip AI improvements, only generate composed versions
    ./Generate-ToolFamily.ps1 -ToolFamily "acr" -SkipAIImprovements
    
    # PowerShell: Skip horizontal article generation
    ./Generate-ToolFamily.ps1 -ToolFamily "aks" -GenerateHorizontalArticle $false
    
    # Bash wrapper: Run all steps (default) - RECOMMENDED
    ./generate-tool-family.sh advisor
    
    # Bash wrapper: Run only step 1 (fast test, ~1 minute)
    ./generate-tool-family.sh advisor 1
    
    # Bash wrapper: Run steps 1 and 2 (with prompts, ~10-15 minutes)
    ./generate-tool-family.sh advisor 1,2
    
    # Bash wrapper: Run steps 1, 2, and 3 (complete tools, ~15-20 minutes) - RECOMMENDED MINIMUM
    ./generate-tool-family.sh advisor 1,2,3
    
    # Bash wrapper: Run all steps (all 5, ~25-30 minutes) - RECOMMENDED FOR FULL PIPELINE
    ./generate-tool-family.sh advisor 1,2,3,4,5
    
    # Bash wrapper: Run steps 1, 3, and 5 (will have incomplete output!)
    ./generate-tool-family.sh advisor 1,3,5
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$ToolFamily,
    
    [string]$OutputPath = "../generated",
    
    [int]$MaxTokens = 8000,
    
    [bool]$GenerateHorizontalArticle = $true,
    
    [bool]$SkipExamplePrompts = $false,
    
    [bool]$SkipAIImprovements = $false,
    
    [bool]$SkipValidation = $false,

    [bool]$UseTextTransformation = $true,
    
    [bool]$SkipBuild = $false,
    
    [int[]]$Steps = @(1, 2, 3, 4, 5),
    
    [switch]$Step1Only = $false,
    
    [switch]$Step1And2 = $false,
    
    [switch]$Step1To3 = $false,
    
    [switch]$Step1To4 = $false
)

$ErrorActionPreference = "Stop"

# Determine which steps to run
# Priority: If any legacy switch is used, use that; otherwise use $Steps array
$runStep1 = $false
$runStep2 = $false
$runStep3 = $false
$runStep4 = $false
$runStep5 = $false

if ($Step1Only -or $Step1And2 -or $Step1To3 -or $Step1To4) {
    # Legacy switch-based approach
    if ($Step1Only) {
        $runStep1 = $true
    } elseif ($Step1And2) {
        $runStep1 = $true
        $runStep2 = $true
    } elseif ($Step1To3) {
        $runStep1 = $true
        $runStep2 = $true
        $runStep3 = $true
    } elseif ($Step1To4) {
        $runStep1 = $true
        $runStep2 = $true
        $runStep3 = $true
        $runStep4 = $true
    }
} else {
    # Array-based approach (new preferred method)
    if (1 -in $Steps) { $runStep1 = $true }
    if (2 -in $Steps) { $runStep2 = $true }
    if (3 -in $Steps) { $runStep3 = $true }
    if (4 -in $Steps) { $runStep4 = $true }
    if (5 -in $Steps) { $runStep5 = $true }
}

# Override Step 5 if user explicitly set GenerateHorizontalArticle to false
if (-not $GenerateHorizontalArticle) {
    $runStep5 = $false
}

# Override Step 2 if user explicitly set SkipExamplePrompts to true
if ($SkipExamplePrompts) {
    $runStep2 = $false
}

function Write-Info { param([string]$Message) Write-Host "INFO: $Message" -ForegroundColor Cyan }
function Write-Success { param([string]$Message) Write-Host "SUCCESS: $Message" -ForegroundColor Green }
function Write-Warning { param([string]$Message) Write-Host "WARNING: $Message" -ForegroundColor Yellow }
function Write-Error { param([string]$Message) Write-Host "ERROR: $Message" -ForegroundColor Red }
function Write-Progress { param([string]$Message) Write-Host "PROGRESS: $Message" -ForegroundColor Magenta }
function Write-Divider { Write-Host ("═" * 80) -ForegroundColor DarkGray }

try {
    Write-Divider
    Write-Progress "Complete Tool Family Documentation Generator"
    Write-Info "Tool Family: $ToolFamily"
    Write-Info "Started: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    Write-Divider
    Write-Host ""

    $scriptDir = $PSScriptRoot
    $outputDir = if ([System.IO.Path]::IsPathRooted($OutputPath)) {
        $OutputPath
    } else {
        $absPath = Join-Path $scriptDir $OutputPath
        [System.IO.Path]::GetFullPath($absPath)
    }

    Write-Info "Output directory: $outputDir"
    Write-Host ""

    # Build .NET packages before running generation steps (unless skipped)
    if (-not $SkipBuild) {
        Write-Progress "Building .NET packages..."
        $solutionFile = Join-Path (Split-Path $scriptDir -Parent) "docs-generation.sln"
        if (Test-Path $solutionFile) {
            & dotnet build $solutionFile --configuration Release --verbosity quiet
            if ($LASTEXITCODE -ne 0) {
                throw ".NET build failed"
            }
            Write-Success "✓ Build succeeded"
        } else {
            Write-Warning "Solution file not found: $solutionFile"
        }
        Write-Host ""
    }

    # Run CLI analyzer for visual analysis
    Write-Divider
    Write-Progress "Running CLI Analyzer"
    Write-Divider
    Write-Host ""
    
    & "$PSScriptRoot\Invoke-CliAnalyzer.ps1" -OutputPath $OutputPath -HtmlOnly $true -SkipBuild $SkipBuild
    Write-Host ""

    # ========================================================================
    # Step 1: Generate annotations, parameters, and raw tool files
    # ========================================================================
    Write-Divider
    Write-Progress "Step 1: Annotations, Parameters, and Raw Tools"
    Write-Divider
    Write-Host ""

    if ($runStep1) {
        $step1Script = Join-Path $scriptDir "1-Generate-AnnotationsParametersRaw-One.ps1"
        if (-not (Test-Path $step1Script)) {
            throw "Step 1 script not found: $step1Script"
        }

        & $step1Script -ToolCommand $ToolFamily -OutputPath $OutputPath -SkipValidation:$SkipValidation
        if ($LASTEXITCODE -ne 0) {
            throw "Step 1 failed: Annotations, parameters, and raw tools generation"
        }

        Write-Host ""
        Write-Success "✓ Step 1 completed: Annotations, parameters, and raw tools"
        Write-Host ""
    } else {
        Write-Warning "⊘ Step 1 skipped"
        Write-Host ""
    }

    # ========================================================================
    # Step 2: Generate example prompts (optional)
    # ========================================================================
    Write-Divider
    Write-Progress "Step 2: Example Prompts"
    Write-Divider
    Write-Host ""

    if ($runStep2) {
        $step2Script = Join-Path $scriptDir "2-Generate-ExamplePrompts-One.ps1"
        if (-not (Test-Path $step2Script)) {
            Write-Warning "Step 2 script not found: $step2Script"
            Write-Warning "Skipping example prompt generation"
        } else {
            & $step2Script -ToolCommand $ToolFamily -OutputPath $OutputPath -SkipValidation:$SkipValidation
            if ($LASTEXITCODE -ne 0) {
                Write-Warning "Step 2 reported issues: Example prompt generation (continuing...)"
            } else {
                Write-Host ""
                Write-Success "✓ Step 2 completed: Example prompts"
                Write-Host ""
            }
        }
    } else {
        Write-Warning "⊘ Step 2 skipped"
        Write-Host ""
    }

    # ========================================================================
    # Step 3: Generate composed and AI-improved tool files
    # ========================================================================
    Write-Divider
    Write-Progress "Step 3: Composed and AI-Improved Tools"
    Write-Divider
    Write-Host ""

    if ($runStep3) {
        $step3Script = Join-Path $scriptDir "3-Generate-ToolGenerationAndAIImprovements-One.ps1"
        if (-not (Test-Path $step3Script)) {
            throw "Step 3 script not found: $step3Script"
        }

        $step3Params = @{
            ToolCommand = $ToolFamily
            OutputPath = $OutputPath
            MaxTokens = $MaxTokens
        }

        if ($SkipValidation) {
            $step3Params.SkipValidation = $true
        }

        if ($SkipAIImprovements) {
            $step3Params.SkipImproved = $true
        }

        & $step3Script @step3Params
        if ($LASTEXITCODE -ne 0) {
            throw "Step 3 failed: Composed and AI-improved tool generation"
        }

        Write-Host ""
        Write-Success "✓ Step 3 completed: Composed and AI-improved tools"
        Write-Host ""
    } else {
        Write-Warning "⊘ Step 3 skipped"
        Write-Host ""
    }

    # ========================================================================
    # Step 4: Generate tool family metadata, related content, and final file
    # ========================================================================
    Write-Divider
    Write-Progress "Step 4: Tool Family File Assembly"
    Write-Divider
    Write-Host ""

    if ($runStep4) {
        Write-Info "Step 4 involves Azure OpenAI API calls - this may take 1-5 minutes..."
        Write-Info "Monitor progress in the Step 4 script output below..."
        Write-Host ""

        $step4Script = Join-Path $scriptDir "4-Generate-ToolFamilyCleanup-One.ps1"
        if (-not (Test-Path $step4Script)) {
            throw "Step 4 script not found: $step4Script"
        }

        $step4Params = @{
            ToolCommand = $ToolFamily
            OutputPath = $OutputPath
            SkipMetadata = $true
            SkipRelated = $true
        }
        
        if ($SkipValidation) {
            $step4Params.SkipValidation = $true
        }

        Write-Info "Invoking: & $step4Script @step4Params"
        $step4StartTime = Get-Date
        & $step4Script @step4Params
        $step4Duration = (Get-Date) - $step4StartTime
        Write-Info "Step 4 completed in $($step4Duration.TotalSeconds)s"
        
        if ($LASTEXITCODE -ne 0) {
            throw "Step 4 failed: Tool family file assembly"
        }

        Write-Host ""
        Write-Success "✓ Step 4 completed: Tool family file assembly"
        Write-Host ""
    } else {
        Write-Warning "⊘ Step 4 skipped"
        Write-Host ""
    }

    # ========================================================================
    # Step 5: Generate horizontal article (optional)
    # ========================================================================
    if ($runStep5) {
        Write-Divider
        Write-Progress "Step 5: Horizontal Article (Optional)"
        Write-Divider
        Write-Host ""

        $step5Script = Join-Path $scriptDir "5-Generate-HorizontalArticles-One.ps1"
        if (-not (Test-Path $step5Script)) {
            Write-Warning "Step 5 script not found: $step5Script"
            Write-Warning "Skipping horizontal article generation"
        } else {
            $step5Params = @{
                ServiceArea = $ToolFamily
                OutputPath = $OutputPath
                UseTextTransformation = $UseTextTransformation
            }
            
            if ($SkipValidation) {
                $step5Params.SkipValidation = $true
            }

            & $step5Script @step5Params
            if ($LASTEXITCODE -ne 0) {
                Write-Warning "Step 5 reported issues: Horizontal article generation (continuing...)"
            } else {
                Write-Host ""
                Write-Success "✓ Step 5 completed: Horizontal article"
                Write-Host ""
            }
        }
    } else {
        Write-Warning "⊘ Step 5 skipped"
        Write-Host ""
    }

    # ========================================================================
    # Summary
    # ========================================================================
    Write-Divider
    Write-Progress "Generation Summary"
    Write-Divider
    Write-Host ""

    # Check for generated files
    $familyFileName = "$ToolFamily.md"
    $familyFile = Join-Path $outputDir "tool-family/$familyFileName"
    $horizontalFile = Join-Path $outputDir "horizontal-articles/horizontal-article-$ToolFamily.md"

    if (Test-Path $familyFile) {
        $fileSize = [math]::Round((Get-Item $familyFile).Length / 1KB, 1)
        $lineCount = (Get-Content $familyFile | Measure-Object -Line).Lines
        Write-Success "✓ Tool Family File: $familyFile"
        Write-Info "  Size: ${fileSize}KB"
        Write-Info "  Lines: $lineCount"
    } else {
        Write-Warning "✗ Tool Family File not found: $familyFile"
    }

    if ($GenerateHorizontalArticle -and (Test-Path $horizontalFile)) {
        $fileSize = [math]::Round((Get-Item $horizontalFile).Length / 1KB, 1)
        $lineCount = (Get-Content $horizontalFile | Measure-Object -Line).Lines
        Write-Success "✓ Horizontal Article: $horizontalFile"
        Write-Info "  Size: ${fileSize}KB"
        Write-Info "  Lines: $lineCount"
    } elseif ($GenerateHorizontalArticle) {
        Write-Warning "✗ Horizontal Article not found: $horizontalFile"
    }

    Write-Host ""
    Write-Success "All generation steps completed successfully!"
    Write-Info "Output directory: $outputDir"
    Write-Host ""

    Write-Divider
    Write-Success "Completed at: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    Write-Divider
    Write-Host ""

} catch {
    Write-Host ""
    Write-Divider
    Write-Error "Generation failed: $($_.Exception.Message)"
    Write-Error $_.ScriptStackTrace
    Write-Divider
    
    exit 1
}
