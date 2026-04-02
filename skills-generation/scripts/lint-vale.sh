#!/bin/bash
# lint-vale.sh — Run Vale prose linter on generated skill pages
# Usage: ./skills-generation/scripts/lint-vale.sh [directory]
# Exit code: 0 if all pass, 1 if issues found

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SKILLS_DIR="$SCRIPT_DIR/.."
VALE_EXE="${VALE_EXE:-vale}"
TARGET_DIR="${1:-./generated-skills/}"

if ! command -v "$VALE_EXE" &> /dev/null; then
    # Try local binary
    if [ -f "$SKILLS_DIR/tools/vale" ]; then
        VALE_EXE="$SKILLS_DIR/tools/vale"
    elif [ -f "$SKILLS_DIR/tools/vale.exe" ]; then
        VALE_EXE="$SKILLS_DIR/tools/vale.exe"
    else
        echo "❌ Vale not found. Install: https://vale.sh/docs/install/"
        exit 1
    fi
fi

echo "Running Vale lint on $TARGET_DIR..."
"$VALE_EXE" --config "$SKILLS_DIR/.vale.ini" "$TARGET_DIR"
EXIT_CODE=$?

if [ $EXIT_CODE -eq 0 ]; then
    echo "✅ Vale: all checks passed"
else
    echo "⚠️ Vale: issues found (exit code $EXIT_CODE)"
fi

exit $EXIT_CODE
