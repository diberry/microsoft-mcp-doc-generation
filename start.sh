#!/bin/bash
# Start: Orchestrator script to generate documentation for all tool families.
#
# Usage:
#   ./start.sh [namespace] [steps]
#   ./start.sh                # Run all steps for all namespaces
#   ./start.sh advisor        # Run all steps for advisor namespace only
#   ./start.sh advisor 1,2,3  # Run steps 1,2,3 for advisor namespace
#   ./start.sh 1,2,3          # Run steps 1,2,3 for all namespaces
#
# Preflight Actions (run once before all namespaces):
#   Delegates to ./docs-generation/scripts/preflight.ps1 which performs:
#   - Validate .env file exists with required AI credentials (STOPS if missing/invalid)
#   - Clean ./generated directory
#   - Create output directories
#   - Build .NET solution (all generator projects)
#   - Generate MCP CLI metadata (cli-output.json, cli-namespace.json, cli-version.json)
#   - Step 0: Brand mapping validation (STOPS if missing branding, outputs required fixes)
#
# Generation Steps (for each namespace):
#   Step 1: Generate annotations and parameters (raw extraction)
#   Step 2: Generate example prompts (with AI)
#   Step 3: Generate tool improvements (AI-enhanced descriptions)
#   Step 4: Generate tool family cleanup (formatting/structure)
#   Step 5: Generate horizontal articles (cross-cutting documentation)
#
# Prerequisites:
#   - Node.js + npm (for MCP CLI metadata)
#   - PowerShell (pwsh)
#   - .NET SDK (for generator projects)
#   - Azure OpenAI env vars (for Steps 2, 3, 5)

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Parse arguments: determine if first arg is namespace or steps
NAMESPACE_ARG=""
STEPS_ARG="1,2,3,4,5"

if [[ $# -gt 0 ]]; then
    # Check if first arg looks like steps (contains comma or is 1-5)
    if [[ "$1" =~ ^[1-5](,[1-5])*$ ]]; then
        STEPS_ARG="$1"
    else
        NAMESPACE_ARG="$1"
        if [[ $# -gt 1 ]]; then
            STEPS_ARG="$2"
        fi
    fi
fi

STEPS="$STEPS_ARG"

# Determine output directory based on whether a specific namespace was provided
if [[ -n "$NAMESPACE_ARG" ]]; then
    OUTPUT_DIR="$ROOT_DIR/generated-$NAMESPACE_ARG"
else
    OUTPUT_DIR="$ROOT_DIR/generated"
fi

echo "==================================================================="
echo "Start: Documentation Generation Orchestrator"
echo "==================================================================="
echo ""

# =================================================================== 
# PREFLIGHT: Global actions (run once before all namespaces)
# =================================================================== 

# Run preflight setup script
pwsh "$ROOT_DIR/docs-generation/scripts/preflight.ps1" -OutputPath "$OUTPUT_DIR"
PREFLIGHT_EXIT=$?
if [ $PREFLIGHT_EXIT -ne 0 ]; then
    echo ""
    echo "⛔ PIPELINE HALTED: Preflight setup failed (exit code: $PREFLIGHT_EXIT)"
    exit $PREFLIGHT_EXIT
fi

# =================================================================== 
# GENERATION: Process namespaces with steps 1-5
# ===================================================================

# Extract namespaces from cli-namespace.json
echo ""
if [[ -n "$NAMESPACE_ARG" ]]; then
    echo "Processing single namespace: $NAMESPACE_ARG"
    echo "Output directory: $OUTPUT_DIR"
    NAMESPACES="$NAMESPACE_ARG"
    NAMESPACE_COUNT=1
else
    echo "Extracting namespaces from CLI metadata..."
    NAMESPACES=$(jq -r '.results[].name' "$OUTPUT_DIR/cli/cli-namespace.json")
    NAMESPACE_COUNT=$(echo "$NAMESPACES" | wc -l)
    echo "✓ Found $NAMESPACE_COUNT namespaces"
fi

# Iterate over all namespaces (or single namespace)
echo ""
echo "==================================================================="
if [[ -n "$NAMESPACE_ARG" ]]; then
    echo "Processing namespace: $NAMESPACE_ARG (steps: $STEPS)"
else
    echo "Processing all $NAMESPACE_COUNT namespaces (steps: $STEPS)"
fi
echo "==================================================================="
echo ""

CURRENT=0
FAILED_NAMESPACES=()

for NAMESPACE in $NAMESPACES; do
    CURRENT=$((CURRENT + 1))
    echo "-------------------------------------------------------------------"
    echo "[$CURRENT/$NAMESPACE_COUNT] Processing namespace: $NAMESPACE"
    echo "-------------------------------------------------------------------"
    
    # Call start-only.sh for each namespace with output directory
    if "$ROOT_DIR/docs-generation/scripts/start-only.sh" "$NAMESPACE" "$STEPS" "$OUTPUT_DIR"; then
        echo "✓ Successfully generated documentation for: $NAMESPACE"
    else
        echo "✗ Failed to generate documentation for: $NAMESPACE"
        FAILED_NAMESPACES+=("$NAMESPACE")
    fi
    echo ""
done

# =================================================================== 
# SUMMARY: Report results
# ===================================================================

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
echo "   Output: $OUTPUT_DIR/tool-family/"
