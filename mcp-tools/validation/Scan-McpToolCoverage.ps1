# Scan-McpToolCoverage.ps1 — Deterministic coverage audit for Azure MCP Server tool articles
# Compares published articles against tools-list.json to find undocumented tools,
# missing parameters, and annotation mismatches.
#
# Usage:
#   .\Scan-McpToolCoverage.ps1 -ToolsJsonPath <path> -ArticlesDir <path> [-OutputJson <path>] [-Namespace <name>]
#
# Examples:
#   .\Scan-McpToolCoverage.ps1 `
#     -ToolsJsonPath ".\test-npm-azure-mcp\<version>\tools-list.json" `
#     -ArticlesDir ".\generated-storage\tool-family"
#
#   # Single namespace:
#   .\Scan-McpToolCoverage.ps1 -ToolsJsonPath ... -ArticlesDir ... -Namespace "deploy"

param(
    [Parameter(Mandatory)]
    [string]$ToolsJsonPath,

    [Parameter(Mandatory)]
    [string]$ArticlesDir,

    [string]$OutputJson,

    [string]$ReportDir,

    [string]$Namespace,

    # Path to JSON file mapping open PRs to their branches/namespaces.
    # Format: [{"pr": 9015, "namespace": "azurebackup", "branch": "diberry/mcp-tool-backup", "file": "azure-backup.md"}, ...]
    [string]$OpenPRsJson,

    # Git repo root for the articles repo (used with OpenPRsJson to read PR branch files via git show)
    [string]$RepoRoot,

    # Pipeline contract mode: when provided, emits a schemaVersion 1.0 artifact
    # compatible with ValidationResultNormalizer (same shape as Test-ArticleHealth.ps1).
    [string]$RunId = ""
)

$ErrorActionPreference = "Stop"

# ─── Load tools-list.json ─────────────────────────────────────────────────────
if (-not (Test-Path $ToolsJsonPath)) {
    Write-Error "tools-list.json not found at: $ToolsJsonPath"
    exit 1
}
if (-not (Test-Path $ArticlesDir)) {
    Write-Error "Articles directory not found at: $ArticlesDir"
    exit 1
}

$toolsData = Get-Content $ToolsJsonPath -Raw | ConvertFrom-Json
$allTools = $toolsData.results

# Filter by namespace if specified
if ($Namespace) {
    $allTools = $allTools | Where-Object { $_.command.Split(' ')[0] -eq $Namespace }
    if ($allTools.Count -eq 0) {
        Write-Error "No tools found for namespace: $Namespace"
        exit 1
    }
}

# ─── Common/global parameters ────────────────────────────────────────────────
# These are excluded from per-tool parameter checks:
# - Global params (--learn, --subscription, --tenant, --auth-method, --retry-*)
#   are ALWAYS excluded — never in per-tool parameter tables
# - --resource-group is excluded unless required: true for that specific tool
$alwaysExcludeParams = @('--learn', '--subscription', '--tenant', '--auth-method',
    '--retry-delay', '--retry-max-delay', '--retry-max-retries', '--retry-mode', '--retry-network-timeout')
$commonParams = @(
    '--resource-group'
)

# ─── Namespace → filename mapping ────────────────────────────────────────────
# TODO(PRD #574, Phase 3): Replace this hardcoded map with generated/namespace-mapping.json
# consumption once CoverageAuditStep becomes pipeline-owned.
$namespaceToFile = @{
    'appconfig'         = 'app-configuration.md'
    'applicationinsights' = 'application-insights.md'
    'group'             = 'resource-group.md'
    'speech'            = 'ai-services-speech.md'
    'subscription'      = 'subscription.md'
    'kusto'             = 'azure-data-explorer.md'
    'datadog'           = 'azure-native-isv.md'
    'foundryextensions' = 'azure-foundry.md'
    'functionapp'       = 'azure-functions.md'
    'get'               = 'azure-best-practices.md'
    'acr'               = 'azure-container-registry.md'
    'advisor'           = 'azure-advisor.md'
    'aks'               = 'azure-kubernetes.md'
    'applens'           = 'azure-app-lens.md'
    'appservice'        = 'azure-app-service.md'
    'azurebackup'       = 'azure-backup.md'
    'azuremigrate'      = 'azure-migrate.md'
    'azureterraform'    = 'azure-terraform.md'
    'azureterraformbestpractices' = 'azure-terraform-best-practices.md'
    'bicepschema'       = 'azure-bicep-schema.md'
    'cloudarchitect'    = 'azure-cloud-architect.md'
    'communication'     = 'azure-communication.md'
    'compute'           = 'azure-compute.md'
    'confidentialledger' = 'azure-confidential-ledger.md'
    'containerapps'     = 'azure-container-apps.md'
    'cosmos'            = 'azure-cosmos-db.md'
    'deploy'            = 'azure-deploy.md'
    'deviceregistry'    = 'azure-device-registry.md'
    'eventgrid'         = 'azure-event-grid.md'
    'eventhubs'         = 'azure-event-hubs.md'
    'extension'         = 'azure-mcp-tool.md'
    'fileshares'        = 'azure-file-shares.md'
    'functions'         = 'azure-functions.md'
    'grafana'           = 'azure-grafana.md'
    'keyvault'          = 'azure-key-vault.md'
    'loadtesting'       = 'azure-load-testing.md'
    'managedlustre'     = 'azure-managed-lustre.md'
    'marketplace'       = 'azure-marketplace.md'
    'monitor'           = 'azure-monitor.md'
    'mysql'             = 'azure-mysql.md'
    'policy'            = 'azure-policy.md'
    'postgres'          = 'azure-database-postgresql.md'
    'pricing'           = 'azure-pricing.md'
    'quota'             = 'azure-quotas.md'
    'redis'             = 'azure-redis.md'
    'resourcehealth'    = 'azure-resource-health.md'
    'role'              = 'azure-rbac.md'
    'search'            = 'azure-ai-search.md'
    'servicebus'        = 'azure-service-bus.md'
    'servicefabric'     = 'azure-service-fabric.md'
    'signalr'           = 'azure-signalr.md'
    'sql'               = 'azure-sql.md'
    'storage'           = 'azure-storage.md'
    'storagesync'       = 'azure-file-sync.md'
    'virtualdesktop'    = 'azure-virtual-desktop.md'
    'wellarchitectedframework' = 'azure-well-architected-framework.md'
    'workbooks'         = 'azure-workbooks.md'
}

