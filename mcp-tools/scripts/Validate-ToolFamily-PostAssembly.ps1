#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Validates a generated tool-family article against its tool files.

.DESCRIPTION
    Runs post-assembly validation for a namespace after DocGeneration.Steps.ToolFamilyCleanup finishes.
    Blocking checks fail the pipeline when the final article silently drops a tool or
    reports an incorrect tool count.

    Blocking checks:
    - Tool count integrity: tool file count == article H2 section count == YAML tool_count
    - Cross-reference: every tool file maps to an article section and vice versa

    Warning checks:
    - Required parameters from the article parameter table appear as concrete values in example prompts
    - Each section uses the exact text "Example prompts include:"
    - Each section has paired <!-- @mcpcli ... --> markers

    Info checks:
    - Basic branding drift scan for known wording issues

.PARAMETER Namespace
    Namespace/tool family name (for example: foundryextensions, compute, sql).

.PARAMETER OutputPath
    Path to the generated output root. Supports both generated/ and generated-<namespace>/ layouts.
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$Namespace,

    [string]$OutputPath = "../../generated"
)

$ErrorActionPreference = "Stop"

. "$PSScriptRoot\Shared-Functions.ps1"

function Remove-Markup {
    param([string]$Text)

    if ([string]::IsNullOrWhiteSpace($Text)) {
        return ""
    }

    $clean = $Text -replace '\*\*', ''
    $clean = $clean -replace '`', ''
    $clean = $clean -replace '<[^>]+>', ''
    $clean = $clean -replace '\s+', ' '
    return $clean.Trim()
}

function Convert-ToSlug {
    param([string]$Text)

    $clean = Remove-Markup $Text
    if ([string]::IsNullOrWhiteSpace($clean)) {
        return ""
    }

    $slug = $clean.ToLowerInvariant() -replace '[^a-z0-9]+', '-'
    return $slug.Trim('-')
}

function Convert-CommandToToolKey {
    param(
        [string]$CommandText,
        [string]$NamespaceName
    )

    $normalized = Normalize-ToolCommand $CommandText
    $normalized = $normalized.Trim().ToLowerInvariant()
    $namespacePrefix = "$($NamespaceName.ToLowerInvariant()) "

    if ($normalized.StartsWith($namespacePrefix)) {
        $normalized = $normalized.Substring($namespacePrefix.Length)
    }

    $normalized = $normalized -replace '[\s_]+', '-'
    $normalized = $normalized -replace '[^a-z0-9\-]+', '-'
    return $normalized.Trim('-')
}

function Get-McpCliCommands {
    param([string]$Text)

    $normalized = $Text -replace "`r`n", "`n"
    return @([regex]::Matches($normalized, '(?m)^<!--\s*@mcpcli\s+(.+?)\s*-->$') | ForEach-Object {
        $_.Groups[1].Value.Trim()
    })
}

function Get-FrontmatterValue {
    param(
        [string]$Frontmatter,
        [string]$Name
    )

    $pattern = '(?mi)^' + [regex]::Escape($Name) + '\s*:\s*(.+?)\s*$'
    $match = [regex]::Match($Frontmatter, $pattern)
    if ($match.Success) {
        return $match.Groups[1].Value.Trim()
    }

    return $null
}

function Convert-TableLineToCells {
    param([string]$Line)

    $trimmed = $Line.Trim()
    $trimmed = $trimmed.Trim('|')
    return @($trimmed -split '\|' | ForEach-Object { $_.Trim() })
}

