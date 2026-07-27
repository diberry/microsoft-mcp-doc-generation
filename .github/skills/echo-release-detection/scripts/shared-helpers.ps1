#requires -Version 7.0

<#
.SYNOPSIS
    Resolves a configured path relative to the repo root or team root.
.DESCRIPTION
    Returns rooted paths unchanged. Relative paths are resolved against the
    current repo root first. In worktree mode, unresolved relative paths fall
    back to the team root above `.worktrees\` when that location exists.
.PARAMETER Value
    The configured path value to resolve.
.PARAMETER RepoRoot
    The current repository root.
.PARAMETER BasePath
    Optional base path used before repo-root fallback.
#>
function Resolve-ConfiguredPath {
    [CmdletBinding()]
    param(
        [AllowNull()]
        [string]$Value,

        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string]$RepoRoot,

        [string]$BasePath
    )

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return $null
    }

    if ([System.IO.Path]::IsPathRooted($Value)) {
        return $Value
    }

    if (-not [string]::IsNullOrWhiteSpace($BasePath)) {
        $baseCandidate = Join-Path $BasePath $Value
        if (Test-Path $baseCandidate) {
            return $baseCandidate
        }
    }

    $repoCandidate = Join-Path $RepoRoot $Value
    if (Test-Path $repoCandidate) {
        return $repoCandidate
    }

    $repoParent = Split-Path -Parent $RepoRoot
    if ((Split-Path -Leaf $repoParent) -eq '.worktrees') {
        $teamRoot = Split-Path -Parent $repoParent
        $teamCandidate = Join-Path $teamRoot $Value
        if (Test-Path $teamCandidate) {
            return $teamCandidate
        }
    }

    if (-not [string]::IsNullOrWhiteSpace($BasePath)) {
        return Join-Path $BasePath $Value
    }

    return $repoCandidate
}

<#
.SYNOPSIS
    Ensures a directory exists.
.PARAMETER Path
    Directory path to create if missing.
.PARAMETER Description
    Friendly directory description for error messages.
#>
function Ensure-Directory {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string]$Path,

        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string]$Description
    )

    try {
        New-Item -ItemType Directory -Path $Path -Force -ErrorAction Stop | Out-Null
    }
    catch {
        throw "Unable to create $Description at '$Path'. $($_.Exception.Message)"
    }
}

<#
.SYNOPSIS
    Validates that a directory exists.
.PARAMETER Path
    Directory path to validate.
.PARAMETER Description
    Friendly directory description for error messages.
#>
function Assert-DirectoryExists {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string]$Path,

        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string]$Description
    )

    if (-not (Test-Path -Path $Path -PathType Container)) {
        throw "$Description was not found at '$Path'."
    }
}

<#
.SYNOPSIS
    Validates that a file exists.
.PARAMETER Path
    File path to validate.
.PARAMETER Description
    Friendly file description for error messages.
#>
function Assert-FileExists {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string]$Path,

        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string]$Description
    )

    if (-not (Test-Path -Path $Path -PathType Leaf)) {
        throw "$Description was not found at '$Path'."
    }
}

<#
.SYNOPSIS
    Renders a tokenized report template.
.PARAMETER TemplatePath
    Path to the markdown template file.
.PARAMETER Values
    Replacement values keyed by token name.
#>
function Render-Template {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string]$TemplatePath,

        [Parameter(Mandatory)]
        [ValidateNotNull()]
        [hashtable]$Values
    )

    Assert-FileExists -Path $TemplatePath -Description 'Report template'

    $content = Get-Content -Path $TemplatePath -Raw -Encoding UTF8
    foreach ($key in $Values.Keys) {
        $replacement = if ($null -eq $Values[$key]) { '' } else { [string]$Values[$key] }
        $content = $content.Replace('{{' + $key + '}}', $replacement)
    }

    return $content
}

<#
.SYNOPSIS
    Normalizes an input version list.
.DESCRIPTION
    Accepts arrays or comma-delimited values, trims whitespace, removes blanks,
    and returns unique versions in sorted order.
.PARAMETER Versions
    Raw version values from parameters or prior-step JSON.
#>
function Normalize-VersionList {
    [CmdletBinding()]
    param(
        [AllowNull()]
        [string[]]$Versions
    )

    $normalized = @()
    foreach ($item in @($Versions)) {
        if ([string]::IsNullOrWhiteSpace($item)) {
            continue
        }

        $normalized += ($item -split ',') |
            ForEach-Object { $_.Trim() } |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    }

    return @($normalized | Sort-Object -Unique)
}

<#
.SYNOPSIS
    Finds the newest JSON artifact matching a pattern.
.PARAMETER Directory
    Directory to search.
.PARAMETER Pattern
    File pattern to match.
#>
function Get-LatestJsonArtifact {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string]$Directory,

        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string]$Pattern
    )

    if (-not (Test-Path -Path $Directory -PathType Container)) {
        return $null
    }

    return Get-ChildItem -Path $Directory -Filter $Pattern -File |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1
}

<#
.SYNOPSIS
    Finds the metadata folder for a version.
.PARAMETER MetadataRoot
    Root directory containing metadata version folders.
.PARAMETER Version
    Version prefix to match.
#>
function Get-VersionFolder {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string]$MetadataRoot,

        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string]$Version
    )

    return Get-ChildItem -Path $MetadataRoot -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -like "$Version*" } |
        Select-Object -First 1
}

<#
.SYNOPSIS
    Extracts a beta ordinal from a semantic version when present.
.PARAMETER Version
    Version string to inspect.
#>
function Get-BetaOrdinal {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string]$Version
    )

    if ($Version -match '(?i)-beta\.(\d+)$') {
        return [int]$Matches[1]
    }

    return -1
}

<#
.SYNOPSIS
    Converts a version into a sortable key.
.DESCRIPTION
    Produces a stable lexical key for semver-like versions used by the Echo
    scripts, including prerelease labels such as beta. Stable releases sort
    after prereleases for the same major.minor.patch value.
.PARAMETER Version
    Version string to normalize.
#>
function Get-VersionSortKey {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string]$Version
    )

    if ($Version -notmatch '^(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)(?:-(?<pre>[0-9A-Za-z.-]+))?$') {
        throw "Unsupported version format '$Version'."
    }

    $major = [int]$Matches.major
    $minor = [int]$Matches.minor
    $patch = [int]$Matches.patch
    $pre = $Matches.pre

    if ([string]::IsNullOrWhiteSpace($pre)) {
        return ('{0:D5}.{1:D5}.{2:D5}.1.zzzz' -f $major, $minor, $patch)
    }

    $preParts = foreach ($segment in ($pre.ToLowerInvariant() -split '\.')) {
        if ($segment -match '^\d+$') {
            '0{0:D10}' -f [int]$segment
        }
        else {
            "1$segment"
        }
    }

    return ('{0:D5}.{1:D5}.{2:D5}.0.{3}' -f $major, $minor, $patch, ($preParts -join '.'))
}


<#
.SYNOPSIS
    Creates a Spark structured-output JSON envelope.
.DESCRIPTION
    Shared producer helper matching structured-output@1.0.0. Domain payloads
    are passed as Result and wrapped under the envelope result property.
#>
function New-StructuredOutputEnvelope {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNull()]
        [object]$Result,

        [ValidateSet('success', 'partial', 'failed')]
        [string]$Status = 'success',

        [object[]]$Errors = @(),

        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string]$Producer,

        [ValidateNotNullOrEmpty()]
        [string]$Schema,

        [ValidateNotNullOrEmpty()]
        [string]$CorrelationId
    )

    $metadata = [ordered]@{
        producer        = $Producer
        contractVersion = '1.0.0'
        format          = 'json'
        generatedAt     = (Get-Date).ToUniversalTime().ToString('o')
    }

    if ($Schema) { $metadata.schema = $Schema }
    if ($CorrelationId) { $metadata.correlationId = $CorrelationId }

    return [ordered]@{
        status   = $Status
        result   = $Result
        errors   = @($Errors)
        metadata = $metadata
    }
}
