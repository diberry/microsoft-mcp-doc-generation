#!/usr/bin/env pwsh
# render-outputs.ps1 — Step 4 of echo-finn-approved-pr-codeowners.
# Pure {{PLACEHOLDER}} substitution into Teams paste and report templates.
[CmdletBinding()]
param(
    [string]$DataDir,
    [string]$Config = "$PSScriptRoot/../config/approved-pr-codeowners.config.json",
    [string]$RunId,
    [string]$FindingsPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'structured-output.ps1')

function Resolve-RunDir {
    param([string]$DataDir, [string]$RunsSubdir, [string]$RunId)
    $base = Join-Path $DataDir $RunsSubdir
    if ($RunId) { return (Join-Path $base $RunId) }
    if (-not (Test-Path -LiteralPath $base)) { throw "No $RunsSubdir directory under '$DataDir' (run collect-approved-prs first)." }
    $latest = Get-ChildItem -LiteralPath $base -Directory | Sort-Object Name -Descending | Select-Object -First 1
    if (-not $latest) { throw "No run directories under '$base' (run collect-approved-prs first)." }
    return $latest.FullName
}
function Get-Tpl([string]$Name) { Get-Content -LiteralPath (Join-Path (Join-Path $PSScriptRoot '..\templates') $Name) -Raw }
function ConvertTo-SafeText([string]$s) { if ($null -eq $s) { return '' }; return (($s -replace '\\', '/' -replace '[\r\n]+', ' ').Trim()) }
function Expand-Scalars([string]$text, [hashtable]$map) { foreach ($k in $map.Keys) { $text = $text.Replace('{{' + $k + '}}', [string]$map[$k]) }; return $text }
function Expand-Rows([string]$text, [object[]]$items) {
    $m = [regex]::Match($text, '(?s)\{\{#ROWS\}\}(.*?)\{\{/ROWS\}\}')
    if (-not $m.Success) { return $text }
    $inner = $m.Groups[1].Value
    $sb = New-Object System.Text.StringBuilder
    foreach ($it in $items) { [void]$sb.Append((Expand-Scalars $inner $it)) }
    return $text.Replace($m.Value, $sb.ToString())
}

if (-not (Test-Path -LiteralPath $Config)) { throw "Config not found: $Config" }
$cfg = Get-Content -LiteralPath $Config -Raw | ConvertFrom-Json
if (-not $DataDir) { $DataDir = $cfg.data_dir }
if (-not $FindingsPath) { $FindingsPath = Join-Path (Resolve-RunDir -DataDir $DataDir -RunsSubdir $cfg.runs_subdir -RunId $RunId) 'findings.json' }
if (-not (Test-Path -LiteralPath $FindingsPath)) { throw "findings.json not found: $FindingsPath" }
$findingsEnvelope = Get-Content -LiteralPath $FindingsPath -Raw | ConvertFrom-Json
$findings = if ($findingsEnvelope.result) { $findingsEnvelope.result } else { $findingsEnvelope }
$runDir = Split-Path -Parent $FindingsPath
$generatedAt = (Get-Date).ToUniversalTime().ToString('o')

$rows = foreach ($pr in @($findings.approved_prs)) {
    $ownerTokens = @($pr.owners | ForEach-Object { $_.gh_handle })
    $mentions = @($pr.owners | ForEach-Object { $_.mention })
    $unres = @($pr.owners | Where-Object { $_.status -eq 'unresolved' } | ForEach-Object { $_.gh_handle })
    @{
        PR_NUMBER = [int]$pr.pr_number
        PR_TITLE  = (ConvertTo-SafeText $pr.pr_title)
        PR_URL    = (ConvertTo-SafeText $pr.pr_url)
        OWNERS    = $(if ($ownerTokens.Count) { ($ownerTokens -join ' ') } else { '(none)' })
        MENTIONS  = $(if ($mentions.Count) { ($mentions -join ' ') } else { '(no codeowners)' })
        UNRESOLVED = $(if ($unres.Count) { ($unres -join ' ') } else { '' })
    }
}

$teamText = Expand-Rows (Get-Tpl 'teams-paste.txt.tmpl') @($rows)
$teamText = Expand-Scalars $teamText @{ RUN_ID = $findings.run_id; GENERATED_AT = $generatedAt }
$teamPath = Join-Path $runDir 'teams-paste.txt'
$teamText | Set-Content -LiteralPath $teamPath -Encoding utf8

$report = Expand-Rows (Get-Tpl 'report.md.tmpl') @($rows)
$report = Expand-Scalars $report @{ RUN_ID = $findings.run_id; GENERATED_AT = $generatedAt }
$reportPath = Join-Path $runDir 'report.md'
$report | Set-Content -LiteralPath $reportPath -Encoding utf8

$result = [ordered]@{
    stage         = 'render'
    run_id        = $findings.run_id
    generated_at  = $generatedAt
    approved_prs  = @($findings.approved_prs)
    artifactPaths = [ordered]@{
        teamsPaste = $teamPath
        report     = $reportPath
    }
}
$renderJsonPath = Join-Path $runDir 'render.json'
$envelope = New-StructuredOutputEnvelope -Result $result -Producer 'echo-finn-approved-pr-codeowners.render-outputs' -CorrelationId "approved-pr-codeowners-$($findings.run_id)"
($envelope | ConvertTo-Json -Depth 100) | Set-Content -LiteralPath $renderJsonPath -Encoding utf8

Write-Host "Rendered outputs in: $runDir"
Write-Host "  teams-paste.txt"
Write-Host "  report.md"
Write-Host "  render.json"
Write-Output $runDir
