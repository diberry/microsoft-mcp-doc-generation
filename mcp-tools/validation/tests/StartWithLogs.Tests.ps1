BeforeAll {
    $script:RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..")).Path
    $script:ProductionScript = Join-Path $script:RepoRoot "start-with-logs.ps1"
    $script:WorkRoot = Join-Path $PSScriptRoot (".work-generate-all-" + [guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Path $script:WorkRoot -Force | Out-Null

    function New-MetadataVersion {
        param(
            [Parameter(Mandatory)]
            [string]$Repository,

            [Parameter(Mandatory)]
            [string]$Version,

            [string[]]$Namespaces = @("storage"),

            [switch]$Unusable
        )

        $versionDirectory = Join-Path $Repository "mcp-cli-metadata\$Version"
        New-Item -ItemType Directory -Path $versionDirectory -Force | Out-Null

        $namespaceMap = [ordered]@{}
        foreach ($namespace in $Namespaces) {
            $namespaceMap[$namespace] = [ordered]@{
                display_name = "Azure $namespace"
                file_name = "azure-$namespace"
                short_name = $namespace
                tools = @("list")
            }
        }

        [ordered]@{
            generated_at = "2026-07-30T00:00:00Z"
            source_version = $Version
            namespace_count = $Namespaces.Count
            tool_count = $Namespaces.Count
            namespaces = $namespaceMap
        } | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $versionDirectory "namespace-mapping.json")

        @{ version = $Version } | ConvertTo-Json | Set-Content (Join-Path $versionDirectory "cli-version.json")
        $nsResults = @($Namespaces | ForEach-Object { @{ name = $_ } })
        @{ results = $nsResults } | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $versionDirectory "cli-namespace.json")
        if (-not $Unusable) {
            @{ results = @() } | ConvertTo-Json | Set-Content (Join-Path $versionDirectory "cli-output.json")
        }

        Set-Content (Join-Path $Repository "mcp-cli-metadata\tracked-version.txt") $Version
    }

    function New-TestRepository {
        param(
            [switch]$WithoutEnvironment,
            [switch]$WithoutMetadata
        )

        $repository = Join-Path $script:WorkRoot ([guid]::NewGuid().ToString("N") + " repo with spaces")
        New-Item -ItemType Directory -Path $repository -Force | Out-Null
        Copy-Item $script:ProductionScript (Join-Path $repository "start-with-logs.ps1")

        $azureEnvironment = Join-Path $repository ".azure\test-env"
        New-Item -ItemType Directory -Path $azureEnvironment -Force | Out-Null
        @{ defaultEnvironment = "test-env" } | ConvertTo-Json |
            Set-Content (Join-Path $azureEnvironment "config.json")
        if (-not $WithoutEnvironment) {
            @"
FOUNDRY_ENDPOINT=https://example.invalid/
FOUNDRY_MODEL_NAME=test-model
FOUNDRY_MODEL_API_VERSION=2025-01-01-preview
FOUNDRY_USE_DEFAULT_CREDENTIAL=true
"@ | Set-Content (Join-Path $azureEnvironment ".env")
        }

        if (-not $WithoutMetadata) {
            New-Item -ItemType Directory -Path (Join-Path $repository "mcp-cli-metadata") -Force | Out-Null
        }

        @'
#!/usr/bin/env bash
set -euo pipefail
printf '%s\n' "$*" >> "$GENERATION_INVOCATION_LOG"
printf 'START-SH:%s\n' "$1"
if [[ "${GENERATION_FAIL_NAMESPACE:-}" == "$1" ]]; then
  exit 17
fi
printf 'END-START-SH:%s\n' "$1"
'@ -replace "`r`n", "`n" | Set-Content -NoNewline (Join-Path $repository "start.sh")

        return $repository
    }

    function Invoke-Generator {
        param(
            [Parameter(Mandatory)]
            [string]$Repository,

            [string[]]$Arguments = @()
        )

        $logPath = Join-Path $Repository "start-invocations.txt"
        $previousLog = $env:GENERATION_INVOCATION_LOG
        $env:GENERATION_INVOCATION_LOG = $logPath
        try {
            $output = & pwsh -NoProfile -File (
                Join-Path $Repository "start-with-logs.ps1"
            ) @Arguments 2>&1
            $exitCode = $LASTEXITCODE
        } finally {
            $env:GENERATION_INVOCATION_LOG = $previousLog
        }

        $invocations = if (Test-Path $logPath) { @(Get-Content $logPath) } else { @() }
        [PSCustomObject]@{
            ExitCode = $exitCode
            Output = @($output)
            Invocations = @($invocations)
        }
    }
}

