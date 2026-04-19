<#
.SYNOPSIS
    Detects drift between generated skill pages and published content on learn.microsoft.com.

.DESCRIPTION
    Fetches published skill pages, compares them section-by-section against generated output,
    and produces a markdown drift report.

.PARAMETER GeneratedDir
    Path to the directory containing generated skill page markdown files.

.PARAMETER InventoryPath
    Path to skills-inventory.json.

.PARAMETER ReportPath
    Output path for the drift report markdown file. Defaults to ./drift-report.md.

.PARAMETER SkipFetch
    Use cached published files instead of fetching from learn.microsoft.com.
#>
param(
    [string]$GeneratedDir,
    [string]$InventoryPath,
    [string]$ReportPath = "./drift-report.md",
    [switch]$SkipFetch
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$defaultGenDir = Join-Path (Split-Path $scriptDir -Parent) "generated"
$defaultInventory = Join-Path (Split-Path $scriptDir -Parent) "data" "skills-inventory.json"

if (-not $GeneratedDir) { $GeneratedDir = $defaultGenDir }
if (-not $InventoryPath) { $InventoryPath = $defaultInventory }

$cacheDir = Join-Path $scriptDir "published-cache"
if (-not (Test-Path $cacheDir)) { New-Item -ItemType Directory -Path $cacheDir -Force | Out-Null }

# --- Helpers ---

function Strip-HtmlTags([string]$html) {
    # Extract content inside <main> if present
    if ($html -match '(?s)<main[^>]*>(.*?)</main>') {
        $html = $Matches[1]
    }
    # Convert common HTML to markdown-ish text
    $html = $html -replace '(?s)<script[^>]*>.*?</script>', ''
    $html = $html -replace '(?s)<style[^>]*>.*?</style>', ''
    $html = $html -replace '<br\s*/?>', "`n"
    $html = $html -replace '</p>', "`n`n"
    $html = $html -replace '</li>', "`n"
    $html = $html -replace '<li[^>]*>', '- '
    $html = $html -replace '<h2[^>]*>', '## '
    $html = $html -replace '</h2>', "`n"
    $html = $html -replace '<h3[^>]*>', '### '
    $html = $html -replace '</h3>', "`n"
    $html = $html -replace '<[^>]+>', ''
    $html = [System.Web.HttpUtility]::HtmlDecode($html)
    return $html.Trim()
}

function Extract-Sections([string]$content) {
    # Strip frontmatter
    $content = $content -replace '(?s)^---\s*\n.*?\n---\s*\n?', ''
    $sections = @{}
    $pattern = '(?m)^##\s+(.+)$'
    $matches = [regex]::Matches($content, $pattern)
    for ($i = 0; $i -lt $matches.Count; $i++) {
        $name = $matches[$i].Groups[1].Value.Trim()
        $start = $matches[$i].Index + $matches[$i].Length
        $end = if ($i + 1 -lt $matches.Count) { $matches[$i + 1].Index } else { $content.Length }
        $body = $content.Substring($start, $end - $start).Trim()
        $sections[$name] = $body
    }
    return $sections
}

function Normalize-SectionName([string]$name) {
    $n = $name.Trim().ToLowerInvariant()
    $n = $n -replace '\s+this\s+skill$', ''
    $n = $n -replace '\s+', ' '
    return $n
}

function Count-Bullets([string]$text) {
    return ([regex]::Matches($text, '(?m)^\s*[-*]\s+')).Count
}

function Count-Words([string]$text) {
    $cleaned = $text -replace '[#*|`\[\]\(\)\-]', ' '
    return ($cleaned -split '\s+' | Where-Object { $_ }).Count
}

function Has-Table([string]$text) {
    return $text -match '\|[-:]+\|'
}

# --- Main ---

Add-Type -AssemblyName System.Web

if (-not (Test-Path $InventoryPath)) {
    Write-Error "Inventory file not found: $InventoryPath"
    exit 1
}

$inventory = Get-Content $InventoryPath -Raw | ConvertFrom-Json
$skills = $inventory.skills

$baseUrl = "https://learn.microsoft.com/en-us/azure/developer/azure-skills/skills"
$allDriftItems = @()
$errorCount = 0

foreach ($skill in $skills) {
    $name = $skill.name
    $slug = if ($skill.slug) { $skill.slug } else { $name }
    $publishedUrl = "$baseUrl/$slug"

    Write-Host "Processing: $name (slug: $slug)" -ForegroundColor Cyan

    # Find generated file
    $genFile = Get-ChildItem -Path $GeneratedDir -Filter "$name.md" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $genFile) {
        $allDriftItems += [PSCustomObject]@{
            Skill       = $name
            Section     = "(entire page)"
            Severity    = "Error"
            Category    = "GenerationBug"
            Description = "Generated file not found for '$name'"
            Fix         = "Run generator for this skill"
        }
        $errorCount++
        continue
    }

    $generatedContent = Get-Content $genFile.FullName -Raw

    # Fetch or load cached published content
    $cacheFile = Join-Path $cacheDir "$slug.md"
    if ($SkipFetch -and (Test-Path $cacheFile)) {
        $publishedContent = Get-Content $cacheFile -Raw
        Write-Host "  Using cached: $cacheFile" -ForegroundColor DarkGray
    }
    else {
        try {
            $response = Invoke-WebRequest -Uri $publishedUrl -UseBasicParsing -TimeoutSec 30 -ErrorAction Stop
            $publishedContent = Strip-HtmlTags $response.Content
            Set-Content -Path $cacheFile -Value $publishedContent -Encoding UTF8
            Write-Host "  Fetched and cached: $publishedUrl" -ForegroundColor Green
        }
        catch {
            Write-Warning "  Failed to fetch $publishedUrl : $_"
            $allDriftItems += [PSCustomObject]@{
                Skill       = $name
                Section     = "(entire page)"
                Severity    = "Info"
                Category    = "ContentPrStale"
                Description = "Could not fetch published page: $publishedUrl"
                Fix         = "Page may not be published yet"
            }
            continue
        }
    }

    # Extract and compare sections
    $genSections = Extract-Sections $generatedContent
    $pubSections = Extract-Sections $publishedContent

    # Missing from generated
    foreach ($pubKey in $pubSections.Keys) {
        $normalized = Normalize-SectionName $pubKey
        $found = $genSections.Keys | Where-Object { (Normalize-SectionName $_) -eq $normalized }
        if (-not $found) {
            $severity = "Error"
            $category = "GenerationBug"
            $allDriftItems += [PSCustomObject]@{
                Skill       = $name
                Section     = $pubKey
                Severity    = $severity
                Category    = $category
                Description = "Section '$pubKey' in published but missing from generated"
                Fix         = "Check template or source SKILL.md"
            }
            $errorCount++
        }
    }

    # Extra in generated
    foreach ($genKey in $genSections.Keys) {
        $normalized = Normalize-SectionName $genKey
        $found = $pubSections.Keys | Where-Object { (Normalize-SectionName $_) -eq $normalized }
        if (-not $found) {
            $allDriftItems += [PSCustomObject]@{
                Skill       = $name
                Section     = $genKey
                Severity    = "Info"
                Category    = "ContentPrStale"
                Description = "Section '$genKey' in generated but not in published"
                Fix         = "Content PR needs update"
            }
        }
    }

    # Content differences in shared sections
    foreach ($genKey in $genSections.Keys) {
        $normalized = Normalize-SectionName $genKey
        $pubMatch = $pubSections.Keys | Where-Object { (Normalize-SectionName $_) -eq $normalized } | Select-Object -First 1
        if ($pubMatch) {
            $genBody = $genSections[$genKey]
            $pubBody = $pubSections[$pubMatch]

            $genBullets = Count-Bullets $genBody
            $pubBullets = Count-Bullets $pubBody
            if ($pubBullets -gt $genBullets -and $genBullets -ge 0) {
                $allDriftItems += [PSCustomObject]@{
                    Skill       = $name
                    Section     = $genKey
                    Severity    = "Warning"
                    Category    = "SourceDataGap"
                    Description = "Bullet count: generated=$genBullets, published=$pubBullets"
                    Fix         = "Review source SKILL.md for additional items"
                }
            }

            $genTable = Has-Table $genBody
            $pubTable = Has-Table $pubBody
            if ($pubTable -and -not $genTable) {
                $allDriftItems += [PSCustomObject]@{
                    Skill       = $name
                    Section     = $genKey
                    Severity    = "Warning"
                    Category    = "GenerationBug"
                    Description = "Published has table, generated does not"
                    Fix         = "Check template table rendering"
                }
            }

            $genWords = Count-Words $genBody
            $pubWords = Count-Words $pubBody
            if ($genWords -gt 0 -and $pubWords -gt 0) {
                $maxW = [Math]::Max($genWords, $pubWords)
                $minW = [Math]::Min($genWords, $pubWords)
                $delta = ($maxW - $minW) / $maxW
                if ($delta -gt 0.50) {
                    $allDriftItems += [PSCustomObject]@{
                        Skill       = $name
                        Section     = $genKey
                        Severity    = "Warning"
                        Category    = "SourceDataGap"
                        Description = "Word count: generated=$genWords, published=$pubWords ($([math]::Round($delta * 100))% delta)"
                        Fix         = "Review content for completeness"
                    }
                }
            }
        }
    }
}

