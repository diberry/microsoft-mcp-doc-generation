# MergeNamespaces.Tests.ps1 — Pester wrapper for the merge-namespaces.sh smoke test (#706).
#
# merge-namespaces.sh (bash + inline-Node) is the multi-namespace merge that
# actually runs in production during start.sh (AD-011). It previously had zero
# automated coverage; the typed NamespaceMerger.cs twin is unit-tested but not
# wired into the pipeline. This wrapper runs the self-contained bash smoke test
# (merge-namespaces-smoke.sh) so the SHIPPING code path is exercised in CI.
#
# The smoke test needs bash + node (guaranteed on the ubuntu-latest runner used
# by the pester-tests job). It is skipped on non-Linux hosts, where that toolchain
# and POSIX path handling are not guaranteed; run the bash script directly there.

BeforeAll {
    $SmokeScript = Join-Path $PSScriptRoot "merge-namespaces-smoke.sh"
}

Describe "merge-namespaces.sh — shipping multi-namespace merge (AD-011)" {

    It "smoke test file exists" {
        Test-Path $SmokeScript | Should -BeTrue
    }

    It "merges canonical + -cli variants per AD-011 (primary header/related, ordered tools, updated tool_count, preserved tab markers)" -Skip:(-not $IsLinux) {
        $output = & bash $SmokeScript 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Host ($output -join "`n")
        }
        $LASTEXITCODE | Should -Be 0 -Because "the shipping merge must satisfy the AD-011 merge rules for both variants"
    }
}
