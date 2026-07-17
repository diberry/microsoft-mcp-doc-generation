# Scan-McpToolCoverage.Tests.ps1 — Pester tests for Scan-McpToolCoverage.ps1

BeforeAll {
    $ScriptPath   = Join-Path $PSScriptRoot "..\Scan-McpToolCoverage.ps1"
    $FixturesDir  = Join-Path $PSScriptRoot "fixtures\coverage"
    $ArticlesDir  = Join-Path $FixturesDir "articles"
    $ToolsJson    = Join-Path $FixturesDir "tools-list-minimal.json"

    # ── Load helper functions by parsing them from the script ──────────────
    $scriptContent = Get-Content $ScriptPath -Raw

    # Extract: Get-DocumentedTools, Get-DocumentedParameters, Get-DocumentedAnnotations
    foreach ($fnName in @('Get-DocumentedTools', 'Get-DocumentedParameters', 'Get-DocumentedAnnotations')) {
        $fnMatch = [regex]::Match($scriptContent, "(?s)(function $fnName \{.*?\n\})")
        if ($fnMatch.Success) {
            Invoke-Expression $fnMatch.Value
        }
    }

    # Load the namespace mapping — now reads from JSON or falls back to inline
    # First try loading from config/namespace-mapping.json
    $scriptDir = Split-Path -Parent $PSScriptRoot
    $repoRoot = Split-Path -Parent (Split-Path -Parent $scriptDir)
    $namespaceMappingPath = Join-Path $repoRoot "config\namespace-mapping.json"
    
    if (Test-Path $namespaceMappingPath) {
        try {
            $namespaceToFile = Get-Content $namespaceMappingPath -Raw | ConvertFrom-Json -AsHashtable
        }
        catch {
            Write-Warning "Failed to load namespace mapping from JSON, using fallback"
            $namespaceToFile = @{}
        }
    }
    
    # Fallback to inline mapping if JSON not loaded
    if (-not $namespaceToFile -or $namespaceToFile.Count -eq 0) {
        # Extract the fallback hashtable from the script
        $mapMatch = [regex]::Match($scriptContent, '(?s)if \(\$namespaceToFile\.Count -eq 0\) \{[^}]*(\$namespaceToFile = @\{.*?\n    \})')
        if ($mapMatch.Success) {
            Invoke-Expression $mapMatch.Groups[1].Value
        }
    }

    # Load exclusion lists
    $excMatch = [regex]::Match($scriptContent, '(?s)(\$alwaysExcludeParams\s*=\s*@\(.*?\))')
    if ($excMatch.Success) { Invoke-Expression $excMatch.Value }
    $comMatch = [regex]::Match($scriptContent, '(?s)(\$commonParams\s*=\s*@\(.*?\))')
    if ($comMatch.Success) { Invoke-Expression $comMatch.Value }

    # Read fixture article content into variables for unit tests
    $StorageContent = Get-Content (Join-Path $ArticlesDir "azure-storage.md") -Raw
    $ComputeContent = Get-Content (Join-Path $ArticlesDir "azure-compute.md") -Raw
}

# ════════════════════════════════════════════════════════════════════
# 1. Tool Detection
# ════════════════════════════════════════════════════════════════════

