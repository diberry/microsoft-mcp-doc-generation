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
# Developer shortcuts (pass flags directly):
#   ./start.sh --inspect --step <step> --namespace <ns> --show prompt-budget --output ./generated-<ns>
#     # Pre-flight: estimate prompt tokens for a step without running the LLM.
#     # Exits 0 if within budget, 1 if over budget. Example:
#     #   ./start.sh --inspect --step horizontal-articles --namespace advisor --show prompt-budget --output ./generated-advisor
#     #   ./start.sh --inspect --step tool-generation --namespace advisor --show prompt-budget --output ./generated-advisor
#
#   ./start.sh --replay --step <step> --from <runId> [--namespace <ns>]
#     # Replay a single step against frozen outputs from a prior run (no LLM call for deterministic steps).
#     # Example: ./start.sh --replay --step tool-generation --from 20240501T120000Z --namespace advisor
#
# Bootstrap step 0 always runs inside DocGeneration.PipelineRunner; start.sh is only a thin wrapper.

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SCRIPT_DIR="$ROOT_DIR"
NAMESPACE_ARG=""
STEPS_ARG="1,2,3,4,5,6"
EXTRA_ARGS=()

ENV_SOURCE="$SCRIPT_DIR/.env"
ENV_TARGET="$SCRIPT_DIR/mcp-tools/.env"

if [[ -f "$ENV_SOURCE" ]]; then
    cp "$ENV_SOURCE" "$ENV_TARGET"
    echo "[start] Copied .env → mcp-tools/.env"
else
    echo "[start] ERROR: .env not found at repo root. Create .env with FOUNDRY_API_KEY, FOUNDRY_ENDPOINT, FOUNDRY_MODEL_NAME." >&2
    exit 1
fi

# If first arg starts with -, pass all args through directly to DocGeneration.PipelineRunner
if [[ $# -gt 0 && "$1" =~ ^- ]]; then
    dotnet run --project "$ROOT_DIR/mcp-tools/DocGeneration.PipelineRunner/DocGeneration.PipelineRunner.csproj" -- "$@"
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
    RUNNER_ARGS+=(--namespace "$NAMESPACE_ARG")
fi

# Append extra flags
RUNNER_ARGS+=("${EXTRA_ARGS[@]+"${EXTRA_ARGS[@]}"}")

echo "==================================================================="
echo "Start: Documentation Generation Orchestrator"
echo "==================================================================="
echo ""

dotnet run --project "$ROOT_DIR/mcp-tools/DocGeneration.PipelineRunner/DocGeneration.PipelineRunner.csproj" -- "${RUNNER_ARGS[@]}"
PIPELINE_EXIT=$?

# Post-assembly: merge multi-namespace articles (AD-011)
# Only runs when tool-family articles exist for merge group members
if [[ $PIPELINE_EXIT -eq 0 && -f "$ROOT_DIR/merge-namespaces.sh" ]]; then
    bash "$ROOT_DIR/merge-namespaces.sh"
fi

exit $PIPELINE_EXIT