# --- Generate Report ---

$reportLines = @()
$reportLines += "# Drift Detection Report"
$reportLines += ""
$reportLines += "Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss UTC')"
$reportLines += ""

$errors = ($allDriftItems | Where-Object { $_.Severity -eq "Error" }).Count
$warnings = ($allDriftItems | Where-Object { $_.Severity -eq "Warning" }).Count
$infos = ($allDriftItems | Where-Object { $_.Severity -eq "Info" }).Count

$reportLines += "## Summary"
$reportLines += ""
$reportLines += "| Severity | Count |"
$reportLines += "|----------|-------|"
$reportLines += "| Error    | $errors |"
$reportLines += "| Warning  | $warnings |"
$reportLines += "| Info     | $infos |"
$reportLines += "| **Total** | **$($allDriftItems.Count)** |"
$reportLines += ""

if ($allDriftItems.Count -gt 0) {
    $reportLines += "## Details"
    $reportLines += ""
    $reportLines += "| Skill | Section | Severity | Category | Description | Fix |"
    $reportLines += "|-------|---------|----------|----------|-------------|-----|"
    foreach ($item in $allDriftItems | Sort-Object -Property @{Expression={switch($_.Severity){"Error"{0}"Warning"{1}"Info"{2}}}}) {
        $reportLines += "| $($item.Skill) | $($item.Section) | $($item.Severity) | $($item.Category) | $($item.Description) | $($item.Fix) |"
    }
    $reportLines += ""
}

if ($allDriftItems.Count -eq 0) {
    $reportLines += "> No drift detected. Generated content matches published content."
    $reportLines += ""
}

$reportContent = $reportLines -join "`n"
Set-Content -Path $ReportPath -Value $reportContent -Encoding UTF8
Write-Host ""
Write-Host "Drift report written to: $ReportPath" -ForegroundColor Green
Write-Host "  Errors: $errors | Warnings: $warnings | Info: $infos" -ForegroundColor $(if ($errors -gt 0) { "Red" } else { "Green" })

if ($errorCount -gt 0) {
    exit 1
}
exit 0