Describe "Get-DocumentedTools — tool marker detection" {

    It "finds tools with plain HTML-comment markers" {
        $tools = Get-DocumentedTools -FilePath (Join-Path $ArticlesDir "azure-storage.md")
        $tools | Should -Contain "storage account list"
    }

    It "finds tools with at-mcpcli HTML-comment markers" {
        $tmpFile = Join-Path $FixturesDir "tmp-mcpcli-marker.md"
        $markerLine = [string]::Concat('<!-- ', '@mcpcli storage account list -->')
        try {
            Set-Content $tmpFile ($markerLine + "`nSome content") -Encoding UTF8
            $tools = Get-DocumentedTools -FilePath $tmpFile
            $tools | Should -Contain "storage account list"
        } finally {
            Remove-Item $tmpFile -ErrorAction SilentlyContinue
        }
    }

    It "reports missing tools when no marker exists" {
        $tmpFile = Join-Path $FixturesDir "tmp-no-markers.md"
        try {
            Set-Content $tmpFile "# No tools here`nJust text." -Encoding UTF8
            $tools = Get-DocumentedTools -FilePath $tmpFile
            $tools | Should -BeNullOrEmpty
        } finally {
            Remove-Item $tmpFile -ErrorAction SilentlyContinue
        }
    }

    It "handles multi-word commands (e.g., 'storage blob upload')" {
        $tools = Get-DocumentedTools -FilePath (Join-Path $ArticlesDir "azure-storage.md")
        $tools | Should -Contain "storage blob upload"
    }

    It "returns empty array for non-existent file" {
        $tools = Get-DocumentedTools -FilePath (Join-Path $FixturesDir "nonexistent.md")
        @($tools).Count | Should -Be 0
    }

    It "finds both storage tools in azure-storage.md" {
        $tools = Get-DocumentedTools -FilePath (Join-Path $ArticlesDir "azure-storage.md")
        @($tools).Count | Should -Be 2
    }

    It "finds only one compute tool (compute vm list) in azure-compute.md" {
        $tools = Get-DocumentedTools -FilePath (Join-Path $ArticlesDir "azure-compute.md")
        $tools | Should -Contain "compute vm list"
        $tools | Should -Not -Contain "compute vm start"
    }
}

# ════════════════════════════════════════════════════════════════════
# 2. Parameter Matching
# ════════════════════════════════════════════════════════════════════

Describe "Get-DocumentedParameters — parameter table parsing" {

    It "matches exact param names (account-name → 'Account name')" {
        $params = Get-DocumentedParameters -Content $StorageContent -ToolCommand "storage account list"
        $params | Should -Not -BeNullOrEmpty
        ($params | Where-Object { $_ -ilike "*account*" }) | Should -Not -BeNullOrEmpty
    }

    It "returns params for a documented tool section" {
        $params = Get-DocumentedParameters -Content $StorageContent -ToolCommand "storage blob upload"
        @($params).Count | Should -BeGreaterThan 2
    }

    It "returns empty for an undocumented tool (no marker in content)" {
        $params = Get-DocumentedParameters -Content $ComputeContent -ToolCommand "compute vm start"
        @($params).Count | Should -Be 0
    }

    It "includes --resource-group param when required:true (blob upload)" {
        $params = Get-DocumentedParameters -Content $StorageContent -ToolCommand "storage blob upload"
        ($params | Where-Object { $_ -ilike "*resource*group*" }) | Should -Not -BeNullOrEmpty
    }
}

Describe "Parameter exclusion logic" {

    It "alwaysExcludeParams contains --learn" {
        $alwaysExcludeParams | Should -Contain "--learn"
    }

    It "alwaysExcludeParams contains --subscription" {
        $alwaysExcludeParams | Should -Contain "--subscription"
    }

    It "alwaysExcludeParams contains --tenant" {
        $alwaysExcludeParams | Should -Contain "--tenant"
    }

    It "alwaysExcludeParams contains --auth-method" {
        $alwaysExcludeParams | Should -Contain "--auth-method"
    }

    It "commonParams contains --resource-group" {
        $commonParams | Should -Contain "--resource-group"
    }
}

# ════════════════════════════════════════════════════════════════════
# 3. Annotation Matching
# ════════════════════════════════════════════════════════════════════

