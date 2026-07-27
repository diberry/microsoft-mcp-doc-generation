<#
.SYNOPSIS
    Detects new Azure MCP releases that need downstream processing.

.DESCRIPTION
    This script reads changelog content from a local path or the upstream
    changelog URL, compares discovered versions with the metadata repo, and
    prepares the next-step release details for metadata generation.

.INPUTS
    Optional local changelog content and existing metadata repo artifacts.

.OUTPUTS
    JSON artifact: echo-release-detection-{timestamp}.json
    Report: echo-release-detection-{timestamp}.md
#>
#requires -Version 7.0
[CmdletBinding()]
param(
    [ValidateNotNullOrEmpty()]
    [string]$ChangelogUrl,
    [ValidateNotNullOrEmpty()]
    [string]$ChangelogPath,
    [ValidateNotNullOrEmpty()]
    [string]$MetadataRepoPath,
    [ValidateNotNullOrEmpty()]
    [string]$MetadataDir,
    [ValidateNotNullOrEmpty()]
    [string]$OutputDir,
    [ValidateNotNullOrEmpty()]
    [string]$VersionPattern,
    [string]$Version,
    [int]$ContextWindow = 2,
    [switch]$SkipAdo,
    [switch]$ChainedFromOrchestrator
)

$ErrorActionPreference = 'Stop'

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$SkillDir = Split-Path -Parent $ScriptDir
$SkillParent = Split-Path -Parent $SkillDir
$GitHubDir = Split-Path -Parent $SkillParent
$RepoRoot = Split-Path -Parent $GitHubDir
. (Join-Path $ScriptDir 'shared-helpers.ps1')

$UPSTREAM_CHANGELOG_URL = 'https://raw.githubusercontent.com/microsoft/mcp/main/servers/Azure.Mcp.Server/CHANGELOG.md'
$METADATA_REPO_PATH = 'repos/public-diberry-microsoft-mcp-doc-generation'
$METADATA_DIR = 'mcp-cli-metadata'
$VERSION_PATTERN = '^##\s+(3\.[0-9]+\.[0-9]+(?:-[^\s(]+)?)\s*\((\d{4}-\d{2}-\d{2})\)\s*$'
$OUTPUT_DIR = 'projects/azure-ai-tools/status'
$ADO_ORG_URL = 'https://dev.azure.com/msft-skilling'
$ADO_PROJECT = 'Content'
$REPORT_TEMPLATE_PATH = Join-Path $SkillDir 'templates\report-template.md'

<#
.SYNOPSIS
    Loads changelog content from disk or the upstream URL.
.PARAMETER SourcePath
    Optional local changelog path.
.PARAMETER SourceUrl
    Upstream changelog URL used when no local path is supplied.
#>
function Get-ChangelogContent {
    [CmdletBinding()]
    param(
        [string]$SourcePath,
        [string]$SourceUrl
    )

    if (-not [string]::IsNullOrWhiteSpace($SourcePath)) {
        Assert-FileExists -Path $SourcePath -Description 'Changelog file'
        return Get-Content -Path $SourcePath -Raw -Encoding UTF8
    }

    try {
        $response = Invoke-WebRequest -Uri $SourceUrl -UseBasicParsing -ErrorAction Stop
        return $response.Content
    }
    catch {
        throw "Failed to fetch changelog from '$SourceUrl'. $($_.Exception.Message)"
    }
}

<#
.SYNOPSIS
    Extracts version headers from changelog content.
.PARAMETER Content
    Raw changelog markdown.
.PARAMETER PatternText
    Regex used to match version headings.
#>
function Get-VersionHeaders {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string]$Content,

        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string]$PatternText
    )

    $regex = [regex]::new($PatternText, [System.Text.RegularExpressions.RegexOptions]::Multiline)
    $headers = foreach ($match in $regex.Matches($Content)) {
        [pscustomobject]@{
            version     = $match.Groups[1].Value
            releaseDate = $match.Groups[2].Value
        }
    }

    return @($headers)
}

<#
.SYNOPSIS
    Gets existing metadata versions from the metadata repo.
.PARAMETER MetadataRoot
    Metadata directory containing version folders.
