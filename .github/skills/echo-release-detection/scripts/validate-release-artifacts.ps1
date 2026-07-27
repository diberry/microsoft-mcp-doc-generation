<#
.SYNOPSIS
    Validates that Azure MCP and Azure Skills release artifacts were not written into the hub repo.
#>
#requires -Version 7.0
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$repoRoot = (git rev-parse --show-toplevel).Trim()
Set-Location $repoRoot

$metadataNames = @('cli-output.json', 'cli-namespace.json', 'cli-version.json', 'namespace-mapping.json')
$probePatterns = @('*echo-release-probe*', '*release-probe*', '*cli-extraction*', '*mcp-cli-extract*', '*azmcp-extract*', '*azure-mcp-extract*')
$offenses = New-Object System.Collections.Generic.List[string]

function Convert-ToRepoPath {
    param([string]$Path)
    $relative = [System.IO.Path]::GetRelativePath($repoRoot, $Path)
    return ($relative -replace '\\', '/')
}

function Test-IsExcludedPath {
    param([string]$RepoPath)
    return $RepoPath -eq '.git' -or
        $RepoPath -like '.git/*' -or
        $RepoPath -like '*/.git' -or
        $RepoPath -like '*/.git/*' -or
        $RepoPath -eq 'repos' -or
        $RepoPath -like 'repos/*' -or
        $RepoPath -like '*/repos' -or
        $RepoPath -like '*/repos/*' -or
        $RepoPath -eq '.worktrees' -or
        $RepoPath -like '.worktrees/*' -or
        $RepoPath -like '*/.worktrees' -or
        $RepoPath -like '*/.worktrees/*'
}

# Marker: the upstream microsoft/azure-skills repo ships .github/plugins/azure-skills/CHANGELOG.md at its own root. Finding that path INSIDE this hub tree means someone cloned the upstream repo in-place instead of reading it via 'gh api' — flag the clone root as a stray in-tree clone.
function Get-StrayAzureSkillsCloneRoot {
    param([string]$RepoPath)
    $normalized = ($RepoPath -replace '\\', '/')
    $marker = '/.github/plugins/azure-skills'

    if ($normalized -eq '.github/plugins/azure-skills' -or $normalized -like '.github/plugins/azure-skills/*') {
        return '.'
    }

    $index = $normalized.IndexOf($marker, [System.StringComparison]::OrdinalIgnoreCase)
    if ($index -ge 0) {
        $root = $normalized.Substring(0, $index)
        if ([string]::IsNullOrWhiteSpace($root)) { return '.' }
        return $root
    }

    return $null
}

$baselineArchives = @{}
& git ls-tree -r --name-only HEAD | ForEach-Object {
    if (-not [string]::IsNullOrWhiteSpace($_) -and ($_ -like '*.zip' -or $_ -like '*.nupkg')) {
        $baselineArchives[$_] = $true
    }
}

$scanRoots = Get-ChildItem -Path $repoRoot -Force | Where-Object {
    $repoPath = Convert-ToRepoPath -Path $_.FullName
    -not (Test-IsExcludedPath -RepoPath $repoPath)
}

$scanRoots | Get-ChildItem -Recurse -Force -File -Attributes !ReparsePoint |
    ForEach-Object {
        $repoPath = Convert-ToRepoPath -Path $_.FullName
        if (Test-IsExcludedPath -RepoPath $repoPath) { return }

        if ($_.Name -in $metadataNames) {
            $offenses.Add("CLI metadata file outside repos/: $repoPath")
        }

        if ($_.Extension -in @('.zip', '.nupkg') -and -not $baselineArchives.ContainsKey($repoPath)) {
            $offenses.Add("Release package/archive in hub: $repoPath")
        }

        if ($repoPath -like '*/.github/plugins/azure-skills/CHANGELOG.md' -or $repoPath -eq '.github/plugins/azure-skills/CHANGELOG.md') {
            $cloneRoot = Get-StrayAzureSkillsCloneRoot -RepoPath $repoPath
            if ($cloneRoot) {
                $offenses.Add("Stray upstream azure-skills clone in hub: $cloneRoot")
            }
        }
    }

$scanRoots | Get-ChildItem -Recurse -Force -Directory -Attributes !ReparsePoint |
    ForEach-Object {
        $repoPath = Convert-ToRepoPath -Path $_.FullName
        if (Test-IsExcludedPath -RepoPath $repoPath) { return }
        $cloneRoot = Get-StrayAzureSkillsCloneRoot -RepoPath $repoPath
        if ($cloneRoot) {
            $offenses.Add("Stray upstream azure-skills clone in hub: $cloneRoot")
        }
    }

$projectsPath = Join-Path $repoRoot 'projects'
if (Test-Path $projectsPath) {
    Get-ChildItem -Path $projectsPath -Recurse -Force -Directory |
        ForEach-Object {
            $name = $_.Name
            foreach ($pattern in $probePatterns) {
                if ($name -like $pattern) {
                    $repoPath = Convert-ToRepoPath -Path $_.FullName
                    $offenses.Add("Release probe/scratch directory under projects/: $repoPath")
                    break
                }
            }
        }
}

& git diff --cached --name-only --diff-filter=ACMR | ForEach-Object {
    if ([string]::IsNullOrWhiteSpace($_)) { return }
    $repoPath = ($_ -replace '\\', '/')
    if (Test-IsExcludedPath -RepoPath $repoPath) { return }
    $fileName = [System.IO.Path]::GetFileName($repoPath)
    $extension = [System.IO.Path]::GetExtension($repoPath)

    if ($fileName -in $metadataNames) {
        $offenses.Add("Staged CLI metadata file outside repos/: $repoPath")
    }
    if ($extension -in @('.zip', '.nupkg') -and -not $baselineArchives.ContainsKey($repoPath)) {
        $offenses.Add("Staged release package/archive in hub: $repoPath")
    }
}

if ($offenses.Count -gt 0) {
    Write-Error ("Release artifact validation failed:`n - " + (($offenses | Sort-Object -Unique) -join "`n - "))
    exit 1
}

Write-Host 'Release artifact validation passed: no new packages, probe/scratch directories, CLI metadata snapshots, or stray azure-skills clones found in the hub repo.'
exit 0
