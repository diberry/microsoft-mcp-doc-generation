#!/bin/bash
# start-azure-skills.sh — Azure Skills documentation generation
# Usage:
#   ./start-azure-skills.sh                          # All 24 skills
#   ./start-azure-skills.sh azure-storage             # Single skill
#   ./start-azure-skills.sh --no-llm                  # All skills, no LLM
#   ./start-azure-skills.sh azure-storage --dry-run   # Single skill, dry run
#   ./start-azure-skills.sh --source github           # Force GitHub API (not recommended)

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SKILLS_DIR="$SCRIPT_DIR/skills-generation"
CLI_PROJECT="$SKILLS_DIR/SkillsGen.Cli/SkillsGen.Cli.csproj"

# Local clone of microsoft/azure-skills (avoids GitHub API rate limits)
SKILLS_SOURCE="$SCRIPT_DIR/skills-source"
SKILLS_REPO="https://github.com/microsoft/azure-skills.git"

echo "═══════════════════════════════════════════════"
echo " Azure Skills Documentation Generator"
echo "═══════════════════════════════════════════════"

# Build
echo "[build] Building skills pipeline..."
dotnet build "$SKILLS_DIR/skills-generation.slnx" --configuration Release --verbosity quiet

if [ $? -ne 0 ]; then
    echo "[build] ❌ Build failed"
    exit 1
fi
echo "[build] ✅ Build succeeded"

# --- Ensure local skills source is available ---
# Skip clone/pull if user explicitly passes --source github
SOURCE_IS_GITHUB=false
PREV_WAS_SOURCE=false
for arg in "$@"; do
    if [ "$PREV_WAS_SOURCE" = "true" ] && [ "$arg" = "github" ]; then
        SOURCE_IS_GITHUB=true
        break
    fi
    PREV_WAS_SOURCE=false
    if [ "$arg" = "--source" ]; then
        PREV_WAS_SOURCE=true
    fi
done

if [ "$SOURCE_IS_GITHUB" = "true" ]; then
    echo "[source] Using GitHub API (--source github specified)"
else
    echo "[source] Ensuring local clone of microsoft/azure-skills..."
    if [ -d "$SKILLS_SOURCE/.git" ]; then
        echo "[source] Updating existing clone..."
        git -C "$SKILLS_SOURCE" pull --quiet 2>/dev/null || echo "[source] ⚠️ git pull failed (offline?), using existing clone"
    else
        echo "[source] Cloning $SKILLS_REPO..."
        git clone --quiet "$SKILLS_REPO" "$SKILLS_SOURCE"
    fi
    echo "[source] ✅ Local skills source ready"
fi

# Build CLI args for source
SOURCE_ARGS=()
if [ "$SOURCE_IS_GITHUB" != "true" ]; then
    SOURCE_ARGS+=(--source local --source-path "$SKILLS_SOURCE/skills/" --tests-path "$SKILLS_SOURCE/tests/")
fi

# Anchor data + template paths to the skills-generation dir so the script is cwd-independent.
# The CLI defaults --data-path/--template-path to cwd-relative "./data/" and "./templates/...".
# This script runs `dotnet run` from the caller's cwd (it does not cd), so without these the
# inventory at skills-generation/data/skills-inventory.json is not found when invoked from the
# repo root — the documented usage — yielding "No skills found in inventory". Placed before
# "$@" so an explicit user --data-path/--template-path still wins (System.CommandLine last-wins).
DATA_ARGS=(--data-path "$SKILLS_DIR/data/" --template-path "$SKILLS_DIR/templates/skill-page-template.hbs")

# Load Azure OpenAI credentials so the LLM rewriter runs. The skills CLI selects the keyless
# AzureOpenAiRewriter only when FOUNDRY_ENDPOINT is present in the process environment (see
# SkillsGen.Cli/Program.cs); otherwise every [LLM] step falls back to the NoOp rewriter and the
# output is mechanical, not AI-polished. Unlike start.sh (which runs preflight), this script
# never puts FOUNDRY_* into the environment, so we source mcp-tools/.env here. Guarded with a
# file-existence check so a missing .env does not abort under `set -euo pipefail` — `--no-llm`
# and metadata-only runs still work without it. `set -a` exports every sourced variable so the
# child `dotnet run` process inherits them (keyless auth also needs `az login`/managed identity).
if [ -f "$SCRIPT_DIR/mcp-tools/.env" ]; then
    set -a
    # shellcheck disable=SC1090
    source "$SCRIPT_DIR/mcp-tools/.env"
    set +a
    echo "[env] ✅ Loaded mcp-tools/.env (LLM rewriter enabled when FOUNDRY_ENDPOINT is set)"
else
    echo "[env] ⚠️ mcp-tools/.env not found — LLM steps fall back to the no-op rewriter (mechanical output)"
fi

# Determine mode
if [ "${1:-}" != "" ] && [[ ! "$1" =~ ^-- ]]; then
    SKILL_NAME="$1"
    shift
    echo "[run]   Generating skill: $SKILL_NAME"
    dotnet run --project "$CLI_PROJECT" --configuration Release --no-build -- generate-skill "$SKILL_NAME" "${SOURCE_ARGS[@]}" "${DATA_ARGS[@]}" "$@"
else
    echo "[run]   Generating all skills..."
    dotnet run --project "$CLI_PROJECT" --configuration Release --no-build -- generate-skills --all "${SOURCE_ARGS[@]}" "${DATA_ARGS[@]}" "$@"
fi
