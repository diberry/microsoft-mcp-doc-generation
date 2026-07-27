#!/usr/bin/env pwsh
# collect-approved-prs.ps1 — Step 1 of echo-finn-approved-pr-codeowners.
# Pure gh reads: candidate PRs, Dina-authored review/comment bodies, changed files, and CODEOWNERS.
[CmdletBinding()]
param(
    [string]$Config = "$PSScriptRoot/../config/approved-pr-codeowners.config.json",
    [string]$DataDir,
    [string]$RunId
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'structured-output.ps1')

function Invoke-GhJson {
    param([string[]]$GhArgs)
    $out = & gh @GhArgs
    if ($LASTEXITCODE -ne 0) { throw "gh $($GhArgs -join ' ') failed (exit $LASTEXITCODE)" }
    return $out
}

function Get-GhArray {
    param([string[]]$GhArgs)
    $out = & gh @GhArgs 2>$null
    if ($LASTEXITCODE -ne 0 -or -not $out) { return @() }
    $json = $out | ConvertFrom-Json
    if ($null -eq $json) { return @() }
    return @($json)
}

function Get-Codeowners {
    param([string]$Repo, [object[]]$Paths)
    $owner, $name = $Repo.Split('/', 2)
    foreach ($p in @($Paths)) {
        $encoded = ($p -split '/' | ForEach-Object { [uri]::EscapeDataString($_) }) -join '/'
        $json = & gh api "repos/$owner/$name/contents/$encoded" 2>$null
        if ($LASTEXITCODE -eq 0 -and $json) {
            $obj = $json | ConvertFrom-Json
            if ($obj.content) {
                $bytes = [Convert]::FromBase64String(($obj.content -replace '\s', ''))
                return [ordered]@{ path = $p; content = [Text.Encoding]::UTF8.GetString($bytes) }
            }
        }
    }
    return [ordered]@{ path = $null; content = '' }
}

if (-not (Test-Path -LiteralPath $Config)) { throw "Config not found: $Config" }
$cfg = Get-Content -LiteralPath $Config -Raw | ConvertFrom-Json
if (-not $DataDir) { $DataDir = $cfg.data_dir }
if (-not $RunId) { $RunId = (Get-Date).ToString('yyyy-MM-dd-HHmm') }

$runDir = Join-Path (Join-Path $DataDir $cfg.runs_subdir) $RunId
New-Item -ItemType Directory -Force -Path $runDir | Out-Null

$me = (Invoke-GhJson @('api', 'user', '--jq', '.login')).Trim()
if (-not $me) { throw 'Could not resolve current GitHub login (gh api user).' }
Write-Host "Authenticated GitHub login: $me"

$repo = $cfg.target_repo
$searchLimit = [int]$cfg.search_limit
$seen = [System.Collections.Generic.HashSet[string]]::new()
$candidates = New-Object System.Collections.ArrayList
function Add-Candidate([int]$Number) {
    if ($seen.Add([string]$Number)) { [void]$candidates.Add($Number) }
}

foreach ($qualifier in @('reviewed-by:@me', 'commenter:@me')) {
    $items = Get-GhArray @('search', 'prs', '--repo', $repo, '--state', 'open', $qualifier, '--json', 'number', '--limit', "$searchLimit")
    if ($items.Count -eq $searchLimit) {
        Write-Warning "$qualifier search hit the $searchLimit-result cap; some PRs may be missed."
    }
    foreach ($item in $items) { Add-Candidate ([int]$item.number) }
}

Write-Host "Found $($candidates.Count) candidate open PR(s). Pulling details..."
$codeowners = Get-Codeowners -Repo $repo -Paths $cfg.codeowners_paths
$owner, $name = $repo.Split('/', 2)
$rawPrs = New-Object System.Collections.ArrayList

foreach ($n in $candidates) {
    $view = & gh pr view $n --repo $repo --json number,title,url,isDraft,state,author,files 2>$null
    if ($LASTEXITCODE -ne 0 -or -not $view) { Write-Warning "Skip $repo#$n (view failed)"; continue }
    $v = $view | ConvertFrom-Json

    $comments = Get-GhArray @('api', '--paginate', "repos/$owner/$name/issues/$n/comments", '--jq', "[.[] | select(.user.login == `"$me`") | {author: .user.login, body: .body, created_at: .created_at}]")
    $reviewComments = Get-GhArray @('api', '--paginate', "repos/$owner/$name/pulls/$n/comments", '--jq', "[.[] | select(.user.login == `"$me`") | {author: .user.login, body: .body, created_at: .created_at}]")
    $reviews = Get-GhArray @('api', '--paginate', "repos/$owner/$name/pulls/$n/reviews", '--jq', "[.[] | select(.user.login == `"$me`") | {author: .user.login, state: .state, body: .body, submitted_at: .submitted_at}]")

    $files = @($v.files | ForEach-Object { if ($_.path) { $_.path } elseif ($_.filename) { $_.filename } })
    [void]$rawPrs.Add([ordered]@{
            repo                 = $repo
            pr_number            = [int]$v.number
            pr_title             = $v.title
            pr_url               = $v.url
            is_draft             = [bool]$v.isDraft
            state                = $v.state
            author               = $v.author.login
            dina_reviews         = $reviews
            dina_comments        = $comments
            dina_review_comments = $reviewComments
            changed_files        = $files
        })
}

$result = [ordered]@{
    stage        = 'raw-collection'
    version      = 1
    run_id       = $RunId
    generated_at = (Get-Date).ToUniversalTime().ToString('o')
    me           = $me
    repo         = $repo
    codeowners   = $codeowners
    prs          = $rawPrs
}

$outPath = Join-Path $runDir 'raw-prs.json'
$envelope = New-StructuredOutputEnvelope -Result $result -Producer 'echo-finn-approved-pr-codeowners.collect-approved-prs' -CorrelationId "approved-pr-codeowners-$RunId"
($envelope | ConvertTo-Json -Depth 100) | Set-Content -LiteralPath $outPath -Encoding utf8
Write-Host "Wrote $outPath ($($rawPrs.Count) PR(s))."
Write-Output $outPath