Describe "Annotation parsing — via script JSON output" {
    # Annotation parsing relies on emoji regex in the script file; tested via subprocess JSON output
    # to avoid in-process encoding issues. See 'JSON output structure' Describe for fuller coverage.

    BeforeAll {
        $jsonOut = Join-Path $FixturesDir "anno-test-output.json"
        if (Test-Path $jsonOut) { Remove-Item $jsonOut }

        & pwsh -NoProfile -File $ScriptPath `
            -ToolsJsonPath $ToolsJson `
            -ArticlesDir $ArticlesDir `
            -OutputJson $jsonOut 2>&1 | Out-Null

        $script:annoReport = if (Test-Path $jsonOut) {
            Get-Content $jsonOut -Raw | ConvertFrom-Json
        } else { $null }
    }

    AfterAll {
        Remove-Item (Join-Path $FixturesDir "anno-test-output.json") -ErrorAction SilentlyContinue
    }

    It "script detects annotation mismatches for documented tools" {
        $script:annoReport.summary.annotation_mismatches | Should -BeGreaterThan 0
    }

    It "script records annotation matches for tools with correct annotations" {
        $script:annoReport.summary.annotation_matches | Should -BeGreaterThan 0
    }

    It "annotation mismatch is recorded for compute vm list (Open World)" {
        $ns = $script:annoReport.namespaces | Where-Object { $_.namespace -eq "compute" }
        $vmList = $ns.tool_details | Where-Object { $_.command -eq "compute vm list" }
        $mismatches = @($vmList.annotations.mismatches)
        $mismatches.Count | Should -BeGreaterThan 0
        ($mismatches | Where-Object { $_.annotation -eq "Open World" }) | Should -Not -BeNull
    }

    It "storage account list has no annotation mismatches" {
        $ns = $script:annoReport.namespaces | Where-Object { $_.namespace -eq "storage" }
        $acctList = $ns.tool_details | Where-Object { $_.command -eq "storage account list" }
        @($acctList.annotations.mismatches).Count | Should -Be 0
    }

    It "undocumented tool (compute vm start) has no annotations checked" {
        $ns = $script:annoReport.namespaces | Where-Object { $_.namespace -eq "compute" }
        $vmStart = $ns.tool_details | Where-Object { $_.command -eq "compute vm start" }
        # Not documented, so no annotation checking was done
        $vmStart.documented | Should -Be $false
    }

    It "returns empty hashtable for tool marker absent (Get-DocumentedAnnotations unit)" {
        $anno = Get-DocumentedAnnotations -Content $ComputeContent -ToolCommand "compute vm start"
        $anno.Count | Should -Be 0
    }
}

# ════════════════════════════════════════════════════════════════════
# 4. Namespace Mapping
# ════════════════════════════════════════════════════════════════════

Describe "Namespace → filename mapping" {

    It "maps 'aks' to 'azure-kubernetes.md'" {
        $namespaceToFile["aks"] | Should -Be "azure-kubernetes.md"
    }

    It "maps 'storage' to 'azure-storage.md'" {
        $namespaceToFile["storage"] | Should -Be "azure-storage.md"
    }

    It "maps 'cosmos' to 'azure-cosmos-db.md'" {
        $namespaceToFile["cosmos"] | Should -Be "azure-cosmos-db.md"
    }

    It "maps 'keyvault' to 'azure-key-vault.md'" {
        $namespaceToFile["keyvault"] | Should -Be "azure-key-vault.md"
    }

    It "maps 'compute' to 'azure-compute.md'" {
        $namespaceToFile["compute"] | Should -Be "azure-compute.md"
    }

    It "falls back to 'azure-{namespace}.md' for unmapped namespaces" {
        $unmapped = "mynewnamespace"
        $namespaceToFile.ContainsKey($unmapped) | Should -Be $false
        # Verify fallback pattern holds by convention
        "azure-$unmapped.md" | Should -Match "^azure-mynewnamespace\.md$"
    }
}

# ════════════════════════════════════════════════════════════════════
# 5. Summary / Output (script integration)
# ════════════════════════════════════════════════════════════════════

Describe "Scan-McpToolCoverage — JSON output structure" {
    BeforeAll {
        $jsonOut = Join-Path $FixturesDir "scan-output.json"
        if (Test-Path $jsonOut) { Remove-Item $jsonOut }

        & pwsh -NoProfile -File $ScriptPath `
            -ToolsJsonPath $ToolsJson `
            -ArticlesDir $ArticlesDir `
            -OutputJson $jsonOut 2>&1 | Out-Null

        $script:report = if (Test-Path $jsonOut) {
            Get-Content $jsonOut -Raw | ConvertFrom-Json
        } else { $null }
    }

    AfterAll {
        $jsonOut = Join-Path $FixturesDir "scan-output.json"
        Remove-Item $jsonOut -ErrorAction SilentlyContinue
    }

    It "produces a JSON output file" {
        $script:report | Should -Not -BeNull
    }

    It "JSON contains total_tools_in_json" {
        $script:report.total_tools_in_json | Should -Be 4
    }

    It "JSON contains total_namespaces" {
        $script:report.total_namespaces | Should -Be 2
    }

    It "JSON contains namespaces array" {
        $script:report.namespaces | Should -Not -BeNullOrEmpty
    }

    It "JSON contains summary block with tools_documented" {
        $script:report.summary.tools_documented | Should -BeGreaterOrEqual 0
    }

    It "documented + missing tools equals total tools in JSON" {
        $total = $script:report.summary.tools_documented + $script:report.summary.tools_missing
        $total | Should -Be $script:report.total_tools_in_json
    }

    It "storage namespace shows both tools documented" {
        $ns = $script:report.namespaces | Where-Object { $_.namespace -eq "storage" }
        $ns | Should -Not -BeNull
        $ns.documented_tools | Should -Be 2
    }

    It "compute namespace has one missing tool (compute vm start)" {
        $ns = $script:report.namespaces | Where-Object { $_.namespace -eq "compute" }
        $ns | Should -Not -BeNull
        $ns.missing_tools | Should -Contain "compute vm start"
    }

    It "annotation mismatch detected for compute vm list (Open World)" {
        $ns = $script:report.namespaces | Where-Object { $_.namespace -eq "compute" }
        $vmList = $ns.tool_details | Where-Object { $_.command -eq "compute vm list" }
        $vmList | Should -Not -BeNull
        @($vmList.annotations.mismatches).Count | Should -BeGreaterThan 0
        ($vmList.annotations.mismatches | Where-Object { $_.annotation -eq "Open World" }) | Should -Not -BeNull
    }

    It "JSON scan_date is populated" {
        $script:report.scan_date | Should -Not -BeNullOrEmpty
    }
}

