#!/bin/bash
# start-azure-skills.sh — Azure Skills documentation generation
# Usage:
#   ./start-azure-skills.sh                          # All 24 skills
#   ./start-azure-skills.sh azure-storage             # Single skill
#   ./start-azure-skills.sh --no-llm                  # All skills, no LLM
#   ./start-azure-skills.sh azure-storage --dry-run   # Single skill, dry run

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SKILLS_DIR="$SCRIPT_DIR/skills-generation"
CLI_PROJECT="$SKILLS_DIR/SkillsGen.Cli/SkillsGen.Cli.csproj"

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

# Determine mode
if [ "${1:-}" != "" ] && [[ ! "$1" =~ ^-- ]]; then
    SKILL_NAME="$1"
    shift
    echo "[run]   Generating skill: $SKILL_NAME"
    dotnet run --project "$CLI_PROJECT" --configuration Release --no-build -- generate-skill "$SKILL_NAME" "$@"
else
    echo "[run]   Generating all skills..."
    dotnet run --project "$CLI_PROJECT" --configuration Release --no-build -- generate-skills --all "$@"
fi