# ─── Load open PR data ────────────────────────────────────────────────────────
$openPRs = @()
$prToolCoverage = @{}  # namespace → @{pr; branch; documented_tools; documented_params; documented_annotations}

if ($OpenPRsJson -and (Test-Path $OpenPRsJson)) {
    $openPRs = Get-Content $OpenPRsJson -Raw | ConvertFrom-Json

    if (-not $RepoRoot) {
        # Infer repo root from ArticlesDir (go up until .git found)
        $candidate = (Resolve-Path $ArticlesDir).Path
        while ($candidate -and -not (Test-Path (Join-Path $candidate ".git"))) {
            $candidate = Split-Path $candidate -Parent
        }
        if ($candidate) { $RepoRoot = $candidate }
    }

    if ($RepoRoot) {
        foreach ($pr in $openPRs) {
            $ns = $pr.namespace
            $branch = $pr.branch
            $prFile = if ($pr.file) { $pr.file } elseif ($namespaceToFile.ContainsKey($ns)) { $namespaceToFile[$ns] } else { "azure-$ns.md" }
            $gitPath = "articles/azure-mcp-server/tools/$prFile"

            # Try to read the file from the PR branch via git show
            try {
                $prContent = $null
                # Try the branch ref directly, then with common remote prefixes
                $refsToTry = @($branch, "origin/$branch", "fork/$branch")
                foreach ($ref in $refsToTry) {
                    $prContent = & git -C $RepoRoot show "${ref}:${gitPath}" 2>$null
                    if ($LASTEXITCODE -eq 0 -and $prContent) { break }
                }
                if (-not $prContent) {
                    # Try fetching the branch from fork remote
                    & git -C $RepoRoot fetch fork $branch --quiet 2>$null
                    $prContent = & git -C $RepoRoot show "fork/${branch}:${gitPath}" 2>$null
                    if ($LASTEXITCODE -ne 0 -or -not $prContent) {
                        & git -C $RepoRoot fetch origin $branch --quiet 2>$null
                        $prContent = & git -C $RepoRoot show "origin/${branch}:${gitPath}" 2>$null
                    }
                }
                if ($LASTEXITCODE -eq 0 -and $prContent) {
                    $prContentStr = $prContent -join "`n"
                    $prToolCoverage[$ns] = @{
                        pr = $pr.pr
                        branch = $branch
                        file = $prFile
                        content = $prContentStr
                        documented_tools = @()
                    }
                    # Extract documented tools from PR content
                    $prMarkers = [regex]::Matches($prContentStr, '<!--\s*(?:@mcpcli\s+)?([a-z][a-z0-9 \-]+?)\s*-->')
                    $prToolCoverage[$ns].documented_tools = @($prMarkers | ForEach-Object { $_.Groups[1].Value.Trim() })
                }
            } catch {
                Write-Warning "Could not read PR #$($pr.pr) branch '$branch' for namespace '$ns': $_"
            }
        }
    } else {
        Write-Warning "RepoRoot not specified and could not be inferred. PR branch scanning skipped."
    }
}

# ─── Scan articles for tool markers (both <!-- @mcpcli cmd --> and <!-- cmd -->) ──
function Get-DocumentedTools {
    param([string]$FilePath)

    if (-not (Test-Path $FilePath)) { return @() }

    $content = Get-Content $FilePath -Raw
    # Match both formats: <!-- @mcpcli {command} --> and <!-- {command} -->
    # Tool commands always start with a lowercase letter (excludes other HTML comments)
    $matches = [regex]::Matches($content, '<!--\s*(?:@mcpcli\s+)?([a-z][a-z0-9 \-]+?)\s*-->')
    return $matches | ForEach-Object { $_.Groups[1].Value.Trim() }
}

