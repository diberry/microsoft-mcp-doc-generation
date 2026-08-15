#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Generate documentation for Azure MCP namespace families with transcript logging.

.DESCRIPTION
    Orchestrates the documentation generation pipeline for namespaces with full
    transcript logging. Supports three namespace selection modes:

    1. Full list (default) — all namespaces from namespace-mapping.json
    2. Comma-separated list — via -NamespaceList parameter
    3. Text file — via -NamespaceFile parameter (one namespace per line)

    Each namespace runs steps 1-5 via start.sh. After each namespace completes
    (or fails), its output is moved from the repo root into a consolidated
    run directory:

        ./generated-<run-datetime>/<namespace>/

.PARAMETER NamespaceList
    Comma-separated list of namespaces to generate (e.g., "advisor,appservice,compute").

.PARAMETER NamespaceFile
    Path to a text file with one namespace per line. Blank lines and lines
    starting with # are ignored.

.PARAMETER PreflightOnly
    Validate environment, credentials, and metadata without generating docs.

.EXAMPLE
    # Full generation of all namespaces
    pwsh ./start-with-logs.ps1

    # Generate specific namespaces (comma list)
    pwsh ./start-with-logs.ps1 -NamespaceList "advisor,appservice,compute"

    # Generate namespaces from a text file
    pwsh ./start-with-logs.ps1 -NamespaceFile ./my-namespaces.txt

    # Preflight check only (no generation)
    pwsh ./start-with-logs.ps1 -PreflightOnly

.OUTPUTS
    Generated docs:  ./generated-<run-datetime>/<namespace>/
    Transcript log:  ./generated-<run-datetime>/generate-all-namespaces.log
    Critical failures (all namespaces):
                     ./generated-<run-datetime>/critical-failures/<namespace>--<timestamp>-<step>-*.json
    Run summary printed to console at end (succeeded/failed counts + list).
#>

param(
    [string]$NamespaceList,
    [string]$NamespaceFile,
    [switch]$PreflightOnly
)

if (-not [string]::IsNullOrWhiteSpace($NamespaceList) -and -not [string]::IsNullOrWhiteSpace($NamespaceFile)) {
    throw "Cannot specify both -NamespaceList and -NamespaceFile. Use one or the other."
}

$ErrorActionPreference = "Stop"

function Read-JsonArtifact {
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Required metadata artifact not found: $Path"
    }

    try {
        return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    } catch {
        throw "Metadata artifact is not valid JSON: $Path. $($_.Exception.Message)"
    }
}

function Resolve-AzdEnvironmentPath {
    param(
        [Parameter(Mandatory)]
        [string]$AzureDirectory
    )

    $rootEnvironmentPath = Join-Path $AzureDirectory ".env"
    if (-not (Test-Path -LiteralPath $AzureDirectory -PathType Container)) {
        throw "Required AZD environment directory not found: $AzureDirectory"
    }

    $environmentDirectories = @(Get-ChildItem -LiteralPath $AzureDirectory -Directory)
    $configPaths = @()
    $rootConfigPath = Join-Path $AzureDirectory "config.json"
    if (Test-Path -LiteralPath $rootConfigPath -PathType Leaf) {
        $configPaths += $rootConfigPath
    }
    $configPaths += @(
        $environmentDirectories |
            ForEach-Object { Join-Path $_.FullName "config.json" } |
            Where-Object { Test-Path -LiteralPath $_ -PathType Leaf }
    )

    $resolvedDefaults = @()
    $missingDefaultEnvironmentPaths = @()
    foreach ($configPath in $configPaths) {
        try {
            $config = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
        } catch {
            throw "AZD configuration is not valid JSON: $configPath. $($_.Exception.Message)"
        }

        $defaultProperty = $config.PSObject.Properties["defaultEnvironment"]
        if ($null -eq $defaultProperty -or
            [string]::IsNullOrWhiteSpace([string]$defaultProperty.Value)) {
            continue
        }

        $defaultEnvironment = ([string]$defaultProperty.Value).Trim()
        $matchingDirectory = @(
            $environmentDirectories |
                Where-Object { $_.Name -eq $defaultEnvironment }
        )
        if ($matchingDirectory.Count -eq 1) {
            $defaultEnvironmentPath = Join-Path $matchingDirectory[0].FullName ".env"
            if (Test-Path -LiteralPath $defaultEnvironmentPath -PathType Leaf) {
                $resolvedDefaults += $defaultEnvironmentPath
            } else {
                $missingDefaultEnvironmentPaths += $defaultEnvironmentPath
            }
        }
    }

    $resolvedDefaults = @($resolvedDefaults | Sort-Object -Unique)
    if ($resolvedDefaults.Count -gt 1) {
        throw "Multiple AZD defaultEnvironment values resolve to different environment directories under: $AzureDirectory"
    }
    if ($resolvedDefaults.Count -eq 1) {
        return $resolvedDefaults[0]
    }

    $nestedEnvironmentPaths = @(
        $environmentDirectories |
            ForEach-Object { Join-Path $_.FullName ".env" } |
            Where-Object { Test-Path -LiteralPath $_ -PathType Leaf }
    )
    if ($nestedEnvironmentPaths.Count -eq 1) {
        return $nestedEnvironmentPaths[0]
    }
    if ($nestedEnvironmentPaths.Count -gt 1) {
        throw "Multiple nested AZD environment files are ambiguous because no resolvable defaultEnvironment was found under: $AzureDirectory"
    }

    if (Test-Path -LiteralPath $rootEnvironmentPath -PathType Leaf) {
        return $rootEnvironmentPath
    }

    $missingDefaultEnvironmentPaths = @($missingDefaultEnvironmentPaths | Sort-Object -Unique)
    if ($missingDefaultEnvironmentPaths.Count -eq 1) {
        throw "Required environment file not found: $($missingDefaultEnvironmentPaths[0])"
    }

    throw "Required AZD environment file not found under: $AzureDirectory"
}

