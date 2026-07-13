# Count-Tools.Tests.ps1 — Pester tests for scripts/count-tools.ps1 (#676).
#
# count-tools.ps1 is a repository-root audit utility that counts and groups the
# Azure MCP tools in a tools-list.json by service/namespace (the first token of
# each tool's `command`). It is exercised here in the CI-covered pester-tests
# lane (Invoke-Pester -Path ./mcp-tools/validation/tests). The script lives at the
# repository root (./scripts/count-tools.ps1) per issue #676, so the test resolves
# three directories up from this file to find it.
#
# Pure helper functions (Get-ServiceFromCommand, Get-ToolCounts) are parsed out of
# the script and evaluated in isolation — the same convention used by
# Scan-McpToolCoverage.Tests.ps1 — so the script's mandatory -FilePath parameter
# and its main body do not run during unit tests.

BeforeAll {
    $RepoRoot   = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..")).Path
    $ScriptPath = Join-Path $RepoRoot "scripts\count-tools.ps1"

    if (Test-Path $ScriptPath) {
        $scriptContent = Get-Content $ScriptPath -Raw
        foreach ($fnName in @('Get-ServiceFromCommand', 'Get-ToolCounts', 'Read-ToolsList')) {
            $fnMatch = [regex]::Match($scriptContent, "(?s)(function $fnName \{.*?\n\})")
            if ($fnMatch.Success) {
                Invoke-Expression $fnMatch.Value
            }
        }
    }

    # Build a self-contained fixture (results-wrapped shape, like real tools-list.json).
    $script:SampleTools = @(
        [PSCustomObject]@{ command = 'acr registry list' }
        [PSCustomObject]@{ command = 'acr registry repository list' }
        [PSCustomObject]@{ command = 'monitor metrics query' }
        [PSCustomObject]@{ command = 'storage account list' }
        [PSCustomObject]@{ command = 'storage blob get' }
        [PSCustomObject]@{ command = 'storage container list' }
    )

    $script:FixtureDir  = Join-Path ([System.IO.Path]::GetTempPath()) ("count-tools-" + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $script:FixtureDir -Force | Out-Null
    $script:FixturePath = Join-Path $script:FixtureDir "tools-list.json"
    [PSCustomObject]@{ status = 200; results = $script:SampleTools } |
        ConvertTo-Json -Depth 6 | Set-Content -Path $script:FixturePath -Encoding utf8
}

AfterAll {
    if ($script:FixtureDir -and (Test-Path $script:FixtureDir)) {
        Remove-Item $script:FixtureDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Describe "count-tools.ps1 — script presence (#676)" {
    It "exists at the repository-root scripts directory" {
        $ScriptPath = Join-Path (Resolve-Path (Join-Path $PSScriptRoot "..\..\..")).Path "scripts\count-tools.ps1"
        Test-Path $ScriptPath | Should -BeTrue -Because "issue #676 documents the script location as ./scripts/count-tools.ps1"
    }
}

Describe "Get-ServiceFromCommand — service is the first token of the command" {
    It "returns the first whitespace-delimited token" {
        Get-ServiceFromCommand -Command 'acr registry list' | Should -Be 'acr'
    }

    It "trims surrounding whitespace before splitting" {
        Get-ServiceFromCommand -Command '  monitor metrics query  ' | Should -Be 'monitor'
    }

    It "returns `$null for empty or whitespace commands" {
        Get-ServiceFromCommand -Command '' | Should -BeNullOrEmpty
        Get-ServiceFromCommand -Command '   ' | Should -BeNullOrEmpty
    }
}

Describe "Get-ToolCounts — totals and per-service breakdown" {
    It "counts the total number of tools" {
        $result = Get-ToolCounts -Tools $script:SampleTools
        $result.Total | Should -Be 6
    }

    It "groups tools by service (first token of command)" {
        $result = Get-ToolCounts -Tools $script:SampleTools
        ($result.ByService | Where-Object { $_.Service -eq 'storage' }).ToolCount | Should -Be 3
        ($result.ByService | Where-Object { $_.Service -eq 'acr' }).ToolCount     | Should -Be 2
        ($result.ByService | Where-Object { $_.Service -eq 'monitor' }).ToolCount | Should -Be 1
    }

    It "orders the breakdown by descending tool count" {
        $result = Get-ToolCounts -Tools $script:SampleTools
        $result.ByService[0].Service   | Should -Be 'storage'
        $result.ByService[0].ToolCount | Should -Be 3
    }

    It "skips tools with an empty or missing command" {
        $tools = @(
            [PSCustomObject]@{ command = 'sql server list' }
            [PSCustomObject]@{ command = '' }
            [PSCustomObject]@{ command = $null }
        )
        $result = Get-ToolCounts -Tools $tools
        $result.Total | Should -Be 1
        ($result.ByService | Where-Object { $_.Service -eq 'sql' }).ToolCount | Should -Be 1
    }
}

Describe "Read-ToolsList — shape handling" {
    It "reads the results array from a results-wrapped tools-list.json" {
        $tools = Read-ToolsList -Path $script:FixturePath
        @($tools).Count | Should -Be 6
    }

    It "reads a bare top-level array tools-list.json" {
        $bareArrayPath = Join-Path $script:FixtureDir "tools-list-array.json"
        $script:SampleTools | ConvertTo-Json -Depth 6 | Set-Content -Path $bareArrayPath -Encoding utf8

        $tools = Read-ToolsList -Path $bareArrayPath

        @($tools).Count | Should -Be 6
        # Real tool objects must be returned — not an array of $nulls from member enumeration.
        ($tools | Where-Object { $null -ne $_.command }).Count | Should -Be 6
        (Get-ToolCounts -Tools $tools).Total | Should -Be 6
    }

    It "throws when the file does not exist" {
        { Read-ToolsList -Path (Join-Path $script:FixtureDir "missing.json") } | Should -Throw
    }
}

Describe "count-tools.ps1 — full invocation" {
    It "emits a count object whose Total matches the fixture" {
        $ScriptPath = Join-Path (Resolve-Path (Join-Path $PSScriptRoot "..\..\..")).Path "scripts\count-tools.ps1"
        $result = & $ScriptPath -FilePath $script:FixturePath
        $counts = @($result) | Where-Object { $null -ne $_ -and $_.PSObject.Properties.Name -contains 'Total' } | Select-Object -Last 1
        $counts.Total | Should -Be 6
        $counts.ByService[0].Service | Should -Be 'storage'
    }
}
