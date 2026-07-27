<#
.SYNOPSIS
    Analyzes content impact of new Azure MCP releases.

.DESCRIPTION
    This script reads metadata snapshots from Step 2, compares them with:
    - Existing articles in the content repo
    - Release changelogs for customer-visible changes
    - In-progress PR branches (if available)
    
    Produces a detailed impact matrix showing:
    - NEW namespaces (no article yet)
    - CHANGED namespaces (article exists, has release notes)
    - UNCHANGED namespaces (article exists, no changes)
    - Tool count tracking per version
    - PR status for in-progress work

.OUTPUTS
    JSON artifact: echo-content-impact-{timestamp}.json
    Report: echo-content-impact-{timestamp}.md
#>
#requires -Version 7.0
[CmdletBinding()]
param(
    [string]$Version,
    [ValidateNotNullOrEmpty()]
    [string]$MetadataRepoPath,
    [ValidateNotNullOrEmpty()]
    [string]$MetadataDir,
    [ValidateNotNullOrEmpty()]
    [string]$ContentRepoPath,
    [ValidateNotNullOrEmpty()]
    [string]$ContentToolsDir,
    [ValidateNotNullOrEmpty()]
    [string]$ChangelogUrl,
    [ValidateNotNullOrEmpty()]
    [string]$OutputDir,
    [switch]$Backfill,
    [int]$AdoItemId,
    [string[]]$DocsPrLinks = @(),
    [string]$BlockerReason,
    [switch]$DryRun,
    [switch]$SkipPrAnalysis,
    [switch]$ChainedFromOrchestrator
)

$ErrorActionPreference = 'Stop'

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$SkillDir = Split-Path -Parent $ScriptDir
$SkillParent = Split-Path -Parent $SkillDir
$GitHubDir = Split-Path -Parent $SkillParent
$RepoRoot = Split-Path -Parent $GitHubDir
. (Join-Path $ScriptDir 'shared-helpers.ps1')

$METADATA_REPO_PATH = 'repos/public-diberry-microsoft-mcp-doc-generation'
$METADATA_DIR = 'mcp-cli-metadata'
$CONTENT_REPO_PATH = 'repos/emu-microsoftdocs-azure-dev-docs-pr'
$CONTENT_TOOLS_DIR = 'articles/azure-mcp-server/tools'
$CHANGELOG_URL = 'https://raw.githubusercontent.com/microsoft/mcp/main/servers/Azure.Mcp.Server/CHANGELOG.md'
$OUTPUT_DIR = 'projects/azure-ai-tools/status'
$REPORT_TEMPLATE_PATH = Join-Path $SkillDir 'templates\report-template.md'

<#
.SYNOPSIS
    Gets all namespace folders from a metadata snapshot.
#>
function Get-NamespacesList {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string]$MetadataRoot
    )

    # The fixed generator (mcp-tools/McpCliMetadata) emits a flat 3-file snapshot per
    # version folder: cli-namespace.json + cli-output.json + cli-version.json.
    # Namespaces come from cli-namespace.json's `results[]` (name/command/description),
    # NOT from per-namespace subdirectories (the obsolete tools-list.json layout).
    $namespaceFile = Join-Path $MetadataRoot 'cli-namespace.json'
    if (-not (Test-Path -Path $namespaceFile)) {
        return @()
    }

    try {
        $data = Get-Content -Path $namespaceFile -Raw | ConvertFrom-Json
    }
    catch {
        return @()
    }

    if ($null -eq $data -or $null -eq $data.results) {
        return @()
    }

    $namespaces = foreach ($ns in $data.results) {
        $name = [string]$ns.name
        if ([string]::IsNullOrWhiteSpace($name)) { continue }
        @{
            name        = $name
            folder      = Split-Path -Path $MetadataRoot -Leaf
            path        = $MetadataRoot
            command     = [string]$ns.command
            description = [string]$ns.description
        }
    }

    return @($namespaces | Sort-Object -Property name -Unique)
}

<#
.SYNOPSIS
    Builds a namespace -> tool-count map from a snapshot's cli-output.json.
.DESCRIPTION
    cli-output.json holds a flat `results[]` of tools; each tool's `command`
    string begins with its namespace token (e.g. "acr registry list" -> "acr").
    Grouping by that first token yields the tool count per namespace. The map is
    built once per version folder and cached by the caller to avoid re-parsing the
    ~1.5MB file for every namespace.
#>
function Get-ToolCountMap {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string]$MetadataRoot
    )

    $map = @{}
    $outputFile = Join-Path $MetadataRoot 'cli-output.json'
    if (-not (Test-Path -Path $outputFile)) {
        return $map
    }

    try {
        $data = Get-Content -Path $outputFile -Raw | ConvertFrom-Json
    }
    catch {
        return $map
    }

    if ($null -eq $data -or $null -eq $data.results) {
        return $map
    }

    foreach ($tool in $data.results) {
        $command = [string]$tool.command
        if ([string]::IsNullOrWhiteSpace($command)) { continue }
        $nsName = ($command -split '\s+')[0]
        if ([string]::IsNullOrWhiteSpace($nsName)) { continue }
        if (-not $map.ContainsKey($nsName)) { $map[$nsName] = 0 }
        $map[$nsName]++
    }

    return $map
}

<#
.SYNOPSIS
    Builds a namespace -> tool/option surface signature map from a snapshot's cli-output.json.
.DESCRIPTION
    For each namespace, the signature is a deterministic concatenation of every tool's
    command plus its sorted option names. Comparing signatures between two snapshots detects
    real surface changes (tools added/removed OR options added/removed) without relying on
    editorial changelog prose. This is the authoritative CHANGED signal for content impact.
#>
function Get-NamespaceSignatureMap {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string]$MetadataRoot
    )

    $sig = @{}
    $outputFile = Join-Path $MetadataRoot 'cli-output.json'
    if (-not (Test-Path -Path $outputFile)) { return $sig }

    try {
        $data = Get-Content -Path $outputFile -Raw | ConvertFrom-Json
    }
    catch {
        return $sig
    }
    if ($null -eq $data -or $null -eq $data.results) { return $sig }

    $byNs = @{}
    foreach ($tool in $data.results) {
        $command = [string]$tool.command
        if ([string]::IsNullOrWhiteSpace($command)) { continue }
        $nsName = ($command -split '\s+')[0]
        if ([string]::IsNullOrWhiteSpace($nsName)) { continue }
        $opts = @()
        if ($tool.option) { $opts = @($tool.option | ForEach-Object { [string]$_.name } | Sort-Object) }
        $line = $command + '::' + ($opts -join ',')
        if (-not $byNs.ContainsKey($nsName)) { $byNs[$nsName] = @() }
        $byNs[$nsName] += $line
    }

    foreach ($k in $byNs.Keys) {
        $sig[$k] = (($byNs[$k] | Sort-Object) -join '||')
    }

    return $sig
}

<#
.SYNOPSIS
    Checks if an article exists for a given namespace in the content repo.
#>
<#
.SYNOPSIS
    Checks if an article exists for a given namespace in the content repo.
.DESCRIPTION
    Uses curated namespace-article-map.json for deterministic namespace->article resolution.
    Falls back to azure-{ns}.md heuristic only for unmapped namespaces.
#>
function Find-ArticleForNamespace {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string]$Namespace,
        
        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string]$ContentToolsPath,
        
        [string]$SnapshotFolder
    )

    if (-not (Test-Path -Path $ContentToolsPath)) {
        return $null
    }

    # Load curated namespace->article map (60 validated entries)
    $mapPath = Join-Path (Split-Path $PSScriptRoot -Parent) 'namespace-article-map.json'
    if (-not (Test-Path $mapPath)) {
        Write-Warning "⚠️  Curated namespace map not found at $mapPath - falling back to heuristic"
        $map = $null
    }
    else {
        try {
            $map = Get-Content -Path $mapPath -Raw | ConvertFrom-Json
        }
        catch {
            Write-Warning "⚠️  Failed to load namespace map from $mapPath - falling back to heuristic"
            $map = $null
        }
    }

    # Deterministic lookup via curated map
    if ($map -and $map.mappings.PSObject.Properties.Name -contains $Namespace) {
        $basename = $map.mappings.$Namespace
        $fileName = "$basename.md"
        $article = Get-ChildItem -Path $ContentToolsPath -Filter $fileName -ErrorAction SilentlyContinue
        if ($article) {
            return $article[0]
        }
    }
    elseif ($map) {
        # Namespace not in map - log warning and try heuristic fallback
        Write-Warning "⚠️  Namespace '$Namespace' not found in curated map - trying heuristic fallback"
    }

    # Last-resort heuristic for unmapped namespaces
    $heuristicPattern = "azure-$($Namespace).md"
    $article = Get-ChildItem -Path $ContentToolsPath -Filter $heuristicPattern -ErrorAction SilentlyContinue
    if ($article) {
        return $article[0]
    }

    return $null
}

<#
.SYNOPSIS
    Extracts version from article frontmatter.
#>
function Get-ArticleVersion {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string]$ArticlePath
    )

    try {
        $content = Get-Content -Path $ArticlePath -Raw -Encoding UTF8
        # Look for mcp-cli.version or similar frontmatter
        if ($content -match 'mcp-cli\.version:\s*(?<version>[^\s\n]+)') {
            return $Matches.version
        }
        if ($content -match 'mcp-server-version:\s*(?<version>[^\s\n]+)') {
            return $Matches.version
        }
        return $null
    }
    catch {
        return $null
    }
}

<#
.SYNOPSIS
    Parses changelog to find release notes for a namespace.
