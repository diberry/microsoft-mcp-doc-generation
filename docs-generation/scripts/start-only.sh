#!/bin/bash
# Start-only: Worker script to generate a single tool family documentation.
#
# Usage:
#   ./start-only.sh <tool-family> [steps]
#   ./start-only.sh advisor
#   ./start-only.sh advisor 1,2,3
#
# What it does:
#   - Runs docs-generation/generate-tool-family.sh with specified steps
#   - Produces ./generated/tool-family/<tool-family>.md
#   - Uses existing CLI metadata files (does NOT regenerate them)
#
# Prerequisites:
#   - CLI metadata files must already exist in ./generated/cli/
#   - PowerShell (pwsh)
#   - .NET SDK (for generator projects)
#   - Azure OpenAI env vars for AI steps (if required by Step 3/4)
#
# Notes:
#   - This script is designed to be called by start.sh orchestrator
#   - Does NOT clean ./generated directory
#   - Does NOT regenerate CLI metadata
#   - Does NOT run validation (should be done once by orchestrator)

set -euo pipefail

# Get repository root (two levels up from this script's location)
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"

if [[ $# -lt 1 ]]; then
	echo "Usage: $0 <tool-family> [steps] [output-dir]"
	echo "Example: $0 advisor"
	echo "Example: $0 advisor 1        (run only step 1)"
	echo "Example: $0 advisor 1,2,3 /path/to/output        (custom output directory)"
	exit 1
fi

TOOL_FAMILY="$1"
STEPS="${2:-1,2,3,4,5}"
OUTPUT_DIR="${3:-$ROOT_DIR/generated}"

# Verify CLI metadata files exist
if [[ ! -f "$OUTPUT_DIR/cli/cli-version.json" ]] || \
   [[ ! -f "$OUTPUT_DIR/cli/cli-output.json" ]] || \
   [[ ! -f "$OUTPUT_DIR/cli/cli-namespace.json" ]]; then
    echo "ERROR: CLI metadata files not found in $OUTPUT_DIR/cli/"
    echo "       Please run start.sh first to generate CLI metadata."
    exit 1
fi

# Ensure output directories exist
mkdir -p "$OUTPUT_DIR/common-general"
mkdir -p "$OUTPUT_DIR/tools"
mkdir -p "$OUTPUT_DIR/example-prompts"
mkdir -p "$OUTPUT_DIR/annotations"
mkdir -p "$OUTPUT_DIR/logs"
mkdir -p "$OUTPUT_DIR/tool-family"

echo "Running tool family pipeline for: $TOOL_FAMILY (steps: $STEPS)"
echo "Output directory: $OUTPUT_DIR"
cd "$SCRIPT_DIR"
./generate-tool-family.sh "$TOOL_FAMILY" "$STEPS" "$OUTPUT_DIR"

echo "OK: Tool family file generated at: $OUTPUT_DIR/tool-family/$TOOL_FAMILY.md"
