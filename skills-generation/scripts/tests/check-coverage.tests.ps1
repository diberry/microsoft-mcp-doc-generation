Describe "check-coverage.ps1" {
    BeforeAll {
        $scriptPath = Join-Path $PSScriptRoot ".." "check-coverage.ps1"
        $fixtureDir = Join-Path $PSScriptRoot "fixtures" "coverage-test"
        New-Item -ItemType Directory -Path "$fixtureDir/sub" -Force | Out-Null
    }

    It "Should pass when coverage meets threshold" {
        $coverageXml = @"
<?xml version="1.0" encoding="utf-8"?>
<coverage line-rate="0.85" branch-rate="0.75" version="1.0">
  <packages><package name="Test" line-rate="0.85" branch-rate="0.75" /></packages>
</coverage>
"@
        Set-Content "$fixtureDir/sub/coverage.cobertura.xml" $coverageXml
        & $scriptPath -MinLineCoverage 80 -MinBranchCoverage 70 -ResultsDir $fixtureDir
        $LASTEXITCODE | Should -Be 0
    }

    It "Should fail when line coverage is below threshold" {
        $coverageXml = @"
<?xml version="1.0" encoding="utf-8"?>
<coverage line-rate="0.70" branch-rate="0.75" version="1.0">
  <packages><package name="Test" line-rate="0.70" branch-rate="0.75" /></packages>
</coverage>
"@
        Set-Content "$fixtureDir/sub/coverage.cobertura.xml" $coverageXml
        & $scriptPath -MinLineCoverage 80 -MinBranchCoverage 70 -ResultsDir $fixtureDir
        $LASTEXITCODE | Should -Be 1
    }

    AfterAll {
        $fixtureDir = Join-Path $PSScriptRoot "fixtures" "coverage-test"
        if (Test-Path $fixtureDir) { Remove-Item $fixtureDir -Recurse -Force }
    }
}
