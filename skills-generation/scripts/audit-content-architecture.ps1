# Usage: .\skills-generation\scripts\audit-content-architecture.ps1 [-InventoryPath <path>] [-ArticlesDir <path>] [-SourceDir <path>] [-ReportPath <path>] [-ContentRepo <owner/repo>] [-CI]
<#
.SYNOPSIS
    Audits generated skills content against the Skills Content Architecture model.

.DESCRIPTION
    Validates inventory metadata, published article coverage, internal-content leakage,
    conditional section usage, source heading mapping, and source sufficiency. Produces
    a markdown audit report and optionally fails CI when blocking user-facing coverage
    issues are found.
#>
param(
    [string]$InventoryPath,
    [string]$ArticlesDir,
    [string]$SourceDir,
    [string]$ReportPath,
    [string]$ContentRepo = 'MicrosoftDocs/azure-dev-docs-pr',
    [switch]$IncludePRs,
    [switch]$CI
)

$ErrorActionPreference = "Stop"

function Resolve-ScriptRelativePath {
    param(
        [string]$Path,
        [string]$DefaultRelativePath
    )

    $candidate = if ([string]::IsNullOrWhiteSpace($Path)) { $DefaultRelativePath } else { $Path }
    if ([System.IO.Path]::IsPathRooted($candidate)) {
        return [System.IO.Path]::GetFullPath($candidate)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot $candidate))
}

function Convert-ToCleanText {
    param([AllowNull()][string]$Text)

    if ([string]::IsNullOrWhiteSpace($Text)) {
        return ""
    }

    $clean = $Text -replace '(?s)<script[^>]*>.*?</script>', ''
    $clean = $clean -replace '(?s)<style[^>]*>.*?</style>', ''
    $clean = $clean -replace '(?i)<br\s*/?>', "`n"
    $clean = $clean -replace '(?i)</p>', "`n`n"
    $clean = $clean -replace '(?i)</li>', "`n"
    $clean = $clean -replace '(?i)<li[^>]*>', '- '
    $clean = $clean -replace '<[^>]+>', ''
    $clean = [System.Net.WebUtility]::HtmlDecode($clean)
    $clean = $clean -replace "`r", ''
    $clean = $clean -replace '[ \t]+', ' '
    $clean = $clean -replace '\n{3,}', "`n`n"
    return $clean.Trim()
}

function Normalize-Heading {
    param([AllowNull()][string]$Heading)

    if ([string]::IsNullOrWhiteSpace($Heading)) {
        return ""
    }

    $normalized = $Heading.Trim().ToLowerInvariant()
    $normalized = $normalized -replace '\s+', ' '
    $normalized = $normalized.Trim(':')
    return $normalized
}

function Get-SkillAuditKey {
    param([object]$Skill)

    $name = [string]$Skill.name
    if (-not [string]::IsNullOrWhiteSpace($name)) {
        return $name
    }

    $slug = [string]$Skill.slug
    if (-not [string]::IsNullOrWhiteSpace($slug)) {
        return "slug:$slug"
    }

    return '<missing-name>'
}

function Get-SectionMap {
    param([AllowNull()][string]$Content)

    $map = [ordered]@{}
    if ([string]::IsNullOrWhiteSpace($Content)) {
        return $map
    }

    $working = $Content -replace '(?s)^---\s*\r?\n.*?\r?\n---\s*\r?\n?', ''
    $matches = [regex]::Matches($working, '(?m)^##\s+(.+)$')

    for ($i = 0; $i -lt $matches.Count; $i++) {
        $heading = $matches[$i].Groups[1].Value.Trim()
        $start = $matches[$i].Index + $matches[$i].Length
        $end = if ($i + 1 -lt $matches.Count) { $matches[$i + 1].Index } else { $working.Length }
        $body = $working.Substring($start, $end - $start).Trim()
        $map[$heading] = $body
    }

    return $map
}

function Get-HeadingMatches {
    param(
        [System.Collections.IDictionary]$Sections,
        [string[]]$Patterns
    )

    $headingMatches = @()
    foreach ($key in $Sections.Keys) {
        $normalized = Normalize-Heading $key
        foreach ($pattern in $Patterns) {
            if ($normalized -match $pattern) {
                $headingMatches += [PSCustomObject]@{
                    Heading = $key
                    Body    = [string]$Sections[$key]
                }
                break
            }
        }
    }

    return $headingMatches
}

function Get-FirstIntroParagraph {
    param([AllowNull()][string]$Content)

    if ([string]::IsNullOrWhiteSpace($Content)) {
        return ""
    }

    $working = $Content -replace '(?s)^---\s*\r?\n.*?\r?\n---\s*\r?\n?', ''
    $firstHeading = [regex]::Match($working, '(?m)^##\s+')
    $introBlock = if ($firstHeading.Success) {
        $working.Substring(0, $firstHeading.Index)
    }
    else {
        $working
    }

    $clean = Convert-ToCleanText $introBlock
    if ([string]::IsNullOrWhiteSpace($clean)) {
        return ""
    }

    $paragraphs = $clean -split '\n\s*\n'
    foreach ($paragraph in $paragraphs) {
        $candidate = ($paragraph -replace '\s+', ' ').Trim()
        # Minimum 20 chars to distinguish real intro from HTML remnants/nav text
        if ($candidate.Length -gt 20) {
            return $candidate
        }
    }

    return ""
}