#>
function Get-ReleaseNotesForNamespace {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string]$ChangelogContent,
        
        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string]$Version,
        
        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string]$Namespace
    )

    # Find the version section
    $versionPattern = "^##\s+$([regex]::Escape($Version))\s*\("
    $lines = $ChangelogContent -split "`n"
    
    $versionStartIndex = -1
    $nextVersionIndex = -1
    
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match $versionPattern) {
            $versionStartIndex = $i
        }
        elseif ($versionStartIndex -ge 0 -and $lines[$i] -match '^\s*##\s+' -and $i -gt $versionStartIndex) {
            $nextVersionIndex = $i
            break
        }
    }

    if ($versionStartIndex -lt 0) {
        return @()
    }

    $endIndex = if ($nextVersionIndex -ge 0) { $nextVersionIndex } else { $lines.Count }
    $versionSection = @($lines[$versionStartIndex..$endIndex])
    
    # Look for namespace mentions
    $namespacePattern = "(?:^|\s)$([regex]::Escape($Namespace))(?:\s|$|:|\.)"
    $notes = @()
    foreach ($line in $versionSection) {
        if ($line -match $namespacePattern) {
            $notes += $line.Trim()
        }
    }

    return @($notes)
}

<#
.SYNOPSIS
    Determines change type based on release notes content.
#>
function Get-ChangeType {
    [CmdletBinding()]
    param(
        [object[]]$ReleaseNotes = @()
    )

    if ($null -eq $ReleaseNotes -or $ReleaseNotes.Count -eq 0) {
        return 'None'
    }

    $text = ($ReleaseNotes -join ' ').ToLower()
    
    $types = @()
    if ($text -match 'breaking|deprecated|removed|changed') { $types += 'Breaking' }
    if ($text -match 'bug|fix|fixed') { $types += 'Bugs' }
    if ($text -match 'feature|added|new') { $types += 'Features' }
    
    if ($types.Count -eq 0) {
        return 'Changes'
    }
    
    return ($types | Select-Object -Unique) -join ', '
}

<#
.SYNOPSIS
    Determines impact type (NEW, CHANGED, UNCHANGED) and priority.
#>
function Get-ImpactClassification {
    [CmdletBinding()]
    param(
        [object]$Article,
        
        [Parameter(Mandatory)]
        [string]$ChangeType
    )

    if ($null -eq $Article) {
        return @{
            impactType = 'NEW'
            priority   = 'CRITICAL'
            action     = 'Create article and document current release changes'
        }
    }

    if ($ChangeType -eq 'None') {
        return @{
            impactType = 'UNCHANGED'
            priority   = 'LOW'
            action     = 'No content change needed'
        }
    }

    if ($ChangeType -match 'Breaking') {
        return @{
            impactType = 'CHANGED'
            priority   = 'HIGH'
            action     = 'Update article'
        }
    }

    return @{
        impactType = 'CHANGED'
        priority   = 'MEDIUM'
        action     = 'Update article'
    }
}

# ADO configuration (from environment or defaults)
$ADO_ORG_URL = if ([string]::IsNullOrWhiteSpace($env:ADO_ORG_URL)) { 'https://dev.azure.com/msft-skilling' } else { $env:ADO_ORG_URL }
$ADO_PROJECT = if ([string]::IsNullOrWhiteSpace($env:ADO_PROJECT)) { 'Content' } else { $env:ADO_PROJECT }
$NO_IMPACT_REASON_CODES = @(
    'INTERNAL_ONLY_BUG_FIX',
    'METADATA_MISSING_BLOCKED',
    'NO_NAMESPACE_TOOL_DIFFS',
    'CHANGELOG_MISSING_BLOCKED'
)

function Assert-Inputs {
    [CmdletBinding()]
    param(
        [string]$VersionValue,
        [switch]$IsBackfill,
        [int]$BackfillAdoItemId
    )

    if ($IsBackfill) {
        if ([string]::IsNullOrWhiteSpace($VersionValue)) {
            throw 'Backfill mode requires -Version <releaseVersion>.'
        }
        if ($BackfillAdoItemId -le 0) {
            throw 'Backfill mode requires -AdoItemId <existing-work-item-id>. No creation path is allowed in backfill mode.'
        }
    }
}

<#
.SYNOPSIS
    Finds an existing ADO work item for a given version.
#>
function Find-AdoWorkItemForVersion {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string]$Version
    )

    $az = Get-Command az -ErrorAction SilentlyContinue
    if (-not $az) {
        return $null
    }

    try {
        # Search for the work item by version — try canonical "Azure MCP Server"
        # titles first, then legacy "MCP |" variants.
        $json = & $az.Source boards work-item list --org $ADO_ORG_URL --project $ADO_PROJECT --query "[?contains(title, 'Azure MCP Server $Version') && state != 'Closed'].id" --output json 2>$null
        if ([string]::IsNullOrWhiteSpace($json) -or ($json | ConvertFrom-Json).Count -eq 0) {
            $json = & $az.Source boards work-item list --org $ADO_ORG_URL --project $ADO_PROJECT --query "[?contains(title, 'MCP | $Version') && state != 'Closed'].id" --output json 2>$null
        }
        if ([string]::IsNullOrWhiteSpace($json) -or ($json | ConvertFrom-Json).Count -eq 0) {
            $json = & $az.Source boards work-item list --org $ADO_ORG_URL --project $ADO_PROJECT --query "[?contains(title, 'mcp | $Version') && state != 'Closed'].id" --output json 2>$null
        }
        if ([string]::IsNullOrWhiteSpace($json)) {
            return $null
        }

        $items = $json | ConvertFrom-Json
        if ($items.Count -eq 0) {
            return $null
        }

        # Return the first matching work item ID
        return $items[0]
    }
    catch {
        return $null
    }
}

<#
.SYNOPSIS
    Gets an ADO work item by its numeric id.
#>
function Get-AdoWorkItemById {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [int]$WorkItemId
    )

    $az = Get-Command az -ErrorAction SilentlyContinue
    if (-not $az) {
        throw "Azure CLI ('az') is required to resolve ADO work item id $WorkItemId. Install Azure CLI and run 'az login'."
    }

    try {
        # NOTE: 'az boards work-item show' does NOT accept --project, and --fields
        # conflicts with the default --expand. Use the minimal form and read fields
        # from the returned object (.id, .fields.'System.Title', .fields.'System.State').
        $json = & $az.Source boards work-item show --id $WorkItemId --org $ADO_ORG_URL --output json 2>$null
        if ([string]::IsNullOrWhiteSpace($json)) {
            return $null
        }

        return ($json | ConvertFrom-Json)
    }
    catch {
        return $null
    }
}

<#
.SYNOPSIS
    Resolves the target ADO work item id for backfill or version-search modes.
#>
function Resolve-AdoTargetWorkItem {
    [CmdletBinding()]
    param(
        [string]$VersionValue,
        [switch]$IsBackfill,
        [int]$BackfillAdoItemId
    )

    if ($IsBackfill) {
        $workItem = Get-AdoWorkItemById -WorkItemId $BackfillAdoItemId
        if (-not $workItem) {
            throw "Backfill failed: could not resolve ADO work item #$BackfillAdoItemId. Verify the ID exists in $ADO_PROJECT and retry."
        }

        $title = [string]$workItem.fields.'System.Title'
        $matchesVersion = $title -like "*MCP | $VersionValue*" -or $title -like "*mcp | $VersionValue*" -or $title -like "*Azure MCP Server $VersionValue*"
        if (-not $matchesVersion) {
            throw "Backfill failed: ADO work item #$BackfillAdoItemId title '$title' does not match version '$VersionValue'. Use matching -Version/-AdoItemId values."
        }

        return [int]$workItem.id
    }

    if ([string]::IsNullOrWhiteSpace($VersionValue)) {
        return $null
    }

    return Find-AdoWorkItemForVersion -Version $VersionValue
}

<#
.SYNOPSIS
    Builds an impact summary comment for the ADO work item.