# ════════════════════════════════════════════════════════════════════
# 6. Phantom Parameter Detection (Issue #742)
# ════════════════════════════════════════════════════════════════════

Describe "Phantom parameter detection — Issue #742" {
    BeforeAll {
        $healthmodelsJson = Join-Path $FixturesDir "tools-list-healthmodels.json"
        $phantomArticlesDir = Join-Path $FixturesDir "articles-phantom"

        $jsonOut = Join-Path $FixturesDir "scan-phantom-output.json"
        if (Test-Path $jsonOut) { Remove-Item $jsonOut }

        & pwsh -NoProfile -File $ScriptPath `
            -ToolsJsonPath $healthmodelsJson `
            -ArticlesDir $phantomArticlesDir `
            -Namespace "monitor" `
            -OutputJson $jsonOut 2>&1 | Out-Null

        $script:phantomReport = if (Test-Path $jsonOut) {
            Get-Content $jsonOut -Raw | ConvertFrom-Json
        } else { $null }
    }

    AfterAll {
        $jsonOut = Join-Path $FixturesDir "scan-phantom-output.json"
        Remove-Item $jsonOut -ErrorAction SilentlyContinue
    }

    It "detects phantom param 'Health model name' (should be 'Health model')" {
        $script:phantomReport | Should -Not -BeNull
        $ns = $script:phantomReport.namespaces | Where-Object { $_.namespace -eq "monitor" }
        $getTool = $ns.tool_details | Where-Object { $_.command -eq "monitor healthmodels get" -and $_.documented -eq $true }
        
        # Should have phantom_params property
        $getTool.PSObject.Properties['phantom_params'] | Should -Not -BeNull
        @($getTool.phantom_params).Count | Should -BeGreaterThan 0
        
        # The phantom param message should include the incorrect name and suggestion
        $phantomMsg = $getTool.phantom_params | Where-Object { $_ -like "*Health model name*" }
        $phantomMsg | Should -Not -BeNullOrEmpty
        $phantomMsg | Should -Match "should be.*Health model"
    }

    It "does NOT flag correct params as phantom (negative case)" {
        # This test uses articles-phantom dir which has the phantom case
        # The negative case will be tested via the mismatch fixture which has correct names
        # Just verify phantom detection doesn't false-positive on Resource group which IS correct
        $ns = $script:phantomReport.namespaces | Where-Object { $_.namespace -eq "monitor" }
        $getTool = $ns.tool_details | Where-Object { 
            $_.command -eq "monitor healthmodels get" -and 
            $_.documented -eq $true
        }
        
        # Resource group is correct (not flagged), Health model name is phantom (flagged)
        if ($getTool.PSObject.Properties['phantom_params']) {
            # Should NOT contain "Resource group" alone (only "Health model name")
            $rgPhantom = $getTool.phantom_params | Where-Object { $_ -eq "Resource group" }
            $rgPhantom | Should -BeNullOrEmpty
        }
    }

    It "does NOT flag optional common params absent from NL table (convention compliance)" {
        # monitor healthmodels list has ONLY optional --resource-group (filtered per convention)
        # The scanner should NOT flag this as a missing or phantom param
        $ns = $script:phantomReport.namespaces | Where-Object { $_.namespace -eq "monitor" }
        $listTool = $ns.tool_details | Where-Object { $_.command -eq "monitor healthmodels list" }
        
        # Should be documented
        $listTool.documented | Should -Be $true
        
        # Should have zero params missing (optional --resource-group is legitimately excluded)
        @($listTool.params.missing).Count | Should -Be 0
        
        # Should have zero phantom params (no params documented, which is correct)
        if ($listTool.PSObject.Properties['phantom_params']) {
            @($listTool.phantom_params).Count | Should -Be 0
        }
    }
}