function Test-DocumentedDegradation {
    param([AllowNull()][string]$Text)

    if ([string]::IsNullOrWhiteSpace($Text)) {
        return $false
    }

    $clean = Convert-ToCleanText $Text
    return $clean -match '(?i)\b(?:coming soon|not yet available|not available offline|placeholder|tbd|todo|pending publication|degraded|degradation)\b'
}

function Test-NonEmptySection {
    param([AllowNull()][string]$Text)

    $clean = Convert-ToCleanText $Text
    return -not [string]::IsNullOrWhiteSpace($clean)
}

function Test-PrerequisitesSection {
    param([AllowNull()][string]$Text)

    $clean = Convert-ToCleanText $Text
    if ([string]::IsNullOrWhiteSpace($clean)) {
        return $false
    }

    if ($clean -match '(?i)\b(?:none required|no prerequisites|no special prerequisites|none needed|none)\b') {
        return $true
    }

    if ($clean -match '(?im)^\s*(?:[-*]|\d+\.)\s+.+$') {
        return $true
    }

    return $clean -match '(?i)\b(?:requires?|need(?:s)?|must have|you must have|you need|before you begin|make sure you have|ensure you have)\b'
}

function Test-ExamplePromptsSection {
    param([AllowNull()][string]$Text)

    $clean = Convert-ToCleanText $Text
    if ([string]::IsNullOrWhiteSpace($clean)) {
        return $false
    }

    if ($clean -match '(?im)^\s*(?:[-*]|\d+\.)\s+.+$') {
        return $true
    }

    if ($clean -match '(?i)\b(?:example prompts?|prompt examples?|placeholder|coming soon|available via triggers?|curated externally)\b') {
        return $true
    }

    # Fallback: any substantial text counts as prompt content
    return $clean.Length -gt 30
}

function Test-RelatedContentSection {
    param([AllowNull()][string]$Text)

    $clean = Convert-ToCleanText $Text
    if ([string]::IsNullOrWhiteSpace($clean)) {
        return $false
    }

    return ($clean -match '(?im)^\s*(?:[-*]|\d+\.)\s+.+$') -or
           ($Text -match '\[[^\]]+\]\([^\)]+\)') -or
           ($clean -match 'https?://') -or
           ($clean.Length -gt 20)
}

function Test-RequiredSectionCoverage {
    param(
        [System.Collections.IDictionary]$Sections,
        [string[]]$Patterns,
        [scriptblock]$Validator,
        [string]$FailureMessage
    )

    $matches = Get-HeadingMatches -Sections $Sections -Patterns $Patterns
    if ($matches.Count -eq 0) {
        return $FailureMessage
    }

    $body = $matches[0].Body
    if ((-not (& $Validator $body)) -and (-not (Test-DocumentedDegradation $body))) {
        return $FailureMessage
    }

    return $null
}

function Escape-MarkdownCell {
    param([AllowNull()][string]$Value)

    if ($null -eq $Value) {
        return ''
    }

    return (($Value -replace '\|', '&#124;') -replace "`r?`n", '<br/>')
}

function Get-ArticleAnalysis {
    param([string]$Path)

    $content = Get-Content -Path $Path -Raw
    $sections = Get-SectionMap $content
    $cleanContent = Convert-ToCleanText $content
    $intro = Get-FirstIntroParagraph $content

    return [PSCustomObject]@{
        Path         = $Path
        Content      = $content
        CleanContent = $cleanContent
        Sections     = $sections
        Intro        = $intro
    }
}

function Get-SourceAnalysis {
    param([string]$Path)

    $content = Get-Content -Path $Path -Raw
    $headingMatches = [regex]::Matches($content, '(?m)^###?\s+(.+)$')
    $headings = foreach ($match in $headingMatches) {
        $match.Groups[1].Value.Trim()
    }

    $descriptionMatch = [regex]::Match($content, '(?m)^description:\s*(.+)$')
    $description = if ($descriptionMatch.Success) { $descriptionMatch.Groups[1].Value.Trim() } else { '' }

    $h1Body = ''
    $h1Match = [regex]::Match($content, '(?ms)^#\s+.+?$\s*(.+?)(?=^##\s+|\z)')
    if ($h1Match.Success) {
        $h1Body = Convert-ToCleanText $h1Match.Groups[1].Value
    }

    return [PSCustomObject]@{
        Path        = $Path
        Content     = $content
        Headings    = @($headings)
        Description = $description
        H1Body      = $h1Body
    }
}