# ─── Parse parameter table for a tool section ────────────────────────────────
function Get-DocumentedParameters {
    param([string]$Content, [string]$ToolCommand)

    # Find the tool section by its marker (supports both formats)
    $markerPattern = "<!--\s*(?:@mcpcli\s+)?" + [regex]::Escape($ToolCommand) + "\s*-->"
    $markerMatch = [regex]::Match($Content, $markerPattern)
    if (-not $markerMatch.Success) { return @() }

    # Get content from marker to next ## heading (or end of file)
    $startIdx = $markerMatch.Index
    $nextH2 = [regex]::Match($Content.Substring($startIdx + $markerMatch.Length), '(?m)^## ')
    $endIdx = if ($nextH2.Success) { $startIdx + $markerMatch.Length + $nextH2.Index } else { $Content.Length }
    $section = $Content.Substring($startIdx, $endIdx - $startIdx)

    # Parse parameter table rows: | **ParamName** | Required/Optional | ... |
    $paramMatches = [regex]::Matches($section, '\|\s*\*\*(.+?)\*\*\s*\|')
    return $paramMatches | ForEach-Object { $_.Groups[1].Value.Trim() }
}

# ─── Parse annotation line for a tool section ────────────────────────────────
function Get-DocumentedAnnotations {
    param([string]$Content, [string]$ToolCommand)

    $markerPattern = "<!--\s*(?:@mcpcli\s+)?" + [regex]::Escape($ToolCommand) + "\s*-->"
    $markerMatch = [regex]::Match($Content, $markerPattern)
    if (-not $markerMatch.Success) { return @{} }

    $startIdx = $markerMatch.Index
    $nextH2 = [regex]::Match($Content.Substring($startIdx + $markerMatch.Length), '(?m)^## ')
    $endIdx = if ($nextH2.Success) { $startIdx + $markerMatch.Length + $nextH2.Index } else { $Content.Length }
    $section = $Content.Substring($startIdx, $endIdx - $startIdx)

    # Parse: Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ✅
    $annotations = @{}
    $annoMatch = [regex]::Match($section, '(?:Destructive|Read Only|Idempotent|Open World|Secret|Local Required):.+')
    if ($annoMatch.Success) {
        $pairs = [regex]::Matches($annoMatch.Value, '(Destructive|Idempotent|Open World|Read Only|Secret|Local Required):\s*([✅❌])')
        foreach ($pair in $pairs) {
            $key = $pair.Groups[1].Value.Trim()
            $val = $pair.Groups[2].Value -eq '✅'
            $annotations[$key] = $val
        }
    }
    return $annotations
}

# ─── Main audit loop ─────────────────────────────────────────────────────────
$namespaces = $allTools | ForEach-Object { $_.command.Split(' ')[0] } | Sort-Object -Unique

$report = @{
    version = ($ToolsJsonPath -replace '.*\\', '' -replace '\\tools-list\.json$', '')
    scan_date = (Get-Date -Format 'yyyy-MM-ddTHH:mm:ss')
    total_tools_in_json = $allTools.Count
    total_namespaces = $namespaces.Count
    open_prs_scanned = $openPRs.Count
    namespaces = @()
    summary = @{
        tools_documented = 0
        tools_missing = 0
        tools_in_open_prs = 0
        params_documented = 0
        params_missing = 0
        params_in_open_prs = 0
        annotation_matches = 0
        annotation_mismatches = 0
        articles_missing = 0
        articles_in_open_prs = 0
    }
}