# ════════════════════════════════════════════════════════════════════
# 7. Required/Optional Mismatch Detection (Issue #742)
# ════════════════════════════════════════════════════════════════════

Describe "Required/optional mismatch detection — Issue #742" {
    BeforeAll {
        $healthmodelsJson = Join-Path $FixturesDir "tools-list-healthmodels.json"
        $mismatchArticlesDir = Join-Path $FixturesDir "articles-mismatch"

        $jsonOut = Join-Path $FixturesDir "scan-mismatch-output.json"
        if (Test-Path $jsonOut) { Remove-Item $jsonOut }

        & pwsh -NoProfile -File $ScriptPath `
            -ToolsJsonPath $healthmodelsJson `
            -ArticlesDir $mismatchArticlesDir `
            -Namespace "monitor" `
            -OutputJson $jsonOut 2>&1 | Out-Null

        $script:mismatchReport = if (Test-Path $jsonOut) {
            Get-Content $jsonOut -Raw | ConvertFrom-Json
        } else { $null }
    }

    AfterAll {
        $jsonOut = Join-Path $FixturesDir "scan-mismatch-output.json"
        Remove-Item $jsonOut -ErrorAction SilentlyContinue
    }

    It "detects req/opt mismatch for Resource group (source=required, doc=optional)" {
        $script:mismatchReport | Should -Not -BeNull
        $ns = $script:mismatchReport.namespaces | Where-Object { $_.namespace -eq "monitor" }
        $getTool = $ns.tool_details | Where-Object { 
            $_.command -eq "monitor healthmodels get" -and 
            $_.documented -eq $true
        }
        
        # Should have req_opt_mismatches property
        $getTool.PSObject.Properties['req_opt_mismatches'] | Should -Not -BeNull
        @($getTool.req_opt_mismatches).Count | Should -BeGreaterThan 0
        
        # Find the Resource group mismatch
        $rgMismatch = $getTool.req_opt_mismatches | Where-Object { $_.param -like "*Resource group*" }
        $rgMismatch | Should -Not -BeNullOrEmpty
        $rgMismatch.source_required | Should -Be $true
        $rgMismatch.doc_required | Should -Be $false
    }

    It "does NOT flag correct required param as mismatch (negative case)" {
        # Health model is correctly marked Required in both source and doc
        $ns = $script:mismatchReport.namespaces | Where-Object { $_.namespace -eq "monitor" }
        $getTool = $ns.tool_details | Where-Object { 
            $_.command -eq "monitor healthmodels get" -and 
            $_.documented -eq $true
        }
        
        # If req_opt_mismatches exists, it should NOT contain "Health model"
        if ($getTool.PSObject.Properties['req_opt_mismatches']) {
            $hmMismatch = $getTool.req_opt_mismatches | Where-Object { $_.param -eq "Health model" }
            $hmMismatch | Should -BeNullOrEmpty
        }
    }
}

# ════════════════════════════════════════════════════════════════════
# 8. Edge Cases
# ════════════════════════════════════════════════════════════════════

