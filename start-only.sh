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

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if [[ $# -lt 1 ]]; then
	echo "Usage: $0 <tool-family> [steps]"
	echo "Example: $0 advisor"
	echo "Example: $0 advisor 1        (run only step 1)"
	exit 1
fi

TOOL_FAMILY="$1"
STEPS="${2:-1,2,3,4,5}"

# Verify CLI metadata files exist
if [[ ! -f "$ROOT_DIR/generated/cli/cli-version.json" ]] || \
   [[ ! -f "$ROOT_DIR/generated/cli/cli-output.json" ]] || \
   [[ ! -f "$ROOT_DIR/generated/cli/cli-namespace.json" ]]; then
    echo "ERROR: CLI metadata files not found in ./generated/cli/"
    echo "       Please run start.sh first to generate CLI metadata."
    exit 1
fi

# Ensure output directories exist
mkdir -p "$ROOT_DIR/generated/common-general"
mkdir -p "$ROOT_DIR/generated/tools"
mkdir -p "$ROOT_DIR/generated/example-prompts"
mkdir -p "$ROOT_DIR/generated/annotations"
mkdir -p "$ROOT_DIR/generated/logs"
mkdir -p "$ROOT_DIR/generated/tool-family"

echo "Running tool family pipeline for: $TOOL_FAMILY (steps: $STEPS)"
cd "$ROOT_DIR/docs-generation"
./generate-tool-family.sh "$TOOL_FAMILY" "$STEPS"

echo "OK: Tool family file generated at: $ROOT_DIR/generated/tool-family/$TOOL_FAMILY.md"
