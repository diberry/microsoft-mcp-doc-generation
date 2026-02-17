#!/usr/bin/env bash
# Pre-test script for CSharpGenerator.Tests
# - Generates live CLI output from azmcp tools list
# - Copies latest config files from docs-generation/data/
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TEST_DATA_DIR="$SCRIPT_DIR/TestData"
DATA_DIR="$SCRIPT_DIR/../data"
NPM_MCP_DIR="$SCRIPT_DIR/../../test-npm-azure-mcp"

mkdir -p "$TEST_DATA_DIR"

# --- 1. Generate live CLI output ---
echo "Generating live CLI output from azmcp tools list..."
if command -v azmcp &>/dev/null; then
    azmcp tools list > "$TEST_DATA_DIR/cli-output-live.json" 2>/dev/null
    echo "  -> Saved cli-output-live.json ($(wc -l < "$TEST_DATA_DIR/cli-output-live.json") lines)"
elif [[ -f "$NPM_MCP_DIR/package.json" ]]; then
    (cd "$NPM_MCP_DIR" && npm run --silent get:tools-json > "$TEST_DATA_DIR/cli-output-live.json" 2>/dev/null)
    echo "  -> Saved cli-output-live.json via npm ($(wc -l < "$TEST_DATA_DIR/cli-output-live.json") lines)"
else
    echo "  -> azmcp not found and test-npm-azure-mcp not available, skipping live CLI output"
fi

# --- 2. Copy latest config files from data/ ---
CONFIG_FILES=("config.json" "nl-parameters.json" "static-text-replacement.json")
echo "Copying config files from data/..."
for file in "${CONFIG_FILES[@]}"; do
    if [[ -f "$DATA_DIR/$file" ]]; then
        cp "$DATA_DIR/$file" "$TEST_DATA_DIR/$file"
        echo "  -> Copied $file"
    else
        echo "  -> WARNING: $DATA_DIR/$file not found"
    fi
done

echo "Pretest setup complete."