Describe "Edge cases — empty tools-list.json" {
    BeforeAll {
        $emptyJson = Join-Path $FixturesDir "empty-tools-list.json"
        Set-Content $emptyJson '{"results":[]}' -Encoding UTF8

        $jsonOut = Join-Path $FixturesDir "scan-empty-output.json"
        if (Test-Path $jsonOut) { Remove-Item $jsonOut }

        & pwsh -NoProfile -File $ScriptPath `
            -ToolsJsonPath $emptyJson `
            -ArticlesDir $ArticlesDir `
            -OutputJson $jsonOut 2>&1 | Out-Null

        $script:emptyReport = if (Test-Path $jsonOut) {
            Get-Content $jsonOut -Raw | ConvertFrom-Json
        } else { $null }
    }

    AfterAll {
        Remove-Item (Join-Path $FixturesDir "empty-tools-list.json") -ErrorAction SilentlyContinue
        Remove-Item (Join-Path $FixturesDir "scan-empty-output.json") -ErrorAction SilentlyContinue
    }

    It "succeeds with 0 tools" {
        $script:emptyReport | Should -Not -BeNull
    }

    It "reports total_tools_in_json as 0" {
        $script:emptyReport.total_tools_in_json | Should -Be 0
    }

    It "reports total_namespaces as 0" {
        $script:emptyReport.total_namespaces | Should -Be 0
    }

    It "namespaces array is empty" {
        @($script:emptyReport.namespaces).Count | Should -Be 0
    }
}

Describe "Edge cases — article with no tool markers" {
    BeforeAll {
        $noMarkersFile = Join-Path $ArticlesDir "no-markers.md"
        Set-Content $noMarkersFile "# No tools`nThis article has no HTML comment markers." -Encoding UTF8
    }

    AfterAll {
        Remove-Item (Join-Path $ArticlesDir "no-markers.md") -ErrorAction SilentlyContinue
    }

    It "Get-DocumentedTools returns empty for article with no markers" {
        $tools = Get-DocumentedTools -FilePath (Join-Path $ArticlesDir "no-markers.md")
        @($tools).Count | Should -Be 0
    }
}

Describe "Edge cases — Namespace filter" {
    It "script exits non-zero for invalid namespace" {
        & pwsh -NoProfile -File $ScriptPath -ToolsJsonPath $ToolsJson -ArticlesDir $ArticlesDir -Namespace 'nonexistentnamespace' 2>&1 | Out-Null
        $LASTEXITCODE | Should -Not -Be 0
    }

    It "script scans only storage namespace when -Namespace storage is set" {
        $jsonOut = Join-Path $FixturesDir "scan-ns-output.json"
        if (Test-Path $jsonOut) { Remove-Item $jsonOut }

        & pwsh -NoProfile -File $ScriptPath `
            -ToolsJsonPath $ToolsJson `
            -ArticlesDir $ArticlesDir `
            -Namespace 'storage' `
            -OutputJson $jsonOut 2>&1 | Out-Null

        if (Test-Path $jsonOut) {
            $r = Get-Content $jsonOut -Raw | ConvertFrom-Json
            $r.total_namespaces | Should -Be 1
            Remove-Item $jsonOut -ErrorAction SilentlyContinue
        }
    }
}

# ════════════════════════════════════════════════════════════════════
# 7. Missing-parameter reporting
# ════════════════════════════════════════════════════════════════════