#>
function Get-ExistingMetadataVersions {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string]$MetadataRoot
    )

    $versions = foreach ($directory in Get-ChildItem -Path $MetadataRoot -Directory) {
        # Only consider version-shaped folders (e.g. 3.0.0-beta.22+sha); skip stray dirs like node_modules/test
        if ($directory.Name -match '^\d+\.\d+\.\d+') {
            ($directory.Name -split '\+')[0]
        }
    }

    return @($versions | Sort-Object -Unique)
}

function Get-ReleaseContext {
    [CmdletBinding()]
    param(
        [object[]]$VersionHeaders,

        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string]$MetadataRoot,

        [int]$Window = 2
    )

    if ($null -eq $VersionHeaders -or $VersionHeaders.Count -eq 0) {
        return @()
    }

    $sortedHeaders = @(
        $VersionHeaders |
            Sort-Object { Get-VersionSortKey -Version $_.version } -Descending |
            Select-Object -First ([Math]::Max($Window, 1))
    )

    $context = foreach ($header in $sortedHeaders) {
        $folder = Get-VersionFolder -MetadataRoot $MetadataRoot -Version $header.version
        $commitSha = $null
        if ($folder -and $folder.Name -match '^[^+]+\+(?<sha>[0-9a-f]{7,40})$') {
            $commitSha = $Matches.sha
        }

        [pscustomobject]@{
            version         = $header.version
            releaseDate     = $header.releaseDate
            commitSha       = $commitSha
            snapshotPresent = ($null -ne $folder)
            metadataFolder  = if ($folder) { $folder.Name } else { $null }
            status          = if ($folder) { 'tracked' } else { 'missing-snapshot' }
        }
    }

    return @($context)
}

<#
.SYNOPSIS
    Builds the optional ADO work item description.
.PARAMETER VersionHeaders
    Newly detected versions that need metadata generation.
#>
function New-AdoDescription {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNull()]
        [object[]]$VersionHeaders
    )

    $lines = @(
        '# Upstream CHANGELOG',
        '[CHANGELOG](https://github.com/microsoft/mcp/blob/main/servers/Azure.Mcp.Server/CHANGELOG.md)',
        '',
        '## New Versions to Generate'
    )

    if ($VersionHeaders.Count -eq 0) {
        $lines += '- None.'
    }
    else {
        foreach ($header in $VersionHeaders) {
            $lines += "- $($header.version) ($($header.releaseDate))"
        }
    }

    $lines += @(
        '',
        '## Acceptance Criteria',
        '- [ ] All new version metadata folders generated',
        '- [ ] All metadata PRs created and merged to main',
        '- [ ] Main branch resynced',
        '- [ ] Content impact analysis completed'
    )

    return ($lines -join "`n")
}

<#
.SYNOPSIS
    Checks if an ADO work item already exists for the given version range.
.PARAMETER VersionHeaders
    Version headers to check.
.OUTPUTS
    The work item ID if found, or $null if not found.
#>
function Find-ExistingAdoWorkItem {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNull()]
        [object[]]$VersionHeaders
    )

    if ($VersionHeaders.Count -eq 0) {
        return $null
    }

    $az = Get-Command az -ErrorAction SilentlyContinue
    if (-not $az) {
        return $null
    }

    # Build a search query for the latest version(s)
    $versionList = @($VersionHeaders | ForEach-Object { $_.version })
    $searchQuery = "Azure MCP Server"

    try {
        # Query for open work items — try canonical uppercase "MCP |" first (user-created items),
        # then lowercase "mcp |" (script-created items), then legacy "Azure MCP Server" format.
        $latestVersion = $VersionHeaders[0].version
        $json = & $az.Source boards work-item list --org $ADO_ORG_URL --project $ADO_PROJECT --query "[?title == 'MCP | $latestVersion' && state != 'Closed'].id" --output json 2>$null
        if ([string]::IsNullOrWhiteSpace($json) -or ($json | ConvertFrom-Json).Count -eq 0) {
            # Try lowercase variant
            $json = & $az.Source boards work-item list --org $ADO_ORG_URL --project $ADO_PROJECT --query "[?title == 'mcp | $latestVersion' && state != 'Closed'].id" --output json 2>$null
        }
        if ([string]::IsNullOrWhiteSpace($json) -or ($json | ConvertFrom-Json).Count -eq 0) {
            # Fallback: legacy "Azure MCP Server {version}" format
            $json = & $az.Source boards work-item list --org $ADO_ORG_URL --project $ADO_PROJECT --query "[?contains(title, 'Azure MCP Server') && contains(title, '$latestVersion') && state != 'Closed'].id" --output json 2>$null
        }
        
        if ([string]::IsNullOrWhiteSpace($json)) {
            return $null
        }

        $items = $json | ConvertFrom-Json
        if ($items.Count -eq 0) {
            return $null
        }

        # Return the first matching work item
        return $items[0]
    }
    catch {
        return $null
    }
}

