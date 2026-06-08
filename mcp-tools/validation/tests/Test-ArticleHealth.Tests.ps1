# Test-ArticleHealth.Tests.ps1 — Pester tests for Test-ArticleHealth.ps1

BeforeAll {
    $ScriptPath  = Join-Path $PSScriptRoot "..\Test-ArticleHealth.ps1"
    $FixturesDir = Join-Path $PSScriptRoot "fixtures"

    # Dot-source the internal Test-File function for unit testing
    . {
        # Re-declare helpers so Test-File can be called directly
        function Write-Pass { param([string]$msg) }
        function Write-Warn { param([string]$msg) }
        function Write-Fail { param([string]$msg) }
        function New-CheckResult {
            param([string]$Name, [string]$Status, [string]$Detail = "")
            [PSCustomObject]@{ Name = $Name; Status = $Status; Detail = $Detail }
        }
    }

    # Load Test-File from the script via dot-sourcing in a restricted way
    # We parse out and invoke only the Test-File function definition
    $scriptContent = Get-Content $ScriptPath -Raw
    $funcMatch = [regex]::Match($scriptContent, '(?s)(function Test-File \{.*?\n\})')
    if ($funcMatch.Success) {
        Invoke-Expression $funcMatch.Value
    }
}

Describe "Test-ArticleHealth.ps1 — valid-article.md" {
    BeforeAll {
        $results = Test-File -Path (Join-Path $FixturesDir "valid-article.md")
    }

    It "ms.date passes" {
        ($results | Where-Object Name -eq "frontmatter.ms.date").Status | Should -Be "pass"
    }
    It "ms.custom passes" {
        ($results | Where-Object Name -eq "frontmatter.ms.custom").Status | Should -Be "pass"
    }
    It "ms.reviewer passes" {
        ($results | Where-Object Name -eq "frontmatter.ms.reviewer").Status | Should -Be "pass"
    }
    It "no blank lines in frontmatter passes" {
        ($results | Where-Object Name -eq "frontmatter.no-blank-lines").Status | Should -Be "pass"
    }
    It "mcp-cli.version passes" {
        ($results | Where-Object Name -eq "frontmatter.mcp-cli.version").Status | Should -Be "pass"
    }
    It "no H3 tool sections passes" {
        ($results | Where-Object Name -eq "headings.no-h3-tool-sections").Status | Should -Be "pass"
    }
    It "no duplicate H2 passes" {
        ($results | Where-Object Name -eq "headings.no-duplicate-h2").Status | Should -Be "pass"
    }
    It "no absolute Learn URLs passes" {
        ($results | Where-Object Name -eq "urls.no-absolute-learn").Status | Should -Be "pass"
    }
    It "param bold names passes" {
        ($results | Where-Object Name -eq "params.bold-names").Status | Should -Be "pass"
    }
    It "no INCLUDE param rows passes" {
        ($results | Where-Object Name -eq "params.no-include-rows").Status | Should -Be "pass"
    }
    It "no failed checks" {
        $results | Where-Object Status -eq "fail" | Should -BeNullOrEmpty
    }
}

Describe "Test-ArticleHealth.ps1 — bad-frontmatter.md" {
    BeforeAll {
        $results = Test-File -Path (Join-Path $FixturesDir "bad-frontmatter.md")
    }

    It "ms.date fails (missing)" {
        ($results | Where-Object Name -eq "frontmatter.ms.date").Status | Should -Be "fail"
    }
    It "ms.custom fails (missing)" {
        ($results | Where-Object Name -eq "frontmatter.ms.custom").Status | Should -Be "fail"
    }
    It "ms.reviewer warns (ms.reviewer key absent — not a bare reviewer: line)" {
        # Script logic: no ms.reviewer: → warn; bare "reviewer:" with no value → fail.
        # bad-frontmatter.md omits ms.reviewer entirely, so the else branch fires: warn.
        ($results | Where-Object Name -eq "frontmatter.ms.reviewer").Status | Should -Be "warn"
    }
    It "blank lines in frontmatter warns" {
        ($results | Where-Object Name -eq "frontmatter.no-blank-lines").Status | Should -Be "warn"
    }
    It "mcp-cli.version warns (missing)" {
        ($results | Where-Object Name -eq "frontmatter.mcp-cli.version").Status | Should -Be "warn"
    }
    It "has at least one fail or warn" {
        $bad = $results | Where-Object { $_.Status -in @("fail","warn") }
        $bad | Should -Not -BeNullOrEmpty
    }
}

