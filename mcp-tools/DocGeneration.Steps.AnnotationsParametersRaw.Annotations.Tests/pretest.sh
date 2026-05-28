#!/usr/bin/env bash
# Pre-test script for DocGeneration.Steps.AnnotationsParametersRaw.Annotations.Tests
# - Generates live CLI output from azmcp tools list
# - Copies latest config files from mcp-tools/data/
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TEST_DATA_DIR="$SCRIPT_DIR/TestData"
DATA_DIR="$SCRIPT_DIR/../data"
MCP_METADATA_DIR="$SCRIPT_DIR/../../mcp-cli-metadata"
TRACKED_VERSION_FILE="$MCP_METADATA_DIR/tracked-version.txt"

mkdir -p "$TEST_DATA_DIR"

# --- 1. Generate live CLI output ---
echo "Generating live CLI output from azmcp tools list..."
if command -v azmcp &>/dev/null; then
    azmcp tools list > "$TEST_DATA_DIR/cli-output-live.json" 2>/dev/null
    echo "  -> Saved cli-output-live.json ($(wc -l < "$TEST_DATA_DIR/cli-output-live.json") lines)"
elif [[ -f "$TRACKED_VERSION_FILE" ]]; then
    TRACKED_VERSION="$(tr -d '[:space:]' < "$TRACKED_VERSION_FILE")"
    SNAPSHOT_PATH="$MCP_METADATA_DIR/$TRACKED_VERSION/tools-list.json"
    if [[ -f "$SNAPSHOT_PATH" ]]; then
        cp "$SNAPSHOT_PATH" "$TEST_DATA_DIR/cli-output-live.json"
        echo "  -> Saved cli-output-live.json from tracked snapshot ($TRACKED_VERSION)"
    else
        echo "  -> azmcp not found and tracked snapshot $TRACKED_VERSION is unavailable, skipping live CLI output"
    fi
else
    echo "  -> azmcp not found and tracked-version.txt is unavailable, skipping live CLI output"
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