foreach ($ns in $namespaces) {
    $nsTools = @($allTools | Where-Object { $_.command.Split(' ')[0] -eq $ns })

    # Resolve filename
    $filename = if ($namespaceToFile.ContainsKey($ns)) { $namespaceToFile[$ns] } else { "azure-$ns.md" }
    $filePath = Join-Path $ArticlesDir $filename

    $nsReport = @{
        namespace = $ns
        filename = $filename
        file_exists = (Test-Path $filePath)
        expected_tools = $nsTools.Count
        documented_tools = 0
        missing_tools = @()
        tools_in_pr = @()
        pr_number = $null
        tool_details = @()
    }

    if (-not (Test-Path $filePath)) {
        # Also try scanning all .md files for @mcpcli markers matching this namespace
        $found = $false
        foreach ($mdFile in (Get-ChildItem $ArticlesDir -Filter "*.md")) {
            $markers = Get-DocumentedTools -FilePath $mdFile.FullName
            $nsMarkers = $markers | Where-Object { $_.StartsWith("$ns ") }
            if ($nsMarkers) {
                $filePath = $mdFile.FullName
                $filename = $mdFile.Name
                $nsReport.filename = $filename
                $nsReport.file_exists = $true
                $found = $true
                break
            }
        }
        if (-not $found) {
            # Check if this namespace is covered by an open PR
            if ($prToolCoverage.ContainsKey($ns)) {
                $prData = $prToolCoverage[$ns]
                $nsReport.pr_number = $prData.pr
                $nsReport.tools_in_pr = $prData.documented_tools
                $report.summary.tools_in_open_prs += $prData.documented_tools.Count
                $report.summary.articles_in_open_prs++
                # Tools in PR are not "missing" — separate them
                $toolsNotInPR = @($nsTools | ForEach-Object { $_.command } | Where-Object { $_ -notin $prData.documented_tools })
                $nsReport.missing_tools = $toolsNotInPR
                $report.summary.tools_missing += $toolsNotInPR.Count
            } else {
                $nsReport.missing_tools = $nsTools | ForEach-Object { $_.command }
                $report.summary.tools_missing += $nsTools.Count
            }
            $report.summary.articles_missing++
            $report.namespaces += $nsReport
            continue
        }
    }

    $content = Get-Content $filePath -Raw
    $documentedCommands = Get-DocumentedTools -FilePath $filePath

    foreach ($tool in $nsTools) {
        $toolCmd = $tool.command
        $isDocumented = $documentedCommands -contains $toolCmd

        $toolDetail = @{
            command = $toolCmd
            name = $tool.name
            documented = $isDocumented
            params = @{ expected = @(); documented = @(); missing = @() }
            annotations = @{ expected = @{}; documented = @{}; mismatches = @() }
        }

        if ($isDocumented) {
            $nsReport.documented_tools++
            $report.summary.tools_documented++

            # ── Parameter check ──
            $docParams = Get-DocumentedParameters -Content $content -ToolCommand $toolCmd
            $expectedParams = @()
            if ($tool.option) {
                foreach ($opt in $tool.option) {
                    $paramName = $opt.name
                    # Always exclude --learn (global skill param)
                    if ($alwaysExcludeParams -contains $paramName) { continue }
                    # Exclude common params UNLESS they are required for this tool
                    if ($commonParams -contains $paramName) {
                        $isRequired = $opt.PSObject.Properties['required'] -and $opt.required -eq $true
                        if (-not $isRequired) { continue }
                    }
                    # Normalize: --param-name → Param name (title case for matching)
                    $displayName = ($paramName -replace '^--', '') -replace '-', ' '
                    $expectedParams += @{ cli_name = $paramName; display_name = $displayName }
                }
            }

            $toolDetail.params.expected = $expectedParams | ForEach-Object { $_.cli_name }
            $toolDetail.params.documented = $docParams

            # Track which doc params have been matched (for fuzzy pass)
            $matchedDocParams = @{}
            $unmatchedExpected = @()

            foreach ($ep in $expectedParams) {
                # Check if param is documented (case-insensitive match on display name)
                $found = $docParams | Where-Object { $_ -replace '\s+', ' ' -ieq ($ep.display_name -replace '\s+', ' ') }
                if (-not $found) {
                    # Also try matching with different patterns
                    $altName = ($ep.cli_name -replace '^--', '').ToUpper().Substring(0,1) + ($ep.cli_name -replace '^--', '').Substring(1) -replace '-(\w)', { $_.Groups[1].Value.ToUpper() }
                    $found = $docParams | Where-Object { $_ -ieq $altName -or $_ -ieq $ep.display_name -or $_ -ieq ($ep.display_name -replace '\b(\w)', { $_.Groups[1].Value.ToUpper() }) }
                }
                if ($found) {
                    $report.summary.params_documented++
                    $matchedDocParams[($found | Select-Object -First 1)] = $ep.cli_name
                } else {
                    $unmatchedExpected += $ep
                }
            }

            # Fuzzy matching pass: for unmatched expected params, check unmatched doc params
            # Handles cases like --account (JSON) documented as "account name" in table
            $unmatchedDocParams = @($docParams | Where-Object { -not $matchedDocParams.ContainsKey($_) })

            foreach ($ep in $unmatchedExpected) {
                $epBase = ($ep.cli_name -replace '^--', '') -replace '-', ' '  # e.g. "account"
                $fuzzyFound = $false

                foreach ($dp in $unmatchedDocParams) {
                    $dpNorm = $dp.ToLower().Trim()
                    # Check: space-stripped equality (e.g. "hostpool" == "host pool" sans spaces)
                    if (($dpNorm -replace '\s','') -eq ($epBase -replace '\s','')) {
                        $fuzzyFound = $true
                        $matchedDocParams[$dp] = $ep.cli_name
                        $unmatchedDocParams = @($unmatchedDocParams | Where-Object { $_ -ne $dp })
                        break
                    }
                    # Check: doc param contains the expected param name, or expected contains doc param
                    # e.g. "account name" contains "account", or "storage account" contains "account"
                    if ($dpNorm -like "*$epBase*" -or $epBase -like "*$dpNorm*") {
                        $fuzzyFound = $true
                        $matchedDocParams[$dp] = $ep.cli_name
                        $unmatchedDocParams = @($unmatchedDocParams | Where-Object { $_ -ne $dp })
                        break
                    }
                    # Check: words overlap significantly (e.g. "provisioned storage" vs "storage")
                    $epWords = $epBase -split '\s+'
                    $dpWords = $dpNorm -split '\s+'
                    $overlap = @($epWords | Where-Object { $_ -in $dpWords })
                    if ($overlap.Count -gt 0 -and ($overlap.Count -ge ($epWords.Count * 0.5) -or $overlap.Count -ge ($dpWords.Count * 0.5))) {
                        $fuzzyFound = $true
                        $matchedDocParams[$dp] = $ep.cli_name
                        $unmatchedDocParams = @($unmatchedDocParams | Where-Object { $_ -ne $dp })
                        break
                    }
                }

                if ($fuzzyFound) {
                    $report.summary.params_documented++
                } else {
                    $toolDetail.params.missing += $ep.cli_name
                    $report.summary.params_missing++
                }
            }

            # ── Annotation check ──
            if ($tool.metadata) {
                $expectedAnno = @{}
                $metaMap = @{
                    'destructive' = 'Destructive'
                    'idempotent'  = 'Idempotent'
                    'openWorld'   = 'Open World'
                    'readOnly'    = 'Read Only'
                    'secret'      = 'Secret'
                    'localRequired' = 'Local Required'
                }
                foreach ($key in $metaMap.Keys) {
                    if ($tool.metadata.PSObject.Properties[$key]) {
                        $expectedAnno[$metaMap[$key]] = $tool.metadata.$key.value
                    }
                }
                $toolDetail.annotations.expected = $expectedAnno

                $docAnnotations = Get-DocumentedAnnotations -Content $content -ToolCommand $toolCmd
                $toolDetail.annotations.documented = $docAnnotations

                foreach ($annoKey in $expectedAnno.Keys) {
                    if ($docAnnotations.ContainsKey($annoKey)) {
                        if ($docAnnotations[$annoKey] -ne $expectedAnno[$annoKey]) {
                            $toolDetail.annotations.mismatches += @{
                                annotation = $annoKey
                                expected = $expectedAnno[$annoKey]
                                actual = $docAnnotations[$annoKey]
                            }
                            $report.summary.annotation_mismatches++
                        } else {
                            $report.summary.annotation_matches++
                        }
                    } else {
                        $toolDetail.annotations.mismatches += @{
                            annotation = $annoKey
                            expected = $expectedAnno[$annoKey]
                            actual = "NOT_FOUND"
                        }
                        $report.summary.annotation_mismatches++
                    }
                }
            }
        } else {
            # Tool not documented on main — check if it's in an open PR
            if ($prToolCoverage.ContainsKey($ns) -and $prToolCoverage[$ns].documented_tools -contains $toolCmd) {
                $nsReport.tools_in_pr += $toolCmd
                $report.summary.tools_in_open_prs++
            } else {
                $nsReport.missing_tools += $toolCmd
                $report.summary.tools_missing++
            }
        }

        $nsReport.tool_details += $toolDetail
    }

    $report.namespaces += $nsReport
}