function Resolve-SourceDirectory {
    param(
        [object]$Skill,
        [System.IO.DirectoryInfo[]]$SourceDirectories
    )

    $skillName = [string]$Skill.name
    if ([string]::IsNullOrWhiteSpace($skillName)) {
        return [PSCustomObject]@{
            Status    = 'Skipped'
            Directory = $null
            Warning   = 'Cannot resolve source folder for a skill with a missing name'
        }
    }

    $exact = $SourceDirectories | Where-Object { $_.Name -ieq $skillName } | Select-Object -First 1
    if ($exact) {
        return [PSCustomObject]@{
            Status    = 'Exact'
            Directory = $exact
            Warning   = $null
        }
    }

    $serviceMatches = @($SourceDirectories | Where-Object {
        ($_.Name -ieq ("{0}-service" -f $skillName)) -or ($_.Name -ieq ("{0}-services" -f $skillName))
    })
    if ($serviceMatches.Count -eq 1) {
        return [PSCustomObject]@{
            Status    = 'ServiceSuffix'
            Directory = $serviceMatches[0]
            Warning   = $null
        }
    }

    $prefixMatches = @($SourceDirectories | Where-Object { $_.Name.StartsWith($skillName, [System.StringComparison]::OrdinalIgnoreCase) })
    if ($prefixMatches.Count -eq 1) {
        return [PSCustomObject]@{
            Status    = 'Prefix'
            Directory = $prefixMatches[0]
            Warning   = $null
        }
    }

    if ($prefixMatches.Count -gt 1) {
        return [PSCustomObject]@{
            Status    = 'Skipped'
            Directory = $null
            Warning   = "Ambiguous prefix match for '$skillName': $($prefixMatches.Name -join ', ')"
        }
    }

    return [PSCustomObject]@{
        Status    = 'Skipped'
        Directory = $null
        Warning   = "No matching source folder found for '$skillName'"
    }
}

function Test-InventoryCoverage {
    param(
        [object[]]$Skills,
        [string]$ArticlesDirectory,
        [System.IO.DirectoryInfo[]]$SourceDirectories
    )

    $articleExists = @{}
    $generated = 0
    $pending = 0

    foreach ($skill in $Skills) {
        $skillKey = Get-SkillAuditKey -Skill $skill
        $slug = if (-not [string]::IsNullOrWhiteSpace($skill.slug)) { $skill.slug } else { $skill.name }
        $articlePath = Join-Path $ArticlesDirectory ("{0}.md" -f $slug)
        $exists = Test-Path -LiteralPath $articlePath
        $articleExists[$skillKey] = $exists
        if ($exists) {
            $generated++
        }
        else {
            $pending++
        }
    }

    $inventoryNames = @($Skills | ForEach-Object {
        $name = [string]$_.name
        if (-not [string]::IsNullOrWhiteSpace($name)) {
            $name.ToLowerInvariant()
        }
    })
    $sourceNotInInventoryCount = @($SourceDirectories | Where-Object { $inventoryNames -notcontains $_.Name.ToLowerInvariant() }).Count
    $status = if ($pending -eq 0) { '✅' } else { '⚠️' }

    return [PSCustomObject]@{
        Status                    = $status
        GeneratedCount            = $generated
        PendingCount              = $pending
        SourceNotInInventoryCount = $sourceNotInInventoryCount
        ArticleExistsBySkill      = $articleExists
    }
}

function Test-IdentityCompleteness {
    param([object[]]$Skills)

    $perSkill = @{}
    $completeCount = 0
    $versionCount = 0

    foreach ($skill in $Skills) {
        $skillKey = Get-SkillAuditKey -Skill $skill
        $missing = @()
        if ([string]::IsNullOrWhiteSpace($skill.name)) { $missing += 'name' }
        if ([string]::IsNullOrWhiteSpace($skill.displayName)) { $missing += 'displayName' }
        if ([string]::IsNullOrWhiteSpace($skill.category)) { $missing += 'category' }
        if (-not [string]::IsNullOrWhiteSpace($skill.skillVersion)) { $versionCount++ }

        $isComplete = $missing.Count -eq 0
        if ($isComplete) {
            $completeCount++
        }

        $perSkill[$skillKey] = [PSCustomObject]@{
            IsComplete = $isComplete
            Missing    = @($missing)
        }
    }

    $total = @($Skills).Count
    $versionCoverage = if ($total -gt 0) { [math]::Round(($versionCount / $total) * 100, 1) } else { 0 }
    $status = if ($completeCount -eq $total) { '✅' } else { '⚠️' }

    return [PSCustomObject]@{
        Status                 = $status
        CompleteCount          = $completeCount
        TotalCount             = $total
        VersionCoveragePercent = $versionCoverage
        PerSkill               = $perSkill
    }
}

function Test-UserFacingCoverage {
    param([pscustomobject]$ArticleAnalysis)

    $sections = $ArticleAnalysis.Sections
    $failures = @()

    if ([string]::IsNullOrWhiteSpace($ArticleAnalysis.Intro) -or $ArticleAnalysis.Intro.Length -le 20) {
        $failures += 'Intro paragraph missing or too short'
    }

    $requiredSections = @(
        @{ Patterns = @('^what it provides$'); Validator = { param($text) Test-NonEmptySection $text }; Failure = '## What it provides missing or empty' },
        @{ Patterns = @('^prerequisites$'); Validator = { param($text) Test-PrerequisitesSection $text }; Failure = '## Prerequisites missing or empty' },
        @{ Patterns = @('^when to use(?: this skill)?$'); Validator = { param($text) Test-NonEmptySection $text }; Failure = '## When to use this skill missing or empty' },
        @{ Patterns = @('^example prompts?$'); Validator = { param($text) Test-ExamplePromptsSection $text }; Failure = '## Example prompts missing or empty' },
        @{ Patterns = @('^related content$'); Validator = { param($text) Test-RelatedContentSection $text }; Failure = '## Related content missing or empty' }
    )

    foreach ($section in $requiredSections) {
        $failure = Test-RequiredSectionCoverage -Sections $sections -Patterns $section.Patterns -Validator $section.Validator -FailureMessage $section.Failure
        if ($failure) {
            $failures += $failure
        }
    }

    return [PSCustomObject]@{
        Passed   = $failures.Count -eq 0
        Failures = @($failures)
    }
}