AfterAll {
    if ($script:WorkRoot -and (Test-Path $script:WorkRoot)) {
        Remove-Item $script:WorkRoot -Recurse -Force
    }
}

Describe "start-with-logs.ps1" {
    It "fails before start.sh when the tracked metadata version is absent" {
        $repository = New-TestRepository
        New-MetadataVersion -Repository $repository -Version "3.0.0-beta.10+aaaaaaaa"
        Set-Content (Join-Path $repository "mcp-cli-metadata\tracked-version.txt") "3.0.0-beta.11"

        $result = Invoke-Generator $repository

        $result.ExitCode | Should -Not -Be 0
        $result.Invocations | Should -BeNullOrEmpty
        ($result.Output -join "`n") | Should -Match "3\.0\.0-beta\.11.*not found"
    }

    It "fails before start.sh when the tracked metadata is unusable" {
        $repository = New-TestRepository
        New-MetadataVersion -Repository $repository -Version "3.0.0-beta.10+aaaaaaaa" -Unusable

        $result = Invoke-Generator $repository

        $result.ExitCode | Should -Not -Be 0
        $result.Invocations | Should -BeNullOrEmpty
        ($result.Output -join "`n") | Should -Match "cli-output\.json"
    }

    It "fails before start.sh when the AZD environment file is absent" {
        $repository = New-TestRepository -WithoutEnvironment
        New-MetadataVersion -Repository $repository -Version "3.0.0-beta.10+aaaaaaaa"

        $result = Invoke-Generator $repository

        $result.ExitCode | Should -Not -Be 0
        $result.Invocations | Should -BeNullOrEmpty
        ($result.Output -join "`n") | Should -Match "\.azure[/\\]test-env[/\\]\.env"
    }

    It "dispatches every mapped namespace to start.sh with steps 1 through 5" {
        $repository = New-TestRepository
        New-MetadataVersion -Repository $repository -Version "3.0.0-beta.10+aaaaaaaa" `
            -Namespaces @("storage", "keyvault", "extension_cli_generate")

        $result = Invoke-Generator $repository

        $result.ExitCode | Should -Be 0 -Because ($result.Output -join "`n")
        # Namespaces dispatched in sorted order; first gets no skip flags
        $result.Invocations | Should -Be @(
            "extension_cli_generate 1,2,3,4,5",
            "keyvault 1,2,3,4,5 --skip-build --skip-npm-update",
            "storage 1,2,3,4,5 --skip-build --skip-npm-update"
        )
    }

    It "continues past a namespace failure and reports it in the summary" {
        $repository = New-TestRepository
        New-MetadataVersion -Repository $repository -Version "3.0.0-beta.10+aaaaaaaa" `
            -Namespaces @("storage", "keyvault", "monitor")
        $previousFailure = $env:GENERATION_FAIL_NAMESPACE
        $env:GENERATION_FAIL_NAMESPACE = "keyvault"
        try {
            $result = Invoke-Generator $repository
        } finally {
            $env:GENERATION_FAIL_NAMESPACE = $previousFailure
        }

        $result.ExitCode | Should -Not -Be 0
        # All 3 namespaces dispatched (sorted); keyvault fails but others continue
        $result.Invocations | Should -Be @(
            "keyvault 1,2,3,4,5",
            "monitor 1,2,3,4,5 --skip-build --skip-npm-update",
            "storage 1,2,3,4,5 --skip-build --skip-npm-update"
        )
        ($result.Output -join "`n") | Should -Match "START-SH:keyvault"
    }

    It "does not call the legacy Generate-ToolFamily script" {
        $scriptText = Get-Content $script:ProductionScript -Raw
        $scriptText | Should -Not -Match "Generate-ToolFamily\.ps1"
        $scriptText | Should -Match "start\.sh"
    }

    It "supports metadata and script paths containing spaces" {
        $repository = New-TestRepository
        New-MetadataVersion -Repository $repository -Version "3.0.0-beta.10+aaaaaaaa" `
            -Namespaces @("speech")

        $result = Invoke-Generator $repository

        $result.ExitCode | Should -Be 0 -Because ($result.Output -join "`n")
        $result.Invocations | Should -Be @("speech 1,2,3,4,5")
    }

    It "preflights without invoking start.sh" {
        $repository = New-TestRepository
        New-MetadataVersion -Repository $repository -Version "3.0.0-beta.10+aaaaaaaa"

        $result = Invoke-Generator $repository -Arguments @("-PreflightOnly")

        $result.ExitCode | Should -Be 0
        $result.Invocations | Should -BeNullOrEmpty
        ($result.Output -join "`n") | Should -Match "Preflight validation succeeded"
    }

    It "generates only comma-listed namespaces via -NamespaceList" {
        $repository = New-TestRepository
        New-MetadataVersion -Repository $repository -Version "3.0.0-beta.10+aaaaaaaa" `
            -Namespaces @("storage", "keyvault", "monitor", "compute")

        $result = Invoke-Generator $repository -Arguments @("-NamespaceList", "storage,monitor")

        $result.ExitCode | Should -Be 0 -Because ($result.Output -join "`n")
        $result.Invocations | Should -Be @(
            "storage 1,2,3,4,5",
            "monitor 1,2,3,4,5 --skip-build --skip-npm-update"
        )
        ($result.Output -join "`n") | Should -Match "comma list"
    }

    It "generates only file-listed namespaces via -NamespaceFile" {
        $repository = New-TestRepository
        New-MetadataVersion -Repository $repository -Version "3.0.0-beta.10+aaaaaaaa" `
            -Namespaces @("storage", "keyvault", "monitor", "compute")

        $nsFile = Join-Path $repository "ns-list.txt"
        @"
# This is a comment
keyvault
compute

# Another comment
"@ | Set-Content $nsFile

        $result = Invoke-Generator $repository -Arguments @("-NamespaceFile", $nsFile)

        $result.ExitCode | Should -Be 0 -Because ($result.Output -join "`n")
        $result.Invocations | Should -Be @(
            "keyvault 1,2,3,4,5",
            "compute 1,2,3,4,5 --skip-build --skip-npm-update"
        )
        ($result.Output -join "`n") | Should -Match "file"
    }

    It "fails when -NamespaceList contains an unknown namespace" {
        $repository = New-TestRepository
        New-MetadataVersion -Repository $repository -Version "3.0.0-beta.10+aaaaaaaa" `
            -Namespaces @("storage", "keyvault")

        $result = Invoke-Generator $repository -Arguments @("-NamespaceList", "storage,bogus")

        $result.ExitCode | Should -Not -Be 0
        $result.Invocations | Should -BeNullOrEmpty
        ($result.Output -join "`n") | Should -Match "bogus"
    }

    It "fails when -NamespaceFile does not exist" {
        $repository = New-TestRepository
        New-MetadataVersion -Repository $repository -Version "3.0.0-beta.10+aaaaaaaa"

        $result = Invoke-Generator $repository -Arguments @("-NamespaceFile", "/no/such/file.txt")

        $result.ExitCode | Should -Not -Be 0
        $result.Invocations | Should -BeNullOrEmpty
        ($result.Output -join "`n") | Should -Match "not found"
    }

    It "fails when both -NamespaceList and -NamespaceFile are provided" {
        $repository = New-TestRepository
        New-MetadataVersion -Repository $repository -Version "3.0.0-beta.10+aaaaaaaa" `
            -Namespaces @("storage")

        $nsFile = Join-Path $repository "ns-list.txt"
        "storage" | Set-Content $nsFile

        $result = Invoke-Generator $repository -Arguments @("-NamespaceList", "storage", "-NamespaceFile", $nsFile)

        $result.ExitCode | Should -Not -Be 0
        $result.Invocations | Should -BeNullOrEmpty
        ($result.Output -join "`n") | Should -Match "Cannot specify both"
    }
}
