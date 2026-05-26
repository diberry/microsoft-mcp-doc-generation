# Test-ArticleHealth.ps1 — PR Health Checker / CI Pre-Flight
# Validates MCP tool article files before they're pushed, catching the same issues CI catches.
#
# Usage:
#   .\Test-ArticleHealth.ps1 -ArticlePath <file.md> [-Strict] [-OutputJson <path>]
#   .\Test-ArticleHealth.ps1 -ArticlesDir <dir> [-Strict] [-OutputJson <path>]

param(
    [string]$ArticlePath,
    [string]$ArticlesDir,
    [switch]$Strict,
    [string]$OutputJson
)

$ErrorActionPreference = "Stop"

# ─── Helpers ──────────────────────────────────────────────────────────────────

function Write-Pass  { param([string]$msg) Write-Host "  [PASS] $msg" -ForegroundColor Green }
function Write-Warn  { param([string]$msg) Write-Host "  [WARN] $msg" -ForegroundColor Yellow }
function Write-Fail  { param([string]$msg) Write-Host "  [FAIL] $msg" -ForegroundColor Red }

function New-CheckResult {
    param([string]$Name, [string]$Status, [string]$Detail = "")
    [PSCustomObject]@{ Name = $Name; Status = $Status; Detail = $Detail }
}

# ─── Per-file checks ──────────────────────────────────────────────────────────

function Test-File {
    param([string]$Path)

    $results = @()
    $raw     = Get-Content $Path -Raw -Encoding UTF8
    $lines   = Get-Content $Path -Encoding UTF8

    # ── 1. Frontmatter validation ─────────────────────────────────────────────

    # Extract frontmatter block (content between first two --- delimiters)
    $fmMatch = [regex]::Match($raw, '(?s)^---\r?\n(.*?)\r?\n---')
    $fm = if ($fmMatch.Success) { $fmMatch.Groups[1].Value } else { "" }

    # ms.date exists and is MM/DD/YYYY
    if ($fm -match 'ms\.date:\s*(\S+)') {
        $dateVal = $Matches[1].Trim()
        if ($dateVal -match '^\d{2}/\d{2}/\d{4}$') {
            $results += New-CheckResult "frontmatter.ms.date" "pass" $dateVal
        } else {
            $results += New-CheckResult "frontmatter.ms.date" "fail" "Expected MM/DD/YYYY, got: $dateVal"
        }
    } else {
        $results += New-CheckResult "frontmatter.ms.date" "fail" "ms.date missing"
    }

    # ms.custom contains build-2025
    if ($fm -match 'ms\.custom:') {
        if ($fm -match 'build-2025') {
            $results += New-CheckResult "frontmatter.ms.custom" "pass" "contains build-2025"
        } else {
            $results += New-CheckResult "frontmatter.ms.custom" "warn" "ms.custom exists but missing build-2025"
        }
    } else {
        $results += New-CheckResult "frontmatter.ms.custom" "fail" "ms.custom missing"
    }

    # ms.reviewer exists (not bare "reviewer:")
    if ($fm -match 'ms\.reviewer:\s*\S+') {
        $results += New-CheckResult "frontmatter.ms.reviewer" "pass"
    } elseif ($fm -match '^reviewer:\s*$' -or $fm -match 'reviewer:\s*\r?\n') {
        $results += New-CheckResult "frontmatter.ms.reviewer" "fail" "bare reviewer: with no value"
    } else {
        $results += New-CheckResult "frontmatter.ms.reviewer" "warn" "ms.reviewer not found"
    }

    # No blank lines inside frontmatter block
    $fmLines = $fm -split '\r?\n'
    $blankInFm = $fmLines | Where-Object { $_ -match '^\s*$' }
    if ($blankInFm.Count -eq 0) {
        $results += New-CheckResult "frontmatter.no-blank-lines" "pass"
    } else {
        $results += New-CheckResult "frontmatter.no-blank-lines" "warn" "$($blankInFm.Count) blank line(s) inside frontmatter"
    }

    # mcp-cli.version exists
    if ($fm -match 'mcp-cli\.version:\s*\S+') {
        $results += New-CheckResult "frontmatter.mcp-cli.version" "pass"
    } else {
        $results += New-CheckResult "frontmatter.mcp-cli.version" "warn" "mcp-cli.version not found in frontmatter"
    }

    # ── 2. Heading level validation ───────────────────────────────────────────

    $h2s   = $lines | Where-Object { $_ -match '^## ' }
    $h3s   = $lines | Where-Object { $_ -match '^### [A-Z]' }

    # Tool sections should be H2, not H3 — look for H3s that look like tool names (Title Case word)
    if ($h3s.Count -eq 0) {
        $results += New-CheckResult "headings.no-h3-tool-sections" "pass"
    } else {
        $results += New-CheckResult "headings.no-h3-tool-sections" "fail" "Found $($h3s.Count) H3 tool-style heading(s): $($h3s -join '; ')"
    }

    # No duplicate H2 headings
    $h2Text    = $h2s | ForEach-Object { $_ -replace '^## ', '' }
    $duplicates = $h2Text | Group-Object | Where-Object { $_.Count -gt 1 }
    if ($duplicates.Count -eq 0) {
        $results += New-CheckResult "headings.no-duplicate-h2" "pass"
    } else {
        $results += New-CheckResult "headings.no-duplicate-h2" "fail" "Duplicate H2(s): $($duplicates.Name -join ', ')"
    }

    # ── 3. No absolute Learn URLs ─────────────────────────────────────────────

    $absLearn = $lines | Where-Object { $_ -match 'https://learn\.microsoft\.com/' } |
                Where-Object { $_ -notmatch '^\s*#' }   # ignore commented-out lines
    if ($absLearn.Count -eq 0) {
        $results += New-CheckResult "urls.no-absolute-learn" "pass"
    } else {
        $results += New-CheckResult "urls.no-absolute-learn" "fail" "$($absLearn.Count) absolute learn.microsoft.com URL(s) found"
    }

    # ── 4. Parameter table validation ─────────────────────────────────────────
    #
    # GAP: This script does not validate the "Required or optional" column value
    # against inputSchema.required[] in cli-output.json (Check 5 Part 2 in validation-checks.md).
    # Implementing that check here would require a -CliOutputJson parameter so the script
    # can load the JSON and look up each tool's required[] array per parameter row.
    # The authoritative spec lives in the AI-agent validation skill
    # (azure-ai-tools-validation). Add -CliOutputJson support here if CI pre-flight
    # coverage of Check 5 Part 2 is needed in the future.

    $hasParamTable = $raw -match '\|\s*Parameter\s*\|'
    if ($hasParamTable) {
        # Check rows: should have **bold** param name in first column
        $tableRows = $lines | Where-Object { $_ -match '^\|' -and $_ -notmatch '^\|[-: ]+\|' -and $_ -notmatch '\|\s*Parameter\s*\|' }
        $badRows = $tableRows | Where-Object { $_ -notmatch '^\|\s*\*\*' }
        if ($badRows.Count -eq 0) {
            $results += New-CheckResult "params.bold-names" "pass"
        } else {
            $results += New-CheckResult "params.bold-names" "warn" "$($badRows.Count) param row(s) missing **bold** name format"
        }

        # No [!INCLUDE param rows
        $includeRows = $tableRows | Where-Object { $_ -match '\[!INCLUDE' }
        if ($includeRows.Count -eq 0) {
            $results += New-CheckResult "params.no-include-rows" "pass"
        } else {
            $results += New-CheckResult "params.no-include-rows" "fail" "$($includeRows.Count) [!INCLUDE row(s) in parameter table"
        }
    } else {
        $results += New-CheckResult "params.bold-names"    "pass" "no parameter table (skipped)"
        $results += New-CheckResult "params.no-include-rows" "pass" "no parameter table (skipped)"
    }

    # ── 5. HTML comment markers ───────────────────────────────────────────────

    $commentLines = $lines | Where-Object { $_ -match '<!--' }
    $badMarkers   = $commentLines | Where-Object {
        # Valid: <!-- word --> or <!-- @mcpcli word --> or <!-- word word -->
        $_ -notmatch '<!--\s+[@\w][\w\s@-]*\s+-->' -and
        $_ -notmatch '<!--\s*-->'   # empty comments also ok as separators
    }
    if ($badMarkers.Count -eq 0) {
        $results += New-CheckResult "markers.well-formed" "pass"
    } else {
        $results += New-CheckResult "markers.well-formed" "warn" "$($badMarkers.Count) potentially malformed HTML comment marker(s)"
    }

    return $results
}