function Get-SectionParameterRows {
    param([string[]]$Lines)

    $tableLines = New-Object System.Collections.Generic.List[string]
    $tableStarted = $false

    foreach ($line in $Lines) {
        $trimmed = $line.Trim()
        if ($trimmed.StartsWith('|')) {
            $tableStarted = $true
            $tableLines.Add($trimmed)
            continue
        }

        if ($tableStarted) {
            break
        }
    }

    if ($tableLines.Count -lt 2) {
        return @()
    }

    $headerCells = Convert-TableLineToCells $tableLines[0]
    $parameterIndex = -1
    $requiredIndex = -1

    for ($i = 0; $i -lt $headerCells.Count; $i++) {
        $header = Remove-Markup $headerCells[$i]
        if ($header -match '(?i)^parameter$') {
            $parameterIndex = $i
        }
        if ($header -match '(?i)required') {
            $requiredIndex = $i
        }
    }

    if ($parameterIndex -lt 0 -or $requiredIndex -lt 0) {
        return @()
    }

    $rows = New-Object System.Collections.Generic.List[object]

    for ($i = 1; $i -lt $tableLines.Count; $i++) {
        $line = $tableLines[$i]
        if ($line -match '^\|\s*[-: ]+\|') {
            continue
        }

        $cells = Convert-TableLineToCells $line
        if ($cells.Count -le [Math]::Max($parameterIndex, $requiredIndex)) {
            continue
        }

        $parameterName = Remove-Markup $cells[$parameterIndex]
        $requiredValue = Remove-Markup $cells[$requiredIndex]
        $isRequired = $requiredValue -match '(?i)^(yes|✅|required\*?)$' -or $requiredValue -match '(?i)^required'

        $rows.Add([pscustomobject]@{
            ParameterName = $parameterName
            RequiredValue = $requiredValue
            IsRequired = $isRequired
        })
    }

    return [object[]]$rows.ToArray()
}

function Get-ConcretePromptCoverage {
    param(
        [string[]]$ExamplePrompts,
        [string]$ParameterName,
        [int]$TotalRequiredParameters
    )

    $slug = Convert-ToSlug $ParameterName
    $words = @($slug -split '-' | Where-Object { $_ })
    $wordPattern = ($words | ForEach-Object { [regex]::Escape($_) }) -join '[-_ ]+'
    $variantList = New-Object System.Collections.Generic.List[string]
    foreach ($variant in @(
        ($ParameterName.ToLowerInvariant()),
        (($words -join ' ')),
        (($words -join '-')),
        (($words -join '_'))
    )) {
        if (-not [string]::IsNullOrWhiteSpace($variant)) {
            $variantList.Add($variant)
        }
    }

    if ($words.Count -gt 1 -and $words[-1] -in @('name', 'text', 'array', 'value')) {
        $baseWords = $words[0..($words.Count - 2)]
        foreach ($variant in @(
            ($baseWords -join ' '),
            ($baseWords -join '-'),
            ($baseWords -join '_')
        )) {
            if (-not [string]::IsNullOrWhiteSpace($variant)) {
                $variantList.Add($variant)
            }
        }
    }

    if ($words.Count -gt 0 -and $words[-1] -eq 'text') {
        $variantList.Add('text')
    }

    if ($words.Count -gt 0 -and $words[-1] -eq 'array') {
        $variantList.Add($words[0])
        $variantList.Add("$($words[0])s")
    }

    $variants = @($variantList | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Sort-Object -Unique)

    $placeholderDetected = $false
    $covered = $false

    foreach ($examplePrompt in $ExamplePrompts) {
        if ([string]::IsNullOrWhiteSpace($examplePrompt)) {
            continue
        }

        $trimmedPrompt = $examplePrompt.Trim()
        $lowerPrompt = $trimmedPrompt.ToLowerInvariant()

        $placeholders = [regex]::Matches($trimmedPrompt, '<[^>]+>|\{[^}]+\}|\[[^\]]+\]')
        foreach ($placeholder in $placeholders) {
            $placeholderSlug = Convert-ToSlug $placeholder.Value
            if ($placeholderSlug -eq $slug -or $placeholderSlug -like "*$slug*" -or (($words | Where-Object { $placeholderSlug -like "*$_*" }).Count -ge [Math]::Min([Math]::Max($words.Count, 1), 2))) {
                $placeholderDetected = $true
            }
        }

        $foundVariant = $false
        $matchIndex = -1
        foreach ($variant in $variants) {
            $candidate = $variant.ToLowerInvariant()
            if ([string]::IsNullOrWhiteSpace($candidate)) {
                continue
            }

            $currentIndex = $lowerPrompt.IndexOf($candidate)
            if ($currentIndex -ge 0) {
                $foundVariant = $true
                $matchIndex = $currentIndex + $candidate.Length
                break
            }
        }

        if (-not $foundVariant -and $wordPattern) {
            $wordMatch = [regex]::Match($lowerPrompt, '(?i)\b' + $wordPattern + '\b')
            if ($wordMatch.Success) {
                $foundVariant = $true
                $matchIndex = $wordMatch.Index + $wordMatch.Length
            }
        }

        if ($foundVariant -and $matchIndex -ge 0) {
            $tail = $trimmedPrompt.Substring([Math]::Min($matchIndex, $trimmedPrompt.Length))
            if (
                $tail -match "^\s*(?:set to|named|name|with|at|for|in|of|is|=|:)?\s*'[^'<>{}\[\]]+'" -or
                $tail -match '^\s*(?:set to|named|name|with|at|for|in|of|is|=|:)?\s*`[^`<>{}\[\]]+`' -or
                $tail -match '^\s*(?:set to|named|name|with|at|for|in|of|is|=|:)?\s*https?://\S+' -or
                $tail -match '^\s*(?:set to|named|name|with|at|for|in|of|is|=|:)?\s*\[(?!\s*[<\{]).+\]' -or
                $tail -match '^\s*(?:set to|named|name|with|at|for|in|of|is|=|:)?\s*\{(?!\s*[<\{]).+\}'
            ) {
                $covered = $true
                break
            }
        }

        if (-not $covered -and $TotalRequiredParameters -eq 1 -and $placeholders.Count -eq 0) {
            if (
                $trimmedPrompt -match "'[^'<>{}\[\]]+'" -or
                $trimmedPrompt -match '`[^`<>{}\[\]]+`' -or
                $trimmedPrompt -match 'https?://\S+'
            ) {
                $covered = $true
                break
            }
        }
    }

    return [pscustomobject]@{
        Covered = $covered
        PlaceholderDetected = $placeholderDetected
    }
}

