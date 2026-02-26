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
    
    - Step 5 (Skills Relevance): Fetches GitHub Copilot skills and ranks by relevance. Non-fatal.
                                Output: skills-relevance/
    
    - Step 6 (Horizontal Articles): Can run independently but should follow other steps
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
    Array of step numbers to run (default: @(1,2,3,4,5,6) - all steps)
    Example: -Steps @(1,3) to run only steps 1 and 3
    Example: -Steps @(1) to run only step 1
    Example: -Steps @(1,2,3) to run steps 1 through 3
    
    WARNING: Running steps out of order or without dependencies will produce incomplete output.
    Recommended combinations:
      @(1) - Fast base generation (~1 min)
      @(1,2) - Add example prompts (~10-15 min)
      @(1,2,3) - Add composed/improved tools (~15-20 min) - RECOMMENDED minimum
      @(1,2,3,4) - Add family file (~20-25 min)
      @(1,2,3,4,5,6) - Add everything (~25-30 min) - FULL PIPELINE

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
    
    # PowerShell: Run all steps (all 6, ~25-30 minutes)
    ./Generate-ToolFamily.ps1 -ToolFamily "advisor" -Steps @(1,2,3,4,5,6)
    
    # PowerShell: Run specific steps (e.g., only steps 1 and 4) - Will have incomplete output!
    ./Generate-ToolFamily.ps1 -ToolFamily "advisor" -Steps @(1,4)
    
    # PowerShell: Skip AI improvements, only generate composed versions
    ./Generate-ToolFamily.ps1 -ToolFamily "acr" -SkipAIImprovements
    
    # PowerShell: Skip horizontal article generation
    ./Generate-ToolFamily.ps1 -ToolFamily "aks" -GenerateHorizontalArticle $false
    
    # From bash (via start-only.sh or start.sh):
    #   bash start.sh compute 1          # Single namespace, step 1
    #   bash start.sh compute 1,2,3      # Single namespace, steps 1-3
    #   bash start.sh                    # All namespaces, all steps
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$ToolFamily,
    
    [string]$OutputPath = "../../generated",
    
    [int]$MaxTokens = 8000,
    
    [bool]$GenerateHorizontalArticle = $true,
    
    [bool]$SkipExamplePrompts = $false,
    
    [bool]$SkipAIImprovements = $false,
    
    [bool]$SkipValidation = $false,

    [bool]$UseTextTransformation = $true,
    
    [switch]$SkipBuild,
    
    # Accepts int array @(1,2,3) or comma-separated string "1,2,3" (for bash -File invocation)
    $Steps = @(1, 2, 3, 4, 5, 6)
)

$ErrorActionPreference = "Stop"

# Normalize $Steps: accept string "1,2,3" (from bash -File) or int[] @(1,2,3) (from PowerShell)
if ($Steps -is [string]) {
    $Steps = $Steps -split ',' | ForEach-Object { [int]$_.Trim() }
} elseif ($Steps -isnot [array]) {
    $Steps = @([int]$Steps)
}

# Determine which steps to run from $Steps array
$runStep1 = 1 -in $Steps
$runStep2 = 2 -in $Steps
$runStep3 = 3 -in $Steps
$runStep4 = 4 -in $Steps
$runStep5 = 5 -in $Steps
$runStep6 = 6 -in $Steps

# Override Step 6 if user explicitly set GenerateHorizontalArticle to false
if (-not $GenerateHorizontalArticle) {
    $runStep6 = $false
}

# Override Step 2 if user explicitly set SkipExamplePrompts to true
if ($SkipExamplePrompts) {
    $runStep2 = $false
}

# Import shared logging and normalization helpers
. "$PSScriptRoot\Shared-Functions.ps1"