<#
.SYNOPSIS
    Attempts to create an ADO work item for new releases.
.PARAMETER VersionHeaders
    Newly detected versions that need metadata generation.
#>
function Try-CreateAdoWorkItem {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNull()]
        [object[]]$VersionHeaders
    )

    if ($SkipAdo -or $VersionHeaders.Count -eq 0) {
        return $null
    }

    $az = Get-Command az -ErrorAction SilentlyContinue
    if (-not $az) {
        return $null
    }

    # Check if an existing work item already covers these versions
    $existingItemId = Find-ExistingAdoWorkItem -VersionHeaders $VersionHeaders
    if ($existingItemId) {
        return $existingItemId
    }

    $versionRange = if ($VersionHeaders.Count -eq 1) {
        $VersionHeaders[0].version
    }
    else {
        "$($VersionHeaders[-1].version)..$($VersionHeaders[0].version)"
    }

    $title = "MCP | $($VersionHeaders[0].version)"
    $description = New-AdoDescription -VersionHeaders $VersionHeaders

    try {
        $json = & $az.Source boards work-item create `
            --org $ADO_ORG_URL `
            --project $ADO_PROJECT `
            --type 'User Story' `
            --title $title `
            --description $description `
            --parent 576070 `
            --area-path "Content\Production\Core AI\Azure Dev Experiences\AI apps and tools\Azure MCP Server" `
            --state 'New' `
            --assigned-to "Dina Berry" `
            --output json 2>$null
        if ([string]::IsNullOrWhiteSpace($json)) {
            return $null
        }

        $item = $json | ConvertFrom-Json
        return $item.id
    }
    catch {
        return $null
    }
}

$resolvedMetadataRepoPath = Resolve-ConfiguredPath -Value $(if ($MetadataRepoPath) { $MetadataRepoPath } else { $METADATA_REPO_PATH }) -RepoRoot $RepoRoot
$resolvedMetadataDir = if ($MetadataDir) { $MetadataDir } else { $METADATA_DIR }
$metadataRoot = Join-Path $resolvedMetadataRepoPath $resolvedMetadataDir
$resolvedOutputDir = Resolve-ConfiguredPath -Value $(if ($OutputDir) { $OutputDir } else { $OUTPUT_DIR }) -RepoRoot $RepoRoot
$resolvedVersionPattern = if ($VersionPattern) { $VersionPattern } else { $VERSION_PATTERN }
$resolvedChangelogUrl = if ($ChangelogUrl) { $ChangelogUrl } else { $UPSTREAM_CHANGELOG_URL }

Assert-DirectoryExists -Path $resolvedMetadataRepoPath -Description 'Metadata repository'
Assert-DirectoryExists -Path $metadataRoot -Description 'Metadata directory'
Ensure-Directory -Path $resolvedOutputDir -Description 'output directory'

$timestampUtc = (Get-Date).ToUniversalTime()
$timestampIso = $timestampUtc.ToString('yyyy-MM-ddTHH:mm:ssZ')
$fileTimestamp = $timestampUtc.ToString('yyyy-MM-ddTHH-mm-ss-fffZ')

$changelogContent = Get-ChangelogContent -SourcePath $ChangelogPath -SourceUrl $resolvedChangelogUrl
$headers = Get-VersionHeaders -Content $changelogContent -PatternText $resolvedVersionPattern
$existingVersions = Get-ExistingMetadataVersions -MetadataRoot $metadataRoot
$latestMetadataVersion = $existingVersions |
    Sort-Object { Get-VersionSortKey -Version $_ } -Descending |
    Select-Object -First 1

# Handle specified version or detect from changelog
$versionStatus = 'Latest detected'
$detectedVersionHeader = $null
$specifiedVersionStatus = $null

