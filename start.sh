#!/bin/bash
# Start: Orchestrator script to generate documentation for all tool families.
#
# Usage:
#   ./start.sh [steps]
#   ./start.sh           # Run all steps for all namespaces
#   ./start.sh 1,2,3     # Run steps 1,2,3 for all namespaces
#
# What it does:
#   1) Clears ./generated
#   2) Generates MCP CLI metadata once (cli-output.json, cli-namespace.json, cli-version.json)
#   3) Runs validation once (Step 0)
#   4) Creates output directories
#   5) Iterates over all namespaces, calling start-only.sh for each
#   6) Runs verification and summary
#
# Prerequisites:
#   - Node.js + npm (for MCP CLI metadata)
#   - PowerShell (pwsh)
#   - .NET SDK (for generator projects)
#   - Azure OpenAI env vars for AI steps (if required)

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
STEPS="${1:-1,2,3,4,5}"

echo "==================================================================="
echo "Start: Documentation Generation Orchestrator"
echo "==================================================================="
echo ""

# Clean up last run
echo "Cleaning previous run..."
rm -rf "$ROOT_DIR/generated/"

# Create output directories
mkdir -p "$ROOT_DIR/generated/"
mkdir -p "$ROOT_DIR/generated/cli"

# Generate tool metadata from MCP CLI (ONCE for all namespaces)
cd "$ROOT_DIR/test-npm-azure-mcp"
echo "Installing npm dependencies..."
npm install --silent
echo "Generating CLI tool metadata..."
npm run --silent get:version > "$ROOT_DIR/generated/cli/cli-version.json"
npm run --silent get:tools-json > "$ROOT_DIR/generated/cli/cli-output.json"
npm run --silent get:tools-namespace > "$ROOT_DIR/generated/cli/cli-namespace.json"
echo "✓ Generated CLI tool metadata"
cd "$ROOT_DIR"

# Step 0: Validate brand mappings before generation (ONCE for all namespaces)
echo ""
echo "Running brand mapping validation (Step 0)..."
cd "$ROOT_DIR/docs-generation"
pwsh -Command "./0-Validate-BrandMappings.ps1 -OutputPath '$ROOT_DIR/generated'"
VALIDATION_EXIT=$?
if [ $VALIDATION_EXIT -ne 0 ]; then
    echo ""
    echo "⛔ PIPELINE HALTED: Brand mapping validation failed (exit code: $VALIDATION_EXIT)"
    echo "   Review suggestions at: $ROOT_DIR/generated/reports/brand-mapping-suggestions.json"
    echo "   Add missing mappings to: $ROOT_DIR/docs-generation/data/brand-to-server-mapping.json"
    echo "   Then re-run this script."
    exit $VALIDATION_EXIT
fi
echo "✓ Brand mapping validation passed"
cd "$ROOT_DIR"

# Create output directories
mkdir -p "$ROOT_DIR/generated/common-general"
mkdir -p "$ROOT_DIR/generated/tools"
mkdir -p "$ROOT_DIR/generated/example-prompts"
mkdir -p "$ROOT_DIR/generated/annotations"
mkdir -p "$ROOT_DIR/generated/logs"
mkdir -p "$ROOT_DIR/generated/tool-family"

# Extract namespaces from cli-namespace.json
echo ""
echo "Extracting namespaces from CLI metadata..."
NAMESPACES=$(jq -r '.results[].name' "$ROOT_DIR/generated/cli/cli-namespace.json")
NAMESPACE_COUNT=$(echo "$NAMESPACES" | wc -l)
echo "✓ Found $NAMESPACE_COUNT namespaces"

# Iterate over all namespaces
echo ""
echo "==================================================================="
echo "Processing all $NAMESPACE_COUNT namespaces (steps: $STEPS)"
echo "==================================================================="
echo ""

CURRENT=0
FAILED_NAMESPACES=()

for NAMESPACE in $NAMESPACES; do
    CURRENT=$((CURRENT + 1))
    echo "-------------------------------------------------------------------"
    echo "[$CURRENT/$NAMESPACE_COUNT] Processing namespace: $NAMESPACE"
    echo "-------------------------------------------------------------------"
    
    # Call start-only.sh for each namespace
    if ./start-only.sh "$NAMESPACE" "$STEPS"; then
        echo "✓ Successfully generated documentation for: $NAMESPACE"
    else
        echo "✗ Failed to generate documentation for: $NAMESPACE"
        FAILED_NAMESPACES+=("$NAMESPACE")
    fi
    echo ""
done

# Report results
echo ""
echo "==================================================================="
echo "Summary"
echo "==================================================================="
echo "Total namespaces: $NAMESPACE_COUNT"
echo "Successfully processed: $((NAMESPACE_COUNT - ${#FAILED_NAMESPACES[@]}))"
echo "Failed: ${#FAILED_NAMESPACES[@]}"

if [ ${#FAILED_NAMESPACES[@]} -gt 0 ]; then
    echo ""
    echo "Failed namespaces:"
    for NAMESPACE in "${FAILED_NAMESPACES[@]}"; do
        echo "  - $NAMESPACE"
    done
fi

echo ""
echo "✓ Documentation generation complete!"
echo "   Output: $ROOT_DIR/generated/tool-family/"
