param(
  [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
  [string]$NamespaceCheck = "resilience"
)

$ErrorActionPreference = "Stop"

function Read-TrimmedFile([string]$Path) {
  if (-not (Test-Path $Path)) { return $null }
  return (Get-Content -Path $Path -Raw).Trim()
}

function Parse-Version([string]$Version) {
  if ([string]::IsNullOrWhiteSpace($Version)) { return $null }
  $base = ($Version -split "\+")[0]
  if ($base -match '^(\d+)\.(\d+)\.(\d+)(?:-beta\.(\d+))?$') {
    return [pscustomobject]@{
      Raw = $Version
      Base = $base
      Major = [int]$Matches[1]
      Minor = [int]$Matches[2]
      Patch = [int]$Matches[3]
      Beta = if ($Matches[4]) { [int]$Matches[4] } else { 999999 }
    }
  }
  return $null
}

function Get-LatestSnapshotVersion([string]$MetadataDir) {
  if (-not (Test-Path $MetadataDir)) { return $null }

  $parsed = @()
  Get-ChildItem -Path $MetadataDir -Directory -ErrorAction SilentlyContinue | ForEach-Object {
    $p = Parse-Version $_.Name
    if ($null -ne $p) {
      $parsed += [pscustomobject]@{
        Name = $_.Name
        Sort = $p
      }
    }
  }

  if ($parsed.Count -eq 0) { return $null }

  $latest = $parsed |
    Sort-Object -Property @{Expression = { $_.Sort.Major }; Descending = $true },
                           @{Expression = { $_.Sort.Minor }; Descending = $true },
                           @{Expression = { $_.Sort.Patch }; Descending = $true },
                           @{Expression = { $_.Sort.Beta }; Descending = $true } |
    Select-Object -First 1

  return $latest.Name
}

function Get-AzureMcpGlobalToolVersion {
  try {
    $line = dotnet tool list --global 2>$null | Select-String -Pattern '^azure\.mcp\s+' | Select-Object -First 1
    if ($null -eq $line) { return $null }

    $parts = ($line.Line -replace '\s+', ' ').Trim().Split(' ')
    if ($parts.Count -ge 2) { return $parts[1] }
    return $null
  }
  catch {
    return $null
  }
}

$toolVersionPath = Join-Path $RepoRoot "mcp-tool-version.txt"
$trackedVersionPath = Join-Path $RepoRoot "mcp-cli-metadata/tracked-version.txt"
$metadataDir = Join-Path $RepoRoot "mcp-cli-metadata"
$brandMappingPath = Join-Path $RepoRoot "mcp-tools/data/brand-to-server-mapping.json"

$mcpToolVersion = Read-TrimmedFile $toolVersionPath
$trackedVersion = Read-TrimmedFile $trackedVersionPath
$globalToolVersion = Get-AzureMcpGlobalToolVersion
$latestSnapshotVersion = Get-LatestSnapshotVersion $metadataDir

$brandHasNamespace = $false
$brandCoverageState = "missing-file"
if (Test-Path $brandMappingPath) {
  try {
    $mapping = Get-Content -Path $brandMappingPath -Raw | ConvertFrom-Json
    if ($null -ne $mapping) {
      $brandHasNamespace = @($mapping | Where-Object { $_.mcpServerName -eq $NamespaceCheck }).Count -gt 0
      $brandCoverageState = if ($brandHasNamespace) { "present" } else { "missing" }
    }
  }
  catch {
    $brandCoverageState = "invalid-json"
  }
}

$rows = @(
  [pscustomobject]@{ Item = "mcp-tool-version.txt"; Value = if ($mcpToolVersion) { $mcpToolVersion } else { "<missing>" } },
  [pscustomobject]@{ Item = "mcp-cli-metadata/tracked-version.txt"; Value = if ($trackedVersion) { $trackedVersion } else { "<missing>" } },
  [pscustomobject]@{ Item = "dotnet global tool azure.mcp"; Value = if ($globalToolVersion) { $globalToolVersion } else { "<not-installed>" } },
  [pscustomobject]@{ Item = "latest local mcp-cli-metadata folder"; Value = if ($latestSnapshotVersion) { $latestSnapshotVersion } else { "<none>" } },
  [pscustomobject]@{ Item = "brand mapping namespace '$NamespaceCheck'"; Value = $brandCoverageState }
)

$rows | Format-Table -AutoSize

$reasons = @()
if (-not $mcpToolVersion) { $reasons += "mcp-tool-version.txt missing" }
if (-not $trackedVersion) { $reasons += "tracked-version.txt missing" }
if (-not $globalToolVersion) { $reasons += "dotnet global tool azure.mcp not installed" }
if (-not $latestSnapshotVersion) { $reasons += "no local mcp-cli-metadata version folders" }
if (-not $brandHasNamespace) { $reasons += "brand mapping missing namespace '$NamespaceCheck'" }

$targetVersion = $mcpToolVersion
if ($mcpToolVersion -and $trackedVersion -and ($mcpToolVersion -ne $trackedVersion)) {
  $reasons += "mcp-tool-version.txt ($mcpToolVersion) != tracked-version.txt ($trackedVersion)"
}
if ($targetVersion -and $globalToolVersion -and ($targetVersion -ne $globalToolVersion)) {
  $reasons += "mcp-tool-version.txt ($targetVersion) != dotnet azure.mcp ($globalToolVersion)"
}

if ($targetVersion -and $latestSnapshotVersion) {
  $latestSnapshotBase = ($latestSnapshotVersion -split "\+")[0]
  if ($targetVersion -ne $latestSnapshotBase -and $targetVersion -ne $latestSnapshotVersion) {
    $reasons += "mcp-tool-version.txt ($targetVersion) not represented by latest snapshot folder ($latestSnapshotVersion)"
  }
}

if ($reasons.Count -eq 0) {
  Write-Host ("ALIGNED at {0}" -f $targetVersion)
  exit 0
}

Write-Host ("MISALIGNED with reasons: {0}" -f ($reasons -join "; "))
exit 1