if ($Version) {
    # If specific version requested, look for it
    Write-Verbose "Looking for version $Version in changelog..."
    $detectedVersionHeader = $headers | Where-Object { $_.version -eq $Version } | Select-Object -First 1
    
    if (-not $detectedVersionHeader) {
        Write-Warning "Version $Version not found in CHANGELOG.md. Will create ADO item and report as missing."
        $detectedVersionHeader = [pscustomobject]@{
            version     = $Version
            releaseDate = 'Unknown'
            body        = "[Version $Version not found in CHANGELOG.md]($resolvedChangelogUrl)"
        }
        $specifiedVersionStatus = 'Missing from changelog'
        $versionStatus = $specifiedVersionStatus
    }
    else {
        $specifiedVersionStatus = 'Found in changelog'
        $versionStatus = $specifiedVersionStatus
    }
    
    # When version is explicitly specified, treat it as new for ADO purposes
    $newVersionHeaders = @($detectedVersionHeader)
}
else {
    # Default: detect new versions not yet in metadata
    $newVersionHeaders = @(
        $headers |
            Where-Object {
                ($existingVersions -notcontains $_.version) -and
                ([string]::IsNullOrWhiteSpace($latestMetadataVersion) -or ((Get-VersionSortKey -Version $_.version) -gt (Get-VersionSortKey -Version $latestMetadataVersion)))
            } |
            Sort-Object { Get-VersionSortKey -Version $_.version } -Descending
    )
}

$newVersions = @($newVersionHeaders | ForEach-Object { $_.version })
$releaseContext = Get-ReleaseContext -VersionHeaders $headers -MetadataRoot $metadataRoot -Window $ContextWindow
$contextVersions = @($releaseContext | ForEach-Object { $_.version })
$adoWorkItemId = if ($newVersionHeaders.Count -gt 0) {
    Try-CreateAdoWorkItem -VersionHeaders $newVersionHeaders
}
else {
    $null
}

$reportPath = Join-Path $resolvedOutputDir ("echo-release-detection-$fileTimestamp.md")
$jsonPath = Join-Path $resolvedOutputDir ("echo-release-detection-$fileTimestamp.json")

$newVersionsList = if ($newVersionHeaders.Count -eq 0) {
    '- None. Existing metadata already covers the detected 3.x releases.'
}
else {
    ($newVersionHeaders | ForEach-Object { "- $($_.version) ($($_.releaseDate))" }) -join "`n"
}

$versionStatusMessage = if ($specifiedVersionStatus -eq 'Missing from changelog') {
    @"
**$specifiedVersionStatus** — Version $($newVersionHeaders[0].version) was not found in the Azure MCP CHANGELOG.

[$resolvedChangelogUrl]($resolvedChangelogUrl)

This version may be:
- Not yet released
- Released but CHANGELOG not updated
- Under development

Run this step again after the CHANGELOG is updated, or run Step 2-3 manually if ready to proceed.
"@
}
else {
    ''
}

$actionItems = if ($newVersionHeaders.Count -eq 0) {
    @(
        '- No metadata generation is required right now.',
        '- Re-run this script after the next upstream Azure MCP release.'
    )
}
else {
    @(
        '- Send the new version queue to Step 2 (echo-metadata-generation).',
        "- Track metadata generation in ADO item: $(if ($adoWorkItemId) { "#$adoWorkItemId" } else { 'not created' })."
    )
}

$adoSummary = if ($adoWorkItemId) {
    "ADO work item created: #$adoWorkItemId"
}
else {
    'ADO work item created: null'
}

$releaseRows = if ($releaseContext.Count -eq 0) {
    '| None | n/a | n/a | No | n/a |'
}
else {
    $releaseContext | ForEach-Object {
        "| $($_.version) | $($_.releaseDate) | $(if ($_.commitSha) { $_.commitSha } else { 'n/a' }) | $(if ($_.snapshotPresent) { 'Yes' } else { 'No' }) | $($_.status) |"
    }
}

