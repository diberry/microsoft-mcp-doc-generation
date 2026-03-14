#!/bin/bash
# Start: Compatibility wrapper for the typed PipelineRunner host.
#
# Usage:
#   ./start.sh [namespace] [steps]
#   ./start.sh                # Run bootstrap + steps 1-6 for all namespaces (output: ./generated/)
#   ./start.sh advisor        # Run bootstrap + steps 1-6 for advisor namespace only (output: ./generated-advisor/)
#   ./start.sh advisor 1,2,3  # Run bootstrap + steps 1,2,3 for advisor namespace (output: ./generated-advisor/)
#   ./start.sh 1,2,3          # Run bootstrap + steps 1,2,3 for all namespaces (output: ./generated/)
#
# Bootstrap step 0 always runs inside PipelineRunner; start.sh is only a thin wrapper.

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
NAMESPACE_ARG=""
STEPS_ARG="1,2,3,4,5,6"

# If first arg starts with -, pass all args through directly to PipelineRunner
if [[ $# -gt 0 && "$1" =~ ^- ]]; then
    dotnet run --project "$ROOT_DIR/docs-generation/PipelineRunner/PipelineRunner.csproj" -- "$@"
    exit $?
fi

if [[ $# -gt 0 ]]; then
    if [[ "$1" =~ ^[1-6](,[1-6])*$ ]]; then
        STEPS_ARG="$1"
    else
        NAMESPACE_ARG="$1"
        if [[ $# -gt 1 ]]; then
            STEPS_ARG="$2"
        fi
    fi
fi

RUNNER_ARGS=(--steps "$STEPS_ARG")
if [[ -n "$NAMESPACE_ARG" ]]; then
    OUTPUT_DIR="$ROOT_DIR/generated-$NAMESPACE_ARG"
    RUNNER_ARGS+=(--namespace "$NAMESPACE_ARG" --output "$OUTPUT_DIR")
else
    OUTPUT_DIR="$ROOT_DIR/generated"
    RUNNER_ARGS+=(--output "$OUTPUT_DIR")
fi

echo "==================================================================="
echo "Start: Documentation Generation Orchestrator"
echo "==================================================================="
echo ""

dotnet run --project "$ROOT_DIR/docs-generation/PipelineRunner/PipelineRunner.csproj" -- "${RUNNER_ARGS[@]}"
exit $?