function Import-ProcessEnvironment {
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    $lineNumber = 0
    foreach ($line in Get-Content -LiteralPath $Path) {
        $lineNumber++
        $entry = $line.Trim()
        if (-not $entry -or $entry.StartsWith("#")) {
            continue
        }

        if ($entry.StartsWith("export ")) {
            $entry = $entry.Substring(7).TrimStart()
        }

        $separator = $entry.IndexOf("=")
        if ($separator -lt 1) {
            throw "Invalid environment entry at line $lineNumber in $Path."
        }

        $name = $entry.Substring(0, $separator).Trim()
        $value = $entry.Substring($separator + 1).Trim()
        if ($name -notmatch '^[A-Za-z_][A-Za-z0-9_]*$') {
            throw "Invalid environment variable name '$name' in $Path"
        }

        if ($value.Length -ge 2) {
            $first = $value[0]
            $last = $value[$value.Length - 1]
            if (($first -eq '"' -and $last -eq '"') -or
                ($first -eq "'" -and $last -eq "'")) {
                $value = $value.Substring(1, $value.Length - 2)
            }
        }

        [Environment]::SetEnvironmentVariable($name, $value, "Process")
    }
}

try {
    $repoRoot = $PSScriptRoot
    $runTimestamp = Get-Date -Format "yyyyMMddTHHmmss"
    $consolidatedDir = Join-Path $repoRoot "generated-$runTimestamp"
    New-Item -ItemType Directory -Path $consolidatedDir -Force | Out-Null
    $logPath = Join-Path $consolidatedDir "generate-all-namespaces.log"
    Start-Transcript -Path $logPath -Append
    Write-Host "Log file: $logPath"

    $metadataRoot = Join-Path $repoRoot "mcp-cli-metadata"
    $azureDirectory = Join-Path $repoRoot ".azure"
    $environmentPath = Resolve-AzdEnvironmentPath -AzureDirectory $azureDirectory
    $startPath = Join-Path $repoRoot "start.sh"

    if (-not (Test-Path -LiteralPath $metadataRoot -PathType Container)) {
        throw "Metadata version directory not found: $metadataRoot"
    }

    $trackedVersionPath = Join-Path $metadataRoot "tracked-version.txt"
    if (-not (Test-Path -LiteralPath $trackedVersionPath -PathType Leaf)) {
        throw "Tracked metadata version file not found: $trackedVersionPath"
    }

    $trackedVersion = (Get-Content -LiteralPath $trackedVersionPath -Raw).Trim()
    if ([string]::IsNullOrWhiteSpace($trackedVersion)) {
        throw "Tracked metadata version is empty: $trackedVersionPath"
    }
    try {
        $null = [System.Management.Automation.SemanticVersion]$trackedVersion
    } catch {
        throw "Tracked metadata version is not a semantic version ('$trackedVersion'): $trackedVersionPath"
    }

    $trackedPattern = "^$([regex]::Escape($trackedVersion))(?:\+.+)?$"
    $matchingDirectories = @(
        Get-ChildItem -LiteralPath $metadataRoot -Directory |
            Where-Object { $_.Name -match $trackedPattern }
    )
    if ($matchingDirectories.Count -eq 0) {
        throw "Tracked metadata version '$trackedVersion' was not found in: $metadataRoot"
    }
    if ($matchingDirectories.Count -gt 1) {
        throw "Tracked metadata version '$trackedVersion' is ambiguous in ${metadataRoot}: $($matchingDirectories.Name -join ', ')"
    }

    $selectedVersion = $matchingDirectories[0].Name
    $selectedDirectory = $matchingDirectories[0].FullName
    Write-Host "Using metadata version: $selectedVersion"

    $artifactNames = @(
        "cli-version.json",
        "cli-namespace.json",
        "cli-output.json",
        "namespace-mapping.json"
    )
    $artifacts = @{}
    foreach ($artifactName in $artifactNames) {
        $artifactPath = Join-Path $selectedDirectory $artifactName
        $artifacts[$artifactName] = Read-JsonArtifact -Path $artifactPath
    }

    $metadataVersion = [string]$artifacts["cli-version.json"].version
    if ([string]::IsNullOrWhiteSpace($metadataVersion)) {
        throw "Metadata artifact has an invalid shape (missing version): $(Join-Path $selectedDirectory 'cli-version.json')"
    }
    try {
        $null = [System.Management.Automation.SemanticVersion]$metadataVersion
    } catch {
        throw "Metadata artifact has an invalid semantic version '$metadataVersion': $(Join-Path $selectedDirectory 'cli-version.json')"
    }
    if ($metadataVersion -ne $selectedVersion) {
        throw "Metadata version '$metadataVersion' does not match directory '$selectedVersion'."
    }

    foreach ($artifactName in @("cli-namespace.json", "cli-output.json")) {
        $artifact = $artifacts[$artifactName]
        if ($null -eq $artifact.PSObject.Properties["results"] -or
            $null -eq $artifact.results -or
            $artifact.results -isnot [array]) {
            throw "Metadata artifact has an invalid shape (results must be an array): $(Join-Path $selectedDirectory $artifactName)"
        }
    }

    # Derive namespace list from cli-namespace.json (actual CLI output) — the source of truth.
    # namespace-mapping.json is a derivative artifact that may include stale entries.
    $cliNamespaceData = $artifacts["cli-namespace.json"]
    if ($null -eq $cliNamespaceData.results -or $cliNamespaceData.results -isnot [array] -or $cliNamespaceData.results.Count -eq 0) {
        throw "cli-namespace.json has no results — cannot determine namespace list: $(Join-Path $selectedDirectory 'cli-namespace.json')"
    }

    $allKnownNamespaces = @($cliNamespaceData.results | ForEach-Object { $_.name } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Sort-Object)
    if ($allKnownNamespaces.Count -eq 0) {
        throw "cli-namespace.json produced no valid namespace names: $(Join-Path $selectedDirectory 'cli-namespace.json')"
    }

    # Resolve namespace list from the three input modes
    if (-not [string]::IsNullOrWhiteSpace($NamespaceFile)) {
        if (-not (Test-Path -LiteralPath $NamespaceFile -PathType Leaf)) {
            throw "Namespace file not found: $NamespaceFile"
        }
        $namespaces = @(
            Get-Content -LiteralPath $NamespaceFile |
                ForEach-Object { $_.Trim() } |
                Where-Object { $_ -and -not $_.StartsWith("#") }
        )
        if ($namespaces.Count -eq 0) {
            throw "Namespace file is empty or contains only comments: $NamespaceFile"
        }
        $inputMode = "file ($NamespaceFile)"
    } elseif (-not [string]::IsNullOrWhiteSpace($NamespaceList)) {
        $namespaces = @(
            $NamespaceList -split ',' |
                ForEach-Object { $_.Trim() } |
                Where-Object { $_ }
        )
        if ($namespaces.Count -eq 0) {
            throw "Namespaces parameter is empty after parsing."
        }
        $inputMode = "comma list ($($namespaces.Count) namespace(s))"
    } else {
        $namespaces = $allKnownNamespaces
        $inputMode = "full list ($($namespaces.Count) namespace(s))"
    }

    # Validate requested namespaces exist in metadata
    $unknownNamespaces = @($namespaces | Where-Object { $_ -notin $allKnownNamespaces })
    if ($unknownNamespaces.Count -gt 0) {
        throw "Unknown namespace(s) not in metadata: $($unknownNamespaces -join ', ')"
    }

    Write-Host "Namespace source: $inputMode"
    Write-Host "Namespaces to generate: $($namespaces -join ', ')"

    Import-ProcessEnvironment -Path $environmentPath

    foreach ($requiredSetting in @(
        "FOUNDRY_ENDPOINT",
        "FOUNDRY_MODEL_NAME",
        "FOUNDRY_MODEL_API_VERSION"
    )) {
        $value = [Environment]::GetEnvironmentVariable($requiredSetting, "Process")
        if ([string]::IsNullOrWhiteSpace($value)) {
            throw "Required keyless Foundry setting '$requiredSetting' must be nonblank in $environmentPath."
        }
    }

    $useDefaultCredentialValue = [Environment]::GetEnvironmentVariable(
        "FOUNDRY_USE_DEFAULT_CREDENTIAL",
        "Process"
    )
    $useDefaultCredential = $false
    if ([string]::IsNullOrWhiteSpace($useDefaultCredentialValue) -or
        -not [bool]::TryParse($useDefaultCredentialValue, [ref]$useDefaultCredential) -or
        -not $useDefaultCredential) {
        throw "Required keyless Foundry setting 'FOUNDRY_USE_DEFAULT_CREDENTIAL' must parse as true in $environmentPath."
    }

    if ($PreflightOnly) {
        Write-Host "Preflight validation succeeded using latest metadata version: $selectedVersion"
        exit 0
    }

    if (-not (Test-Path -LiteralPath $startPath -PathType Leaf)) {
        throw "Generation entry point not found: $startPath"
    }

    $bashPath = $null
    if ($IsWindows -or $env:OS -eq "Windows_NT") {
        $gitBashCandidates = @(
            (Join-Path $env:ProgramFiles "Git\bin\bash.exe"),
            (Join-Path $env:ProgramFiles "Git\usr\bin\bash.exe")
        )
        $bashPath = $gitBashCandidates |
            Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } |
            Select-Object -First 1
    }
    if ([string]::IsNullOrWhiteSpace($bashPath)) {
        $bashPath = (Get-Command bash -ErrorAction SilentlyContinue).Source
    }
    if ([string]::IsNullOrWhiteSpace($bashPath)) {
        throw "bash was not found in PATH. Install Git Bash or another bash implementation to run start.sh."
    }

    $failedNamespaces = @()
    $sharedBuildConfirmed = $false
    for ($index = 0; $index -lt $namespaces.Count; $index++) {
        $namespace = $namespaces[$index]
        $progress = "$namespace - $($index + 1)/$($namespaces.Count)"
        Write-Host ""
        Write-Host "Generating namespace family: $progress"

        # AD-029 §7: skip the shared build/npm update only after an earlier invocation
        # actually built and exited 0 ($sharedBuildConfirmed). Loop position never gates this.
        $startArguments = @($startPath, $namespace, "1,2,3,4,5")
        if ($sharedBuildConfirmed) {
            $startArguments += @("--skip-build", "--skip-npm-update")
        }

        $global:LASTEXITCODE = 0
        & $bashPath @startArguments
        $namespaceExitCode = $LASTEXITCODE

        # Confirm the shared build only when THIS invocation built (omitted --skip-build)
        # and exited cleanly. A namespace that built but exited nonzero leaves the build
        # unconfirmed, so the next namespace rebuilds. AD-029 §7.
        if (-not $sharedBuildConfirmed -and $namespaceExitCode -eq 0) {
            $sharedBuildConfirmed = $true
        }

        # Move generated output into consolidated directory
        $generatedDirs = @(Get-ChildItem -LiteralPath $repoRoot -Directory -Filter "generated-${namespace}-*" |
            Sort-Object LastWriteTime -Descending)
        if ($generatedDirs.Count -gt 0) {
            $sourceDir = $generatedDirs[0].FullName
            $targetDir = Join-Path $consolidatedDir $namespace
            if (Test-Path -LiteralPath $targetDir) {
                Remove-Item -LiteralPath $targetDir -Recurse -Force
            }
            Move-Item -LiteralPath $sourceDir -Destination $targetDir
            Write-Host "  $progress - Moved output → $targetDir"

            # Also move the trace directory (generated-<namespace>/ without timestamp) if present
            $traceDir = Join-Path $repoRoot "generated-${namespace}"
            if (Test-Path -LiteralPath $traceDir) {
                $traceTarget = Join-Path $targetDir "trace"
                if (Test-Path -LiteralPath $traceTarget) {
                    Remove-Item -LiteralPath $traceTarget -Recurse -Force
                }
                $traceSource = Join-Path $traceDir "trace"
                if (Test-Path -LiteralPath $traceSource) {
                    Move-Item -LiteralPath $traceSource -Destination $traceTarget
                }
                Remove-Item -LiteralPath $traceDir -Recurse -Force
                Write-Host "  $progress - Moved trace → $traceTarget"
            }

            # Hoist critical-failure JSONs to top-level critical-failures/ with namespace in filename
            $nsFailureDir = Join-Path $targetDir "critical-failures"
            if (Test-Path -LiteralPath $nsFailureDir) {
                $topFailureDir = Join-Path $consolidatedDir "critical-failures"
                if (-not (Test-Path -LiteralPath $topFailureDir)) {
                    New-Item -ItemType Directory -Path $topFailureDir -Force | Out-Null
                }
                foreach ($f in Get-ChildItem -LiteralPath $nsFailureDir -Filter "*.json") {
                    $newName = "${namespace}--$($f.Name)"
                    Copy-Item -LiteralPath $f.FullName -Destination (Join-Path $topFailureDir $newName)
                }
            }
        }

        if ($namespaceExitCode -ne 0) {
            $failedNamespaces += $namespace
            Write-Warning "$progress - Namespace generation failed with exit code $namespaceExitCode. Continuing with next namespace."
        }
    }

    if ($failedNamespaces.Count -gt 0) {
        Write-Host ""
        Write-Host "====================================================================="
        Write-Host "Generation Summary: $($namespaces.Count - $failedNamespaces.Count)/$($namespaces.Count) succeeded, $($failedNamespaces.Count) failed"
        Write-Host "====================================================================="
        Write-Host "Failed namespaces:"
        foreach ($ns in $failedNamespaces) {
            $topFailureDir = Join-Path $consolidatedDir "critical-failures"
            $nsPattern = "${ns}--*.json"
            if ((Test-Path -LiteralPath $topFailureDir) -and
                @(Get-ChildItem -LiteralPath $topFailureDir -Filter $nsPattern).Count -gt 0) {
                $count = @(Get-ChildItem -LiteralPath $topFailureDir -Filter $nsPattern).Count
                Write-Host "  ❌ $ns ($count critical failure(s))"
            } else {
                Write-Host "  ❌ $ns"
            }
        }
        Write-Host ""
    }

    # AD-029 §6 / ADDENDUM A: six-category run-accounting catalog summary.
    # Aggregate each processed namespace's run-accounting.json (relocated by the move block above
    # into <consolidatedDir>/<namespace>/) into ONE catalog summary that reports the six categories
    # on separate, individually identifiable lines. Live categories 1-4 (successful, root-failed,
    # warning-only, suppressed) are SUMMED across namespaces. Catalog-constant categories 5-6
    # (cascades imported from historical fixtures, unclassified records) are a pure function of the
    # frozen beta34 baseline — identical in every namespace's file — so they are taken ONCE and
    # never summed (guards against inflating (10)/(1) into (30)/(3)).
    $accountingSuccessful = @()
    $accountingRootFailed = @()
    $accountingWarningOnly = @()
    $accountingSuppressed = @()
    $accountingCascadeImported = 0
    $accountingUnclassified = 0
    $accountingBaselineCaptured = $false
    $accountingUnreadable = @()

    foreach ($accountingNamespace in $namespaces) {
        $accountingPath = Join-Path (Join-Path $consolidatedDir $accountingNamespace) "run-accounting.json"
        if (-not (Test-Path -LiteralPath $accountingPath -PathType Leaf)) {
            # A missing artifact is non-fatal here and cannot mask a real failure: the authoritative
            # per-namespace failure signal is $failedNamespaces (captured from $namespaceExitCode and
            # reported above), which is computed independently of this read.
            continue
        }

        try {
            $accountingData = Get-Content -LiteralPath $accountingPath -Raw | ConvertFrom-Json
        } catch {
            # Malformed JSON must never crash the summary or silently corrupt counts. It is recorded
            # for a visible note and excluded from every live/baseline count; the independent
            # $failedNamespaces list remains the authoritative status, so no failure is hidden.
            $accountingUnreadable += $accountingNamespace
            continue
        }

        # Null-filter each list: a missing property yields $null, and @($null) would otherwise add a
        # phantom entry that inflates the count. Empty arrays contribute nothing.
        $accountingSuccessful += @($accountingData.successfulNamespaces | Where-Object { $null -ne $_ })
        $accountingRootFailed += @($accountingData.rootFailedNamespaces | Where-Object { $null -ne $_ })
        $accountingWarningOnly += @($accountingData.warningOnlyFailures | Where-Object { $null -ne $_ })
        $accountingSuppressed += @($accountingData.suppressedSteps | Where-Object { $null -ne $_ })

        # Categories 5-6 are catalog-constant: capture ONCE from the first file that carries them.
        if (-not $accountingBaselineCaptured -and
            $null -ne $accountingData.reconciliation -and
            $null -ne $accountingData.reconciliation.categoryCounts) {
            $accountingCounts = $accountingData.reconciliation.categoryCounts
            if ($null -ne $accountingCounts.cascadeImported) {
                $accountingCascadeImported = [int]$accountingCounts.cascadeImported
            }
            if ($null -ne $accountingCounts.unclassifiedDiagnostic) {
                $accountingUnclassified = [int]$accountingCounts.unclassifiedDiagnostic
            }
            $accountingBaselineCaptured = $true
        }
    }

    $accountingSuccessful = @($accountingSuccessful)
    $accountingRootFailed = @($accountingRootFailed)
    $accountingWarningOnly = @($accountingWarningOnly)
    $accountingSuppressed = @($accountingSuppressed)

    Write-Host ""
    Write-Host "====================================================================="
    Write-Host "Run accounting - six categories"
    Write-Host "====================================================================="

    # Category 1 — Successful namespaces (live, aggregated).
    $accountingSuccessfulLine = "  Successful namespaces ($($accountingSuccessful.Count))"
    if ($accountingSuccessful.Count -gt 0) {
        $accountingSuccessfulLine += ": " + (($accountingSuccessful | ForEach-Object { [string]$_ }) -join ", ")
    }
    Write-Host $accountingSuccessfulLine

    # Category 2 — Root-failed namespaces (live, aggregated), each named with its stable root id.
    $accountingRootFailedLine = "  Root-failed namespaces ($($accountingRootFailed.Count))"
    if ($accountingRootFailed.Count -gt 0) {
        $accountingRootFailedLine += ": " + (($accountingRootFailed | ForEach-Object {
            "$($_.namespace) [step $($_.rootStepId) root=$($_.rootFailureId)]"
        }) -join ", ")
    }
    Write-Host $accountingRootFailedLine

    # Category 3 — Warning-only failures (live, aggregated), separate from roots.
    $accountingWarningOnlyLine = "  Warning-only failures ($($accountingWarningOnly.Count))"
    if ($accountingWarningOnly.Count -gt 0) {
        $accountingWarningOnlyLine += ": " + (($accountingWarningOnly | ForEach-Object {
            "$($_.namespace) [step $($_.stepId)]"
        }) -join ", ")
    }
    Write-Host $accountingWarningOnlyLine

    # Category 4 — Suppressed steps (live, aggregated), each linked to its root id.
    $accountingSuppressedLine = "  Suppressed steps ($($accountingSuppressed.Count))"
    if ($accountingSuppressed.Count -gt 0) {
        $accountingSuppressedLine += ": " + (($accountingSuppressed | ForEach-Object {
            "$($_.namespace) [step $($_.stepId) root=$($_.rootFailureId)]"
        }) -join ", ")
    }
    Write-Host $accountingSuppressedLine

    # Categories 5-6 — baseline constants, printed once (never summed across namespaces).
    Write-Host "  Cascades imported from historical fixtures ($accountingCascadeImported)"
    Write-Host "  Unclassified records ($accountingUnclassified)"

    if ($accountingUnreadable.Count -gt 0) {
        Write-Warning ("Run accounting could not parse run-accounting.json for: " +
            ($accountingUnreadable -join ", ") +
            ". Excluded from the six-category counts above; see the per-namespace failure list for authoritative status.")
    }
    Write-Host ""

    Write-Host "Output: $consolidatedDir"
    Write-Host "Log:    $logPath"
    Write-Host "Generated $($namespaces.Count - $failedNamespaces.Count)/$($namespaces.Count) namespace family file(s) using metadata $selectedVersion."
    Stop-Transcript
    if ($failedNamespaces.Count -gt 0) { exit 1 }
    exit 0
} catch {
    Write-Error $_.Exception.Message
    Stop-Transcript
    exit 1
}
