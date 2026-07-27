#!/usr/bin/env pwsh
# resolve-aliases.ps1 — Step 3 of echo-finn-approved-pr-codeowners.
# Offline alias-cache resolution only. Teams pass through; individual misses stay unresolved.
# OSPO lookup is intentionally an LLM step, not done here. This script only reads/writes the offline alias-cache and reports unresolved handles; the orchestrating agent invokes the OSPO sub-skill on misses, warms the cache, then re-runs this script.
[CmdletBinding()]
param(
    [string]$DataDir,
    [string]$Config = "$PSScriptRoot/../config/approved-pr-codeowners.config.json",
    [string]$RunId,
    [string]$FindingsPath,
    [string]$CachePath
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

if (-not (Test-Path -LiteralPath $Config)) { throw "Config not found: $Config" }
$cfg = Get-Content -LiteralPath $Config -Raw | ConvertFrom-Json
if (-not $DataDir) { $DataDir = $cfg.data_dir }
if (-not $FindingsPath) { $FindingsPath = Join-Path (Resolve-RunDir -DataDir $DataDir -RunsSubdir $cfg.runs_subdir -RunId $RunId) 'findings.json' }
if (-not (Test-Path -LiteralPath $FindingsPath)) { throw "findings.json not found: $FindingsPath" }
if (-not $CachePath) { $CachePath = Join-Path $cfg.data_dir 'alias-cache.json' }

if (Test-Path -LiteralPath $CachePath) { $cache = Get-Content -LiteralPath $CachePath -Raw | ConvertFrom-Json -AsHashtable }
else { $cache = @{ version = 1; entries = @{} } }
if (-not $cache.ContainsKey('entries') -or $null -eq $cache.entries) { $cache.entries = @{} }

$findingsEnvelope = Get-Content -LiteralPath $FindingsPath -Raw | ConvertFrom-Json
$findings = if ($findingsEnvelope.result) { $findingsEnvelope.result } else { $findingsEnvelope }
$unresolved = New-Object System.Collections.ArrayList
$cacheDirty = $false
$upnDomain = $cfg.upn_domain
$retryDays = [double]$cfg.unresolved_retry_days
$ospoBase = 'https://repos.opensource.microsoft.com/people?q='

function Resolve-Individual {
    param([string]$Owner)
    $handle = $Owner.TrimStart('@')
    $key = $handle.ToLowerInvariant()
    if ($cache.entries.ContainsKey($key)) {
        $e = $cache.entries[$key]
        if ($e.status -eq 'resolved' -and $e.ms_alias) {
            $upn = if ($e.upn) { $e.upn } else { "$($e.ms_alias)@$upnDomain" }
            return @{ status = 'resolved'; ms_alias = $e.ms_alias; upn = $upn; mention = "@$($e.ms_alias)" }
        }
        $stale = $true
        if ($e.resolved_at) {
            try { $stale = (((Get-Date).ToUniversalTime() - [datetimeoffset]::Parse($e.resolved_at).UtcDateTime).TotalDays -ge $retryDays) }
            catch { $stale = $true }
        }
        if ($stale) { $e.resolved_at = (Get-Date).ToUniversalTime().ToString('o'); $e.source = 'pending-retry'; $cache.entries[$key] = $e; $script:cacheDirty = $true }
        return @{ status = 'unresolved'; ms_alias = $null; upn = $null; mention = "@$handle (unresolved)" }
    }
    $cache.entries[$key] = [ordered]@{ gh_alias = $handle; ms_alias = $null; upn = $null; full_name = $null; resolved_at = (Get-Date).ToUniversalTime().ToString('o'); source = 'pending'; status = 'unresolved' }
    $script:cacheDirty = $true
    return @{ status = 'unresolved'; ms_alias = $null; upn = $null; mention = "@$handle (unresolved)" }
}

foreach ($pr in @($findings.approved_prs)) {
    $resolvedOwners = New-Object System.Collections.ArrayList
    foreach ($owner in @($pr.owners)) {
        if ([bool]$owner.is_team) {
            [void]$resolvedOwners.Add([ordered]@{ gh_handle = $owner.gh_handle; is_team = $true; status = 'team'; ms_alias = $null; upn = $null; mention = $owner.gh_handle; note = 'team-owner-not-resolved-via-ospo' })
            continue
        }
        $r = Resolve-Individual $owner.gh_handle
        [void]$resolvedOwners.Add([ordered]@{ gh_handle = $owner.gh_handle; is_team = $false; status = $r.status; ms_alias = $r.ms_alias; upn = $r.upn; mention = $r.mention })
        if ($r.status -ne 'resolved') {
            $handle = $owner.gh_handle.TrimStart('@')
            if (-not ($unresolved | Where-Object { $_.gh_handle -eq $handle })) { [void]$unresolved.Add([ordered]@{ gh_handle = $handle; lookup = "$ospoBase$handle" }) }
        }
    }
    $pr.owners = $resolvedOwners
}
$findings | Add-Member -NotePropertyName unresolved_aliases -NotePropertyValue $unresolved -Force
$findings | Add-Member -NotePropertyName stage -NotePropertyValue 'alias-resolution' -Force
$status = if ($unresolved.Count -gt 0) { 'partial' } else { 'success' }
$errors = @()
if ($unresolved.Count -gt 0) {
    $errors += [ordered]@{ code = 'UNRESOLVED_ALIASES'; message = "$($unresolved.Count) GitHub owner handle(s) require OSPO lookup before Teams mentions are complete."; severity = 'warning'; target = 'unresolved_aliases' }
}
$envelope = New-StructuredOutputEnvelope -Result $findings -Status $status -Errors $errors -Producer 'echo-finn-approved-pr-codeowners.resolve-aliases' -CorrelationId "approved-pr-codeowners-$($findings.run_id)"
($envelope | ConvertTo-Json -Depth 100) | Set-Content -LiteralPath $FindingsPath -Encoding utf8

if ($cacheDirty -or -not (Test-Path -LiteralPath $CachePath)) {
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $CachePath) | Out-Null
    ($cache | ConvertTo-Json -Depth 20) | Set-Content -LiteralPath $CachePath -Encoding utf8
    Write-Host "Updated alias cache: $CachePath"
}
Write-Host "Resolved aliases. Unresolved individual handles: $($unresolved.Count)"
if ($unresolved.Count -gt 0) { foreach ($u in $unresolved) { Write-Warning "Unresolved: $($u.gh_handle) -> $($u.lookup)" } }
Write-Output $FindingsPath
