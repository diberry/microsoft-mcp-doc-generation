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

# Determine mode
if [ "${1:-}" != "" ] && [[ ! "$1" =~ ^-- ]]; then
    SKILL_NAME="$1"
    shift
    echo "[run]   Generating skill: $SKILL_NAME"
    dotnet run --project "$CLI_PROJECT" --configuration Release --no-build -- generate-skill "$SKILL_NAME" "${SOURCE_ARGS[@]}" "$@"
else
    echo "[run]   Generating all skills..."
    dotnet run --project "$CLI_PROJECT" --configuration Release --no-build -- generate-skills --all "${SOURCE_ARGS[@]}" "$@"
fi