# ─── Console Output ──────────────────────────────────────────────────────────
Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  Azure MCP Server Tool Coverage Audit" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Tools JSON: $ToolsJsonPath"
Write-Host "  Articles:   $ArticlesDir"
if ($openPRs.Count -gt 0) {
    Write-Host "  Open PRs:   $($openPRs.Count) scanned" -ForegroundColor Cyan
}
Write-Host "  Scan date:  $($report.scan_date)"
Write-Host ""
Write-Host "── Summary ─────────────────────────────────────────────────────" -ForegroundColor Yellow
Write-Host "  Total tools in JSON:    $($report.total_tools_in_json)"
Write-Host "  Total namespaces:       $($report.total_namespaces)"
Write-Host "  Tools documented:       $($report.summary.tools_documented)" -ForegroundColor Green
if ($report.summary.tools_in_open_prs -gt 0) {
    Write-Host "  Tools in open PRs:      $($report.summary.tools_in_open_prs)" -ForegroundColor Cyan
}
Write-Host "  Tools MISSING:          $($report.summary.tools_missing)" -ForegroundColor $(if ($report.summary.tools_missing -gt 0) { "Red" } else { "Green" })
if ($report.summary.articles_in_open_prs -gt 0) {
    Write-Host "  Articles in open PRs:   $($report.summary.articles_in_open_prs)" -ForegroundColor Cyan
}
Write-Host "  Articles MISSING:       $($report.summary.articles_missing)" -ForegroundColor $(if ($report.summary.articles_missing -gt 0) { "Red" } else { "Green" })
Write-Host "  Params documented:      $($report.summary.params_documented)" -ForegroundColor Green
Write-Host "  Params MISSING:         $($report.summary.params_missing)" -ForegroundColor $(if ($report.summary.params_missing -gt 0) { "Red" } else { "Green" })
Write-Host "  Annotation matches:     $($report.summary.annotation_matches)" -ForegroundColor Green
Write-Host "  Annotation MISMATCHES:  $($report.summary.annotation_mismatches)" -ForegroundColor $(if ($report.summary.annotation_mismatches -gt 0) { "Red" } else { "Green" })
Write-Host ""

# ── Open PR coverage ──
if ($report.summary.tools_in_open_prs -gt 0) {
    Write-Host "── Open PR Coverage ────────────────────────────────────────────" -ForegroundColor Cyan
    foreach ($ns in $report.namespaces | Where-Object { $_.tools_in_pr.Count -gt 0 }) {
        $prNum = if ($ns.pr_number) { " (PR #$($ns.pr_number))" } else {
            $prInfo = $openPRs | Where-Object { $_.namespace -eq $ns.namespace }
            if ($prInfo) { " (PR #$($prInfo.pr))" } else { "" }
        }
        Write-Host "  ✅ $($ns.namespace)$prNum — $($ns.tools_in_pr.Count) tools covered" -ForegroundColor Cyan
    }
    Write-Host ""
}