function Test-InternalLeakage {
    param([pscustomobject]$ArticleAnalysis)

    $findings = @()
    $raw = $ArticleAnalysis.Content
    $clean = $ArticleAnalysis.CleanContent

    # Match azmcp_* or azure__* tool name patterns
    foreach ($match in [regex]::Matches($raw, '(?i)\b(?:azmcp_\w+|azure__\w+)\b')) {
        $findings += [PSCustomObject]@{
            Type    = 'MCP tool name'
            Excerpt = $match.Value
        }
    }

    # Detect Step|Action table structure with numbered rows
    if ($raw -match '(?is)\|\s*step\s*\|\s*action\s*\|.*?\|\s*1\s*\|') {
        $findings += [PSCustomObject]@{
            Type    = 'Workflow step table'
            Excerpt = 'Detected Step/Action table with numbered rows'
        }
    }
    elseif ($clean -match '(?is)\bstep\b.*\baction\b.*(?:^|\n)\s*1[\.)]\s+') {
        $findings += [PSCustomObject]@{
            Type    = 'Workflow step table'
            Excerpt = 'Detected step/action sequence with numbered instructions'
        }
    }

    foreach ($match in [regex]::Matches($raw, '(?i)@azure-[a-z0-9-]+')) {
        $findings += [PSCustomObject]@{
            Type    = 'Sub-skill orchestration'
            Excerpt = $match.Value
        }
    }

    # Detect agent-directed imperative language (MUST/FORBIDDEN/SHALL NOT) near agent/copilot keywords
    $ruleLines = [regex]::Matches($clean, '(?m)^.*(?:(?i:\b(?:agent|assistant|copilot|you)\b).*\b(?:MUST|FORBIDDEN|SHALL NOT)\b|\b(?:MUST|FORBIDDEN|SHALL NOT)\b.*(?i:\b(?:agent|assistant|copilot|you)\b)).*$')
    foreach ($match in $ruleLines) {
        $findings += [PSCustomObject]@{
            Type    = 'Raw agent rule'
            Excerpt = ($match.Value.Trim() -replace '\s+', ' ')
        }
    }

    foreach ($match in [regex]::Matches($raw, '(?i)(?:\.\.?[\\/])?references[\\/][^\s\)\]]*')) {
        $findings += [PSCustomObject]@{
            Type    = 'Reference file path'
            Excerpt = $match.Value
        }
    }

    $deduped = @()
    $seen = New-Object 'System.Collections.Generic.HashSet[string]'
    foreach ($finding in $findings) {
        $key = "{0}|{1}" -f $finding.Type, $finding.Excerpt
        if ($seen.Add($key)) {
            $deduped += $finding
        }
    }

    return [PSCustomObject]@{
        Passed   = $deduped.Count -eq 0
        Findings = @($deduped)
    }
}

function Test-ConditionalRendering {
    param(
        [pscustomobject]$ArticleAnalysis,
        [bool]$DecisionGuidanceExpected = $true
    )

    $sections = $ArticleAnalysis.Sections
    $present = @()
    $failures = @()

    if ((Get-HeadingMatches -Sections $sections -Patterns @('^automatic activation$')).Count -gt 0) {
        $present += 'Automatic activation'
    }

    $decisionGuidancePresent = (Get-HeadingMatches -Sections $sections -Patterns @('^decision guidance$')).Count -gt 0
    if ($decisionGuidancePresent) {
        $present += 'Decision guidance'
    }

    if ((Get-HeadingMatches -Sections $sections -Patterns @('^related skills$')).Count -gt 0) {
        $present += 'Related skills'
    }

    # Offline audit only knows section presence/absence; tier-specific rendering data is not available here.
    if ($decisionGuidancePresent -and -not $DecisionGuidanceExpected) {
        $failures += 'Decision guidance present for a non-Tier-1 context'
    }

    return [PSCustomObject]@{
        Passed                  = $failures.Count -eq 0
        PresentSections         = @($present)
        Failures                = @($failures)
        DecisionGuidanceExpected = $DecisionGuidanceExpected
    }
}