function Get-NamespaceFilePrefixes {
    param(
        [string]$NamespaceName,
        [string]$DocsGenRoot
    )

    $prefixes = New-Object System.Collections.Generic.List[string]
    $namespaceLower = $NamespaceName.ToLowerInvariant()
    $prefixes.Add($namespaceLower)

    $brandMappingPath = Join-Path $DocsGenRoot 'data\brand-to-server-mapping.json'
    if (Test-Path $brandMappingPath) {
        try {
            $brandMappings = Get-Content $brandMappingPath -Raw | ConvertFrom-Json
            $mapping = $brandMappings | Where-Object { $_.mcpServerName -eq $namespaceLower } | Select-Object -First 1
            if ($mapping -and $mapping.fileName) {
                $mappedPrefix = $mapping.fileName.ToLowerInvariant()
                $prefixes.Add($mappedPrefix)
                if (-not $mappedPrefix.StartsWith('azure-')) {
                    $prefixes.Add("azure-$mappedPrefix")
                }
            }
        } catch {
            Write-Warning "Could not read brand mapping file for validation: $($_.Exception.Message)"
        }
    }

    $prefixes.Add("ai-$namespaceLower")
    $prefixes.Add("azure-$namespaceLower")

    return @($prefixes | Sort-Object Length -Descending -Unique)
}