# ─── Collect files ────────────────────────────────────────────────────────────

$files = @()
if ($ArticlePath) {
    if (-not (Test-Path $ArticlePath)) { Write-Error "File not found: $ArticlePath"; exit 1 }
    $files = @(Get-Item $ArticlePath)
} elseif ($ArticlesDir) {
    if (-not (Test-Path $ArticlesDir)) { Write-Error "Directory not found: $ArticlesDir"; exit 1 }
    $files = Get-ChildItem $ArticlesDir -Filter "*.md" -Recurse
} else {
    Write-Error "Provide -ArticlePath or -ArticlesDir"
    exit 1
}

if ($files.Count -eq 0) { Write-Host "No .md files found." -ForegroundColor Yellow; exit 0 }

# ─── Run checks ───────────────────────────────────────────────────────────────

$allResults = @()
$anyFail    = $false

foreach ($file in $files) {
    Write-Host "`n$($file.Name)" -ForegroundColor Cyan

    $checks = Test-File -Path $file.FullName

    foreach ($c in $checks) {
        switch ($c.Status) {
            "pass" { Write-Pass "$($c.Name)$(if ($c.Detail) { ': ' + $c.Detail })" }
            "warn" {
                Write-Warn "$($c.Name)$(if ($c.Detail) { ': ' + $c.Detail })"
                if ($Strict) { $anyFail = $true }
            }
            "fail" {
                Write-Fail "$($c.Name)$(if ($c.Detail) { ': ' + $c.Detail })"
                $anyFail = $true
            }
        }
    }

    $allResults += [PSCustomObject]@{
        File    = $file.FullName
        Checks  = $checks
    }
}

# ─── JSON output ──────────────────────────────────────────────────────────────

if ($OutputJson) {
    $allResults | ConvertTo-Json -Depth 6 | Set-Content $OutputJson -Encoding UTF8
    Write-Host "`nJSON written to: $OutputJson" -ForegroundColor DarkGray
}

# ─── Summary ──────────────────────────────────────────────────────────────────

$total  = ($allResults.Checks).Count
$passed = ($allResults.Checks | Where-Object Status -eq "pass").Count
$warned = ($allResults.Checks | Where-Object Status -eq "warn").Count
$failed = ($allResults.Checks | Where-Object Status -eq "fail").Count

Write-Host "`n─────────────────────────────────────────" -ForegroundColor DarkGray
Write-Host "Checked $($files.Count) file(s) | $total checks | $passed pass  $warned warn  $failed fail" -ForegroundColor $(if ($anyFail) { "Red" } else { "Green" })

exit $(if ($anyFail) { 1 } else { 0 })