function Test-UnmappedHeadings {
    param([pscustomobject]$SourceAnalysis)

    # Disposition tracking is deferred for now; this check only flags headings that are not yet mapped.

    $knownPatterns = @(
        '^prerequisites$',
        '^required inputs$',
        '^required roles$',
        '^when to use(?: this skill)?$',
        '^do not use(?: this skill)?$',
        '^use for:?$',
        '^do not use for:?$',
        '^steps$',
        '^workflow$',
        '^mcp tools?$',
        '^tools$',
        '^rules$',
        '^how to use this skill$',
        '^category index$'
    )

    $unmapped = @()
    foreach ($heading in $SourceAnalysis.Headings) {
        $normalized = Normalize-Heading $heading
        $isKnown = $false
        foreach ($pattern in $knownPatterns) {
            if ($normalized -match $pattern) {
                $isKnown = $true
                break
            }
        }

        if (-not $isKnown) {
            $unmapped += $heading
        }
    }

    return [PSCustomObject]@{
        Count    = $unmapped.Count
        Headings = @($unmapped)
    }
}

function Test-SourceSufficiency {
    param([pscustomobject]$SourceAnalysis)

    $content = $SourceAnalysis.Content
    # Minimum meaningful description length
    $descriptionPass = (-not [string]::IsNullOrWhiteSpace($SourceAnalysis.Description)) -or ($SourceAnalysis.H1Body.Length -gt 20)
    $prerequisitesPass = ($content -match '(?im)^##\s+(?:prerequisites|required inputs|required roles)\s*$') -or ($content -match '(?i)\brequires\b')
    $useCasePass = ($content -match '(?im)^##\s+when to use(?: this skill)?\s*$') -or
                   ($content -match '(?im)^##\s+use for:?\s*$') -or
                   ($content -match '(?i)\b(?:use when|use this skill when|when you need to|helps? with|use for)\b')
    $examplePromptSourcePass = ($content -match '(?i)\btriggers\.test\.ts\b') -or
                               ($content -match '(?i)\bcurated\b[^\r\n]{0,80}\.json\b') -or
                               ($content -match '(?i)\.json\b[^\r\n]{0,80}\bcurated\b')

    $missing = @()
    if (-not $descriptionPass) { $missing += 'description' }
    if (-not $prerequisitesPass) { $missing += 'prerequisites source' }
    if (-not $useCasePass) { $missing += 'use-case source' }

    $warnings = @()
    if (-not $examplePromptSourcePass) {
        $warnings += 'No example-prompt source reference detected (expected triggers.test.ts or curated JSON reference)'
    }

    return [PSCustomObject]@{
        Passed   = $missing.Count -eq 0
        Missing  = @($missing)
        Warnings = @($warnings)
        Skipped  = $false
    }
}

# --- PR Content Fetching ---

function Get-OpenSkillsPRs {
    param([string]$Repo)

    $prData = @{}
    try {
        $json = gh pr list --repo $Repo --search "skill" --state open --json number,title,headRefName,files --limit 50 2>$null
        if (-not $json) { return $prData }
        $prs = $json | ConvertFrom-Json
        foreach ($pr in $prs) {
            foreach ($f in $pr.files) {
                if ($f.path -match 'articles/azure-skills/skills/(.+)\.md$') {
                    $slug = $Matches[1]
                    $prData[$slug] = [PSCustomObject]@{
                        Number      = $pr.number
                        Title       = $pr.title
                        HeadRef     = $pr.headRefName
                        FilePath    = $f.path
                    }
                    break
                }
            }
        }
    }
    catch {
        Write-Warning "Failed to fetch open PRs from $Repo : $_"
    }
    return $prData
}

function Get-PRArticleContent {
    param(
        [string]$Repo,
        [int]$PRNumber,
        [string]$FilePath
    )

    try {
        # Get PR head branch info
        $prJson = gh pr view $PRNumber --repo $Repo --json headRefName,headRepository,headRepositoryOwner 2>$null | ConvertFrom-Json
        $headRef = $prJson.headRefName
        $headOwner = $prJson.headRepositoryOwner.login
        $headRepoName = $prJson.headRepository.name
        # Fetch raw file content using the media type for raw content
        $content = gh api "repos/$headOwner/$headRepoName/contents/$FilePath" --header "Accept: application/vnd.github.raw+json" --method GET --field "ref=$headRef" 2>$null
        if ($content) {
            return $content
        }
    }
    catch {
        Write-Warning "Failed to fetch PR #$PRNumber content: $_"
    }
    return $null
}

# --- Main Execution ---

$resolvedInventoryPath = Resolve-ScriptRelativePath -Path $InventoryPath -DefaultRelativePath '..\data\skills-inventory.json'
$resolvedArticlesDir = Resolve-ScriptRelativePath -Path $ArticlesDir -DefaultRelativePath '.\published-cache'
$resolvedSourceDir = Resolve-ScriptRelativePath -Path $SourceDir -DefaultRelativePath '..\..\mcp-tools\skills-source'
$resolvedReportPath = Resolve-ScriptRelativePath -Path $ReportPath -DefaultRelativePath '.\audit-report.md'

Write-Host "Loading inventory from $resolvedInventoryPath" -ForegroundColor Cyan
if (-not (Test-Path -LiteralPath $resolvedInventoryPath)) {
    Write-Error -Message "Inventory file not found: $resolvedInventoryPath" -ErrorAction Continue
    exit 1
}

if (-not (Test-Path -LiteralPath $resolvedSourceDir)) {
    Write-Error "Source directory not found: $resolvedSourceDir"
}