Describe "Test-ArticleHealth.ps1 — bare-reviewer.md (bare reviewer: with no value)" {
    BeforeAll {
        $results = Test-File -Path (Join-Path $FixturesDir "bare-reviewer.md")
    }

    It "ms.reviewer fails when frontmatter has bare reviewer: with no value" {
        # Regression: bare "reviewer:" (no ms. prefix, no value) must hit the fail branch,
        # not the warn branch. If the fix is reverted, this test will fail.
        ($results | Where-Object Name -eq "frontmatter.ms.reviewer").Status | Should -Be "fail"
    }

    It "ms.reviewer detail reports bare reviewer: with no value" {
        $detail = ($results | Where-Object Name -eq "frontmatter.ms.reviewer").Detail
        $detail | Should -Match "bare reviewer"
    }
}

Describe "Test-ArticleHealth.ps1 — bad-headings.md" {
    BeforeAll {
        $results = Test-File -Path (Join-Path $FixturesDir "bad-headings.md")
    }

    It "H3 tool sections fails" {
        ($results | Where-Object Name -eq "headings.no-h3-tool-sections").Status | Should -Be "fail"
    }
    It "Detail mentions count of H3 headings" {
        $detail = ($results | Where-Object Name -eq "headings.no-h3-tool-sections").Detail
        $detail | Should -Match '\d+'
    }
}

Describe "Test-ArticleHealth.ps1 — bad-urls.md" {
    BeforeAll {
        $results = Test-File -Path (Join-Path $FixturesDir "bad-urls.md")
    }

    It "absolute Learn URLs fails" {
        ($results | Where-Object Name -eq "urls.no-absolute-learn").Status | Should -Be "fail"
    }
    It "Detail mentions URL count" {
        $detail = ($results | Where-Object Name -eq "urls.no-absolute-learn").Detail
        $detail | Should -Match '\d+'
    }
}

Describe "Test-ArticleHealth.ps1 — bad-params.md" {
    BeforeAll {
        $results = Test-File -Path (Join-Path $FixturesDir "bad-params.md")
    }

    It "INCLUDE param rows fails" {
        ($results | Where-Object Name -eq "params.no-include-rows").Status | Should -Be "fail"
    }
}

Describe "Test-ArticleHealth.ps1 — bad-markers.md (malformed HTML comment)" {
    BeforeAll {
        $results = Test-File -Path (Join-Path $FixturesDir "bad-markers.md")
    }

    It "markers.well-formed warns when comment has no space after <!--" {
        # <!--nospace--> fails both valid-marker patterns → warn (not pass)
        ($results | Where-Object Name -eq "markers.well-formed").Status | Should -Be "warn"
    }

    It "markers.well-formed detail reports at least one malformed marker" {
        $detail = ($results | Where-Object Name -eq "markers.well-formed").Detail
        $detail | Should -Match '\b1\b'
    }

    It "valid fields still pass in bad-markers.md" {
        ($results | Where-Object Name -eq "frontmatter.ms.date").Status    | Should -Be "pass"
        ($results | Where-Object Name -eq "frontmatter.ms.reviewer").Status | Should -Be "pass"
    }
}

Describe "Test-ArticleHealth.ps1 — script integration (exit codes)" {
    It "exits 0 for valid article" {
        $output = & pwsh -NoProfile -File $ScriptPath -ArticlePath (Join-Path $FixturesDir 'valid-article.md') 2>&1
        $LASTEXITCODE | Should -Be 0
    }

    It "exits 1 for article with failures" {
        & pwsh -NoProfile -File $ScriptPath -ArticlePath (Join-Path $FixturesDir 'bad-urls.md') 2>&1 | Out-Null
        $LASTEXITCODE | Should -Be 1
    }

    It "exits 1 with -Strict on article with warnings" {
        & pwsh -NoProfile -File $ScriptPath -ArticlePath (Join-Path $FixturesDir 'bad-frontmatter.md') -Strict 2>&1 | Out-Null
        $LASTEXITCODE | Should -Be 1
    }

    It "accepts -ArticlesDir and checks all .md files" {
        $output = & pwsh -NoProfile -File $ScriptPath -ArticlesDir $FixturesDir 2>&1
        $output | Should -Not -BeNullOrEmpty
    }

    It "writes JSON when -OutputJson is specified" {
        $jsonPath = Join-Path $PSScriptRoot "health-output.json"
        if (Test-Path $jsonPath) { Remove-Item $jsonPath }
        & pwsh -NoProfile -File $ScriptPath -ArticlePath (Join-Path $FixturesDir 'valid-article.md') -OutputJson $jsonPath 2>&1 | Out-Null
        Test-Path $jsonPath | Should -Be $true
        $json = Get-Content $jsonPath -Raw | ConvertFrom-Json
        $json | Should -Not -BeNullOrEmpty
        Remove-Item $jsonPath -ErrorAction SilentlyContinue
    }
}
