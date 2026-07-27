#!/usr/bin/env pwsh
# analyze-approved.ps1 — Step 2 of echo-finn-approved-pr-codeowners.
# Filters Dina-approved PRs and computes path-specific CODEOWNERS owners.
[CmdletBinding()]
param(
    [string]$DataDir,
    [string]$Config = "$PSScriptRoot/../config/approved-pr-codeowners.config.json",
    [string]$RunId,
    [string]$RawPrsPath
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

function Remove-CodeSpans {
    param([string]$Body)
    if ([string]::IsNullOrEmpty($Body)) { return '' }
    $t = [regex]::Replace($Body, '(?s)```.*?```', ' ')
    $t = [regex]::Replace($t, '(?s)~~~.*?~~~', ' ')
    return [regex]::Replace($t, '`[^`]*`', ' ')
}

function ConvertTo-CodeownersRegex {
    param([string]$Pattern)
    $p = ($Pattern -replace '\\ ', ' ').Trim()
    if (-not $p -or $p.StartsWith('#') -or $p.StartsWith('!')) { return $null }
    $anchored = $p.StartsWith('/')
    if ($anchored) { $p = $p.Substring(1) }
    $dirOnly = $p.EndsWith('/')
    if ($dirOnly) { $p = $p.TrimEnd('/') }
    $hasSlash = $p.Contains('/')
    $sb = New-Object System.Text.StringBuilder
    for ($i = 0; $i -lt $p.Length; $i++) {
        $ch = $p[$i]
        if ($ch -eq '*') {
            if (($i + 1) -lt $p.Length -and $p[$i + 1] -eq '*') { [void]$sb.Append('.*'); $i++ }
            else { [void]$sb.Append('[^/]*') }
        }
        elseif ($ch -eq '?') { [void]$sb.Append('[^/]') }
        else { [void]$sb.Append([regex]::Escape([string]$ch)) }
    }
    $body = $sb.ToString()
    if ($dirOnly) { $body = "$body(?:/.*)?" }
    # GitHub CODEOWNERS follows gitignore anchoring: leading or middle slash anchors to repo root; slash-less patterns match at any depth.
    $prefix = if ($anchored -or $hasSlash) { '^' } else { '(^|.*/)' }
    return [regex]::new($prefix + $body + '$', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
}

function Parse-Codeowners {
    param([string]$Content)
    $rules = New-Object System.Collections.ArrayList
    $lineNo = 0
    foreach ($line in ($Content -split "`r?`n")) {
        $lineNo++
        $trim = $line.Trim()
        if (-not $trim -or $trim.StartsWith('#')) { continue }
        $parts = $trim -split '\s+'
        if ($parts.Count -lt 2) { continue }
        $owners = @($parts[1..($parts.Count - 1)] | Where-Object { $_ -like '@*' })
        if ($owners.Count -eq 0) { continue }
        $rx = ConvertTo-CodeownersRegex $parts[0]
        if ($null -eq $rx) { continue }
        [void]$rules.Add([ordered]@{ line = $lineNo; pattern = $parts[0]; regex = $rx; owners = $owners })
    }
    return @($rules)
}

if (-not (Test-Path -LiteralPath $Config)) { throw "Config not found: $Config" }
$cfg = Get-Content -LiteralPath $Config -Raw | ConvertFrom-Json
if (-not $DataDir) { $DataDir = $cfg.data_dir }
if (-not $RawPrsPath) { $RawPrsPath = Join-Path (Resolve-RunDir -DataDir $DataDir -RunsSubdir $cfg.runs_subdir -RunId $RunId) 'raw-prs.json' }
if (-not (Test-Path -LiteralPath $RawPrsPath)) { throw "raw-prs.json not found: $RawPrsPath" }

$rawEnvelope = Get-Content -LiteralPath $RawPrsPath -Raw | ConvertFrom-Json
$raw = if ($rawEnvelope.result) { $rawEnvelope.result } else { $rawEnvelope }
$approvalRx = [regex]::new($cfg.approval_comment_regex, [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
$rules = Parse-Codeowners $raw.codeowners.content
$approved = New-Object System.Collections.ArrayList
$skipped = New-Object System.Collections.ArrayList

foreach ($pr in @($raw.prs)) {
    if ($pr.state -ne 'OPEN') { [void]$skipped.Add([ordered]@{ pr_number = [int]$pr.pr_number; reason = 'not-open' }); continue }
    if ($pr.author -and $raw.me -and $pr.author.ToLowerInvariant() -eq $raw.me.ToLowerInvariant()) { [void]$skipped.Add([ordered]@{ pr_number = [int]$pr.pr_number; reason = 'authored-by-dina' }); continue }

    $formalApproved = @($pr.dina_reviews | Where-Object { $_.state -eq 'APPROVED' }).Count -gt 0
    $commentApproved = $false
    foreach ($c in @($pr.dina_comments) + @($pr.dina_review_comments)) {
        if ($approvalRx.IsMatch((Remove-CodeSpans "$($c.body)"))) { $commentApproved = $true; break }
    }
    if (-not ($formalApproved -or $commentApproved)) { [void]$skipped.Add([ordered]@{ pr_number = [int]$pr.pr_number; reason = 'no-dina-approval' }); continue }

    $ownersByKey = [ordered]@{}
    foreach ($file in @($pr.changed_files)) {
        $path = ($file -replace '\\', '/')
        $match = $null
        foreach ($rule in $rules) { if ($rule.regex.IsMatch($path)) { $match = $rule } }
        if ($null -eq $match) { continue }
        foreach ($owner in @($match.owners)) {
            if ($owner.TrimStart('@').ToLowerInvariant() -eq $raw.me.ToLowerInvariant()) { continue }
            $key = $owner.ToLowerInvariant()
            if (-not $ownersByKey.Contains($key)) {
                $ownersByKey[$key] = [ordered]@{ gh_handle = $owner; is_team = $owner.Contains('/') }
            }
        }
    }

    [void]$approved.Add([ordered]@{
            repo            = $pr.repo
            pr_number       = [int]$pr.pr_number
            pr_title        = $pr.pr_title
            pr_url          = $pr.pr_url
            approval_source = $(if ($formalApproved) { 'review' } else { 'comment' })
            changed_files   = @($pr.changed_files)
            owners          = @($ownersByKey.Values)
        })
}

$out = [ordered]@{
    stage           = 'findings'
    version         = 1
    run_id          = $raw.run_id
    generated_at    = (Get-Date).ToUniversalTime().ToString('o')
    repo            = $raw.repo
    dina_login      = $raw.me
    codeowners_path = $raw.codeowners.path
    approved_prs    = $approved
    skipped         = $skipped
}

$outPath = Join-Path (Split-Path -Parent $RawPrsPath) 'findings.json'
$envelope = New-StructuredOutputEnvelope -Result $out -Producer 'echo-finn-approved-pr-codeowners.analyze-approved' -CorrelationId "approved-pr-codeowners-$($raw.run_id)"
($envelope | ConvertTo-Json -Depth 100) | Set-Content -LiteralPath $outPath -Encoding utf8
Write-Host "Wrote $outPath (approved=$($approved.Count) skipped=$($skipped.Count))."
Write-Output $outPath