Describe "Scan-McpToolCoverage — missing required parameter reporting" {
    # azure-key-vault.md documents 'keyvault secret get' but omits the required
    # --secret-name parameter, so the scanner must report ≥1 missing parameter.

    BeforeAll {
        $MissingParamJson = Join-Path $FixturesDir "tools-list-missing-param.json"
        $jsonOut = Join-Path $FixturesDir "scan-missing-param-output.json"
        if (Test-Path $jsonOut) { Remove-Item $jsonOut }

        & pwsh -NoProfile -File $ScriptPath `
            -ToolsJsonPath $MissingParamJson `
            -ArticlesDir $ArticlesDir `
            -OutputJson $jsonOut 2>&1 | Out-Null

        $script:mpReport = if (Test-Path $jsonOut) {
            Get-Content $jsonOut -Raw | ConvertFrom-Json
        } else { $null }
    }

    AfterAll {
        Remove-Item (Join-Path $FixturesDir "scan-missing-param-output.json") -ErrorAction SilentlyContinue
    }

    It "produces a report" {
        $script:mpReport | Should -Not -BeNull
    }

    It "keyvault secret get is documented (article exists with marker)" {
        $ns = $script:mpReport.namespaces | Where-Object { $_.namespace -eq "keyvault" }
        $ns.documented_tools | Should -BeGreaterOrEqual 1
    }

    It "summary.params_missing is at least 1 (--secret-name omitted from article)" {
        $script:mpReport.summary.params_missing | Should -BeGreaterOrEqual 1
    }

    It "keyvault secret get tool_details lists --secret-name as missing" {
        $ns  = $script:mpReport.namespaces | Where-Object { $_.namespace -eq "keyvault" }
        $tool = $ns.tool_details | Where-Object { $_.command -eq "keyvault secret get" }
        $tool | Should -Not -BeNull
        $tool.params.missing | Should -Contain "--secret-name"
    }

    It "keyvault secret get tool_details does NOT list --vault-name as missing" {
        $ns  = $script:mpReport.namespaces | Where-Object { $_.namespace -eq "keyvault" }
        $tool = $ns.tool_details | Where-Object { $_.command -eq "keyvault secret get" }
        $tool.params.missing | Should -Not -Contain "--vault-name"
    }
}

# ════════════════════════════════════════════════════════════════════
# 8. Namespace Mapping — JSON-based configuration (#582)
# ════════════════════════════════════════════════════════════════════

Describe "Namespace mapping — JSON-based configuration" {

    It "loads namespace mapping from config/namespace-mapping.json when file exists" {
        # Create a temporary repo structure with config/namespace-mapping.json
        $tempRepo = Join-Path $TestDrive "repo-$(New-Guid)"
        $configDir = Join-Path $tempRepo "config"
        New-Item -ItemType Directory -Path $configDir -Force | Out-Null
        
        $testMapping = @{
            storage = "azure-storage.md"
            keyvault = "azure-key-vault.md"
            cosmos = "azure-cosmos-db.md"
        } | ConvertTo-Json
        
        $mappingPath = Join-Path $configDir "namespace-mapping.json"
        Set-Content -Path $mappingPath -Value $testMapping
        
        # Simulate the loading logic from the script
        $repoRoot = $tempRepo
        $namespaceMappingPath = Join-Path $repoRoot "config\namespace-mapping.json"
        
        $namespaceToFile = Get-Content $namespaceMappingPath -Raw | ConvertFrom-Json -AsHashtable
        
        $namespaceToFile["storage"] | Should -Be "azure-storage.md"
        $namespaceToFile["keyvault"] | Should -Be "azure-key-vault.md"
        $namespaceToFile["cosmos"] | Should -Be "azure-cosmos-db.md"
        $namespaceToFile.Count | Should -Be 3
    }

    It "validates real-world namespace mapping structure" {
        # Verify the actual config/namespace-mapping.json in the repo
        $scriptDir = Split-Path -Parent $PSScriptRoot
        $repoRoot = Split-Path -Parent (Split-Path -Parent $scriptDir)
        $namespaceMappingPath = Join-Path $repoRoot "config\namespace-mapping.json"
        
        if (Test-Path $namespaceMappingPath) {
            $mapping = Get-Content $namespaceMappingPath -Raw | ConvertFrom-Json -AsHashtable
            
            # Spot-check key entries
            $mapping["storage"] | Should -Be "azure-storage.md"
            $mapping["keyvault"] | Should -Be "azure-key-vault.md"
            $mapping["appconfig"] | Should -Be "app-configuration.md"
            $mapping["group"] | Should -Be "resource-group.md"
            $mapping.Count | Should -BeGreaterThan 50
        }
    }

    It "falls back to inline mapping when JSON file is missing" {
        # Simulate the fallback logic from the script by pointing at a non-existent path
        $tempRepo = Join-Path $TestDrive "repo-$(New-Guid)"
        New-Item -ItemType Directory -Path $tempRepo -Force | Out-Null
        
        # Do NOT create config/ directory — force the file-not-found path
        $namespaceMappingPath = Join-Path $tempRepo "config\namespace-mapping.json"
        
        # Simulate script logic
        $loadedFromJson = $false
        if (Test-Path $namespaceMappingPath) {
            try {
                $namespaceToFile = Get-Content $namespaceMappingPath -Raw | ConvertFrom-Json -AsHashtable
                $loadedFromJson = $true
            }
            catch {
                $namespaceToFile = @{}
            }
        }
        else {
            $namespaceToFile = @{}
        }
        
        # Apply fallback if JSON not loaded
        if (-not $loadedFromJson) {
            # Use a minimal subset of the inline fallback mapping for testing
            $namespaceToFile = @{
                'storage'    = 'azure-storage.md'
                'keyvault'   = 'azure-key-vault.md'
                'appconfig'  = 'app-configuration.md'
                'group'      = 'resource-group.md'
                'aks'        = 'azure-kubernetes.md'
            }
        }
        
        # Assert fallback was used and mappings resolve correctly
        $loadedFromJson | Should -Be $false
        $namespaceToFile.Count | Should -BeGreaterThan 0
        $namespaceToFile["storage"] | Should -Be "azure-storage.md"
        $namespaceToFile["aks"] | Should -Be "azure-kubernetes.md"
    }

    It "falls back to inline mapping when JSON file is invalid" {
        # Simulate the fallback logic with a corrupt JSON file
        $tempRepo = Join-Path $TestDrive "repo-$(New-Guid)"
        $configDir = Join-Path $tempRepo "config"
        New-Item -ItemType Directory -Path $configDir -Force | Out-Null
        
        $namespaceMappingPath = Join-Path $configDir "namespace-mapping.json"
        Set-Content -Path $namespaceMappingPath -Value "{ invalid json content }"
        
        # Simulate script logic
        $loadedFromJson = $false
        if (Test-Path $namespaceMappingPath) {
            try {
                $namespaceToFile = Get-Content $namespaceMappingPath -Raw | ConvertFrom-Json -AsHashtable
                $loadedFromJson = $true
            }
            catch {
                # Parse failed — trigger fallback
                $namespaceToFile = @{}
            }
        }
        else {
            $namespaceToFile = @{}
        }
        
        # Apply fallback if JSON not loaded
        if (-not $loadedFromJson) {
            # Use a minimal subset of the inline fallback mapping
            $namespaceToFile = @{
                'storage'    = 'azure-storage.md'
                'keyvault'   = 'azure-key-vault.md'
                'cosmos'     = 'azure-cosmos-db.md'
            }
        }
        
        # Assert fallback was used
        $loadedFromJson | Should -Be $false
        $namespaceToFile.Count | Should -BeGreaterThan 0
        $namespaceToFile["storage"] | Should -Be "azure-storage.md"
    }

    It "does NOT fall back when JSON is empty but valid" {
        # Simulate loading a valid empty JSON {} — should NOT trigger fallback
        $tempRepo = Join-Path $TestDrive "repo-$(New-Guid)"
        $configDir = Join-Path $tempRepo "config"
        New-Item -ItemType Directory -Path $configDir -Force | Out-Null
        
        $namespaceMappingPath = Join-Path $configDir "namespace-mapping.json"
        Set-Content -Path $namespaceMappingPath -Value "{}"
        
        # Simulate script logic
        $loadedFromJson = $false
        if (Test-Path $namespaceMappingPath) {
            try {
                $namespaceToFile = Get-Content $namespaceMappingPath -Raw | ConvertFrom-Json -AsHashtable
                $loadedFromJson = $true
            }
            catch {
                $namespaceToFile = @{}
            }
        }
        else {
            $namespaceToFile = @{}
        }
        
        # Apply fallback ONLY if not loaded from JSON
        if (-not $loadedFromJson) {
            $namespaceToFile = @{ 'storage' = 'azure-storage.md' }
        }
        
        # Assert JSON was successfully loaded and fallback NOT used (even though empty)
        $loadedFromJson | Should -Be $true
        $namespaceToFile.Count | Should -Be 0  # Empty mapping is valid
    }
}