$reportValues = @{
    REPORT_TITLE            = 'Echo — Release Detection Report'
    SKILL_NAME              = 'echo-release-detection'
    AGENT_NAME              = 'Echo'
    TIMESTAMP               = $timestampIso
    INPUT_SOURCES           = "- CHANGELOG: $resolvedChangelogUrl`n- Metadata repo: $resolvedMetadataRepoPath`n- Metadata directory: $resolvedMetadataDir"
    SUMMARY_STATUS          = $(if ($newVersionHeaders.Count -gt 0) { 'New versions detected.' } else { 'No new versions detected.' })
    NEW_VERSIONS_COUNT      = [string]$newVersionHeaders.Count
    LATEST_UPSTREAM         = $(if ($releaseContext.Count -gt 0) { $releaseContext[0].version } else { 'n/a' })
    LATEST_METADATA         = $(if ($latestMetadataVersion) { $latestMetadataVersion } else { 'n/a' })
    RELEASE_TABLE           = ($releaseRows -join "`n")
    NEW_VERSIONS_LIST       = $newVersionsList
    ACTION_ITEMS            = ($actionItems -join "`n")
    NEXT_STEPS              = $(if ($newVersionHeaders.Count -gt 0) { 'Proceed to Step 2: echo-metadata-generation.' } else { 'No Step 2 work is required until a new version appears.' })
    CROSS_REFERENCES        = '- Step 2 report pattern: `echo-metadata-generation-{timestamp}.md`' + "`n" + '- Step 3 report pattern: `echo-content-impact-{timestamp}.md`'
    ADO_WORK_ITEM           = $adoSummary
    VERSION_STATUS          = if ($specifiedVersionStatus) { $specifiedVersionStatus } else { 'n/a' }
    VERSION_STATUS_SECTION  = if ($versionStatusMessage) { "## ⚠️ Version Status`n`n$versionStatusMessage`n" } else { '' }
}

$reportContent = Render-Template -TemplatePath $REPORT_TEMPLATE_PATH -Values $reportValues
Set-Content -Path $reportPath -Value $reportContent -Encoding UTF8

$resultPayload = [ordered]@{
    TIMESTAMP              = $timestampIso
    FILE_TIMESTAMP         = $fileTimestamp
    NEW_VERSIONS           = $newVersions
    NEW_VERSIONS_COUNT     = $newVersionHeaders.Count
    ADO_WORK_ITEM_ID       = $adoWorkItemId
    VERSION_DETAILS        = @($newVersionHeaders)
    RELEASE_CONTEXT        = $releaseContext
    CONTEXT_VERSIONS       = $contextVersions
    LATEST_UPSTREAM_VERSION = if ($releaseContext.Count -gt 0) { $releaseContext[0].version } else { $null }
    VERSION_STATUS         = if ($specifiedVersionStatus) { $specifiedVersionStatus } else { 'n/a' }
    LATEST_METADATA_VERSION = $latestMetadataVersion
    DETECTED_VERSION_COUNT = $headers.Count
    METADATA_VERSIONS      = $existingVersions
    REPORT_PATH            = $reportPath
    JSON_PATH              = $jsonPath
    nextStep               = if ($newVersionHeaders.Count -gt 0 -and $specifiedVersionStatus -ne 'Missing from changelog') { 'echo-metadata-generation' } else { $null }
}
$correlationVersion = if ($newVersions.Count -gt 0) { $newVersions[0] } elseif ($releaseContext.Count -gt 0) { $releaseContext[0].version } else { 'no-version' }
$output = New-StructuredOutputEnvelope -Result $resultPayload -Producer 'echo-release-detection' -Schema 'echo-release-detection@1.0.0' -CorrelationId "echo-azure-mcp-$correlationVersion"

$outputJson = $output | ConvertTo-Json -Depth 100
Set-Content -Path $jsonPath -Value $outputJson -Encoding UTF8
$outputJson

# ── Chain to Step 2 (gate: skip if version missing from changelog) ──────────
if (-not $ChainedFromOrchestrator) {
    if ($resultPayload.VERSION_STATUS -eq 'Missing from changelog') {
        Write-Host '[WARN] Version missing from changelog — skipping Steps 2 & 3.'
        Write-Host '[WARN] Re-run after CHANGELOG is updated, or pass -Version once the version appears.'
    }
    elseif ($resultPayload.NEW_VERSIONS_COUNT -gt 0) {
        Write-Host '[INFO] 🔗 Chaining to Step 2: echo-metadata-generation...'
        $step2Script = Join-Path $ScriptDir '..\..\echo-metadata-generation\scripts\echo-metadata-generation.ps1'
        & pwsh -NoProfile -File $step2Script -Step1OutputPath $jsonPath -ChainedFromOrchestrator
    }
    else {
        Write-Host '[INFO] No new versions detected — Steps 2 & 3 not required.'
    }
}