try {
    # Strip \r on Windows (jq in Git Bash) and trim whitespace.
    # Note: Do NOT convert underscores to spaces here — $ToolFamily is used as-is
    # for file paths (e.g. tool-family/$ToolFamily.md). The step scripts normalize
    # internally when matching against CLI tool commands.
    if ($IsWindows -or $env:OS -eq 'Windows_NT') {
        $ToolFamily = $ToolFamily -replace '\r', ''
    }
    $ToolFamily = $ToolFamily.Trim()

    Write-Divider
    Write-Progress "Complete Tool Family Documentation Generator"
    Write-Info "Tool Family: $ToolFamily"
    Write-Info "Started: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    Write-Divider
    Write-Host ""

    $scriptDir = $PSScriptRoot
    $docsGenDir = Split-Path -Parent $scriptDir
    $outputDir = Resolve-OutputDir $OutputPath

    Write-Info "Output directory: $outputDir"
    Write-Host ""

    # Build .NET packages before running generation steps (unless skipped)
    Invoke-DotnetBuild -SkipBuild:$SkipBuild

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

        & $step1Script -ToolCommand $ToolFamily -OutputPath $OutputPath -SkipValidation:$SkipValidation -SkipBuild:$SkipBuild
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
            & $step2Script -ToolCommand $ToolFamily -OutputPath $OutputPath -SkipValidation:$SkipValidation -SkipBuild:$SkipBuild
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

        if ($SkipBuild) {
            $step3Params.SkipBuild = $true
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

        if ($SkipBuild) {
            $step4Params.SkipBuild = $true
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
    # Step 5: Generate GitHub Copilot skills relevance report (optional)
    # ========================================================================
    Write-Divider
    Write-Progress "Step 5: GitHub Copilot Skills Relevance"
    Write-Divider
    Write-Host ""

    if ($runStep5) {
        $step5Script = Join-Path $scriptDir "5-Generate-SkillsRelevance-One.ps1"
        if (-not (Test-Path $step5Script)) {
            Write-Warning "Step 5 script not found: $step5Script"
            Write-Warning "Skipping skills relevance generation"
        } else {
            $step5Params = @{
                ServiceArea = $ToolFamily
                OutputPath  = $OutputPath
            }

            if ($SkipBuild) {
                $step5Params.SkipBuild = $true
            }

            & $step5Script @step5Params
            if ($LASTEXITCODE -ne 0) {
                Write-Warning "Step 5 reported issues: Skills relevance generation (continuing...)"
            } else {
                Write-Host ""
                Write-Success "✓ Step 5 completed: GitHub Copilot skills relevance"
                Write-Host ""
            }
        }
    } else {
        Write-Warning "⊘ Step 5 skipped"
        Write-Host ""
    }

    # ========================================================================
    # Step 6: Generate horizontal article (optional)
    # ========================================================================
    if ($runStep6) {
        Write-Divider
        Write-Progress "Step 6: Horizontal Article (Optional)"
        Write-Divider
        Write-Host ""

        $step6Script = Join-Path $scriptDir "6-Generate-HorizontalArticles-One.ps1"
        if (-not (Test-Path $step6Script)) {
            Write-Warning "Step 6 script not found: $step6Script"
            Write-Warning "Skipping horizontal article generation"
        } else {
            $step6Params = @{
                ServiceArea = $ToolFamily
                OutputPath = $OutputPath
                UseTextTransformation = $UseTextTransformation
            }
            
            if ($SkipValidation) {
                $step6Params.SkipValidation = $true
            }

            if ($SkipBuild) {
                $step6Params.SkipBuild = $true
            }

            & $step6Script @step6Params
            if ($LASTEXITCODE -ne 0) {
                Write-Warning "Step 6 reported issues: Horizontal article generation (continuing...)"
            } else {
                Write-Host ""
                Write-Success "✓ Step 6 completed: Horizontal article"
                Write-Host ""
            }
        }
    } else {
        Write-Warning "⊘ Step 6 skipped"
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
    $skillsFile = Join-Path $outputDir "skills-relevance/$ToolFamily-skills-relevance.md"

    if (Test-Path $familyFile) {
        $fileSize = [math]::Round((Get-Item $familyFile).Length / 1KB, 1)
        $lineCount = (Get-Content $familyFile | Measure-Object -Line).Lines
        Write-Success "✓ Tool Family File: $familyFile"
        Write-Info "  Size: ${fileSize}KB"
        Write-Info "  Lines: $lineCount"
    } else {
        Write-Warning "✗ Tool Family File not found: $familyFile"
    }

    if ($runStep5 -and (Test-Path $skillsFile)) {
        $fileSize = [math]::Round((Get-Item $skillsFile).Length / 1KB, 1)
        $lineCount = (Get-Content $skillsFile | Measure-Object -Line).Lines
        Write-Success "✓ Skills Relevance: $skillsFile"
        Write-Info "  Size: ${fileSize}KB"
        Write-Info "  Lines: $lineCount"
    } elseif ($runStep5) {
        Write-Warning "✗ Skills Relevance file not found: $skillsFile"
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
    Write-ErrorMessage "Generation failed: $($_.Exception.Message)"
    Write-ErrorMessage $_.ScriptStackTrace
    Write-Divider
    
    exit 1
}