# ── Missing articles ──
if ($report.summary.articles_missing -gt 0) {
    Write-Host "── Missing Articles ────────────────────────────────────────────" -ForegroundColor Red
    foreach ($ns in $report.namespaces | Where-Object { -not $_.file_exists }) {
        $prNote = if ($ns.pr_number) { " [PR #$($ns.pr_number) pending]" } else { "" }
        Write-Host "  ❌ $($ns.namespace) — expected: $($ns.filename) ($($ns.expected_tools) tools)$prNote" -ForegroundColor Red
    }
    Write-Host ""
}

# ── Missing tools ──
$nsMissing = $report.namespaces | Where-Object { $_.missing_tools.Count -gt 0 -and $_.file_exists }
if ($nsMissing) {
    Write-Host "── Missing Tools (article exists but tool not documented) ───────" -ForegroundColor Red
    foreach ($ns in $nsMissing) {
        Write-Host "  $($ns.namespace) ($($ns.filename)):" -ForegroundColor Yellow
        foreach ($t in $ns.missing_tools) {
            Write-Host "    ❌ $t" -ForegroundColor Red
        }
    }
    Write-Host ""
}

# ── Annotation mismatches ──
$annoIssues = $report.namespaces | ForEach-Object { $_.tool_details } | Where-Object { $_.annotations.mismatches.Count -gt 0 }
if ($annoIssues) {
    Write-Host "── Annotation Mismatches ───────────────────────────────────────" -ForegroundColor Red
    foreach ($tool in $annoIssues) {
        Write-Host "  $($tool.command):" -ForegroundColor Yellow
        foreach ($m in $tool.annotations.mismatches) {
            Write-Host "    ⚠️  $($m.annotation): expected=$($m.expected), actual=$($m.actual)" -ForegroundColor Red
        }
    }
    Write-Host ""
}

# ── Parameter gaps ──
$paramIssues = $report.namespaces | ForEach-Object { $_.tool_details } | Where-Object { $_.params.missing.Count -gt 0 }
if ($paramIssues) {
    Write-Host "── Missing Parameters ──────────────────────────────────────────" -ForegroundColor Red
    foreach ($tool in $paramIssues | Select-Object -First 20) {
        Write-Host "  $($tool.command):" -ForegroundColor Yellow
        foreach ($p in $tool.params.missing) {
            Write-Host "    ❌ $p" -ForegroundColor Red
        }
    }
    if ($paramIssues.Count -gt 20) {
        Write-Host "  ... and $($paramIssues.Count - 20) more tools with missing params" -ForegroundColor Yellow
    }
    Write-Host ""
}

Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan

# ─── Pipeline Contract Output (schemaVersion 1.0) ────────────────────────────
# When -RunId is provided, emit the standardized artifact that ValidationResultNormalizer reads.
if ($RunId) {
    # Determine verdict: pass/warn/fail based on coverage gaps
    $coverageVerdict = if ($report.summary.tools_missing -gt 0) {
        "fail"
    } elseif ($report.summary.params_missing -gt 0 -or $report.summary.annotation_mismatches -gt 0) {
        "warn"
    } else {
        "pass"
    }

    $checks = @(
        [PSCustomObject]@{
            name   = "tools_coverage"
            status = if ($report.summary.tools_missing -gt 0) { "fail" } else { "pass" }
            detail = "$($report.summary.tools_documented)/$($report.summary.tools_documented + $report.summary.tools_missing) tools documented"
        },
        [PSCustomObject]@{
            name   = "params_coverage"
            status = if ($report.summary.params_missing -gt 0) { "warn" } else { "pass" }
            detail = "$($report.summary.params_documented)/$($report.summary.params_documented + $report.summary.params_missing) params documented"
        },
        [PSCustomObject]@{
            name   = "annotation_accuracy"
            status = if ($report.summary.annotation_mismatches -gt 0) { "warn" } else { "pass" }
            detail = "$($report.summary.annotation_mismatches) mismatches"
        }
    )

    $contractArtifact = [PSCustomObject]@{
        schemaVersion = "1.0"
        runId         = $RunId
        namespace     = if ($Namespace) { $Namespace } else { "all" }
        generatedAt   = (Get-Date -Format 'yyyy-MM-ddTHH:mm:ssZ')
        verdict       = $coverageVerdict
        checks        = $checks
        summary       = $report.summary
    }

    # Write to -OutputJson path (required in pipeline contract mode)
    if ($OutputJson) {
        $contractArtifact | ConvertTo-Json -Depth 10 | Set-Content $OutputJson -Encoding UTF8
        Write-Host "  Pipeline contract artifact saved to: $OutputJson" -ForegroundColor Green
    }
}
# ─── Legacy JSON Output (when no -RunId) ─────────────────────────────────────
elseif ($OutputJson) {
    $report | ConvertTo-Json -Depth 10 | Set-Content $OutputJson -Encoding UTF8
    Write-Host "  JSON report saved to: $OutputJson" -ForegroundColor Green
}