if (-not (Test-Path -LiteralPath $resolvedArticlesDir)) {
    Write-Warning "Articles directory not found: $resolvedArticlesDir. Coverage checks will treat all articles as pending."
}

$reportDirectory = Split-Path -Path $resolvedReportPath -Parent
if (-not (Test-Path -LiteralPath $reportDirectory)) {
    New-Item -ItemType Directory -Path $reportDirectory -Force | Out-Null
}

$inventory = Get-Content -Path $resolvedInventoryPath -Raw | ConvertFrom-Json
$skills = @($inventory.skills)
$sourceDirectories = @(Get-ChildItem -Path $resolvedSourceDir -Directory | Where-Object { $_.Name -ne 'sync-metadata.json' })

# Fetch open PRs if requested
$openPRs = @{}
if ($IncludePRs) {
    Write-Host "Fetching open skills PRs from $ContentRepo..." -ForegroundColor Cyan
    $openPRs = Get-OpenSkillsPRs -Repo $ContentRepo
    Write-Host "  Found $($openPRs.Count) skills with open PRs" -ForegroundColor Green
}

Write-Host "Running check A: Inventory Coverage" -ForegroundColor Cyan
$inventoryCoverage = Test-InventoryCoverage -Skills $skills -ArticlesDirectory $resolvedArticlesDir -SourceDirectories $sourceDirectories

Write-Host "Running check B: Identity Completeness" -ForegroundColor Cyan
$identityCompleteness = Test-IdentityCompleteness -Skills $skills

$matrixRows = @()
$userCoverageFailures = @()
$leakageDetails = @()
$conditionalFailures = @()
$unmappedHeadingDetails = @()
$mappingWarnings = @()
$sourceSufficiencyWarnings = @()
$categoryCPassCount = 0
$leakageFindingCount = 0
$conditionalSectionCount = 0
$mappedSourceCount = 0
$sourceSufficientCount = 0
$totalUnmappedHeadingCount = 0