function Get-NamespaceToolFiles {
    param(
        [string]$ToolsDirectory,
        [string]$NamespaceName,
        [string[]]$Prefixes
    )

    $files = New-Object System.Collections.Generic.List[object]
    $namespaceLower = $NamespaceName.ToLowerInvariant()

    foreach ($file in Get-ChildItem -Path $ToolsDirectory -Filter '*.md' -File -ErrorAction SilentlyContinue) {
        $baseName = [System.IO.Path]::GetFileNameWithoutExtension($file.Name).ToLowerInvariant()
        $content = Get-Content $file.FullName -Raw
        $commandText = $null

        $commands = @(Get-McpCliCommands $content)
        foreach ($candidate in $commands) {
            $normalized = Normalize-ToolCommand $candidate
            if ($normalized -and $normalized.Split(' ')[0].ToLowerInvariant() -eq $namespaceLower) {
                $commandText = $candidate
                break
            }
        }

        $matchedPrefix = $null
        foreach ($prefix in $Prefixes) {
            if ($baseName -eq $prefix -or $baseName.StartsWith("$prefix-")) {
                $matchedPrefix = $prefix
                break
            }
        }

        $belongsToNamespace = $false
        if ($commandText) {
            $belongsToNamespace = $true
        } elseif ($matchedPrefix) {
            $belongsToNamespace = $true
        }

        if (-not $belongsToNamespace) {
            continue
        }

        $toolKey = if ($commandText) {
            Convert-CommandToToolKey -CommandText $commandText -NamespaceName $NamespaceName
        } elseif ($matchedPrefix -and $baseName.StartsWith("$matchedPrefix-")) {
            $baseName.Substring($matchedPrefix.Length + 1)
        } else {
            $baseName
        }

        $files.Add([pscustomobject]@{
            Name = $file.Name
            FullName = $file.FullName
            ToolKey = $toolKey
            CommandText = $commandText
        })
    }

    return [object[]]$files.ToArray()
}

function Get-ArticleSections {
    param(
        [string]$ArticleContent,
        [string]$NamespaceName
    )

    $normalized = $ArticleContent -replace "`r`n", "`n"
    $frontmatterMatch = [regex]::Match($normalized, '(?s)^---\n(.*?)\n---\n?')
    if (-not $frontmatterMatch.Success) {
        throw 'Tool-family article is missing YAML frontmatter.'
    }

    $frontmatter = $frontmatterMatch.Groups[1].Value
    $body = $normalized.Substring($frontmatterMatch.Length)
    $headingMatches = [regex]::Matches($body, '(?m)^##\s+(.*)$')
    $sections = New-Object System.Collections.Generic.List[object]

    for ($index = 0; $index -lt $headingMatches.Count; $index++) {
        $heading = $headingMatches[$index].Groups[1].Value.Trim()
        $startIndex = $headingMatches[$index].Index
        $endIndex = if ($index -lt $headingMatches.Count - 1) { $headingMatches[$index + 1].Index } else { $body.Length }
        $sectionText = $body.Substring($startIndex, $endIndex - $startIndex).TrimEnd()

        if ($heading -eq 'Related content') {
            continue
        }

        $sectionLines = $sectionText -split "`n"
        $commands = @(Get-McpCliCommands $sectionText)
        $toolKey = if ($commands.Count -gt 0) {
            Convert-CommandToToolKey -CommandText $commands[0] -NamespaceName $NamespaceName
        } else {
            Convert-ToSlug $heading
        }

        $markerLineIndices = New-Object System.Collections.Generic.List[int]
        $exampleHeaderIndex = -1
        $tableStartIndex = -1
        $alternateExampleHeader = $null

        for ($lineIndex = 0; $lineIndex -lt $sectionLines.Count; $lineIndex++) {
            $trimmed = $sectionLines[$lineIndex].Trim()
            if ($trimmed -match '^<!--\s*@mcpcli\s+') {
                $markerLineIndices.Add($lineIndex)
            }
            if ($trimmed -eq 'Example prompts include:') {
                $exampleHeaderIndex = $lineIndex
            }
            if ($tableStartIndex -lt 0 -and $trimmed.StartsWith('|')) {
                $tableStartIndex = $lineIndex
            }
            if (-not $alternateExampleHeader -and $trimmed -match '^(?i)(example prompts|example commands|usage examples|examples|try this|to .* use commands like):') {
                $alternateExampleHeader = $trimmed
            }
        }

        $examplePrompts = New-Object System.Collections.Generic.List[string]
        if ($exampleHeaderIndex -ge 0) {
            $currentPrompt = $null
            for ($lineIndex = $exampleHeaderIndex + 1; $lineIndex -lt $sectionLines.Count; $lineIndex++) {
                $trimmed = $sectionLines[$lineIndex].Trim()
                if ($trimmed.StartsWith('|') -or $trimmed -match '^\[Tool annotation hints\]' -or $trimmed -match '^Destructive:' -or $trimmed -match '^<!--\s*@mcpcli\s+') {
                    break
                }

                if ($trimmed -match '^-\s+') {
                    if ($currentPrompt) {
                        $examplePrompts.Add($currentPrompt)
                    }
                    $currentPrompt = ($trimmed -replace '^-\s+', '').Trim()
                    continue
                }

                if ($currentPrompt -and -not [string]::IsNullOrWhiteSpace($trimmed)) {
                    $currentPrompt = "$currentPrompt $trimmed"
                }
            }

            if ($currentPrompt) {
                $examplePrompts.Add($currentPrompt)
            }
        }

        $parameterRows = Get-SectionParameterRows $sectionLines
        $requiredParameters = @($parameterRows | Where-Object { $_.IsRequired } | Select-Object -ExpandProperty ParameterName)

        $sections.Add([pscustomobject]@{
            Heading = $heading
            ToolKey = $toolKey
            Commands = $commands
            MarkerCount = $markerLineIndices.Count
            MarkerLineIndices = @($markerLineIndices)
            ExampleHeaderIndex = $exampleHeaderIndex
            TableStartIndex = $tableStartIndex
            AlternateExampleHeader = $alternateExampleHeader
            ExamplePrompts = @($examplePrompts)
            RequiredParameters = @($requiredParameters)
        })
    }

    return [pscustomobject]@{
        Frontmatter = $frontmatter
        Sections = [object[]]$sections.ToArray()
    }
}

