<#
.SYNOPSIS
    Counts and analyzes Azure MCP tools in a tools-list.json file by namespace/service.

.DESCRIPTION
    Reads a tools-list.json (the Azure MCP CLI metadata tools list) and reports:
      - the total number of tools, and
      - a per-service breakdown, where the service/namespace is the first
        whitespace-delimited token of each tool's `command`
        (e.g. "acr registry list" -> "acr").

    Use this to audit tool counts across metadata versions and to validate
    coverage in documentation PRs.

.PARAMETER FilePath
    Path to a tools-list.json file. Accepts either the API-style shape
    ({ "results": [ ... ] }) or a bare top-level array of tools.

.PARAMETER Top
    Number of top services (by tool count) to display. Defaults to 10.
    Use 0 to display every service.

.EXAMPLE
    .\scripts\count-tools.ps1 -FilePath .\mcp-cli-metadata\3.0.0-beta.6\tools-list.json

.EXAMPLE
    .\scripts\count-tools.ps1 -FilePath .\tools-list.json -Top 0
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$FilePath,

    [int]$Top = 10
)

$ErrorActionPreference = 'Stop'

function Get-ServiceFromCommand {
    <#
    .SYNOPSIS
        Returns the service/namespace token (first whitespace-delimited word) of a command.
    #>
    param([string]$Command)

    if ([string]::IsNullOrWhiteSpace($Command)) {
        return $null
    }

    return ($Command.Trim() -split '\s+')[0]
}

function Get-ToolCounts {
    <#
    .SYNOPSIS
        Computes the total tool count and a per-service breakdown, ordered by
        descending count (then service name ascending for a stable tie-break).
    #>
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [object[]]$Tools
    )

    $byService = @{}
    $total = 0

    foreach ($tool in $Tools) {
        $service = Get-ServiceFromCommand -Command $tool.command
        if (-not $service) {
            continue
        }

        $total++
        if ($byService.ContainsKey($service)) {
            $byService[$service]++
        }
        else {
            $byService[$service] = 1
        }
    }

    $breakdown = $byService.GetEnumerator() |
        Sort-Object `
            @{ Expression = { $_.Value }; Descending = $true }, `
            @{ Expression = { $_.Key };   Descending = $false } |
        ForEach-Object {
            [PSCustomObject]@{
                Service   = $_.Key
                ToolCount = $_.Value
            }
        }

    return [PSCustomObject]@{
        Total     = $total
        ByService = @($breakdown)
    }
}

function Read-ToolsList {
    <#
    .SYNOPSIS
        Loads the tools array from a tools-list.json, tolerating both the
        results-wrapped shape and a bare top-level array.
    #>
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not (Test-Path $Path)) {
        throw "tools-list.json not found: $Path"
    }

    $json = Get-Content -Path $Path -Raw | ConvertFrom-Json

    if ($null -ne $json.results) {
        return @($json.results)
    }

    if ($json -is [System.Array]) {
        return @($json)
    }

    throw "Unrecognized tools-list.json shape: expected a 'results' array or a top-level array of tools."
}

# ─────────────────────────── Main ───────────────────────────
$tools  = Read-ToolsList -Path $FilePath
$counts = Get-ToolCounts -Tools $tools

$display = if ($Top -le 0) { $counts.ByService } else { $counts.ByService | Select-Object -First $Top }
$heading = if ($Top -le 0) { "Services by tool count:" } else { "Top $Top services by tool count:" }

Write-Host ""
Write-Host "Total tool count: $($counts.Total)"
Write-Host ""
Write-Host $heading
Write-Host ("{0,-24} {1}" -f 'Service', 'Tool Count')
Write-Host ("{0,-24} {1}" -f '-------', '----------')
foreach ($row in $display) {
    Write-Host ("{0,-24} {1}" -f $row.Service, $row.ToolCount)
}
Write-Host ""

# Emit the structured result so callers (and tests) can consume it programmatically.
$counts