#>
function New-AdoImpactComment {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [object[]]$ImpactMatrix,
        [string]$ReportUrl,
        [hashtable]$NoImpactContract,
        [string]$VersionValue,
        [switch]$IsBackfill,
        [int]$TargetAdoItemId
    )

    # All three counts come from the TARGET version rows so they reconcile:
    # NEW + CHANGED + UNCHANGED == number of namespaces in the target snapshot.
    # (Earlier logic pulled CHANGED from baseline rows, which double-counted and made
    # totals exceed the namespace count.) CHANGED is set upstream via surface-signature diff.
    $versions = @($ImpactMatrix.version | Select-Object -Unique | Sort-Object)
    if ($versions.Count -eq 0) {
        throw 'New-AdoImpactComment: ImpactMatrix contains no version rows; cannot build a comment.'
    }
    $targetVersion = $versions[-1]

    $targetRows = @($ImpactMatrix | Where-Object { $_.version -eq $targetVersion })
    $context = Get-AdoVerdictContext -TargetRows $targetRows -NoImpactContract $NoImpactContract -VersionValue $VersionValue
    $newItems = @($context.NewItems)
    $unchangedItems = @($context.UnchangedItems)
    $changedItems = @($context.ChangedItems)
    
    $newCount = $newItems.Count
    $changedCount = $changedItems.Count
    $unchangedCount = $unchangedItems.Count

    # ADO 'az boards work-item update --discussion' truncates on any newline and
    # strips '&', non-ASCII, and emoji. Emit SINGLE-LINE ASCII-only HTML: <b>, <br>,
    # <ul>/<li>, and <a href='...'> render; use ' / ' and ' and ' instead of '&'.
    # Sanitize any dynamic value to stay ASCII and drop ampersands/angle brackets.
    $san = {
        param($s)
        $t = [string]$s
        $t = $t -replace '&', ' and '
        $t = $t -replace '[<>]', ''
        $t = ($t.ToCharArray() | Where-Object { [int]$_ -ge 32 -and [int]$_ -le 126 }) -join ''
        return $t.Trim()
    }

    $mode = if ($IsBackfill) { 'BACKFILL (update existing item)' } else { 'STANDARD (auto-resolved item)' }
    $generated = (Get-Date -Format 'yyyy-MM-dd HH:mm:ss') + ' UTC'

    # Verdict banner (first line, ASCII-only): makes the final answer unambiguous even
    # when multiple runs stack as separate comments on the same work item. Derived only
    # from data already computed above - no new parameters required.
    $actionCount = $newCount + $changedCount
    $decisionValue = if ($NoImpactContract) { [string]$NoImpactContract.decision } else { '' }
    if ($actionCount -eq 0 -and $decisionValue -like 'NO_CONTENT_CHANGE*' -and $decisionValue -notlike '*PENDING*') {
        $verdict = '[FINAL: NO DOC IMPACT]'
    }
    elseif ($actionCount -gt 0) {
        $verdict = "[ACTION NEEDED: $newCount new / $changedCount changed]"
    }
    else {
        $verdict = '[NO DELTA DETECTED - needs review]'
    }

    # Scope line: names which comparison produced these counts, so a "no impact" verdict
    # (release delta) is never confused with a "tools exist but undocumented" coverage gap.
    $scope = if ($versions.Count -gt 1) {
        "release delta ($(& $san $versions[0]) -> $(& $san $targetVersion))"
    }
    else {
        "single-version snapshot ($(& $san $targetVersion))"
    }

    $parts = @()
    $parts += "<b>$verdict</b>"
    $parts += "<br><b>Content Impact Summary (Step 3) - $(& $san $VersionValue)</b>"
    $parts += "<br>Scope: $scope"
    $parts += "<br>Generated: $generated"
    $parts += "<br>Note: this is the latest Echo run; earlier comments on this item are prior runs."
    $parts += "<br>Mode: $mode / Target ADO item: #$TargetAdoItemId"
    $parts += "<br><b>Impact breakdown:</b> NEW=$newCount / CHANGED=$changedCount / UNCHANGED=$unchangedCount"

    if ($newItems.Count -gt 0) {
        $parts += "<br><b>NEW namespaces (article generation required):</b>"
        $li = @($newItems | ForEach-Object { "<li>$(& $san $_.namespace) ($($_.toolCount) tools)</li>" }) -join ''
        $parts += "<ul>$li</ul>"
    }

    if ($changedItems.Count -gt 0) {
        $parts += "<b>CHANGED namespaces (update existing articles):</b>"
        $li = ''
        foreach ($priority in @('HIGH', 'MEDIUM', 'LOW')) {
            $items = @($changedItems | Where-Object { $_.priority -eq $priority })
            foreach ($it in $items) {
                $li += "<li>[$priority] $(& $san $it.namespace): $(& $san $it.action)</li>"
            }
        }
        $parts += "<ul>$li</ul>"
    }

    if ($ReportUrl) {
        $parts += "<br>Report: $(& $san $ReportUrl)"
    }

    if ($NoImpactContract) {
        $reasonCodes = if ($NoImpactContract.reasonCodes -and $NoImpactContract.reasonCodes.Count -gt 0) {
            ($NoImpactContract.reasonCodes -join ', ')
        }
        else {
            'UNSPECIFIED_NEEDS_REVIEW'
        }
        $parts += "<br><b>No-impact contract:</b> Decision: $(& $san $NoImpactContract.decision) / Reason: $(& $san $reasonCodes) / Evidence: $(& $san $NoImpactContract.evidence) / Unblock: $(& $san $NoImpactContract.unblockStep)"
    }

    # Join with no newlines so the discussion comment is a single line.
    return ($parts -join '')
}

<#
.SYNOPSIS
    Updates an ADO work item with the impact summary.
#>
function Update-AdoWorkItemWithImpact {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [int]$WorkItemId,
        [Parameter(Mandatory)]
        [string]$ImpactComment
    )

    $az = Get-Command az -ErrorAction SilentlyContinue
    if (-not $az) {
        return $false
    }

    try {
        # 'az boards work-item comment add' does NOT exist in this az CLI; use
        # 'work-item update --discussion' (writes to System.History). It does not
        # take --project. Capture stderr and check the exit code so a failed write
        # is not reported as success.
        $err = & $az.Source boards work-item update --id $WorkItemId --discussion $ImpactComment --org $ADO_ORG_URL --output json 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Failed to update ADO work item $WorkItemId (exit $LASTEXITCODE): $($err | Out-String)"
            return $false
        }
        return $true
    }
    catch {
        Write-Warning "Failed to update ADO work item $WorkItemId : $_"
        return $false
    }
}

# ── Mandatory on-item VERDICT + trace (standing rule) ───────────────────────
# Every Echo release work item MUST carry an explicit verdict (CONTENT IMPACT /
# NO CONTENT IMPACT) and a self-contained trace in BOTH System.Description and
# AcceptanceCriteria. ADO CLI field writes strip ampersands, non-ASCII text, and
# truncate on newlines, so these helpers emit single-line ASCII-only HTML.

function ConvertTo-AdoSafeText {
    [CmdletBinding()]
    param([AllowNull()][string]$Text)

    if ([string]::IsNullOrEmpty($Text)) { return '' }
    $t = $Text -replace '&', 'and' -replace '<', '(' -replace '>', ')'
    $sb = [System.Text.StringBuilder]::new()
    foreach ($ch in $t.ToCharArray()) {
        $code = [int]$ch
        if ($code -ge 32 -and $code -lt 127) {
            [void]$sb.Append($ch)
        }
        elseif ($code -eq 9 -or $code -eq 10 -or $code -eq 13) {
            [void]$sb.Append(' ')
        }
    }
    return ($sb.ToString() -replace '\s+', ' ').Trim()
}

