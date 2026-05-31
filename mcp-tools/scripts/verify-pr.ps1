#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Lightweight PR verification script for content generation changes.

.DESCRIPTION
    Validates that tab ordering is correct (MCP Server tab before CLI tab) in generated
    content by running a focused set of namespaces through the pipeline. Exercises the
    main structural patterns: single namespace, merge groups, and multi-namespace output.

    Namespaces tested:
      monitor            - single namespace (azure-monitor primary)
      workbooks          - merges into azure-monitor (secondary → 1 output)
      extension_cli_generate + extension_cli_install - multiple namespaces (azure-cli-extension)
      functionapp + functions - cross-namespace merge (azure-functions)

    Phases:
      1. Build .NET solution (unless -SkipBuild)
      2. Tab-ordering unit + integration tests (CliTabWrapperTests, CliTabPilotTests)
      3. Pipeline Step 1 for each target namespace (deterministic, ~1 min/namespace)
      4. Tab-ordering check on any existing tool-family/*.md files

.PARAMETER SkipBuild
    Skip the dotnet build phase (pass when solution is already built).

.PARAMETER SkipPipelineRuns
    Skip Step 1 pipeline runs — only run tests and check existing generated files.

.PARAMETER Configuration
    Build/test configuration. Default: Release.

.EXAMPLE
    # From repo root:
    pwsh mcp-tools/scripts/verify-pr.ps1

    # From mcp-tools/scripts/:
    ./verify-pr.ps1

    # Skip build (already built):
    ./verify-pr.ps1 -SkipBuild

    # Only run tests, skip pipeline step 1:
    ./verify-pr.ps1 -SkipPipelineRuns
#>

param(
    [switch]$SkipBuild,
    [switch]$SkipPipelineRuns,
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

# ── Paths ──────────────────────────────────────────────────────────────────────
$scriptDir  = $PSScriptRoot
$mcpToolsDir = Split-Path -Parent $scriptDir
$repoRoot    = Split-Path -Parent $mcpToolsDir
$solutionFile = Join-Path $repoRoot "mcp-doc-generation.sln"

# ── Helpers ────────────────────────────────────────────────────────────────────
function Write-Header  { param([string]$m) Write-Host "`n$("═" * 72)`n  $m`n$("═" * 72)" -ForegroundColor Magenta }
function Write-Ok      { param([string]$m) Write-Host "  ✓ $m" -ForegroundColor Green }
function Write-Warn    { param([string]$m) Write-Host "  ⚠ $m" -ForegroundColor Yellow }
function Write-Fail    { param([string]$m) Write-Host "  ✗ $m" -ForegroundColor Red }
function Write-Info    { param([string]$m) Write-Host "  · $m" -ForegroundColor Cyan }

$script:failures = @()
$script:warnings = @()

function Record-Failure { param([string]$m) $script:failures += $m; Write-Fail $m }
function Record-Warning { param([string]$m) $script:warnings += $m; Write-Warn $m }

# ── Target namespaces ──────────────────────────────────────────────────────────
$targetNamespaces = @(
    [PSCustomObject]@{ Namespace = "monitor";               MergeGroup = "azure-monitor";       Pattern = "Single namespace (primary)" }
    [PSCustomObject]@{ Namespace = "workbooks";             MergeGroup = "azure-monitor";       Pattern = "Merge into 1 output (secondary)" }
    [PSCustomObject]@{ Namespace = "extension_cli_generate"; MergeGroup = "azure-cli-extension"; Pattern = "Multi-namespace split (part 1)" }
    [PSCustomObject]@{ Namespace = "extension_cli_install";  MergeGroup = "azure-cli-extension"; Pattern = "Multi-namespace split (part 2)" }
    [PSCustomObject]@{ Namespace = "functionapp";           MergeGroup = "azure-functions";     Pattern = "Cross-namespace merge (primary)" }
    [PSCustomObject]@{ Namespace = "functions";             MergeGroup = "azure-functions";     Pattern = "Cross-namespace merge (secondary)" }
)

# Tab ordering markers (from CliTabWrapper.BuildTabBlock)
$MCP_TAG = "#### [MCP Server](#tab/mcp-server)"
$CLI_TAG  = "#### [Azure MCP CLI](#tab/azure-mcp-cli)"

Write-Header "PR Verification — Tab Ordering & Content Generation"
Write-Info "Repo root : $repoRoot"
Write-Info "Solution  : mcp-doc-generation.sln"
Write-Info "Config    : $Configuration"
Write-Info "Namespaces: $($targetNamespaces.Namespace -join ', ')"

# ═══════════════════════════════════════════════════════════════════════════════
# Phase 1: Build
# ═══════════════════════════════════════════════════════════════════════════════
Write-Header "Phase 1: Build"

if ($SkipBuild) {
    Write-Warn "Build skipped (-SkipBuild)"
} else {
    Write-Info "dotnet build mcp-doc-generation.sln --configuration $Configuration --no-incremental"
    dotnet build $solutionFile --configuration $Configuration --no-incremental -v quiet
    if ($LASTEXITCODE -ne 0) {
        Record-Failure "Build failed — aborting."
        exit 1
    }
    Write-Ok "Build succeeded"
}

# ═══════════════════════════════════════════════════════════════════════════════
# Phase 2: Tab-ordering tests
# ═══════════════════════════════════════════════════════════════════════════════
Write-Header "Phase 2: Tab-Ordering Tests"

Write-Info "Running: CliTabWrapperTests (unit) + CliTabPilotTests (integration)"
$testFilter = "CliTabWrapperTests|CliTabPilotTests"
# --no-build only when we explicitly built in Phase 1; otherwise let dotnet test decide
$testArgs = @($solutionFile, "--filter", $testFilter, "--configuration", $Configuration)
if (-not $SkipBuild) { $testArgs += "--no-build" }

dotnet test @testArgs -v normal
$testExitCode = $LASTEXITCODE

if ($testExitCode -eq 0) {
    Write-Ok "Tab-ordering tests passed (exit 0)"
} else {
    Record-Failure "Tab-ordering tests failed (exit $testExitCode)"
}

# ═══════════════════════════════════════════════════════════════════════════════
# Phase 3: Pipeline Step 1 for target namespaces
# ═══════════════════════════════════════════════════════════════════════════════
Write-Header "Phase 3: Pipeline Step 1 — Target Namespaces"

if ($SkipPipelineRuns) {
    Write-Warn "Pipeline runs skipped (-SkipPipelineRuns)"
} else {
    $generateScript   = Join-Path $scriptDir "Generate-ToolFamily.ps1"
    $globalCliDir     = Join-Path $repoRoot "generated" "cli"
    $globalCliJson    = Join-Path $globalCliDir "cli-output.json"

    if (-not (Test-Path $globalCliJson)) {
        Record-Failure "Global CLI data missing: $globalCliJson — run bootstrap (step 0) first"
    } else {
        foreach ($entry in $targetNamespaces) {
            $ns         = $entry.Namespace
            $pattern    = $entry.Pattern
            $outputPath = Join-Path $repoRoot "generated-$ns"

            Write-Info "[$ns] $pattern"
            Write-Info "[$ns] Output → generated-$ns/"

            # Seed the namespace dir with global CLI data if not already present.
            # Step 1 reads cli-output.json from the output dir to filter by namespace.
            $nsCliDir  = Join-Path $outputPath "cli"
            $nsCliJson = Join-Path $nsCliDir "cli-output.json"
            if (-not (Test-Path $nsCliJson)) {
                Write-Info "[$ns] Seeding CLI data from generated/cli/ → generated-$ns/cli/"
                New-Item -ItemType Directory -Path $nsCliDir -Force | Out-Null
                Copy-Item -Path (Join-Path $globalCliDir "*") -Destination $nsCliDir -Recurse -Force
            }

            & $generateScript -ToolFamily $ns -Steps @(1) -SkipBuild -OutputPath $outputPath
            $stepExit = $LASTEXITCODE

            if ($stepExit -eq 0 -and (Test-Path $nsCliJson)) {
                $toolCount = (Get-Content $nsCliJson -Raw | ConvertFrom-Json).results.Count
                Write-Ok "[$ns] Step 1 complete — $toolCount tools in CLI JSON"
            } elseif ($stepExit -eq 0) {
                Record-Warning "[$ns] Step 1 succeeded but cli-output.json not found at expected path"
            } else {
                Record-Failure "[$ns] Step 1 failed (exit $stepExit)"
            }
        }
    }
}

# ═══════════════════════════════════════════════════════════════════════════════
# Phase 4: Tab-ordering check on tool-family files in target namespace dirs
# ═══════════════════════════════════════════════════════════════════════════════
Write-Header "Phase 4: Tab-Ordering Check — Target Namespace Tool-Family Files"

$toolFamilyFiles = @()

# Collect only from the target namespace dirs tested in Phase 3
foreach ($entry in $targetNamespaces) {
    $tfDir = Join-Path $repoRoot "generated-$($entry.Namespace)" "tool-family"
    if (Test-Path $tfDir) {
        $toolFamilyFiles += Get-ChildItem $tfDir -Filter "*.md" -ErrorAction SilentlyContinue
    }
}

$toolFamilyFiles = $toolFamilyFiles | Sort-Object FullName -Unique

if ($toolFamilyFiles.Count -eq 0) {
    Write-Info "No tool-family/*.md files in target namespace dirs — skipped (steps 2-4 not run)"
    Write-Info "Tab ordering is validated by Phase 2 unit tests (CliTabWrapperTests + CliTabPilotTests)"
} else {
    Write-Info "Checking $($toolFamilyFiles.Count) tool-family file(s) in target namespace dirs..."
    $tabCheckFail = 0
    $tabCheckPass = 0

    foreach ($f in $toolFamilyFiles) {
        $content = Get-Content $f.FullName -Raw
        $mcpIdx  = $content.IndexOf($MCP_TAG)
        $cliIdx  = $content.IndexOf($CLI_TAG)

        if ($mcpIdx -lt 0 -or $cliIdx -lt 0) {
            Write-Info "$($f.Name): no tab markers found (skipping)"
            continue
        }

        if ($mcpIdx -gt $cliIdx) {
            Record-Failure "$($f.Name): MCP Server tab appears AFTER CLI tab — fix not applied"
            $tabCheckFail++
        } else {
            Write-Ok "$($f.Name): MCP Server tab before CLI tab ✓"
            $tabCheckPass++
        }
    }

    Write-Info "Tab check: $tabCheckPass correct, $tabCheckFail failures"
}

# ═══════════════════════════════════════════════════════════════════════════════
# Summary
# ═══════════════════════════════════════════════════════════════════════════════
Write-Header "Summary"

if ($script:warnings.Count -gt 0) {
    Write-Host ""
    Write-Host "  Warnings:" -ForegroundColor Yellow
    $script:warnings | ForEach-Object { Write-Host "    · $_" -ForegroundColor Yellow }
}

if ($script:failures.Count -eq 0) {
    Write-Host ""
    Write-Host "  ✓ All checks passed" -ForegroundColor Green
    Write-Host ""
    exit 0
} else {
    Write-Host ""
    Write-Host "  Failures ($($script:failures.Count)):" -ForegroundColor Red
    $script:failures | ForEach-Object { Write-Host "    ✗ $_" -ForegroundColor Red }
    Write-Host ""
    exit 1
}