foreach ($skill in $skills) {
    $skillLabel = Get-SkillAuditKey -Skill $skill
    Write-Host ("Auditing skill: {0}" -f $skillLabel) -ForegroundColor DarkCyan

    $slug = if (-not [string]::IsNullOrWhiteSpace($skill.slug)) { $skill.slug } else { $skill.name }
    $articlePath = Join-Path $resolvedArticlesDir ("{0}.md" -f $slug)
    $hasArticle = Test-Path -LiteralPath $articlePath
    $articleAnalysis = if ($hasArticle) { Get-ArticleAnalysis -Path $articlePath } else { $null }

    # Check for PR content — overrides or supplements published article
    $prInfo = $null
    $articleSource = 'published'
    if ($IncludePRs -and $openPRs.ContainsKey($slug)) {
        $prInfo = $openPRs[$slug]
        Write-Host ("  PR #{0} found: {1}" -f $prInfo.Number, $prInfo.Title) -ForegroundColor DarkYellow
        $prContent = Get-PRArticleContent -Repo $ContentRepo -PRNumber $prInfo.Number -FilePath $prInfo.FilePath
        if ($prContent) {
            # PR content is clean markdown — use it for analysis (overrides published cache)
            $prTempPath = Join-Path $env:TEMP ("audit-pr-{0}.md" -f $slug)
            Set-Content -Path $prTempPath -Value $prContent -Encoding UTF8
            $articleAnalysis = Get-ArticleAnalysis -Path $prTempPath
            Remove-Item -Path $prTempPath -Force -ErrorAction SilentlyContinue
            $hasArticle = $true
            $articleSource = 'pr'
        }
        else {
            Write-Warning "  Could not fetch PR content for $slug — falling back to published cache"
        }
    }

    $sourceResolution = Resolve-SourceDirectory -Skill $skill -SourceDirectories $sourceDirectories
    $sourceAnalysis = $null
    $sourceSufficiency = [PSCustomObject]@{ Passed = $false; Missing = @(); Warnings = @(); Skipped = $true }
    $unmappedHeadings = [PSCustomObject]@{ Count = 0; Headings = @() }

    if ($sourceResolution.Directory) {
        $mappedSourceCount++
        $sourcePath = Join-Path $sourceResolution.Directory.FullName 'SKILL.md'
        if (Test-Path -LiteralPath $sourcePath) {
            $sourceAnalysis = Get-SourceAnalysis -Path $sourcePath
            $sourceSufficiency = Test-SourceSufficiency -SourceAnalysis $sourceAnalysis
            if ($sourceSufficiency.Passed) {
                $sourceSufficientCount++
            }
            $unmappedHeadings = Test-UnmappedHeadings -SourceAnalysis $sourceAnalysis
            $totalUnmappedHeadingCount += $unmappedHeadings.Count
            if ($unmappedHeadings.Count -gt 0) {
                $unmappedHeadingDetails += [PSCustomObject]@{
                    Skill    = $skillLabel
                    Headings = @($unmappedHeadings.Headings)
                }
            }
            if ($sourceSufficiency.Warnings.Count -gt 0) {
                $sourceSufficiencyWarnings += [PSCustomObject]@{
                    Skill    = $skillLabel
                    Warnings = @($sourceSufficiency.Warnings)
                }
            }
        }
        else {
            $mappingWarnings += "Mapped source folder '$($sourceResolution.Directory.Name)' for '$skillLabel' does not contain SKILL.md"
        }
    }
    elseif ($sourceResolution.Warning) {
        Write-Warning $sourceResolution.Warning
        $mappingWarnings += $sourceResolution.Warning
    }

    $coverageResult = [PSCustomObject]@{ Passed = $false; Failures = @(); Skipped = -not $hasArticle }
    $leakageResult = [PSCustomObject]@{ Passed = $true; Findings = @(); Skipped = -not $hasArticle }
    $conditionalResult = [PSCustomObject]@{ Passed = $true; PresentSections = @(); Failures = @(); Skipped = -not $hasArticle }

    if ($articleAnalysis) {
        $coverageResult = Test-UserFacingCoverage -ArticleAnalysis $articleAnalysis
        if ($coverageResult.Passed) {
            $categoryCPassCount++
        }
        else {
            $userCoverageFailures += [PSCustomObject]@{
                Skill    = $skillLabel
                Slug     = $slug
                Failures = @($coverageResult.Failures)
            }
        }

        $leakageResult = Test-InternalLeakage -ArticleAnalysis $articleAnalysis
        $leakageFindingCount += $leakageResult.Findings.Count
        if ($leakageResult.Findings.Count -gt 0) {
            $leakageDetails += [PSCustomObject]@{
                Skill    = $skillLabel
                Findings = @($leakageResult.Findings)
            }
        }

        # Offline audit cannot resolve real tier metadata yet, so inventory skills are treated as Tier 1 for now.
        $conditionalResult = Test-ConditionalRendering -ArticleAnalysis $articleAnalysis -DecisionGuidanceExpected $true
        $conditionalSectionCount += $conditionalResult.PresentSections.Count
        if (-not $conditionalResult.Passed) {
            $conditionalFailures += [PSCustomObject]@{
                Skill    = $skillLabel
                Failures = @($conditionalResult.Failures)
            }
        }
    }

    $identityResult = $identityCompleteness.PerSkill[$skillLabel]

    $matrixRows += [PSCustomObject]@{
        Skill         = $skillLabel
        Identity      = if ($identityResult.IsComplete) { '✓' } else { '✗' }
        Source        = if ($sourceSufficiency.Skipped) { '⏸️' } elseif ($sourceSufficiency.Passed) { '✓' } else { '⚠️' }
        Article       = if (-not $hasArticle) { '⏸️' } elseif ($articleSource -eq 'pr') { "✓ (PR #$($prInfo.Number))" } else { '✓' }
        Coverage      = if (-not $hasArticle) { '⏸️' } elseif ($coverageResult.Passed) { '✓' } else { '✗' }
        NoLeakage     = if (-not $hasArticle) { '⏸️' } elseif ($leakageResult.Passed) { '✓' } else { '⚠️' }
        Conditional   = if (-not $hasArticle) { '⏸️' } elseif ($conditionalResult.Passed) { '✓' } else { '✗' }
        UnmappedCount = if ($sourceSufficiency.Skipped) { 'n/a' } else { [string]$unmappedHeadings.Count }
    }
}

$checkCStatus = if ($userCoverageFailures.Count -eq 0) { '✅' } else { '❌' }
$checkDStatus = if ($leakageFindingCount -eq 0) { '✅' } else { '⚠️' }
$checkEStatus = if ($conditionalFailures.Count -eq 0) { '✅' } else { '❌' }
$checkFStatus = if ($totalUnmappedHeadingCount -eq 0) { '✅' } else { '⚠️' }
$checkGStatus = if ($mappedSourceCount -gt 0 -and $sourceSufficientCount -eq $mappedSourceCount) { '✅' } else { '⚠️' }