function Get-BrandingIssues {
    param([string]$ArticleContent)

    $issues = New-Object System.Collections.Generic.List[string]
    $normalized = $ArticleContent -replace "`r`n", "`n"
    $lines = $normalized -split "`n"

    $checks = @(
        @{ Pattern = '(?i)\bthis command\b'; Message = 'Use "this tool" instead of "this command".' },
        @{ Pattern = '(?i)\bCosmosDB\b'; Message = 'Use "Azure Cosmos DB" on first mention instead of "CosmosDB".' },
        @{ Pattern = '(?i)\bAzure VMs\b'; Message = 'Use "Azure Virtual Machines" on first mention instead of "Azure VMs".' },
        @{ Pattern = '(?i)\bMSSQL\b'; Message = 'Use "Azure SQL" or "SQL Server" as appropriate instead of "MSSQL".' },
        @{ Pattern = '(?i)\bFoundry\b'; Message = 'Verify first mention uses the full product name (for example, "Microsoft Foundry").' }
    )

    foreach ($line in $lines) {
        $trimmed = $line.Trim()
        if ([string]::IsNullOrWhiteSpace($trimmed) -or $trimmed.StartsWith('- ') -or $trimmed.StartsWith('|')) {
            continue
        }

        foreach ($check in $checks) {
            if ($trimmed -match $check.Pattern) {
                if ($check.Pattern -eq '(?i)\bFoundry\b' -and $trimmed -match '(?i)Microsoft Foundry') {
                    continue
                }
                $issues.Add("$($check.Message) [$trimmed]")
                break
            }
        }
    }

    return @($issues | Sort-Object -Unique)
}

