#!/bin/bash
# lint-vale.sh — Run Vale prose linter on generated mcp-tools output
# Usage: ./mcp-tools/scripts/lint-vale.sh [directory]
# Exit code: 0 if all pass, non-zero if issues found

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DOCS_GEN_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
REPO_ROOT="$(cd "$DOCS_GEN_DIR/.." && pwd)"
VALE_EXE="${VALE_EXE:-vale}"

# Find Vale binary
if ! command -v "$VALE_EXE" &> /dev/null; then
    if [ -f "$DOCS_GEN_DIR/tools/vale" ]; then
        VALE_EXE="$DOCS_GEN_DIR/tools/vale"
    elif [ -f "$DOCS_GEN_DIR/tools/vale.exe" ]; then
        VALE_EXE="$DOCS_GEN_DIR/tools/vale.exe"
    else
        echo "❌ Vale not found. Install: https://vale.sh/docs/install/"
        exit 1
    fi
fi

VALE_CONFIG="$DOCS_GEN_DIR/.vale.ini"
OVERALL_EXIT=0

if [ $# -gt 0 ]; then
    TARGETS=("$@")
else
    TARGETS=()
    if [ -d "$REPO_ROOT/generated/multi-page" ]; then
        TARGETS+=("$REPO_ROOT/generated/multi-page")
    fi
    for dir in "$REPO_ROOT"/generated-*/; do
        [ -d "$dir" ] && TARGETS+=("$dir")
    done
    if [ ${#TARGETS[@]} -eq 0 ]; then
        echo "⚠️ No generated output directories found. Run the pipeline first."
        exit 0
    fi
fi

for target in "${TARGETS[@]}"; do
    echo "Running Vale lint on $target..."
    "$VALE_EXE" --config "$VALE_CONFIG" "$target" || {
        EXIT_CODE=$?
        if [ $EXIT_CODE -gt $OVERALL_EXIT ]; then
            OVERALL_EXIT=$EXIT_CODE
        fi
    }
done

if [ $OVERALL_EXIT -eq 0 ]; then
    echo "✅ Vale: all checks passed"
else
    echo "⚠️ Vale: issues found (exit code $OVERALL_EXIT)"
fi

exit $OVERALL_EXIT
