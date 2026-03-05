#!/usr/bin/env bash
# generate-skill-list.sh - Generate Azure Agent Skills catalog page
#
# Usage:
#   ./docs-generation/scripts/generate-skill-list.sh [output-dir]
#
# Arguments:
#   output-dir  Path to the generated output directory (default: ./generated)
#
# This generates a single skill-list.md page listing all 187+ Agent Skills
# with name, related products, description, and GitHub link.
# It should run ONCE (not per-namespace) after sync-agent-skills.sh.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
DOCS_GEN_DIR="$REPO_ROOT/docs-generation"
SKILLS_SOURCE="$DOCS_GEN_DIR/skills-source"
OUTPUT_DIR="${1:-$REPO_ROOT/generated}"

echo "Generating Azure Agent Skills catalog..."

# Verify CATALOG.md exists
if [ ! -f "$SKILLS_SOURCE/CATALOG.md" ]; then
    echo "⚠ CATALOG.md not found at $SKILLS_SOURCE — skipping skill list generation"
    echo "  Run sync-agent-skills.sh first to download catalog files"
    exit 0
fi

echo "  Found CATALOG.md in $SKILLS_SOURCE"

# Run the SkillList generator
dotnet run --project "$DOCS_GEN_DIR/SkillList" --configuration Release --no-build -- \
    --output-path "$OUTPUT_DIR" \
    --skills-source "$SKILLS_SOURCE"

EXIT_CODE=$?
if [ $EXIT_CODE -ne 0 ]; then
    echo "⚠ Skill list generation failed (exit code: $EXIT_CODE)"
    exit $EXIT_CODE
fi

echo "✓ Skill list catalog generated"
