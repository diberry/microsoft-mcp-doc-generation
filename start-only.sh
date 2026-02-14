#!/bin/bash
# Start-only: generate a single tool family end-to-end (Steps 1–5).
#
# Usage:
#   ./start-only.sh <tool-family>
#   ./start-only.sh advisor
#
# What it does:
#   1) Clears ./generated
#   2) Generates MCP CLI metadata via test-npm-azure-mcp
#   3) Runs docs-generation/generate-tool-family.sh with Steps 1,2,3,4,5
#   4) Produces ./generated/tool-family/<tool-family>.md
#
# Prerequisites:
#   - Node.js + npm (for MCP CLI metadata)
#   - PowerShell (pwsh)
#   - .NET SDK (for generator projects)
#   - Azure OpenAI env vars for AI steps (if required by Step 3/4)
#
# Notes:
#   - This script is destructive: it removes ./generated
#   - Run from repo root or any location; paths are resolved by this script

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if [[ $# -lt 1 ]]; then
	echo "Usage: $0 <tool-family> [steps]"
	echo "Example: $0 advisor"
	echo "Example: $0 advisor 1        (run only step 1)"
	exit 1
fi

TOOL_FAMILY="$1"
STEPS="${2:-1,2,3,4,5}"

# Clean up last run
rm -rf "$ROOT_DIR/generated/"

# Create output directories
mkdir -p "$ROOT_DIR/generated/"
mkdir -p "$ROOT_DIR/generated/cli"

# # Parse command line arguments
# VALIDATE_PROMPTS=""
# while [[ $# -gt 0 ]]; do
#     case $1 in
#         --validate)
#             VALIDATE_PROMPTS="--validate-prompts"
#             echo "✓ Example prompt validation enabled (will run after content generation)"
#             shift
#             ;;
#         *)
#             echo "Unknown option: $1"
#             echo "Usage: $0 [--validate]"
#             exit 1
#             ;;
#     esac
# done

# Generate tool metadata from MCP CLI
cd "$ROOT_DIR/test-npm-azure-mcp"
npm install
echo "Generating CLI tool metadata..."
npm run --silent get:version > "$ROOT_DIR/generated/cli/cli-version.json"
npm run --silent get:tools-json > "$ROOT_DIR/generated/cli/cli-output.json"
npm run --silent get:tools-namespace > "$ROOT_DIR/generated/cli/cli-namespace.json"
echo "OK: Generated CLI tool metadata"
cd "$ROOT_DIR"

# Step 0: Validate brand mappings before generation
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
cd "$ROOT_DIR"

mkdir -p "$ROOT_DIR/generated/common-general"
mkdir -p "$ROOT_DIR/generated/tools"
mkdir -p "$ROOT_DIR/generated/example-prompts"
mkdir -p "$ROOT_DIR/generated/annotations"
mkdir -p "$ROOT_DIR/generated/logs"

echo "Running tool family pipeline for: $TOOL_FAMILY (steps: $STEPS)"
cd "$ROOT_DIR/docs-generation"
./generate-tool-family.sh "$TOOL_FAMILY" "$STEPS"

echo "OK: Tool family file generated at: $ROOT_DIR/generated/tool-family/$TOOL_FAMILY.md"
