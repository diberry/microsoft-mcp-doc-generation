# Pester tests for New-AdoImpactComment (verdict banner + scope + sanitization).
# The function lives inside the monolithic echo-content-impact.ps1 script, so we
# AST-extract just that function and load it in isolation — no production change,
# and the script's main body never runs.

BeforeAll {
    $scriptPath = Resolve-Path (Join-Path $PSScriptRoot '..' 'echo-content-impact.ps1')
    $tokens = $null; $errs = $null
    $ast = [System.Management.Automation.Language.Parser]::ParseFile($scriptPath, [ref]$tokens, [ref]$errs)
    $fn = $ast.Find({
            param($n)
            $n -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -eq 'New-AdoImpactComment'
        }, $true)
    if (-not $fn) { throw "New-AdoImpactComment not found in $scriptPath" }
    . ([scriptblock]::Create($fn.Extent.Text))

    function New-Row {
        param($Version, $ImpactType, $Namespace = 'ns', $ToolCount = 1, $Priority = 'MEDIUM', $ActionText = 'Update article')
        [pscustomobject]@{
            version = $Version; impactType = $ImpactType; namespace = $Namespace
            toolCount = $ToolCount; priority = $Priority; action = $ActionText
        }
    }
}

Describe 'New-AdoImpactComment verdict banner' {

    It 'renders [FINAL: NO DOC IMPACT] for a resolved no-impact release delta' {
        $matrix = @(
            (New-Row -Version '3.0.0-beta.19' -ImpactType 'UNCHANGED'),
            (New-Row -Version '3.0.0-beta.20' -ImpactType 'UNCHANGED')
        )
        $contract = @{ decision = 'NO_CONTENT_CHANGE'; reasonCodes = @('NO_NAMESPACE_TOOL_DIFFS'); evidence = 'NEW=0'; unblockStep = 'None' }
        $out = New-AdoImpactComment -ImpactMatrix $matrix -VersionValue '3.0.0-beta.20' -TargetAdoItemId 123 -NoImpactContract $contract
        $out | Should -Match '\[FINAL: NO DOC IMPACT\]'
        $out | Should -Match 'Scope: release delta'
    }

    It 'renders [ACTION NEEDED] when a NEW namespace exists' {
        $matrix = @( (New-Row -Version '3.0.0-beta.20' -ImpactType 'NEW' -Namespace 'aks' -ToolCount 2) )
        $out = New-AdoImpactComment -ImpactMatrix $matrix -VersionValue '3.0.0-beta.20' -TargetAdoItemId 1
        $out | Should -Match '\[ACTION NEEDED: 1 new / 0 changed\]'
        $out | Should -Match 'Scope: single-version snapshot'
    }

    It 'renders [NO DELTA DETECTED - needs review] when contract is still pending' {
        $matrix = @( (New-Row -Version '3.0.0-beta.20' -ImpactType 'UNCHANGED') )
        $contract = @{ decision = 'NO_CONTENT_CHANGE_PENDING'; reasonCodes = @(); evidence = ''; unblockStep = '' }
        $out = New-AdoImpactComment -ImpactMatrix $matrix -VersionValue '3.0.0-beta.20' -TargetAdoItemId 1 -NoImpactContract $contract
        $out | Should -Match '\[NO DELTA DETECTED - needs review\]'
    }

    It 'preserves the original "Content Impact Summary (Step 3)" header for downstream parsing' {
        $matrix = @( (New-Row -Version '3.0.0-beta.20' -ImpactType 'UNCHANGED') )
        $out = New-AdoImpactComment -ImpactMatrix $matrix -VersionValue '3.0.0-beta.20' -TargetAdoItemId 1
        $out | Should -Match 'Content Impact Summary \(Step 3\)'
    }
}

Describe 'New-AdoImpactComment sanitization and guards' {

    It 'sanitizes ampersands and angle brackets in dynamic values' {
        $matrix = @( (New-Row -Version '3.0.0-beta.20' -ImpactType 'NEW' -Namespace 'a&b<c>d' -ToolCount 1) )
        $out = New-AdoImpactComment -ImpactMatrix $matrix -VersionValue 'v&1' -TargetAdoItemId 1
        # '&' -> ' and ', '<' and '>' stripped: 'a&b<c>d' becomes 'a and bcd'
        $out | Should -Match 'a and bcd'
        $out | Should -Match 'v and 1'
    }

    It 'throws when the impact matrix has no version rows' {
        { New-AdoImpactComment -ImpactMatrix @() -VersionValue 'x' -TargetAdoItemId 1 } | Should -Throw
    }
}
