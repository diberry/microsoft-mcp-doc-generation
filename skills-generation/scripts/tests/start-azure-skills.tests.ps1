# start-azure-skills.tests.ps1 — Tests for the Azure Skills generation orchestrator script
#
# Regression guard for the cwd-dependency bug: start-azure-skills.sh computes SCRIPT_DIR
# but runs `dotnet run` from the caller's working directory. The CLI's --data-path and
# --template-path options default to cwd-relative "./data/" and "./templates/...", so when
# the script is invoked from the repo root (its documented usage) the inventory file at
# skills-generation/data/skills-inventory.json is not found and "No skills found in
# inventory" is emitted — no articles are generated.
#
# The fix anchors both paths to the skills-generation directory ($SKILLS_DIR) so the script
# is cwd-independent. These tests FAIL if that anchoring is reverted.
Describe "start-azure-skills.sh" {
    BeforeAll {
        $repoRoot = Join-Path $PSScriptRoot ".." ".." ".."
        $script:ScriptPath = (Resolve-Path (Join-Path $repoRoot "start-azure-skills.sh")).Path
        $script:ScriptText = Get-Content $script:ScriptPath -Raw
    }

    It "Passes --data-path anchored to the skills-generation dir (not the cwd-relative default)" {
        # Must reference SKILLS_DIR so ./data/ is never resolved against the caller's cwd.
        $script:ScriptText | Should -Match '--data-path[^\r\n]*\$?\{?SKILLS_DIR'
    }

    It "Passes --template-path anchored to the skills-generation dir" {
        $script:ScriptText | Should -Match '--template-path[^\r\n]*\$?\{?SKILLS_DIR'
    }

    It "Anchors the data path to the actual inventory location" {
        # The anchored data path must point at a directory that really contains the inventory.
        $dataDir = Join-Path $PSScriptRoot ".." ".." "data"
        Test-Path (Join-Path $dataDir "skills-inventory.json") | Should -BeTrue
    }

    It "Both generate-skill and generate-skills invocations receive the anchored args" {
        # Whether via a shared args array or inline, every dotnet-run generation invocation
        # must carry the anchored data path. Assert the anchored token appears at least twice
        # OR a shared args array is expanded on both invocation lines.
        $sharedArrayUses = ([regex]::Matches($script:ScriptText, '\$\{DATA_ARGS\[@\]\}')).Count
        $inlineDataPath  = ([regex]::Matches($script:ScriptText, '--data-path[^\r\n]*SKILLS_DIR')).Count
        ($sharedArrayUses -ge 2 -or $inlineDataPath -ge 2) | Should -BeTrue
    }

    # --- .env-sourcing regression guard (LLM enablement) ---
    # The skills CLI selects the keyless Azure OpenAI rewriter only when FOUNDRY_ENDPOINT is
    # present in the process environment (SkillsGen.Cli/Program.cs). start-azure-skills.sh does
    # not run through start.sh's preflight, so without sourcing mcp-tools/.env the FOUNDRY_*
    # variables are absent and every [LLM] step silently falls back to the NoOp rewriter,
    # producing mechanical (non-AI-polished) output. These tests FAIL if the .env sourcing is
    # removed.
    Context ".env sourcing enables the LLM rewriter" {
        It "References the mcp-tools/.env file anchored to the script/repo dir" {
            $script:ScriptText | Should -Match 'mcp-tools/\.env'
        }

        It "Auto-exports sourced variables so the child dotnet process inherits FOUNDRY_*" {
            # `set -a` before sourcing marks all assigned vars for export; `set +a` restores.
            # Without export, `source`d vars stay shell-local and the dotnet child never sees them.
            $script:ScriptText | Should -Match '(?s)set -a.*(source|\.)\s+["'']?\$\{?[A-Za-z_]+\}?[^\r\n]*mcp-tools/\.env'
        }

        It "Guards the source with a file-existence check (no crash under set -euo pipefail when absent)" {
            # A bare `source missing.env` aborts the script under `set -e`. The sourcing must be
            # wrapped in an `if [ -f ... ]` (or equivalent) guard.
            $script:ScriptText | Should -Match '(?s)if\s+\[\s+-f\s+["'']?\$\{?[A-Za-z_]+\}?[^\r\n]*mcp-tools/\.env'
        }

        It "Sources .env before the dotnet run generation invocations" {
            $sourceIdx = $script:ScriptText.IndexOf('mcp-tools/.env')
            $firstRunIdx = $script:ScriptText.IndexOf('dotnet run --project "$CLI_PROJECT"')
            $sourceIdx | Should -BeGreaterThan -1
            $firstRunIdx | Should -BeGreaterThan -1
            $sourceIdx | Should -BeLessThan $firstRunIdx
        }
    }
}