function Get-AdoArticlePath {
    [CmdletBinding()]
    param([AllowNull()][string]$ArticlePath)

    if ([string]::IsNullOrWhiteSpace($ArticlePath)) {
        return '/azure/developer/azure-mcp-server/tools/'
    }
    $safe = (ConvertTo-AdoSafeText $ArticlePath).Replace('\', '/')
    if ($safe -like 'articles/azure-mcp-server/tools/*') {
        return '/' + $safe.Substring('articles/'.Length)
    }
    if ($safe -like '*.md') {
        return "/azure/developer/azure-mcp-server/tools/$safe"
    }
    return $safe
}

function Get-NamespaceFromSourcePath {
    [CmdletBinding()]
    param([AllowNull()][string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path)) { return $null }
    if ($Path -match 'Azure\.Mcp\.Tools\.([^/\\]+)') {
        return $Matches[1].ToLowerInvariant()
    }
    return $null
}

function Format-AdoPrFileSummary {
    [CmdletBinding()]
    param([object]$Pr)

    $files = @($Pr.files | Where-Object { $_ })
    if ($files.Count -eq 0) { return 'files unavailable from gh' }

    $namespaces = @($files | ForEach-Object { Get-NamespaceFromSourcePath -Path $_ } | Where-Object { $_ } | Select-Object -Unique)
    if ($namespaces.Count -gt 0) {
        return (ConvertTo-AdoSafeText (('namespace folders: ' + ($namespaces -join ', ') + " ($($files.Count) changed files)")))
    }

    $sample = @($files | Select-Object -First 3)
    $suffix = if ($files.Count -gt $sample.Count) { " plus $($files.Count - $sample.Count) more" } else { '' }
    return (ConvertTo-AdoSafeText (($sample -join ', ') + $suffix))
}

function Get-ReleaseTraceForVersion {
    [CmdletBinding()]
    param(
        [string]$ChangelogContent,
        [string]$VersionValue,
        [object[]]$TargetRows
    )

    # Decision flow: find the target version's CHANGELOG section, extract referenced
    # release PRs, then map changed source files back to tool article namespaces.
    $releasePrs = @()
    if (-not [string]::IsNullOrWhiteSpace($ChangelogContent) -and -not [string]::IsNullOrWhiteSpace($VersionValue)) {
        $lines = $ChangelogContent -split "`n"
        $start = -1
        for ($i = 0; $i -lt $lines.Count; $i++) {
            if ($lines[$i] -match "^\s*##\s+$([regex]::Escape($VersionValue))(\s|\(|$)") {
                $start = $i
                break
            }
        }
        if ($start -ge 0) {
            $end = $lines.Count
            for ($i = $start + 1; $i -lt $lines.Count; $i++) {
                if ($lines[$i] -match '^\s*##\s+') { $end = $i; break }
            }
            $section = ($lines[$start..($end - 1)] -join "`n")
            $matches = [regex]::Matches($section, 'github\.com/(?<owner>[^/\s\)]+)/(?<repo>[^/\s\)]+)/pull/(?<number>\d+)|(?<![\w/])#(?<short>\d+)')
            $seen = @{}
            foreach ($m in $matches) {
                $owner = if ($m.Groups['owner'].Success) { $m.Groups['owner'].Value } else { 'microsoft' }
                $repo = if ($m.Groups['repo'].Success) { $m.Groups['repo'].Value } else { 'mcp' }
                $number = if ($m.Groups['number'].Success) { [int]$m.Groups['number'].Value } else { [int]$m.Groups['short'].Value }
                $key = "$owner/$repo#$number"
                if ($seen.ContainsKey($key)) { continue }
                $seen[$key] = $true
                $url = "https://github.com/$owner/$repo/pull/$number"
                $title = ''
                # This trace feeds the ADO item body. The discussion comment is a terse latest-run
                # summary; Description/AcceptanceCriteria carry the durable PR/file/article trace.
                $files = @()
                $gh = Get-Command gh -ErrorAction SilentlyContinue
                if ($gh) {
                    try {
                        $json = & $gh.Source pr view $number --repo "$owner/$repo" --json title,files,url,number 2>$null
                        if (-not [string]::IsNullOrWhiteSpace($json)) {
                            $prObj = $json | ConvertFrom-Json
                            $title = [string]$prObj.title
                            $url = [string]$prObj.url
                            $files = @($prObj.files | ForEach-Object { [string]$_.path } | Where-Object { $_ })
                        }
                    }
                    catch {
                        $files = @()
                    }
                }
                $releasePrs += [pscustomobject]@{
                    owner  = $owner
                    repo   = $repo
                    number = $number
                    url    = $url
                    title  = $title
                    files  = @($files)
                }
            }
        }
    }

    $articleByNamespace = @{}
    foreach ($row in @($TargetRows)) {
        if (-not [string]::IsNullOrWhiteSpace($row.namespace)) {
            $articleByNamespace[$row.namespace.ToLowerInvariant()] = $row.articlePath
        }
    }

    $files = @()
    foreach ($pr in $releasePrs) {
        foreach ($file in @($pr.files)) {
            $ns = Get-NamespaceFromSourcePath -Path $file
            $article = if ($ns -and $articleByNamespace.ContainsKey($ns)) { $articleByNamespace[$ns] } else { $null }
            $files += [pscustomobject]@{
                prNumber      = $pr.number
                prUrl         = $pr.url
                filePath      = $file
                namespace     = $ns
                articlePath   = $article
                noImpactReason = if ($ns) { 'No reader-visible tool or option delta detected for this namespace.' } else { 'Source file does not map to an Azure MCP tool article namespace.' }
            }
        }
    }

    return [pscustomobject][ordered]@{
        sourceRepo = 'microsoft/mcp'
        version = $VersionValue
        versionDetermination = "Compared metadata snapshot $VersionValue against the previous available metadata snapshot and the upstream Azure MCP Server CHANGELOG.md entry."
        releasePrs = @($releasePrs)
        files = @($files)
    }
}

function Get-AdoVerdictContext {
    [CmdletBinding()]
    param(
        [object[]]$TargetRows,
        [hashtable]$NoImpactContract,
        [string]$VersionValue
    )

    $newItems = @($TargetRows | Where-Object { $_.impactType -eq 'NEW' })
    $changedItems = @($TargetRows | Where-Object { $_.impactType -eq 'CHANGED' })
    $unchangedItems = @($TargetRows | Where-Object { $_.impactType -eq 'UNCHANGED' })
    $isNoImpact = ($newItems.Count -eq 0 -and $changedItems.Count -eq 0)
    $reasonCodes = if ($NoImpactContract -and $NoImpactContract.reasonCodes -and $NoImpactContract.reasonCodes.Count -gt 0) {
        ConvertTo-AdoSafeText ($NoImpactContract.reasonCodes -join ', ')
    }
    else {
        'UNSPECIFIED_NEEDS_REVIEW'
    }

    return [pscustomobject][ordered]@{
        NewItems       = @($newItems)
        ChangedItems   = @($changedItems)
        UnchangedItems = @($unchangedItems)
        IsNoImpact     = $isNoImpact
        Verdict        = if ($isNoImpact) { 'NO CONTENT IMPACT' } else { 'CONTENT IMPACT' }
        SafeVersion    = ConvertTo-AdoSafeText $VersionValue
        ReasonCodes    = $reasonCodes
    }
}

function New-AdoTraceReleasePrsHtml {
    [CmdletBinding()]
    param([object]$Trace)

    $parts = [System.Collections.Generic.List[string]]::new()
    $parts.Add('<h3>Release PRs inspected</h3>')
    if (@($Trace.releasePrs).Count -eq 0) {
        $parts.Add('<p>No release PR links were found in the CHANGELOG entry; metadata snapshot diff was still inspected.</p>')
    }
    else {
        $parts.Add('<ul>')
        foreach ($pr in @($Trace.releasePrs)) {
            $fileText = Format-AdoPrFileSummary -Pr $pr
            $parts.Add("<li><a href=`"$($pr.url)`">PR #$($pr.number)</a>; files: <code>$fileText</code>.</li>")
        }
        $parts.Add('</ul>')
    }
    return $parts.ToArray()
}

function New-AdoArticleDecisionTraceHtml {
    [CmdletBinding()]
    param(
        [object]$Context,
        [object]$Trace,
        [hashtable]$NoImpactContract,
        [string[]]$DocsPrLinks = @(),
        [string]$BlockerReason
    )

    $parts = [System.Collections.Generic.List[string]]::new()
    $parts.Add('<h3>Article mapping and content decision</h3>')
    if (-not $Context.IsNoImpact) {
        $parts.Add('<ul>')
        foreach ($row in @($Context.NewItems + $Context.ChangedItems)) {
            $classification = ConvertTo-AdoSafeText $row.impactType
            $ns = ConvertTo-AdoSafeText $row.namespace
            $article = Get-AdoArticlePath $row.articlePath
            $parts.Add("<li><b>$classification</b> <code>$ns</code> -> <code>$article</code>.</li>")
        }
        $parts.Add('</ul>')
        if ($DocsPrLinks -and $DocsPrLinks.Count -gt 0) {
            $parts.Add('<p><b>Docs PR(s):</b> ' + ((@($DocsPrLinks) | ForEach-Object { "<a href=`"$_`">$_</a>" }) -join '; ') + '</p>')
        }
        elseif (-not [string]::IsNullOrWhiteSpace($BlockerReason)) {
            $parts.Add("<p><b>Docs PR blocker:</b> $(ConvertTo-AdoSafeText $BlockerReason)</p>")
        }
        else {
            $parts.Add('<p><b>Docs PR status:</b> content impact exists; create and link the docs PR, or record an Azure resource / azd blocker before closing this item.</p>')
        }
    }
    else {
        $parts.Add("<p>No reader-visible namespace, tool, option, or description delta requires docs work.</p><h4>No-impact contract</h4><ul><li><b>Decision:</b> $(ConvertTo-AdoSafeText $NoImpactContract.decision)</li><li><b>Reason code(s):</b> $($Context.ReasonCodes)</li><li><b>Evidence:</b> $(ConvertTo-AdoSafeText $NoImpactContract.evidence)</li><li><b>Unblock:</b> $(ConvertTo-AdoSafeText $NoImpactContract.unblockStep)</li></ul>")
        $parts.Add('<h4>PRs/files inspected and why no docs changed</h4><ul>')
        if (@($Trace.files).Count -eq 0) {
            $parts.Add('<li>No source file list was available; the metadata surface diff still showed zero NEW and zero CHANGED namespaces.</li>')
        }
        else {
            foreach ($f in @($Trace.files)) {
                $parts.Add("<li><a href=`"$($f.prUrl)`">PR #$($f.prNumber)</a> file <code>$(ConvertTo-AdoSafeText $f.filePath)</code>: $(ConvertTo-AdoSafeText $f.noImpactReason)</li>")
            }
        }
        $parts.Add('</ul>')
    }
    return $parts.ToArray()
}

function New-AdoVerdictDescriptionHtml {
    [CmdletBinding()]
    param(
        [object[]]$ImpactMatrix,
        [object[]]$TargetRows,
        [hashtable]$NoImpactContract,
        [object]$Trace,
        [string]$VersionValue,
        [string]$ReportRelativePath,
        [string]$AttachmentFileName,
        [switch]$AttachmentSucceeded,
        [string[]]$DocsPrLinks = @(),
        [string]$BlockerReason
    )

    $null = $ImpactMatrix
    $context = Get-AdoVerdictContext -TargetRows $TargetRows -NoImpactContract $NoImpactContract -VersionValue $VersionValue
    $parts = [System.Collections.Generic.List[string]]::new()
    $parts.Add("<h2>VERDICT: $($context.Verdict) - Azure MCP Server $($context.SafeVersion)</h2>")
    $parts.Add("<p><b>Source and version:</b> $(ConvertTo-AdoSafeText $Trace.sourceRepo) <code>$($context.SafeVersion)</code>; determined by metadata diff against previous snapshot plus CHANGELOG.</p>")
    foreach ($line in (New-AdoTraceReleasePrsHtml -Trace $Trace)) { $parts.Add($line) }
    foreach ($line in (New-AdoArticleDecisionTraceHtml -Context $context -Trace $Trace -NoImpactContract $NoImpactContract -DocsPrLinks $DocsPrLinks -BlockerReason $BlockerReason)) { $parts.Add($line) }
    $parts.Add("<p><b>Classification totals:</b> $(@($context.NewItems).Count) NEW / $(@($context.ChangedItems).Count) CHANGED / $(@($context.UnchangedItems).Count) UNCHANGED.</p>")
    if ($AttachmentSucceeded) {
        $parts.Add("<p><b>Impact report:</b> see the attached file <code>$(ConvertTo-AdoSafeText $AttachmentFileName)</code> on this work item.</p>")
    }
    else {
        $parts.Add("<p><b>Impact report:</b> generated locally at <code>$(ConvertTo-AdoSafeText $ReportRelativePath)</code>. NOTE: automatic attachment failed; use the inline trace above as the authoritative record.</p>")
    }
    $parts.Add("<p><i>Written by Echo (echo-content-impact, Step 3) on $((Get-Date).ToString('yyyy-MM-dd')); enforces the on-item verdict and trace rule.</i></p>")
    return ($parts -join '')
}

function New-AdoVerdictAcceptanceHtml {
    [CmdletBinding()]
    param(
        [object[]]$TargetRows,
        [hashtable]$NoImpactContract,
        [object]$Trace,
        [string]$VersionValue,
        [string[]]$DocsPrLinks = @(),
        [string]$BlockerReason
    )

    $context = Get-AdoVerdictContext -TargetRows $TargetRows -NoImpactContract $NoImpactContract -VersionValue $VersionValue
    $parts = [System.Collections.Generic.List[string]]::new()
    $parts.Add('<ul>')
    $parts.Add("<li>VERDICT on this item is <b>$($context.Verdict)</b> for Azure MCP Server $($context.SafeVersion), with source repo / version / PR / file / article trace on the item body.</li>")
    foreach ($row in @($context.NewItems)) {
        $parts.Add("<li>Create or regenerate article <code>$(Get-AdoArticlePath $row.articlePath)</code> for NEW namespace <code>$(ConvertTo-AdoSafeText $row.namespace)</code>.</li>")
    }
    foreach ($row in @($context.ChangedItems)) {
        $parts.Add("<li>Update article <code>$(Get-AdoArticlePath $row.articlePath)</code> for CHANGED namespace <code>$(ConvertTo-AdoSafeText $row.namespace)</code>.</li>")
    }
    if ($context.IsNoImpact) {
        $parts.Add("<li>Confirm NO CONTENT IMPACT: reason code(s) $($context.ReasonCodes); inspected PR count $(@($Trace.releasePrs).Count); zero reader-visible doc deltas.</li>")
    }
    elseif ($DocsPrLinks -and $DocsPrLinks.Count -gt 0) {
        $parts.Add("<li>Docs PR link(s) are recorded on the item body: $(ConvertTo-AdoSafeText (@($DocsPrLinks) -join '; ')).</li>")
    }
    elseif (-not [string]::IsNullOrWhiteSpace($BlockerReason)) {
        $parts.Add("<li>No docs PR exists because of this recorded blocker: $(ConvertTo-AdoSafeText $BlockerReason).</li>")
    }
    else {
        $parts.Add('<li>Before closing, link the docs PR or record an Azure resource / azd blocker on this item.</li>')
    }
    $parts.Add('</ul>')
    return ($parts -join '')
}

function Update-AdoWorkItemFields {
    [CmdletBinding()]
    param(
        [int]$WorkItemId,
        [string]$DescriptionHtml,
        [string]$AcceptanceHtml
    )

    $az = Get-Command az -ErrorAction SilentlyContinue
    if (-not $az) { return $false }
    try {
        $descOutput = & $az.Source boards work-item update --id $WorkItemId `
            --fields "System.Description=$DescriptionHtml" `
            --org $ADO_ORG_URL --output none 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Failed to write description verdict/trace to ADO work item $WorkItemId : $descOutput"
            return $false
        }

        $acOutput = & $az.Source boards work-item update --id $WorkItemId `
            --fields "Microsoft.VSTS.Common.AcceptanceCriteria=$AcceptanceHtml" `
            --org $ADO_ORG_URL --output none 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Failed to write acceptance-criteria verdict/trace to ADO work item $WorkItemId : $acOutput"
            return $false
        }
        return $true
    }
    catch {
        Write-Warning "Failed to write verdict/trace fields to ADO work item $WorkItemId : $_"
        return $false
    }
}


function Invoke-AdoVerdictTraceUpdate {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [int]$WorkItemId,
        [Parameter(Mandatory)]
        [string]$DescriptionHtml,
        [Parameter(Mandatory)]
        [string]$AcceptanceHtml,
        [Parameter(Mandatory)]
        [string]$ImpactComment,
        [string]$VersionValue,
        [switch]$DryRun,
        [switch]$Backfill,
        [switch]$PendingMetadata,
        [switch]$ShowFieldDryRunDetails
    )

    if ($DryRun) {
        Write-Host "🧪 DryRun: skipped ADO update for work item #$WorkItemId" -ForegroundColor Yellow
        if ($ShowFieldDryRunDetails) {
            Write-Host "🧪 DryRun: System.Description would be set to: $DescriptionHtml" -ForegroundColor Yellow
            Write-Host "🧪 DryRun: AcceptanceCriteria would be set to: $AcceptanceHtml" -ForegroundColor Yellow
        }
        return [pscustomobject][ordered]@{
            Status                    = 'dry-run-skipped'
            DescriptionUpdated        = $false
            AcceptanceCriteriaUpdated = $false
            CommentPosted             = $false
        }
    }

    $descriptionUpdated = $false
    $acceptanceCriteriaUpdated = $false
    $commentPosted = $false

    if (Update-AdoWorkItemFields -WorkItemId $WorkItemId -DescriptionHtml $DescriptionHtml -AcceptanceHtml $AcceptanceHtml) {
        $descriptionUpdated = $true
        $acceptanceCriteriaUpdated = $true
        if ($PendingMetadata) {
            Write-Host "✅ Wrote pending VERDICT + blocker trace to ADO work item #$WorkItemId" -ForegroundColor Green
        }
        else {
            Write-Host "✅ Wrote VERDICT + trace to description and acceptance criteria on ADO work item #$WorkItemId" -ForegroundColor Green
        }
    }
    elseif ($PendingMetadata) {
        Write-Warning "⚠️  Could not write pending verdict/trace fields to ADO work item #$WorkItemId"
    }

    if (Update-AdoWorkItemWithImpact -WorkItemId $WorkItemId -ImpactComment $ImpactComment) {
        $commentPosted = $true
        if ($PendingMetadata) {
            if ($Backfill) {
                Write-Host "✅ Backfill pending-status posted to ADO work item #$WorkItemId for version $VersionValue" -ForegroundColor Green
            }
            else {
                Write-Host "✅ Posted pending-metadata status to ADO work item #$WorkItemId" -ForegroundColor Green
            }
        }
        elseif ($Backfill) {
            Write-Host "✅ Backfill impact comment posted to ADO work item #$WorkItemId for version $VersionValue" -ForegroundColor Green
        }
        else {
            Write-Host "✅ Posted impact summary comment to ADO work item #$WorkItemId" -ForegroundColor Green
        }
    }
    elseif ($PendingMetadata) {
        Write-Warning "⚠️  Could not post pending-metadata comment to ADO work item #$WorkItemId"
    }
    else {
        Write-Warning "⚠️  Could not post impact comment to ADO work item #$WorkItemId"
    }

    return [pscustomobject][ordered]@{
        Status                    = if ($descriptionUpdated -and $acceptanceCriteriaUpdated) { 'updated' } else { 'blocked' }
        DescriptionUpdated        = $descriptionUpdated
        AcceptanceCriteriaUpdated = $acceptanceCriteriaUpdated
        CommentPosted             = $commentPosted
    }
}

function Get-AdoRestToken {
    [CmdletBinding()]
    param()

    $az = Get-Command az -ErrorAction SilentlyContinue
    if (-not $az) { return $null }
    $tenant = if ([string]::IsNullOrWhiteSpace($env:ADO_TENANT_ID)) { '72f988bf-86f1-41af-91ab-2d7cd011db47' } else { $env:ADO_TENANT_ID }
    try {
        $tok = & $az.Source account get-access-token --resource '499b84ac-1321-427f-aa17-267ca6975798' --tenant $tenant --query accessToken -o tsv 2>$null
        if ([string]::IsNullOrWhiteSpace($tok)) { return $null }
        return $tok.Trim()
    }
    catch { return $null }
}

function Add-AdoReportAttachment {
    [CmdletBinding()]
    param(
        [int]$WorkItemId,
        [string]$ReportPath
    )

    if (-not (Test-Path -Path $ReportPath)) {
        Write-Warning "Report file not found for attachment: $ReportPath"
        return $false
    }
    $token = Get-AdoRestToken
    if (-not $token) {
        Write-Warning "Could not acquire an ADO REST token (resource 499b84ac; tenant 72f988bf); skipping report attachment."
        return $false
    }
    $authHeader = 'Bearer ' + $token
    $headers = @{ Authorization = $authHeader }
    $fileName = Split-Path -Leaf $ReportPath
    $orgBase = $ADO_ORG_URL.TrimEnd('/')
    try {
        $uploadUri = "$orgBase/$ADO_PROJECT/_apis/wit/attachments?fileName=$fileName&api-version=7.1"
        $bytes = [System.IO.File]::ReadAllBytes($ReportPath)
        $upload = Invoke-RestMethod -Method Post -Uri $uploadUri -Headers $headers -ContentType 'application/octet-stream' -Body $bytes
        if ([string]::IsNullOrWhiteSpace($upload.url)) {
            Write-Warning "Attachment upload returned no URL for $fileName; report not attached."
            return $false
        }
    }
    catch {
        $detail = if ($_.ErrorDetails -and $_.ErrorDetails.Message) { $_.ErrorDetails.Message } else { $_.Exception.Message }
        Write-Warning "Attachment upload failed for $fileName : $detail"
        return $false
    }
    try {
        $patchUri = "$orgBase/$ADO_PROJECT/_apis/wit/workitems/$WorkItemId" + "?api-version=7.1"
        $patch = @(
            @{ op = 'add'; path = '/relations/-'; value = @{ rel = 'AttachedFile'; url = $upload.url; attributes = @{ comment = 'Echo content impact report (Step 3)' } } }
        )
        $body = ConvertTo-Json -InputObject $patch -Depth 6
        Invoke-RestMethod -Method Patch -Uri $patchUri -Headers $headers -ContentType 'application/json-patch+json' -Body ([System.Text.Encoding]::UTF8.GetBytes($body)) | Out-Null
    }
    catch {
        $detail = if ($_.ErrorDetails -and $_.ErrorDetails.Message) { $_.ErrorDetails.Message } else { $_.Exception.Message }
        Write-Warning "Uploaded $fileName but failed to add AttachedFile relation to work item $WorkItemId : $detail"
        return $false
    }
    return $true
}

# Resolve paths
$resolvedMetadataRepoPath = Resolve-ConfiguredPath -Value $(if ($MetadataRepoPath) { $MetadataRepoPath } else { $METADATA_REPO_PATH }) -RepoRoot $RepoRoot
$resolvedMetadataDir = if ($MetadataDir) { $MetadataDir } else { $METADATA_DIR }
$metadataRoot = Join-Path $resolvedMetadataRepoPath $resolvedMetadataDir
$resolvedContentRepoPath = Resolve-ConfiguredPath -Value $(if ($ContentRepoPath) { $ContentRepoPath } else { $CONTENT_REPO_PATH }) -RepoRoot $RepoRoot
$resolvedContentToolsDir = if ($ContentToolsDir) { $ContentToolsDir } else { $CONTENT_TOOLS_DIR }
$contentToolsPath = Join-Path $resolvedContentRepoPath $resolvedContentToolsDir
$resolvedOutputDir = Resolve-ConfiguredPath -Value $(if ($OutputDir) { $OutputDir } else { $OUTPUT_DIR }) -RepoRoot $RepoRoot

Assert-DirectoryExists -Path $resolvedMetadataRepoPath -Description 'Metadata repository'
$metadataRootExists = Test-Path -Path $metadataRoot -PathType Container
$contentToolsPathExists = Test-Path -Path $contentToolsPath -PathType Container
Ensure-Directory -Path $resolvedOutputDir -Description 'output directory'
Assert-Inputs -VersionValue $Version -IsBackfill:$Backfill -BackfillAdoItemId $AdoItemId

$timestampUtc = (Get-Date).ToUniversalTime()
$timestampIso = $timestampUtc.ToString('yyyy-MM-ddTHH:mm:ssZ')
$fileTimestamp = $timestampUtc.ToString('yyyy-MM-ddTHH-mm-ss-fffZ')

# Get changelog
$resolvedChangelogUrl = if ($ChangelogUrl) { $ChangelogUrl } else { $CHANGELOG_URL }
try {
    $response = Invoke-WebRequest -Uri $resolvedChangelogUrl -UseBasicParsing -ErrorAction Stop
    $changelogContent = $response.Content
}
catch {
    $changelogContent = ''
}

# Get metadata versions (all folders in metadata root)
$metadataVersionFolders = @()
if ($metadataRootExists) {
    $metadataVersionFolders = @(Get-ChildItem -Path $metadataRoot -Directory | Sort-Object Name -Descending)
}

if (-not $metadataRootExists -or $metadataVersionFolders.Count -eq 0) {
    # Try to get version/date/work-item info from the latest Step 1 artifact
    $pendingVersion   = if (-not [string]::IsNullOrWhiteSpace($Version)) { $Version } else { 'unknown' }
    $pendingDate      = 'unknown'
    $pendingWorkItemId = if ($Backfill) { $AdoItemId } else { $null }

    $step1Artifact = Get-LatestJsonArtifact -Directory $resolvedOutputDir -Pattern 'echo-release-detection-*.json'
    if ($step1Artifact) {
        try {
            $step1Envelope = Get-Content -Path $step1Artifact.FullName -Raw | ConvertFrom-Json
            $step1Data = if ($step1Envelope.result) { $step1Envelope.result } else { $step1Envelope }
            if (-not [string]::IsNullOrWhiteSpace($step1Data.LATEST_UPSTREAM_VERSION)) {
                $pendingVersion = $step1Data.LATEST_UPSTREAM_VERSION
            }
            if ($step1Data.ADO_WORK_ITEM_ID) {
                $pendingWorkItemId = $step1Data.ADO_WORK_ITEM_ID
            }
            $releaseCtx = @($step1Data.RELEASE_CONTEXT) |
                Where-Object { $_.version -eq $pendingVersion } |
                Select-Object -First 1
            if ($releaseCtx -and -not [string]::IsNullOrWhiteSpace($releaseCtx.releaseDate)) {
                $pendingDate = $releaseCtx.releaseDate
            }
        }
        catch {
            # Step 1 artifact unreadable — continue with placeholder values
        }
    }

    $pendingNoImpactContract = @{
        decision    = 'NO_CONTENT_CHANGE_PENDING'
        reasonCodes = @('METADATA_MISSING_BLOCKED')
        evidence    = "No metadata snapshot folder found for $pendingVersion at $metadataRoot"
        unblockStep = "./start.sh $pendingVersion, then re-run Step 1 for auto-chain"
    }
    if (-not $pendingNoImpactContract.reasonCodes -or $pendingNoImpactContract.reasonCodes.Count -eq 0) {
        Write-Warning "No-impact contract has no reason codes (allowing run with warning)."
    }
    else {
        $invalidPendingReasons = @($pendingNoImpactContract.reasonCodes | Where-Object { $NO_IMPACT_REASON_CODES -notcontains $_ })
        if ($invalidPendingReasons.Count -gt 0) {
            Write-Warning "No-impact contract contains unrecognized reason codes: $($invalidPendingReasons -join ', ') (allowing run with warning)."
        }
    }

    # Write a minimal pending report so there is always a file showing the script ran
    $pendingReportPath = Join-Path $resolvedOutputDir "echo-content-impact-pending-$fileTimestamp.md"
    $pendingReportLines = @(
        "# Echo — Content Impact Pending"
        ""
        "**Status:** Metadata generation required"
        "**Version:** $pendingVersion"
        "**Release date:** $pendingDate"
        "**Generated:** $timestampIso"
        ""
        "## Action required"
        ""
        "Run the following command from ``repos/public-diberry-microsoft-mcp-doc-generation`` to generate the metadata snapshot:"
        ""
        '```'
        "./start.sh $pendingVersion"
        '```'
        ""
        "Then re-run Step 1 to auto-chain Steps 2 and 3:"
        ""
        '```'
        "pwsh .github/skills/echo-release-detection/scripts/echo-release-detection.ps1 -Version $pendingVersion"
        '```'
        ""
        "Steps 2 and 3 will auto-chain once the metadata snapshot exists."
        ""
        "## No-impact contract"
        ""
        "- **Decision:** $($pendingNoImpactContract.decision)"
        "- **Reason code(s):** $($pendingNoImpactContract.reasonCodes -join ', ')"
        "- **Evidence:** $($pendingNoImpactContract.evidence)"
        "- **Unblock step:** $($pendingNoImpactContract.unblockStep)"
    )
    Set-Content -Path $pendingReportPath -Value ($pendingReportLines -join "`n") -Encoding UTF8
    Write-Host "📄 Wrote pending report: $pendingReportPath" -ForegroundColor Cyan

    # Post a clearly-marked pending-metadata status comment to the ADO work item
    $adoId = if ($pendingWorkItemId) {
        Resolve-AdoTargetWorkItem -VersionValue $pendingVersion -IsBackfill:$Backfill -BackfillAdoItemId $pendingWorkItemId
    }
    elseif (-not [string]::IsNullOrWhiteSpace($pendingVersion) -and $pendingVersion -ne 'unknown') {
        Find-AdoWorkItemForVersion -Version $pendingVersion
    }
    else {
        $null
    }
    if ($adoId) {
        $pendingTrace = [pscustomobject][ordered]@{
            sourceRepo = 'microsoft/mcp'
            version = $pendingVersion
            versionDetermination = "Requested version $pendingVersion could not be compared because no metadata snapshot folder was found."
            releasePrs = @()
            files = @()
        }
        $pendingReportName = Split-Path -Leaf $pendingReportPath
        $pendingAttachSucceeded = $false
        if ($DryRun) {
            $pendingAttachSucceeded = $true
        }
        else {
            $pendingAttachSucceeded = Add-AdoReportAttachment -WorkItemId $adoId -ReportPath $pendingReportPath
        }
        $pendingDescription = New-AdoVerdictDescriptionHtml -ImpactMatrix @() -TargetRows @() -NoImpactContract $pendingNoImpactContract -Trace $pendingTrace -VersionValue $pendingVersion -ReportRelativePath ('projects/azure-ai-tools/status/' + $pendingReportName) -AttachmentFileName $pendingReportName -AttachmentSucceeded:$pendingAttachSucceeded -BlockerReason $pendingNoImpactContract.unblockStep
        $pendingAcceptance = New-AdoVerdictAcceptanceHtml -TargetRows @() -NoImpactContract $pendingNoImpactContract -Trace $pendingTrace -VersionValue $pendingVersion -BlockerReason $pendingNoImpactContract.unblockStep
        $pendingCommentSafe = '<b>[NO DELTA DETECTED - needs review]</b><br><b>Content Impact Pending (Step 3)</b><br>Scope: metadata missing for ' + (ConvertTo-AdoSafeText $pendingVersion) + '<br>Decision: ' + (ConvertTo-AdoSafeText $pendingNoImpactContract.decision) + '<br>Evidence: ' + (ConvertTo-AdoSafeText $pendingNoImpactContract.evidence) + '<br>Unblock: ' + (ConvertTo-AdoSafeText $pendingNoImpactContract.unblockStep)
        $null = Invoke-AdoVerdictTraceUpdate -WorkItemId $adoId -DescriptionHtml $pendingDescription -AcceptanceHtml $pendingAcceptance -ImpactComment $pendingCommentSafe -VersionValue $pendingVersion -DryRun:$DryRun -Backfill:$Backfill -PendingMetadata
    }
    else {
        Write-Host "ℹ️  No ADO work item found for version '$pendingVersion' — skipping ADO comment" -ForegroundColor Yellow
    }

    Write-Host "⚠️  No metadata snapshot found for $pendingVersion. Run ./start.sh $pendingVersion from repos/public-diberry-microsoft-mcp-doc-generation, then re-run Step 1." -ForegroundColor Yellow
    exit 0
}

# Analyze a specific version when requested; otherwise analyze the 2 most recent versions
$versionsToAnalyze = @()
if (-not [string]::IsNullOrWhiteSpace($Version)) {
    $targetFolder = @($metadataVersionFolders | Where-Object { (($_.Name -split '\+')[0]) -eq $Version } | Select-Object -First 1)
    if ($targetFolder.Count -eq 0) {
        throw "Requested version '$Version' was not found in metadata snapshots at '$metadataRoot'. Run metadata generation for this version first."
    }

    $sortedFolders = @($metadataVersionFolders | Sort-Object Name -Descending)
    $targetIndex = [array]::IndexOf($sortedFolders, $targetFolder[0])
    $versionsToAnalyze += $targetFolder[0]
    if ($targetIndex -ge 0 -and ($targetIndex + 1) -lt $sortedFolders.Count) {
        $versionsToAnalyze += $sortedFolders[$targetIndex + 1]
    }
    $versionsToAnalyze = @($versionsToAnalyze | Sort-Object Name)
}
else {
    $versionsToAnalyze = @($metadataVersionFolders | Select-Object -First 2 | Sort-Object Name)
}
$impactMatrix = @()
$namespaceInventory = @{}

# Signature-diff is the authoritative CHANGED signal. Precompute tool/option surface
# signatures for the target (newest) and baseline (previous) snapshots. A namespace is
# CHANGED when its target signature differs from baseline; this is deterministic and does
# NOT depend on editorial changelog prose.
$targetVersionName = ($versionsToAnalyze[-1].Name -split '\+')[0]
$targetSigMap = Get-NamespaceSignatureMap -MetadataRoot $versionsToAnalyze[-1].FullName
$baselineSigMap = if ($versionsToAnalyze.Count -ge 2) { Get-NamespaceSignatureMap -MetadataRoot $versionsToAnalyze[0].FullName } else { @{} }

foreach ($versionFolder in $versionsToAnalyze) {
    $version = ($versionFolder.Name -split '\+')[0]
    $metadataPath = $versionFolder.FullName
    
    $namespaces = Get-NamespacesList -MetadataRoot $metadataPath
    $toolCountMap = Get-ToolCountMap -MetadataRoot $metadataPath
    
    foreach ($ns in $namespaces) {
        $toolCount = if ($toolCountMap.ContainsKey($ns.name)) { $toolCountMap[$ns.name] } else { 0 }
        $article = Find-ArticleForNamespace -Namespace $ns.name -ContentToolsPath $contentToolsPath -SnapshotFolder $metadataPath
        $articleVersion = if ($article) { Get-ArticleVersion -ArticlePath $article.FullName } else { $null }
        $releaseNotes = @(Get-ReleaseNotesForNamespace -ChangelogContent $changelogContent -Version $version -Namespace $ns.name)

        if ($version -eq $targetVersionName) {
            # Determine CHANGED via deterministic surface-signature diff against baseline.
            $tSig = if ($targetSigMap.ContainsKey($ns.name)) { $targetSigMap[$ns.name] } else { '' }
            $bSig = if ($baselineSigMap.ContainsKey($ns.name)) { $baselineSigMap[$ns.name] } else { $null }
            if ($null -eq $bSig) {
                # Namespace absent from baseline snapshot -> newly added tool surface.
                $changeType = if ([string]::IsNullOrEmpty($tSig)) { 'None' } else { 'Changes' }
            }
            elseif ($tSig -ne $bSig) {
                # Refine priority from changelog notes when available; else generic Changes.
                $ct = Get-ChangeType -ReleaseNotes $releaseNotes
                $changeType = if ($ct -eq 'None') { 'Changes' } else { $ct }
            }
            else {
                $changeType = 'None'
            }
        }
        else {
            # Baseline rows are not counted in the ADO summary; keep changelog-derived type.
            $changeType = Get-ChangeType -ReleaseNotes $releaseNotes
        }

        $impact = Get-ImpactClassification -Article $article -ChangeType $changeType
        
        $entry = [pscustomobject]@{
            version         = $version
            namespace       = $ns.name
            toolCount       = $toolCount
            articleStatus   = if ($article) { "Exists ($($article.BaseName).md)" } else { 'Missing' }
            articlePath     = if ($article) { $article.Name } else { $null }
            articleVersion  = $articleVersion
            changeTypes     = $changeType
            impactType      = $impact.impactType
            priority        = $impact.priority
            action          = $impact.action
            releaseNotes    = @($releaseNotes)
        }
        
        $impactMatrix += $entry
        
        # Track namespace inventory
        if (-not $namespaceInventory.ContainsKey($ns.name)) {
            $namespaceInventory[$ns.name] = @()
        }
    }
}

# Calculate summary stats (target version only, so counts reconcile to namespace total)
$allVersions = @($impactMatrix | Select-Object -ExpandProperty version -Unique)
$targetVersionForStats = @($allVersions | Sort-Object -Descending | Select-Object -First 1)[0]
$targetMatrixRows = @($impactMatrix | Where-Object { $_.version -eq $targetVersionForStats })
$newNamespaces = @($targetMatrixRows | Where-Object { $_.impactType -eq 'NEW' } | Select-Object -ExpandProperty namespace -Unique)
$changedNamespaces = @($targetMatrixRows | Where-Object { $_.impactType -eq 'CHANGED' } | Select-Object -ExpandProperty namespace -Unique)
$unchangedNamespaces = @($targetMatrixRows | Where-Object { $_.impactType -eq 'UNCHANGED' } | Select-Object -ExpandProperty namespace -Unique)
$isNoImpact = ($newNamespaces.Count -eq 0 -and $changedNamespaces.Count -eq 0)
$noImpactContract = $null

if ($isNoImpact) {
    $noImpactReasons = @()

    if ($unchangedNamespaces.Count -gt 0) {
        $noImpactReasons += 'NO_NAMESPACE_TOOL_DIFFS'
    }

    if (-not [string]::IsNullOrWhiteSpace($changelogContent)) {
        $allNoneOrBugs = @($impactMatrix | Where-Object { $_.changeTypes -in @('None', 'Bugs') }).Count -eq $impactMatrix.Count
        if ($allNoneOrBugs -and $impactMatrix.Count -gt 0) {
            $noImpactReasons += 'INTERNAL_ONLY_BUG_FIX'
        }
    }
    else {
        $noImpactReasons += 'CHANGELOG_MISSING_BLOCKED'
    }

    if ($noImpactReasons.Count -eq 0) {
        Write-Warning "No-impact detected but no reason code identified (allowing run with warning)."
    }
    else {
        $invalidNoImpactReasons = @($noImpactReasons | Where-Object { $NO_IMPACT_REASON_CODES -notcontains $_ })
        if ($invalidNoImpactReasons.Count -gt 0) {
            Write-Warning "Unrecognized no-impact reason codes: $($invalidNoImpactReasons -join ', ') (allowing run with warning)."
        }
    }

    $evidence = "NEW=$($newNamespaces.Count), CHANGED=$($changedNamespaces.Count), UNCHANGED=$($unchangedNamespaces.Count), analyzedVersions=$($allVersions -join ', ')"
    $noImpactContract = @{
        decision    = 'NO_CONTENT_CHANGE'
        reasonCodes = @($noImpactReasons | Select-Object -Unique)
        evidence    = $evidence
        unblockStep = 'None. If release details change, re-run Step 1 to re-evaluate.'
    }
}

$releaseTrace = Get-ReleaseTraceForVersion -ChangelogContent $changelogContent -VersionValue $targetVersionForStats -TargetRows $targetMatrixRows

$executiveSummary = "Analyzed **$($allVersions.Count)** versions containing **$($namespaceInventory.Keys.Count)** namespaces."
$executiveSummary += "`n`n| Metric | Count |"
$executiveSummary += "`n|---|---:|"
$executiveSummary += "`n| Update mode | $(if ($Backfill) { "BACKFILL → ADO #$AdoItemId" } else { 'STANDARD (auto-resolve by latest version)' }) |"
$executiveSummary += "`n| Total namespaces | $($namespaceInventory.Keys.Count) |"
$executiveSummary += "`n| NEW (no article) | $($newNamespaces.Count) |"
$executiveSummary += "`n| CHANGED (release notes) | $($changedNamespaces.Count) |"
$executiveSummary += "`n| UNCHANGED (no changes) | $($unchangedNamespaces.Count) |"


$workItemsByVersion = @(
    $impactMatrix |
        Group-Object -Property version |
        ForEach-Object {
            $items = @(
                $_.Group |
                    Where-Object { $_.impactType -ne 'UNCHANGED' -and $_.changeTypes -ne 'None' } |
                    ForEach-Object {
                        [ordered]@{
                            namespace          = $_.namespace
                            status             = $_.impactType
                            priority           = $_.priority
                            workType           = if ($_.articleStatus -eq 'Missing' -or $_.impactType -eq 'NEW') { 'full-rewrite' } else { 'surgical-fix' }
                            description        = $_.action
                            toolsAdded         = @()
                            optionsAdded       = @()
                            descriptionChanges = @($_.releaseNotes)
                            affectedArticles   = @($(if ($_.articlePath) { $_.articlePath } else { $null }))
                        }
                    }
            )
            [ordered]@{
                version   = $_.Name
                workItems = $items
            }
        }
)

$noImpactContractText = 'Not applicable — impact includes NEW and/or CHANGED namespaces.'
if ($noImpactContract) {
    $reasonCodesText = if ($noImpactContract.reasonCodes.Count -gt 0) { $noImpactContract.reasonCodes -join ', ' } else { 'UNSPECIFIED_NEEDS_REVIEW' }
    $noImpactContractText = @"
- **Decision:** $($noImpactContract.decision)
- **Reason code(s):** $reasonCodesText
- **Evidence:** $($noImpactContract.evidence)
- **Unblock step:** $($noImpactContract.unblockStep)
"@
}

# Build impact matrix table
$impactRows = $impactMatrix | ForEach-Object {
    "| $($_.version) | $($_.namespace) | $($_.toolCount) | $($_.articleStatus) | $($_.changeTypes) | $($_.impactType) | $($_.priority) | $($_.action) |"
}

$reportPath = Join-Path $resolvedOutputDir ("echo-content-impact-$fileTimestamp.md")
$jsonPath = Join-Path $resolvedOutputDir ("echo-content-impact-$fileTimestamp.json")

$reportValues = @{
    REPORT_TITLE     = 'Echo — Content Impact Analysis'
    SKILL_NAME       = 'echo-content-impact'
    AGENT_NAME       = 'Echo'
    TIMESTAMP        = $timestampIso
    METADATA_SOURCES = ($versionsToAnalyze | ForEach-Object { "`n- $($_.FullName -replace [regex]::Escape($RepoRoot), '')" }) -join ''
    CONTENT_REPO     = "Content repo: $($resolvedContentRepoPath -replace [regex]::Escape($RepoRoot), '')"
    EXECUTIVE_SUMMARY = $executiveSummary
    IMPACT_TABLE     = ($impactRows -join "`n")
    NO_IMPACT_CONTRACT = $noImpactContractText
    ACTION_ITEMS     = "- Review NEW namespaces for articles to create`n- Review CHANGED/HIGH-PRIORITY namespaces for content updates`n- LOW-priority items can be batched or deferred"
}

$reportContent = Render-Template -TemplatePath $REPORT_TEMPLATE_PATH -Values $reportValues
Set-Content -Path $reportPath -Value $reportContent -Encoding UTF8

# Update ADO work item with impact summary
$structuredErrors = @()
$latestVersion = @($allVersions | Sort-Object -Descending | Select-Object -First 1)[0]
$targetVersionForAdo = if ($Backfill) { $Version } else { $latestVersion }
$workItemId = $null
$adoUpdateStatus = 'not-attempted'
$attachmentAttempted = $false
$attachmentSucceeded = $false
$descriptionUpdated = $false
$acceptanceCriteriaUpdated = $false
$commentPosted = $false
if ($Backfill) {
    $workItemId = Resolve-AdoTargetWorkItem -VersionValue $targetVersionForAdo -IsBackfill:$Backfill -BackfillAdoItemId $AdoItemId
}
elseif (-not [string]::IsNullOrWhiteSpace($targetVersionForAdo)) {
    $workItemId = Find-AdoWorkItemForVersion -Version $targetVersionForAdo
}
if ($workItemId) {
    $reportRel = 'projects/azure-ai-tools/status/' + (Split-Path -Leaf $reportPath)
    $reportName = Split-Path -Leaf $reportPath
    if ($DryRun) {
        $attachmentSucceeded = $true
    }
    else {
        $attachmentAttempted = $true
        $attachmentSucceeded = Add-AdoReportAttachment -WorkItemId $workItemId -ReportPath $reportPath
        if ($attachmentSucceeded) {
            Write-Host "✅ Attached impact report '$reportName' to ADO work item #$workItemId" -ForegroundColor Green
        }
        else {
            $structuredErrors += [ordered]@{ code = 'ADO_ATTACHMENT_BLOCKED'; message = 'Report attachment upload or relation add failed; body references local report path with failure note.'; severity = 'warning'; target = "ADO #$workItemId" }
        }
    }
    $descriptionHtml = New-AdoVerdictDescriptionHtml -ImpactMatrix $impactMatrix -TargetRows $targetMatrixRows -NoImpactContract $noImpactContract -Trace $releaseTrace -VersionValue $targetVersionForAdo -ReportRelativePath $reportRel -AttachmentFileName $reportName -AttachmentSucceeded:$attachmentSucceeded -DocsPrLinks $DocsPrLinks -BlockerReason $BlockerReason
    $acceptanceHtml = New-AdoVerdictAcceptanceHtml -TargetRows $targetMatrixRows -NoImpactContract $noImpactContract -Trace $releaseTrace -VersionValue $targetVersionForAdo -DocsPrLinks $DocsPrLinks -BlockerReason $BlockerReason
    $reportUrl = "See attached report on the work item: $reportName"
    $impactComment = New-AdoImpactComment -ImpactMatrix $impactMatrix -ReportUrl $reportUrl -NoImpactContract $noImpactContract -VersionValue $targetVersionForAdo -IsBackfill:$Backfill -TargetAdoItemId $workItemId
    $adoWriteResult = Invoke-AdoVerdictTraceUpdate -WorkItemId $workItemId -DescriptionHtml $descriptionHtml -AcceptanceHtml $acceptanceHtml -ImpactComment $impactComment -VersionValue $targetVersionForAdo -DryRun:$DryRun -Backfill:$Backfill -ShowFieldDryRunDetails
    $adoUpdateStatus = $adoWriteResult.Status
    $descriptionUpdated = $adoWriteResult.DescriptionUpdated
    $acceptanceCriteriaUpdated = $adoWriteResult.AcceptanceCriteriaUpdated
    $commentPosted = $adoWriteResult.CommentPosted
    if ($DryRun) {
        $structuredErrors += [ordered]@{ code = 'ADO_UPDATE_DRY_RUN'; message = "Dry run skipped ADO work item update for #$workItemId."; severity = 'warning'; target = 'ADO_ITEM_ID' }
    }
    else {
        if (-not ($descriptionUpdated -and $acceptanceCriteriaUpdated)) {
            $structuredErrors += [ordered]@{ code = 'ADO_FIELD_WRITE_BLOCKED'; message = 'ADO field update failed for System.Description or AcceptanceCriteria.'; severity = 'warning'; target = "ADO #$workItemId" }
        }
        if (-not $commentPosted) {
            $structuredErrors += [ordered]@{ code = 'ADO_COMMENT_WRITE_BLOCKED'; message = 'ADO discussion comment write failed or is blocked for this identity; use paste-ready content from the run output/report.'; severity = 'warning'; target = "ADO #$workItemId" }
        }
    }
}
elseif ($Backfill) {
    throw "Backfill failed: target ADO work item could not be resolved for version '$targetVersionForAdo'."
}
else {
    $structuredErrors += [ordered]@{ code = 'ADO_WORK_ITEM_NOT_FOUND'; message = 'No matching ADO work item was found; ADO update was not attempted.'; severity = 'warning'; target = 'ADO_ITEM_ID' }
}
$structuredErrors += [ordered]@{ code = 'ADO_COMMENT_WRITE_LIMITED'; message = 'Agents cannot reliably add or delete ADO work-item comments; manual UI paste may be required for comment changes.'; severity = 'warning'; target = 'ADO comments' }

# Output JSON
$resultPayload = [ordered]@{
    TIMESTAMP             = $timestampIso
    FILE_TIMESTAMP        = $fileTimestamp
    VERSION_LIST          = @($allVersions)
    VERSIONS_ANALYZED     = @($allVersions)
    ANALYSES              = @($impactMatrix)
    WORK_ITEMS_BY_VERSION = @($workItemsByVersion)
    VALIDATION_RESULTS    = @()
    H2_STRUCTURE_SUMMARY  = $noImpactContract
    PUBLISH_STATUS        = $(if ($newNamespaces.Count -eq 0 -and $changedNamespaces.Count -eq 0) { 'no-doc-impact' } else { 'content-actions-required' })
    NAMESPACE_COUNT       = $namespaceInventory.Keys.Count
    NEW_NAMESPACE_COUNT   = $newNamespaces.Count
    CHANGED_NAMESPACE_COUNT = $changedNamespaces.Count
    UNCHANGED_NAMESPACE_COUNT = $unchangedNamespaces.Count
    NEW_NAMESPACES        = @($newNamespaces)
    CHANGED_NAMESPACES    = @($changedNamespaces)
    UNCHANGED_NAMESPACES  = @($unchangedNamespaces)
    IMPACT_MATRIX         = @($impactMatrix)
    TRACE                 = [ordered]@{
        sourceRepo = $releaseTrace.sourceRepo
        version = $releaseTrace.version
        versionDetermination = $releaseTrace.versionDetermination
        releasePrs = @($releaseTrace.releasePrs)
        files = @($releaseTrace.files)
        articleMappings = @($targetMatrixRows | ForEach-Object {
            [ordered]@{
                namespace = $_.namespace
                type = $_.impactType
                articlePath = Get-AdoArticlePath $_.articlePath
                action = $_.action
            }
        })
    }
    ATTACHMENT            = [ordered]@{
        attempted = [bool]$attachmentAttempted
        succeeded = [bool]$attachmentSucceeded
        fileName = if ($reportPath) { Split-Path -Leaf $reportPath } else { $null }
        reportPath = $reportPath
    }
    REPORT_PATH           = $reportPath
    JSON_PATH             = $jsonPath
    BACKFILL              = [bool]$Backfill
    DRY_RUN               = [bool]$DryRun
    ADO_ITEM_ID           = $workItemId
    ADO_TARGET_VERSION    = $targetVersionForAdo
    ADO_UPDATE_STATUS     = $adoUpdateStatus
    ADO_UPDATE            = [ordered]@{
        targetWorkItemId = $workItemId
        descriptionUpdated = [bool]$descriptionUpdated
        acceptanceCriteriaUpdated = [bool]$acceptanceCriteriaUpdated
        commentPosted = [bool]$commentPosted
        limitations = @($structuredErrors | Where-Object { $_.target -like 'ADO*' -or $_.target -like "ADO #*" } | ForEach-Object { $_.message })
    }
    DOCS_PR_LINKS         = @($DocsPrLinks)
    BLOCKER_REASON        = $BlockerReason
    nextStep              = 'content-team-handoff'
}
$correlationVersion = if ($allVersions.Count -gt 0) { $allVersions[0] } elseif ($targetVersionForAdo) { $targetVersionForAdo } else { 'no-version' }
$outputStatus = if ($structuredErrors.Count -gt 0) { 'partial' } else { 'success' }
$output = New-StructuredOutputEnvelope -Result $resultPayload -Status $outputStatus -Errors $structuredErrors -Producer 'echo-content-impact' -Schema 'echo-content-impact@1.0.0' -CorrelationId "echo-azure-mcp-$correlationVersion"

$outputJson = $output | ConvertTo-Json -Depth 100
Set-Content -Path $jsonPath -Value $outputJson -Encoding UTF8
$outputJson
