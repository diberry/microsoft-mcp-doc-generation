#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Shared helper functions for generation pipeline scripts.

.DESCRIPTION
    Dot-source this file from any step script to get common logging
    and tool-command normalization functions.

    Usage:
        . "$PSScriptRoot\Shared-Functions.ps1"
#>

# ═══════════════════════════════════════════════════════════════
# Logging helpers
# ═══════════════════════════════════════════════════════════════

function Write-Info { param([string]$Message) Write-Host "INFO: $Message" -ForegroundColor Cyan }
function Write-Success { param([string]$Message) Write-Host "SUCCESS: $Message" -ForegroundColor Green }
function Write-Warning { param([string]$Message) Write-Host "WARNING: $Message" -ForegroundColor Yellow }
function Write-ErrorMessage { param([string]$Message) Write-Host "ERROR: $Message" -ForegroundColor Red }
function Write-Progress { param([string]$Message) Write-Host "PROGRESS: $Message" -ForegroundColor Magenta }
function Write-Divider { Write-Host ("═" * 80) -ForegroundColor DarkGray }

# ═══════════════════════════════════════════════════════════════
# Tool command normalization
# ═══════════════════════════════════════════════════════════════

<#
.SYNOPSIS
    Normalizes a tool command or namespace name for matching against CLI output.

.DESCRIPTION
    Namespace names use underscores (e.g. extension_azqr, get_azure_bestpractices)
    but tool commands in cli-output.json use spaces (e.g. "extension azqr",
    "get azure bestpractices get"). This function:
    1. Strips \r on Windows (jq outputs \r\n in Git Bash)
    2. Trims whitespace
    3. Replaces underscores with spaces

.PARAMETER Command
    The raw tool command or namespace name to normalize.

.OUTPUTS
    The normalized command string ready for matching.
#>
function Normalize-ToolCommand {
    param([string]$Command)

    if ([string]::IsNullOrWhiteSpace($Command)) {
        return $Command
    }

    # On Windows, bash may pass \r from jq output
    if ($IsWindows -or $env:OS -eq 'Windows_NT') {
        $Command = $Command -replace '\r', ''
    }

    $Command = $Command.Trim()

    # Namespace names use underscores but tool commands use spaces
    $Command = $Command -replace '_', ' '

    return $Command
}

# ═══════════════════════════════════════════════════════════════
# Tool filename builder (mirrors C# ToolFileNameBuilder)
# ═══════════════════════════════════════════════════════════════

# Cache for data files (loaded once per session)
$script:_brandLookup = $null
$script:_compoundWords = $null
$script:_stopWords = $null

<#
.SYNOPSIS
    Loads brand mapping, compound words, and stop words data files.

.DESCRIPTION
    Caches the data files on first call. Must be called before Get-ToolBaseFileName
    if using dot-sourcing in a fresh session.
#>
function Initialize-ToolFileNameData {
    if ($null -ne $script:_brandLookup) { return }

    $dataDir = Join-Path (Split-Path -Parent $PSScriptRoot) "data"

    # brand-to-server-mapping.json → lookup by mcpServerName
    $brandArray = Get-Content (Join-Path $dataDir "brand-to-server-mapping.json") -Raw | ConvertFrom-Json
    $script:_brandLookup = @{}
    foreach ($entry in $brandArray) {
        if ($entry.mcpServerName) {
            $script:_brandLookup[$entry.mcpServerName] = $entry.fileName
        }
    }

    # compound-words.json → key/value dictionary
    $compoundObj = Get-Content (Join-Path $dataDir "compound-words.json") -Raw | ConvertFrom-Json
    $script:_compoundWords = @{}
    $compoundObj.PSObject.Properties | ForEach-Object { $script:_compoundWords[$_.Name] = $_.Value }

    # stop-words.json → HashSet
    $stopArray = Get-Content (Join-Path $dataDir "stop-words.json") -Raw | ConvertFrom-Json
    $script:_stopWords = [System.Collections.Generic.HashSet[string]]::new([string[]]$stopArray)
}

<#
.SYNOPSIS
    Builds the canonical base filename for a tool command.

.DESCRIPTION
    Mirrors the C# ToolFileNameBuilder.BuildBaseFileName logic:
    1. Resolve brand prefix from brand mapping or compound words
    2. Ensure "azure-" prefix
    3. Clean remaining parts (compound word expansion + stop word removal)

    Example: "advisor recommendation list" → "azure-advisor-recommendation-list"
    Example: "aks nodepool get" → "azure-kubernetes-service-node-pool-get"

.PARAMETER Command
    The CLI command (e.g., "advisor recommendation list").

.OUTPUTS
    The base filename string (e.g., "azure-advisor-recommendation-list").
#>
function Get-ToolBaseFileName {
    param([string]$Command)

    Initialize-ToolFileNameData

    if ([string]::IsNullOrWhiteSpace($Command)) { return "unknown" }

    $parts = $Command.Split(' ', [System.StringSplitOptions]::RemoveEmptyEntries)
    if ($parts.Count -eq 0) { return "unknown" }

    $area = $parts[0]

    # Step 1: Brand prefix (3-tier: brand mapping → compound words → raw)
    if ($script:_brandLookup.ContainsKey($area)) {
        $brandPrefix = $script:_brandLookup[$area]
    } else {
        $areaLower = $area.ToLowerInvariant()
        if ($script:_compoundWords.ContainsKey($areaLower)) {
            $brandPrefix = $script:_compoundWords[$areaLower]
        } else {
            $brandPrefix = $areaLower
        }
    }

    # Step 2: Ensure "azure-" prefix
    if (-not $brandPrefix.StartsWith("azure-")) {
        $brandPrefix = "azure-$brandPrefix"
    }

    # Step 3: Clean remaining parts
    if ($parts.Count -le 1) { return $brandPrefix }

    $remaining = ($parts[1..($parts.Count - 1)] -join '-').ToLowerInvariant()
    $cleaned = [System.Collections.Generic.List[string]]::new()

    foreach ($part in $remaining.Split('-')) {
        if ([string]::IsNullOrWhiteSpace($part)) { continue }
        $lower = $part.ToLowerInvariant()

        if ($script:_compoundWords.ContainsKey($lower)) {
            foreach ($sub in $script:_compoundWords[$lower].Split('-')) {
                $subLower = $sub.ToLowerInvariant()
                if (-not $script:_stopWords.Contains($subLower)) {
                    $cleaned.Add($subLower)
                }
            }
        } else {
            if (-not $script:_stopWords.Contains($lower)) {
                $cleaned.Add($lower)
            }
        }
    }

    $cleanedStr = $cleaned -join '-'
    if ($cleanedStr) { return "$brandPrefix-$cleanedStr" } else { return $brandPrefix }
}
