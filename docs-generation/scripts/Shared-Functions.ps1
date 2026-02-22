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

# ═══════════════════════════════════════════════════════════════
# Step script initialization helpers
# ═══════════════════════════════════════════════════════════════

<#
.SYNOPSIS
    Resolves OutputPath to an absolute directory path.

.DESCRIPTION
    If OutputPath is already rooted, returns it as-is.
    Otherwise resolves it relative to the calling script's directory ($PSScriptRoot).

.NOTES
    Uses $PSScriptRoot which, when dot-sourced, refers to the CALLER's directory.
    All callers must reside in the same scripts/ directory for correct resolution.
#>
function Resolve-OutputDir {
    param([string]$OutputPath)

    if ([System.IO.Path]::IsPathRooted($OutputPath)) {
        return $OutputPath
    }

    $absPath = Join-Path $PSScriptRoot $OutputPath
    return [System.IO.Path]::GetFullPath($absPath)
}

<#
.SYNOPSIS
    Reads the CLI version string from cli-version.json.

.PARAMETER OutputDir
    Absolute path to the generated output directory.

.OUTPUTS
    CLI version string (e.g., "2.0.0-beta.21+..."), or "unknown" if not found.
#>
function Get-CliVersion {
    param([string]$OutputDir)

    $versionFile = Join-Path $OutputDir "cli/cli-version.json"
    if (-not (Test-Path $versionFile)) { return "unknown" }

    $content = Get-Content $versionFile -Raw
    if ($content.Trim().StartsWith('{')) {
        $data = $content | ConvertFrom-Json
        return ($data.version ?? $data.Version ?? "unknown")
    }

    return $content.Trim()
}

<#
.SYNOPSIS
    Loads CLI output JSON and returns the parsed object and tools array.

.PARAMETER OutputDir
    Absolute path to the generated output directory.

.OUTPUTS
    Hashtable with keys: CliOutput (parsed JSON), AllTools (results array), FilePath.
    Throws if cli-output.json is not found.
#>
function Get-CliOutput {
    param([string]$OutputDir)

    $cliOutputFile = Join-Path $OutputDir "cli/cli-output.json"
    if (-not (Test-Path $cliOutputFile)) {
        throw "CLI output file not found: $cliOutputFile"
    }

    Write-Progress "Loading CLI output..."
    $cliOutput = Get-Content $cliOutputFile -Raw | ConvertFrom-Json
    $allTools = $cliOutput.results
    Write-Info "Total tools in CLI output: $($allTools.Count)"

    return @{
        CliOutput = $cliOutput
        AllTools  = $allTools
        FilePath  = $cliOutputFile
    }
}

<#
.SYNOPSIS
    Finds tools matching a command or family prefix in the CLI output.

.PARAMETER AllTools
    Array of tool objects from CLI output.

.PARAMETER ToolCommand
    The normalized tool command or family prefix to match.

.OUTPUTS
    Array of matching tool objects. Exits with code 1 if no matches found.
#>
function Find-MatchingTools {
    param(
        [array]$AllTools,
        [string]$ToolCommand
    )

    $matching = @($AllTools | Where-Object {
        $_.command -eq $ToolCommand -or $_.command -like "$ToolCommand *"
    })

    if ($matching.Count -eq 0) {
        Write-ErrorMessage "No tools found matching: $ToolCommand"
        Write-Info "Available tools (first 10):"
        $AllTools | Select-Object -First 10 -ExpandProperty command | ForEach-Object { Write-Info "  - $_" }
        exit 1
    }

    if ($matching.Count -eq 1) {
        $tool = $matching[0]
        Write-Success "✓ Found tool: $($tool.name)"
        Write-Info "  Command: $($tool.command)"
        Write-Info "  Description: $($tool.description)"
    } else {
        Write-Success "✓ Found $($matching.Count) tools in family: $ToolCommand"
        foreach ($t in $matching | Select-Object -First 5) {
            Write-Info "  - $($t.command)"
        }
        if ($matching.Count -gt 5) {
            Write-Info "  ... and $($matching.Count - 5) more"
        }
    }

    return $matching
}

<#
.SYNOPSIS
    Creates a temp directory with a filtered CLI output JSON containing only the matching tools.

.PARAMETER CliOutput
    The full parsed CLI output object (with .version and .results).

.PARAMETER MatchingTools
    Array of matching tool objects to include.

.PARAMETER TempDirName
    Name of the temp directory to create under $PSScriptRoot (default: "temp").

.OUTPUTS
    Hashtable with keys: TempDir (path), FilteredFile (path to JSON file).
#>
function New-FilteredCliFile {
    param(
        [object]$CliOutput,
        [array]$MatchingTools,
        [string]$TempDirName = "temp"
    )

    $tempDir = Join-Path $PSScriptRoot $TempDirName
    New-Item -ItemType Directory -Path $tempDir -Force | Out-Null

    $filtered = @{
        version = $CliOutput.version
        results = @($MatchingTools)
    }

    $filteredFile = Join-Path $tempDir "cli-output-single-tool.json"
    $filtered | ConvertTo-Json -Depth 10 | Set-Content $filteredFile -Encoding UTF8
    Write-Info "Created filtered CLI output: $filteredFile"

    return @{
        TempDir      = $tempDir
        FilteredFile = $filteredFile
    }
}

<#
.SYNOPSIS
    Builds the .NET solution if not already built.

.PARAMETER SkipBuild
    If true, skip the build step (already built by preflight).
#>
function Invoke-DotnetBuild {
    param([switch]$SkipBuild)

    if ($SkipBuild) {
        Write-Info "Skipping build (already built by preflight)"
        return
    }

    Write-Progress "Building .NET packages..."
    $docsGenDir = Split-Path -Parent $PSScriptRoot
    $solutionFile = Join-Path (Split-Path $docsGenDir -Parent) "docs-generation.sln"
    if (Test-Path $solutionFile) {
        & dotnet build $solutionFile --configuration Release --verbosity quiet
        if ($LASTEXITCODE -ne 0) {
            throw ".NET build failed"
        }
        Write-Success "✓ Build succeeded"
    }
    Write-Host ""
}

<#
.SYNOPSIS
    Removes a temp directory if it exists.

.PARAMETER TempDir
    Path to the temp directory to remove.
#>
function Remove-TempDir {
    param([string]$TempDir)

    if ($TempDir -and (Test-Path $TempDir)) {
        Remove-Item -Path $TempDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}
