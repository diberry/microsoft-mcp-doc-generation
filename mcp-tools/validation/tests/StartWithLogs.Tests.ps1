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

    It "dispatches every mapped namespace to start.sh with steps 1 through 6" {
        $repository = New-TestRepository
        New-MetadataVersion -Repository $repository -Version "3.0.0-beta.10+aaaaaaaa" `
            -Namespaces @("storage", "keyvault", "extension_cli_generate")

        $result = Invoke-Generator $repository

        $result.ExitCode | Should -Be 0 -Because ($result.Output -join "`n")
        # Namespaces dispatched in sorted order; first gets no skip flags
        $result.Invocations | Should -Be @(
            "extension_cli_generate 1,2,3,4,5,6",
            "keyvault 1,2,3,4,5,6 --skip-build --skip-npm-update",
            "storage 1,2,3,4,5,6 --skip-build --skip-npm-update"
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
        # Sorted order: keyvault, monitor, storage. keyvault (idx0) builds but FAILS, so the shared
        # build is never confirmed; monitor (idx1) must REBUILD (no skip flags) and succeeds — which
        # confirms the build — so only storage (idx2) skips. AD-029 §7 $sharedBuildConfirmed gate.
        # (Current code uses "if ($index -gt 0)" and wrongly skip-flags monitor → RED.)
        $result.Invocations | Should -Be @(
            "keyvault 1,2,3,4,5,6",
            "monitor 1,2,3,4,5,6",
            "storage 1,2,3,4,5,6 --skip-build --skip-npm-update"
        )
        ($result.Output -join "`n") | Should -Match "START-SH:keyvault"
        ($result.Output -join "`n") | Should -Match "Generation Summary: 2/3 succeeded, 1 failed"
    }

    It "rebuilds the next namespace when the prior namespace built but exited nonzero" {
        $repository = New-TestRepository
        New-MetadataVersion -Repository $repository -Version "3.0.0-beta.10+aaaaaaaa" `
            -Namespaces @("aaa", "bbb")
        $previousFailure = $env:GENERATION_FAIL_NAMESPACE
        $env:GENERATION_FAIL_NAMESPACE = "aaa"
        try {
            $result = Invoke-Generator $repository
        } finally {
            $env:GENERATION_FAIL_NAMESPACE = $previousFailure
        }

        $result.ExitCode | Should -Not -Be 0
        # aaa (idx0) builds but FAILS → build NOT confirmed → bbb (idx1) must rebuild with NO skip
        # flags. A blanket "if ($index -gt 0)" would wrongly add --skip-build/--skip-npm-update to bbb.
        $result.Invocations | Should -Be @(
            "aaa 1,2,3,4,5,6",
            "bbb 1,2,3,4,5,6"
        )
    }

    It "captures the namespace exit code immediately after start.sh before any output-move cmdlet" {
        $scriptText = Get-Content $script:ProductionScript -Raw

        # Positive control: the bash dispatch line exists (the anchor the crux regex hangs off).
        $scriptText | Should -Match '&\s+\$bashPath\s+@startArguments'

        # Self-check: the crux regex DOES match the desired immediate-capture shape, so a typo'd
        # regex cannot pass this test vacuously.
        $sample = "        & `$bashPath @startArguments`n        `$namespaceExitCode = `$LASTEXITCODE"
        $sample | Should -Match '&\s+\$bashPath\s+@startArguments[ \t]*\r?\n[ \t]*\$namespaceExitCode\s*=\s*\$LASTEXITCODE'

        # Crux (RED on current source): the line IMMEDIATELY after the bash call must capture the exit
        # code into $namespaceExitCode — no Move-Item/Get-ChildItem/Write-Host may sit between them, or
        # the preserved nonzero exit is lost. Current source runs output-move cmdlets first.
        $scriptText | Should -Match '&\s+\$bashPath\s+@startArguments[ \t]*\r?\n[ \t]*\$namespaceExitCode\s*=\s*\$LASTEXITCODE'
    }

    It "uses the captured namespace exit code rather than `$LASTEXITCODE for the failure decision" {
        $scriptText = Get-Content $script:ProductionScript -Raw

        # Positive control: $LASTEXITCODE is a real token in the script (its reset + immediate capture),
        # so the "does not branch on $LASTEXITCODE" assertion below cannot pass vacuously.
        $scriptText | Should -Match '\$LASTEXITCODE'
        # Self-check: the legacy-pattern detector regex actually matches the legacy shape it forbids.
        'if ($LASTEXITCODE -ne 0) {' | Should -Match 'if\s*\(\s*\$LASTEXITCODE\s+-ne\s+0'

        # Crux 1 (RED): the per-namespace failure decision references the captured local.
        $scriptText | Should -Match 'if\s*\(\s*\$namespaceExitCode\s+-ne\s+0'
        # Crux 2 (RED): it must NOT branch on the volatile $LASTEXITCODE (clobbered by intervening cmdlets).
        $scriptText | Should -Not -Match 'if\s*\(\s*\$LASTEXITCODE\s+-ne\s+0'
    }

    It "declares no param or local variable whose name collides case-insensitively (AD-027)" {
        # Counts how many lowercase-folded names map to more than one DISTINCT (case-sensitive) spelling.
        $collisionCounter = {
            param([string[]]$Names)
            @($Names | Group-Object { $_.ToLowerInvariant() } |
                Where-Object { @($_.Group | Sort-Object -CaseSensitive -Unique).Count -gt 1 }).Count
        }

        # Positive control: two spellings that fold to one lowercase form ARE flagged (channel works).
        (& $collisionCounter @("NamespaceList", "namespacelist")) | Should -Be 1
        # Negative control: genuinely distinct names are NOT flagged (no false positives).
        (& $collisionCounter @("NamespaceList", "NamespaceFile")) | Should -Be 0

        $tokens = $null
        $errors = $null
        $ast = [System.Management.Automation.Language.Parser]::ParseFile($script:ProductionScript, [ref]$tokens, [ref]$errors)

        $insideFunction = {
            param($node)
            $parent = $node.Parent
            while ($parent) {
                if ($parent -is [System.Management.Automation.Language.FunctionDefinitionAst]) { return $true }
                $parent = $parent.Parent
            }
            return $false
        }

        # Top-level script param() names — the AD-027 collision surface (PR #785 renamed a script param).
        $paramNames = @($ast.ParamBlock.Parameters | ForEach-Object { $_.Name.VariablePath.UserPath })
        $paramNames | Should -Contain 'NamespaceList'   # channel: params really are enumerated

        # Top-level (non-function) local assignments + foreach loop variables.
        $locals = @($ast.FindAll({ param($n) $n -is [System.Management.Automation.Language.AssignmentStatementAst] }, $true) |
            Where-Object { -not (& $insideFunction $_) } |
            ForEach-Object { $_.Left } |
            Where-Object { $_ -is [System.Management.Automation.Language.VariableExpressionAst] -and $_.VariablePath.IsUnqualified } |
            ForEach-Object { $_.VariablePath.UserPath })
        $foreachVars = @($ast.FindAll({ param($n) $n -is [System.Management.Automation.Language.ForEachStatementAst] }, $true) |
            Where-Object { -not (& $insideFunction $_) } |
            ForEach-Object { $_.Variable.VariablePath.UserPath })

        # Inject the two locals AD-029 §7 introduces and prove they are actually present in the set.
        $comparisonSet = @($paramNames) + @($locals) + @($foreachVars) + @('namespaceExitCode', 'sharedBuildConfirmed')
        $comparisonSet | Should -Contain 'namespaceExitCode'
        $comparisonSet | Should -Contain 'sharedBuildConfirmed'

        # The full top-level declaration set (incl. the new locals) is collision-free. Becomes a live
        # guard once Quinn adds the locals: renaming one to $namespacelist would fold onto $NamespaceList.
        (& $collisionCounter $comparisonSet) | Should -Be 0
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
        $result.Invocations | Should -Be @("speech 1,2,3,4,5,6")
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
            "storage 1,2,3,4,5,6",
            "monitor 1,2,3,4,5,6 --skip-build --skip-npm-update"
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
            "keyvault 1,2,3,4,5,6",
            "compute 1,2,3,4,5,6 --skip-build --skip-npm-update"
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

    # ── P7 (ADDENDUM A — S3, M31/M32/M33): catalog summary aggregates cats 1–4, prints 5–6 once ──
    # RUNTIME-RED: start-with-logs.ps1 currently prints only "Generation Summary: X/Y …"; the
    # six-category catalog block does not exist, so the first `Should -Match` below fails. The three
    # stub run-accounting.json files are pre-staged into generated-<ns>-fixture (matching the script's
    # real "generated-<ns>-*" move glob); the fake start.sh stays byte-identical and its real move
    # block relocates each stub to <consolidatedDir>/<ns>/run-accounting.json exactly as production.
    It "reports all six accounting categories in the catalog summary and never sums the baseline constants" {
        $repository = New-TestRepository
        New-MetadataVersion -Repository $repository -Version "3.0.0-beta.10+aaaaaaaa" `
            -Namespaces @("aaa", "bbb", "ccc")

        # aaa = successful
        $aaaFixture = Join-Path $repository "generated-aaa-fixture"
        New-Item -ItemType Directory -Path $aaaFixture -Force | Out-Null
        @'
{ "schemaVersion":"1.0","successfulNamespaces":["aaa"],"rootFailedNamespaces":[],"warningOnlyFailures":[],"suppressedSteps":[],
  "reconciliation":{"logicalRecordTotal":34,"physicalCopyTotal":68,
    "categoryCounts":{"successful":0,"rootFailed":23,"warningOnly":0,"suppressed":0,"cascadeImported":10,"unclassifiedDiagnostic":1}} }
'@ | Set-Content (Join-Path $aaaFixture "run-accounting.json")

        # bbb = root failure + 2 suppressed
        $bbbFixture = Join-Path $repository "generated-bbb-fixture"
        New-Item -ItemType Directory -Path $bbbFixture -Force | Out-Null
        @'
{ "schemaVersion":"1.0","successfulNamespaces":[],
  "rootFailedNamespaces":[{"namespace":"bbb","rootStepId":2,"rootStepName":"Generate example prompts","rootFailureId":"bbb.02.root","exitCode":1}],
  "warningOnlyFailures":[],
  "suppressedSteps":[{"namespace":"bbb","stepId":3,"rootFailureId":"bbb.02.root"},{"namespace":"bbb","stepId":4,"rootFailureId":"bbb.02.root"}],
  "reconciliation":{"logicalRecordTotal":34,"physicalCopyTotal":68,
    "categoryCounts":{"successful":0,"rootFailed":23,"warningOnly":0,"suppressed":0,"cascadeImported":10,"unclassifiedDiagnostic":1}} }
'@ | Set-Content (Join-Path $bbbFixture "run-accounting.json")

        # ccc = warning-only failure
        $cccFixture = Join-Path $repository "generated-ccc-fixture"
        New-Item -ItemType Directory -Path $cccFixture -Force | Out-Null
        @'
{ "schemaVersion":"1.0","successfulNamespaces":[],"rootFailedNamespaces":[],
  "warningOnlyFailures":[{"namespace":"ccc","stepId":7,"stepName":"Validate article health"}],"suppressedSteps":[],
  "reconciliation":{"logicalRecordTotal":34,"physicalCopyTotal":68,
    "categoryCounts":{"successful":0,"rootFailed":23,"warningOnly":0,"suppressed":0,"cascadeImported":10,"unclassifiedDiagnostic":1}} }
'@ | Set-Content (Join-Path $cccFixture "run-accounting.json")

        $result = Invoke-Generator $repository

        $result.ExitCode | Should -Be 0 -Because ($result.Output -join "`n")
        $output = ($result.Output -join "`n")

        # Cat 1 — successful namespaces aggregate to 1 (aaa only).
        $output | Should -Match 'Successful namespaces \(1\)'
        $successfulLine = ($result.Output | Where-Object { $_ -match 'Successful namespaces' }) -join "`n"
        $successfulLine | Should -Match 'aaa'
        $successfulLine | Should -Not -Match 'bbb'

        # Cat 2 — root-failed namespaces aggregate to 1 (bbb), named with its stable root id.
        $output | Should -Match 'Root-failed namespaces \(1\)'
        $rootLine = ($result.Output | Where-Object { $_ -match 'Root-failed namespaces' }) -join "`n"
        $rootLine | Should -Match 'bbb'
        $rootLine | Should -Match 'bbb\.02\.root'
        $rootLine | Should -Not -Match 'aaa'

        # Cat 3 — warning-only failures aggregate to 1 (ccc).
        $output | Should -Match 'Warning-only failures \(1\)'
        $warningLine = ($result.Output | Where-Object { $_ -match 'Warning-only failures' }) -join "`n"
        $warningLine | Should -Match 'ccc'

        # Cat 4 — suppressed steps aggregate to 2 (bbb steps 3 & 4).
        $output | Should -Match 'Suppressed steps \(2\)'
        $suppressedLine = ($result.Output | Where-Object { $_ -match 'Suppressed steps' }) -join "`n"
        $suppressedLine | Should -Match 'bbb\.02\.root'

        # Anti-sum crux — cats 5 & 6 are catalog-constant (a pure function of the frozen beta34
        # baseline): reported ONCE, never summed across the three namespaces. Each absent-assertion
        # is paired with its positive control.
        $output | Should -Match 'Cascades imported from historical fixtures \(10\)'
        $output | Should -Not -Match 'Cascades imported from historical fixtures \(30\)'
        $output | Should -Match 'Unclassified records \(1\)'
        $output | Should -Not -Match 'Unclassified records \(3\)'
    }

    # ── P8 (ADDENDUM A — S3, M34): every category label prints even when all namespaces succeed ──
    # RUNTIME-RED: same missing catalog block — all six labels must print (incl. count 0) and the
    # baseline constants must remain non-zero, proving the channel is live rather than dead.
    It "prints every accounting category label even when all namespaces succeed" {
        $repository = New-TestRepository
        New-MetadataVersion -Repository $repository -Version "3.0.0-beta.10+aaaaaaaa" `
            -Namespaces @("aaa", "bbb")

        # Two successful stubs; all live lists empty except successfulNamespaces. Baseline 10/1.
        $aaaFixture = Join-Path $repository "generated-aaa-fixture"
        New-Item -ItemType Directory -Path $aaaFixture -Force | Out-Null
        @'
{ "schemaVersion":"1.0","successfulNamespaces":["aaa"],"rootFailedNamespaces":[],"warningOnlyFailures":[],"suppressedSteps":[],
  "reconciliation":{"logicalRecordTotal":34,"physicalCopyTotal":68,
    "categoryCounts":{"successful":0,"rootFailed":23,"warningOnly":0,"suppressed":0,"cascadeImported":10,"unclassifiedDiagnostic":1}} }
'@ | Set-Content (Join-Path $aaaFixture "run-accounting.json")

        $bbbFixture = Join-Path $repository "generated-bbb-fixture"
        New-Item -ItemType Directory -Path $bbbFixture -Force | Out-Null
        @'
{ "schemaVersion":"1.0","successfulNamespaces":["bbb"],"rootFailedNamespaces":[],"warningOnlyFailures":[],"suppressedSteps":[],
  "reconciliation":{"logicalRecordTotal":34,"physicalCopyTotal":68,
    "categoryCounts":{"successful":0,"rootFailed":23,"warningOnly":0,"suppressed":0,"cascadeImported":10,"unclassifiedDiagnostic":1}} }
'@ | Set-Content (Join-Path $bbbFixture "run-accounting.json")

        $result = Invoke-Generator $repository

        $result.ExitCode | Should -Be 0 -Because ($result.Output -join "`n")
        $output = ($result.Output -join "`n")

        # Cat 1 — catalog union of the two successful files.
        $output | Should -Match 'Successful namespaces \(2\)'
        $successfulLine = ($result.Output | Where-Object { $_ -match 'Successful namespaces' }) -join "`n"
        $successfulLine | Should -Match 'aaa'
        $successfulLine | Should -Match 'bbb'

        # Labels-always-print trio: every live category prints even at 0.
        $output | Should -Match 'Root-failed namespaces \(0\)'
        $output | Should -Match 'Warning-only failures \(0\)'
        $output | Should -Match 'Suppressed steps \(0\)'

        # Paired positive controls — baseline constants remain non-zero (channel is live).
        $output | Should -Match 'Cascades imported from historical fixtures \(10\)'
        $output | Should -Match 'Unclassified records \(1\)'

        # Negative control: the zeros are genuine, not a mislabeled non-zero.
        $output | Should -Not -Match 'Root-failed namespaces \(1\)'
    }
}