$reportLines = @()
$reportLines += '# Skills Content Architecture Audit Report'
$reportLines += ''
$reportLines += ('Generated: {0}' -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss K'))
if ($IncludePRs) {
    $reportLines += ''
    $reportLines += ('**Article sources:** Published cache + {0} open PRs from `{1}`' -f $openPRs.Count, $ContentRepo)
}
$reportLines += ''
$reportLines += '## Summary'
$reportLines += ''
$reportLines += '| Check | Status | Details |'
$reportLines += '|-------|--------|---------|'
$reportLines += ('| A. Inventory Coverage | {0} | {1} generated, {2} pending, {3} source folders not in inventory |' -f $inventoryCoverage.Status, $inventoryCoverage.GeneratedCount, $inventoryCoverage.PendingCount, $inventoryCoverage.SourceNotInInventoryCount)
$reportLines += ('| B. Identity Completeness | {0} | {1}/{2} fully complete, version coverage {3}% |' -f $identityCompleteness.Status, $identityCompleteness.CompleteCount, $identityCompleteness.TotalCount, $identityCompleteness.VersionCoveragePercent)
$reportLines += ('| C. User-Facing Coverage | {0} | {1}/{2} pass all required sections |' -f $checkCStatus, $categoryCPassCount, $inventoryCoverage.GeneratedCount)
$reportLines += ('| D. Internal Leakage | {0} | {1} leakage findings |' -f $checkDStatus, $leakageFindingCount)
$reportLines += ('| E. Conditional Rendering | {0} | {1} conditional sections found, {2} mismatches (presence/absence only; Tier 1 assumed offline) |' -f $checkEStatus, $conditionalSectionCount, $conditionalFailures.Count)
$reportLines += ('| F. Unmapped Headings | {0} | {1} unmapped headings across {2} skills |' -f $checkFStatus, $totalUnmappedHeadingCount, $unmappedHeadingDetails.Count)
$reportLines += ('| G. Source Sufficiency | {0} | {1}/{2} sources sufficient, {3} prompt-source warnings |' -f $checkGStatus, $sourceSufficientCount, $mappedSourceCount, $sourceSufficiencyWarnings.Count)
$reportLines += ''
$reportLines += '## Conformance Matrix'
$reportLines += ''
$reportLines += '| Skill | Identity ✓ | Source ✓ | Article ✓ | Coverage ✓ | No Leakage ✓ | Conditional ✓ | Unmapped |'
$reportLines += '|-------|---|---|---|---|---|---|---|'
foreach ($row in $matrixRows) {
    $reportLines += ('| {0} | {1} | {2} | {3} | {4} | {5} | {6} | {7} |' -f
        (Escape-MarkdownCell $row.Skill),
        $row.Identity,
        $row.Source,
        $row.Article,
        $row.Coverage,
        $row.NoLeakage,
        $row.Conditional,
        $row.UnmappedCount)
}
$reportLines += ''
$reportLines += '## Detailed Findings'
$reportLines += ''
$reportLines += '### C. User-Facing Coverage (failures only)'
$reportLines += ''
if ($userCoverageFailures.Count -eq 0) {
    $reportLines += '> No user-facing coverage failures detected.'
}
else {
    foreach ($failure in $userCoverageFailures) {
        $reportLines += ('- **{0}**' -f (Escape-MarkdownCell $failure.Skill))
        foreach ($item in $failure.Failures) {
            $reportLines += ('  - {0}' -f (Escape-MarkdownCell $item))
        }
    }
}
$reportLines += ''
$reportLines += '### D. Internal Leakage (findings only)'
$reportLines += ''
if ($leakageDetails.Count -eq 0) {
    $reportLines += '> No internal leakage findings detected.'
}
else {
    foreach ($detail in $leakageDetails) {
        $reportLines += ('- **{0}**' -f (Escape-MarkdownCell $detail.Skill))
        foreach ($finding in $detail.Findings) {
            $reportLines += ('  - {0}: {1}' -f (Escape-MarkdownCell $finding.Type), (Escape-MarkdownCell $finding.Excerpt))
        }
    }
}
$reportLines += ''
$reportLines += '### E. Conditional Rendering (mismatches only)'
$reportLines += ''
if ($conditionalFailures.Count -eq 0) {
    $reportLines += '> No conditional rendering mismatches detected. Offline validation is limited to section presence/absence.'
}
else {
    foreach ($detail in $conditionalFailures) {
        $reportLines += ('- **{0}**' -f (Escape-MarkdownCell $detail.Skill))
        foreach ($item in $detail.Failures) {
            $reportLines += ('  - {0}' -f (Escape-MarkdownCell $item))
        }
    }
}
$reportLines += ''
$reportLines += '### F. Unmapped Headings'
$reportLines += ''
if ($unmappedHeadingDetails.Count -eq 0 -and $mappingWarnings.Count -eq 0) {
    $reportLines += '> No unmapped headings detected.'
}
else {
    foreach ($detail in $unmappedHeadingDetails) {
        $reportLines += ('- **{0}**: {1}' -f (Escape-MarkdownCell $detail.Skill), (Escape-MarkdownCell ($detail.Headings -join ', ')))
    }
    if ($mappingWarnings.Count -gt 0) {
        $reportLines += ''
        $reportLines += '**Mapping warnings**'
        foreach ($warning in $mappingWarnings) {
            $reportLines += ('- {0}' -f (Escape-MarkdownCell $warning))
        }
    }
}
$reportLines += ''
$reportLines += '### G. Source Sufficiency (warnings only)'
$reportLines += ''
if ($sourceSufficiencyWarnings.Count -eq 0) {
    $reportLines += '> No example-prompt source warnings detected.'
}
else {
    foreach ($detail in $sourceSufficiencyWarnings) {
        $reportLines += ('- **{0}**' -f (Escape-MarkdownCell $detail.Skill))
        foreach ($warning in $detail.Warnings) {
            $reportLines += ('  - {0}' -f (Escape-MarkdownCell $warning))
        }
    }
}

Set-Content -Path $resolvedReportPath -Value ($reportLines -join "`n") -Encoding UTF8
Write-Host ("Audit report written to {0}" -f $resolvedReportPath) -ForegroundColor Green

if ($CI -and $userCoverageFailures.Count -gt 0) {
    Write-Host 'Blocking user-facing coverage failures detected in CI mode.' -ForegroundColor Red
    exit 1
}

exit 0
