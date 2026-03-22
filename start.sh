#!/bin/bash
# Start: Compatibility wrapper for the typed DocGeneration.PipelineRunner host.
#
# Usage:
#   ./start.sh [namespace] [steps] [--skip-deps]
#   ./start.sh                      # Run bootstrap + steps 1-6 for all namespaces (output: ./generated/)
#   ./start.sh advisor              # Run bootstrap + steps 1-6 for advisor namespace only (output: ./generated-advisor/)
#   ./start.sh advisor 1,2,3        # Run bootstrap + steps 1,2,3 for advisor namespace (output: ./generated-advisor/)
#   ./start.sh 1,2,3                # Run bootstrap + steps 1,2,3 for all namespaces (output: ./generated/)
#   ./start.sh advisor 4 --skip-deps  # Run step 4 skipping dependency validation
#
# Bootstrap step 0 always runs inside DocGeneration.PipelineRunner; start.sh is only a thin wrapper.

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
NAMESPACE_ARG=""
STEPS_ARG="1,2,3,4,5,6"
EXTRA_ARGS=()

# If first arg starts with -, pass all args through directly to DocGeneration.PipelineRunner
if [[ $# -gt 0 && "$1" =~ ^- ]]; then
    dotnet run --project "$ROOT_DIR/docs-generation/DocGeneration.PipelineRunner/DocGeneration.PipelineRunner.csproj" -- "$@"
    exit $?
fi

if [[ $# -gt 0 ]]; then
    if [[ "$1" =~ ^[1-6](,[1-6])*$ ]]; then
        STEPS_ARG="$1"
        shift
    else
        NAMESPACE_ARG="$1"
        shift
        if [[ $# -gt 0 && "$1" =~ ^[1-6](,[1-6])*$ ]]; then
            STEPS_ARG="$1"
            shift
        fi
    fi
fi

# Collect remaining flags (e.g., --skip-deps)
while [[ $# -gt 0 ]]; do
    EXTRA_ARGS+=("$1")
    shift
done

RUNNER_ARGS=(--steps "$STEPS_ARG")
if [[ -n "$NAMESPACE_ARG" ]]; then
    OUTPUT_DIR="$ROOT_DIR/generated-$NAMESPACE_ARG"
    RUNNER_ARGS+=(--namespace "$NAMESPACE_ARG" --output "$OUTPUT_DIR")
else
    OUTPUT_DIR="$ROOT_DIR/generated"
    RUNNER_ARGS+=(--output "$OUTPUT_DIR")
fi

# Append extra flags
RUNNER_ARGS+=("${EXTRA_ARGS[@]+"${EXTRA_ARGS[@]}"}")

echo "==================================================================="
echo "Start: Documentation Generation Orchestrator"
echo "==================================================================="
echo ""

dotnet run --project "$ROOT_DIR/docs-generation/DocGeneration.PipelineRunner/DocGeneration.PipelineRunner.csproj" -- "${RUNNER_ARGS[@]}"
PIPELINE_EXIT=$?

# Post-assembly: merge multi-namespace articles (AD-011)
# Only runs when tool-family articles exist for merge group members
if [[ $PIPELINE_EXIT -eq 0 && -f "$ROOT_DIR/merge-namespaces.sh" ]]; then
    bash "$ROOT_DIR/merge-namespaces.sh"
fi

exit $PIPELINE_EXIT