# ─── Markdown Report (actionable issues list) ────────────────────────────────
if ($ReportDir) {
    if (-not (Test-Path $ReportDir)) {
        New-Item -ItemType Directory -Path $ReportDir -Force | Out-Null
    }

    $timestamp = Get-Date -Format 'yyyy-MM-dd-HHmmss'
    $reportPath = Join-Path $ReportDir "mcp-coverage-report-$timestamp.md"

    $md = @()
    $md += "# Azure MCP Server Tool Coverage Report"
    $md += ""
    $md += "**Generated:** $($report.scan_date)"
    $md += "**Source:** ``tools-list.json`` (beta.10)"
    $md += "**Target:** Published articles on ``main``"
    if ($openPRs.Count -gt 0) {
        $md += "**Open PRs scanned:** $($openPRs.Count)"
    }
    $md += ""
    $md += "## Summary"
    $md += ""
    $md += "| Metric | Count |"
    $md += "|--------|-------|"
    $md += "| Total tools in JSON | $($report.total_tools_in_json) |"
    $md += "| Total namespaces | $($report.total_namespaces) |"
    $md += "| Tools documented (main) | $($report.summary.tools_documented) |"
    if ($report.summary.tools_in_open_prs -gt 0) {
        $md += "| Tools in open PRs | $($report.summary.tools_in_open_prs) |"
    }
    $md += "| **Tools MISSING (no PR)** | **$($report.summary.tools_missing)** |"
    if ($report.summary.articles_in_open_prs -gt 0) {
        $md += "| Articles in open PRs | $($report.summary.articles_in_open_prs) |"
    }
    $md += "| Articles missing | $($report.summary.articles_missing) |"
    $md += "| Params documented | $($report.summary.params_documented) |"
    $md += "| **Params MISSING** | **$($report.summary.params_missing)** |"
    $md += "| Annotation matches | $($report.summary.annotation_matches) |"
    $md += "| **Annotation mismatches** | **$($report.summary.annotation_mismatches)** |"
    $md += ""

    # ── Open PR coverage section ──
    if ($report.summary.tools_in_open_prs -gt 0) {
        $md += "## Coverage from Open PRs"
        $md += ""
        $md += "These tools are not on ``main`` but ARE documented in open PRs (pending merge)."
        $md += ""
        $md += "| PR | Namespace | Tools Covered | Status |"
        $md += "|----|-----------|---------------|--------|"
        foreach ($ns in $report.namespaces | Where-Object { $_.tools_in_pr.Count -gt 0 }) {
            $prNum = if ($ns.pr_number) { $ns.pr_number } else {
                $prInfo = $openPRs | Where-Object { $_.namespace -eq $ns.namespace }
                if ($prInfo) { $prInfo.pr } else { "?" }
            }
            $md += "| #$prNum | ``$($ns.namespace)`` | $($ns.tools_in_pr.Count) | Pending merge |"
        }
        $md += ""
    }

    # ── Issues: Missing articles ──
    $missingArticles = @($report.namespaces | Where-Object { -not $_.file_exists })
    if ($missingArticles.Count -gt 0) {
        $md += "## Issue: Missing Articles"
        $md += ""
        $md += "These namespaces have tools in ``tools-list.json`` but no article on ``main``."
        $md += ""
        $md += "| Namespace | Expected Filename | Tool Count | PR Status | Action |"
        $md += "|-----------|-------------------|------------|-----------|--------|"
        foreach ($ns in $missingArticles) {
            $prStatus = if ($ns.pr_number) { "PR #$($ns.pr_number) open" } else { "No PR" }
            $action = if ($ns.pr_number) { "Merge PR #$($ns.pr_number)" } else { "Create new article" }
            $md += "| ``$($ns.namespace)`` | ``$($ns.filename)`` | $($ns.expected_tools) | $prStatus | $action |"
        }
        $md += ""
    }

    # ── Issues: Missing tools ──
    $toolGaps = @($report.namespaces | Where-Object { $_.missing_tools.Count -gt 0 -and $_.file_exists })
    if ($toolGaps.Count -gt 0) {
        $md += "## Issue: Undocumented Tools"
        $md += ""
        $md += "These tools exist in ``tools-list.json`` but have no ``<!-- @mcpcli ... -->`` marker in the article."
        $md += ""
        $md += "| Namespace | Article | Missing Tool Command | Action |"
        $md += "|-----------|---------|---------------------|--------|"
        foreach ($ns in $toolGaps) {
            foreach ($t in $ns.missing_tools) {
                $md += "| ``$($ns.namespace)`` | ``$($ns.filename)`` | ``$t`` | Add H2 section with tool documentation |"
            }
        }
        $md += ""
    }

    # ── Issues: Annotation mismatches ──
    $annoGaps = @($report.namespaces | ForEach-Object { $_.tool_details } | Where-Object { $_.annotations.mismatches.Count -gt 0 })
    if ($annoGaps.Count -gt 0) {
        $md += "## Issue: Annotation Mismatches"
        $md += ""
        $md += "Tool annotation values in the article do not match ``metadata`` in ``tools-list.json``."
        $md += ""
        $md += "| Tool Command | Annotation | Expected | Actual |"
        $md += "|-------------|-----------|----------|--------|"
        foreach ($tool in $annoGaps) {
            foreach ($m in $tool.annotations.mismatches) {
                $expStr = if ($m.expected -eq $true) { '✅' } elseif ($m.expected -eq $false) { '❌' } else { $m.expected }
                $actStr = if ($m.actual -eq $true) { '✅' } elseif ($m.actual -eq $false) { '❌' } else { $m.actual }
                $md += "| ``$($tool.command)`` | $($m.annotation) | $expStr | $actStr |"
            }
        }
        $md += ""
    }

    # ── Issues: Missing parameters ──
    $paramGaps = @($report.namespaces | ForEach-Object { $_.tool_details } | Where-Object { $_.params.missing.Count -gt 0 })
    if ($paramGaps.Count -gt 0) {
        $md += "## Issue: Missing Parameters"
        $md += ""
        $md += "These required/non-common parameters are in ``tools-list.json`` but not in the article's parameter table."
        $md += "Common params (``--learn``, ``--subscription``, ``--tenant``, ``--auth-method``, ``--retry-*``) are ALWAYS excluded (global parameters)."
        $md += "``--resource-group`` excluded unless ``required: true`` for the specific tool."
        $md += ""
        $md += "| Tool Command | Missing Parameter |"
        $md += "|-------------|------------------|"
        foreach ($tool in $paramGaps) {
            foreach ($p in $tool.params.missing) {
                $md += "| ``$($tool.command)`` | ``$p`` |"
            }
        }
        $md += ""
    }

    # ── Coverage by namespace ──
    $md += "## Coverage by Namespace"
    $md += ""
    $md += "| Namespace | Expected | Documented | In PR | Missing | Params OK | Params Missing | Anno Mismatches | Coverage |"
    $md += "|-----------|----------|------------|-------|---------|-----------|----------------|-----------------|----------|"
    foreach ($ns in $report.namespaces | Sort-Object { $_.expected_tools - $_.documented_tools } -Descending) {
        $pct = if ($ns.expected_tools -gt 0) { [math]::Round(($ns.documented_tools / $ns.expected_tools) * 100) } else { 0 }
        $status = if ($pct -eq 100) { "✅" } elseif ($pct -ge 50) { "⚠️" } else { "❌" }
        # Aggregate params and annotations for this namespace
        $nsParamsOk = 0
        $nsParamsMissing = 0
        $nsAnnoMismatches = 0
        foreach ($td in $ns.tool_details) {
            $nsParamsOk += ($td.params.expected.Count - $td.params.missing.Count)
            $nsParamsMissing += $td.params.missing.Count
            $nsAnnoMismatches += $td.annotations.mismatches.Count
        }
        # PR coverage for this namespace
        $prInfo = if ($ns.pr_number) { "PR #$($ns.pr_number) ($($ns.tools_in_pr.Count))" } elseif ($ns.tools_in_pr.Count -gt 0) { "$($ns.tools_in_pr.Count) tools" } else { "—" }
        $trueMissing = $ns.expected_tools - $ns.documented_tools - $ns.tools_in_pr.Count
        if ($trueMissing -lt 0) { $trueMissing = 0 }
        # Add ❌ markers to incomplete cells
        $docCell = if ($ns.documented_tools -lt $ns.expected_tools) { "❌ $($ns.documented_tools)" } else { "✅ $($ns.documented_tools)" }
        $missingCell = if ($trueMissing -gt 0) { "❌ $trueMissing" } else { "✅ 0" }
        $paramsMissingCell = if ($nsParamsMissing -gt 0) { "❌ $nsParamsMissing" } else { "✅ $nsParamsMissing" }
        $annoCell = if ($nsAnnoMismatches -gt 0) { "❌ $nsAnnoMismatches" } else { "✅ $nsAnnoMismatches" }
        $md += "| ``$($ns.namespace)`` | $($ns.expected_tools) | $docCell | $prInfo | $missingCell | $nsParamsOk | $paramsMissingCell | $annoCell | $status $pct% |"
    }
    $md += ""

    # ── Rules applied ──
    $md += "## Rules Applied"
    $md += ""
    $md += "- ``--learn``, ``--subscription``, ``--tenant``, ``--auth-method``, ``--retry-*`` are ALWAYS excluded (global parameters, never documented per-tool)"
    $md += "- ``--resource-group`` excluded unless ``required: true`` for the specific tool"
    $md += "- Tool detection uses ``<!-- {command} -->`` or ``<!-- @mcpcli {command} -->`` HTML comment markers"
    $md += "- Parameter detection parses markdown tables for ``| **ParamName** |`` pattern"
    $md += "- Annotation detection parses ``Key: ✅/❌`` patterns in annotation hint lines"

    $md -join "`n" | Set-Content $reportPath -Encoding UTF8
    Write-Host ""
    Write-Host "  📋 Markdown report saved to: $reportPath" -ForegroundColor Green
}