try {
    $namespaceLower = $Namespace.ToLowerInvariant()
    $outputDir = Resolve-OutputDir $OutputPath
    $mcpToolsDir = Split-Path -Parent $PSScriptRoot
    $toolsDir = Join-Path $outputDir 'tools'
    $articlePath = Join-Path $outputDir "tool-family\$namespaceLower.md"
    $reportDir = Join-Path $outputDir 'reports'
    $reportPath = Join-Path $reportDir "tool-family-validation-$namespaceLower.txt"

    if (-not (Test-Path $toolsDir)) {
        throw "Tools directory not found: $toolsDir"
    }

    if (-not (Test-Path $articlePath)) {
        throw "Tool-family article not found: $articlePath"
    }

    $prefixes = Get-NamespaceFilePrefixes -NamespaceName $namespaceLower -DocsGenRoot $mcpToolsDir
    $toolFiles = Get-NamespaceToolFiles -ToolsDirectory $toolsDir -NamespaceName $namespaceLower -Prefixes $prefixes
    $toolFileCount = $toolFiles.Count

    if ($toolFileCount -eq 0) {
        throw "No tool files found for namespace '$namespaceLower' in $toolsDir"
    }

    $articleContent = Get-Content $articlePath -Raw
    $article = Get-ArticleSections -ArticleContent $articleContent -NamespaceName $namespaceLower
    $sections = @($article.Sections)
    $articleSectionCount = $sections.Count

    $toolCountValue = Get-FrontmatterValue -Frontmatter $article.Frontmatter -Name 'tool_count'
    $frontmatterToolCount = if ($toolCountValue -match '^\d+$') { [int]$toolCountValue } else { $null }

    $blockingIssues = New-Object System.Collections.Generic.List[string]
    $warningIssues = New-Object System.Collections.Generic.List[string]

    $toolFileLookup = @{}
    foreach ($toolFile in $toolFiles) {
        if (-not $toolFileLookup.ContainsKey($toolFile.ToolKey)) {
            $toolFileLookup[$toolFile.ToolKey] = New-Object System.Collections.Generic.List[object]
        }
        $toolFileLookup[$toolFile.ToolKey].Add($toolFile)
    }

    $sectionLookup = @{}
    foreach ($section in $sections) {
        if (-not $sectionLookup.ContainsKey($section.ToolKey)) {
            $sectionLookup[$section.ToolKey] = New-Object System.Collections.Generic.List[object]
        }
        $sectionLookup[$section.ToolKey].Add($section)
    }

    if ($toolFileCount -ne $articleSectionCount -or $null -eq $frontmatterToolCount -or $frontmatterToolCount -ne $toolFileCount -or $frontmatterToolCount -ne $articleSectionCount) {
        $blockingIssues.Add('Tool count integrity check failed.')
    }

    $duplicateToolFileKeys = @($toolFileLookup.GetEnumerator() | Where-Object { $_.Value.Count -gt 1 })
    foreach ($duplicate in $duplicateToolFileKeys) {
        $duplicateFiles = @($duplicate.Value | ForEach-Object { $_.Name }) -join ', '
        $blockingIssues.Add("Duplicate tool file mapping for '$($duplicate.Key)': $duplicateFiles")
    }

    $duplicateSectionKeys = @($sectionLookup.GetEnumerator() | Where-Object { $_.Value.Count -gt 1 })
    foreach ($duplicate in $duplicateSectionKeys) {
        $duplicateHeadings = @($duplicate.Value | ForEach-Object { $_.Heading }) -join ', '
        $blockingIssues.Add("Duplicate article section mapping for '$($duplicate.Key)': $duplicateHeadings")
    }

    $missingFromArticle = New-Object System.Collections.Generic.List[string]
    foreach ($toolFile in $toolFiles) {
        if (-not $sectionLookup.ContainsKey($toolFile.ToolKey)) {
            $missingFromArticle.Add($toolFile.Name)
        }
    }

    $missingFromFiles = New-Object System.Collections.Generic.List[string]
    foreach ($section in $sections) {
        if (-not $toolFileLookup.ContainsKey($section.ToolKey)) {
            $missingFromFiles.Add($section.Heading)
        }
    }

    if ($missingFromArticle.Count -gt 0 -or $missingFromFiles.Count -gt 0) {
        $blockingIssues.Add('Cross-reference check failed.')
    }

    $requiredParamsPassingTools = 0
    $requiredParamWarnings = New-Object System.Collections.Generic.List[string]
    foreach ($section in $sections) {
        $requiredParameters = @($section.RequiredParameters)
        if ($requiredParameters.Count -eq 0) {
            $requiredParamsPassingTools++
            continue
        }

        $missingParameters = New-Object System.Collections.Generic.List[string]
        foreach ($requiredParameter in $requiredParameters) {
            $coverage = Get-ConcretePromptCoverage -ExamplePrompts $section.ExamplePrompts -ParameterName $requiredParameter -TotalRequiredParameters $requiredParameters.Count
            if (-not $coverage.Covered) {
                $missingParameters.Add($requiredParameter)
            }
        }

        if ($missingParameters.Count -eq 0) {
            $requiredParamsPassingTools++
        } else {
            $requiredParamWarnings.Add("⚠️ $($section.ToolKey): missing $((@($missingParameters | ForEach-Object { "'$($_)'" }) -join ', ')) in example prompt$(if ($missingParameters.Count -gt 1) { 's' } else { '' })")
        }
    }

    foreach ($warning in $requiredParamWarnings) {
        $warningIssues.Add($warning)
    }

    $standardHeaderSections = 0
    $headerWarnings = New-Object System.Collections.Generic.List[string]
    foreach ($section in $sections) {
        $headerIsStandard = $section.ExampleHeaderIndex -ge 0
        $headerIsPositionedCorrectly = $true
        if ($headerIsStandard -and $section.MarkerLineIndices.Count -gt 0) {
            $headerIsPositionedCorrectly = $section.ExampleHeaderIndex -gt $section.MarkerLineIndices[-1]
        }
        if ($headerIsStandard -and $section.TableStartIndex -ge 0) {
            $headerIsPositionedCorrectly = $headerIsPositionedCorrectly -and $section.ExampleHeaderIndex -lt $section.TableStartIndex
        }

        if ($headerIsStandard -and $headerIsPositionedCorrectly) {
            $standardHeaderSections++
        } else {
            $usedHeader = if ($section.AlternateExampleHeader) { $section.AlternateExampleHeader } elseif ($section.ExampleHeaderIndex -lt 0) { 'missing' } else { 'misplaced' }
            $headerWarnings.Add("⚠️ $($section.ToolKey): example prompt header is $usedHeader")
        }
    }

    foreach ($warning in $headerWarnings) {
        $warningIssues.Add($warning)
    }

    $markerWarnings = New-Object System.Collections.Generic.List[string]
    $totalMarkers = ($sections | Measure-Object -Property MarkerCount -Sum).Sum
    foreach ($section in $sections) {
        if ($section.MarkerCount -ne 1) {
            $markerWarnings.Add("⚠️ $($section.ToolKey): expected 1 annotation marker, found $($section.MarkerCount)")
        }
    }

    foreach ($warning in $markerWarnings) {
        $warningIssues.Add($warning)
    }

    $brandingIssues = Get-BrandingIssues -ArticleContent $articleContent

    $reportLines = New-Object System.Collections.Generic.List[string]
    $reportLines.Add("=== Tool Family Validation: $namespaceLower ===")
    $reportLines.Add("Tool files found: $toolFileCount")
    $reportLines.Add("Article H2 sections: $articleSectionCount")
    $reportLines.Add("Frontmatter tool_count: $(if ($null -ne $frontmatterToolCount) { $frontmatterToolCount } else { 'missing' })")
    if ($toolFileCount -eq $articleSectionCount -and $null -ne $frontmatterToolCount -and $frontmatterToolCount -eq $toolFileCount) {
        $reportLines.Add('✅ Tool count integrity: PASS')
    } else {
        $reportLines.Add('❌ Tool count integrity: FAIL')
    }

    $reportLines.Add('')
    $reportLines.Add('Cross-reference:')
    if ($missingFromArticle.Count -eq 0) {
        $reportLines.Add("  ✅ All $toolFileCount tool files have matching article sections")
    } else {
        $reportLines.Add("  ❌ Missing from article: $($missingFromArticle.Count)")
        foreach ($item in $missingFromArticle | Sort-Object) {
            $reportLines.Add("    - $item")
        }
    }
    if ($missingFromFiles.Count -eq 0) {
        $reportLines.Add("  ✅ All $articleSectionCount article sections have matching tool files")
    } else {
        $reportLines.Add("  ❌ Missing from files: $($missingFromFiles.Count)")
        foreach ($item in $missingFromFiles | Sort-Object) {
            $reportLines.Add("    - $item")
        }
    }
    foreach ($duplicate in $duplicateToolFileKeys) {
        $duplicateFiles = @($duplicate.Value | ForEach-Object { $_.Name }) -join ', '
        $reportLines.Add("  ❌ Duplicate tool file mapping for '$($duplicate.Key)': $duplicateFiles")
    }
    foreach ($duplicate in $duplicateSectionKeys) {
        $duplicateHeadings = @($duplicate.Value | ForEach-Object { $_.Heading }) -join ', '
        $reportLines.Add("  ❌ Duplicate article section mapping for '$($duplicate.Key)': $duplicateHeadings")
    }

    $reportLines.Add('')
    $reportLines.Add('Required params in prompts:')
    if ($requiredParamWarnings.Count -eq 0) {
        $reportLines.Add("  ✅ $requiredParamsPassingTools/$articleSectionCount tools have all required params in examples")
    } else {
        $reportLines.Add("  ⚠️ $requiredParamsPassingTools/$articleSectionCount tools have all required params in examples")
        foreach ($warning in $requiredParamWarnings) {
            $reportLines.Add("  $warning")
        }
    }

    $reportLines.Add('')
    $reportLines.Add("Annotation markers: $totalMarkers found (expected $articleSectionCount) $(if ($totalMarkers -eq $articleSectionCount -and $markerWarnings.Count -eq 0) { '✅' } else { '⚠️' })")
    foreach ($warning in $markerWarnings) {
        $reportLines.Add("  $warning")
    }

    $reportLines.Add("Example headers: $standardHeaderSections/$articleSectionCount use standard format $(if ($headerWarnings.Count -eq 0) { '✅' } else { '⚠️' })")
    foreach ($warning in $headerWarnings) {
        $reportLines.Add("  $warning")
    }

    $reportLines.Add("Branding: $($brandingIssues.Count) issue$(if ($brandingIssues.Count -ne 1) { 's' } else { '' }) found $(if ($brandingIssues.Count -eq 0) { '✅' } else { 'ℹ️' })")
    foreach ($issue in $brandingIssues) {
        $reportLines.Add("  - $issue")
    }

    $reportLines.Add('')
    $warningCount = $warningIssues.Count
    if ($blockingIssues.Count -eq 0) {
        $reportLines.Add("RESULT: PASS $(if ($warningCount -gt 0) { "($warningCount warning$(if ($warningCount -ne 1) { 's' } else { '' }))" } else { '(clean)' })")
    } else {
        $reportLines.Add("RESULT: FAIL ($($blockingIssues.Count) blocking issue$(if ($blockingIssues.Count -ne 1) { 's' } else { '' }), $warningCount warning$(if ($warningCount -ne 1) { 's' } else { '' }))")
    }

    if (-not (Test-Path $reportDir)) {
        New-Item -ItemType Directory -Path $reportDir -Force | Out-Null
    }

    Set-Content -Path $reportPath -Value $reportLines
    foreach ($reportLine in $reportLines) {
        Write-Host $reportLine
    }
    Write-Info "Validation report: $reportPath"

    if ($blockingIssues.Count -gt 0) {
        exit 1
    }

    exit 0
} catch {
    Write-ErrorMessage "Tool family validation failed: $($_.Exception.Message)"
    Write-ErrorMessage $_.ScriptStackTrace
    exit 1
}
