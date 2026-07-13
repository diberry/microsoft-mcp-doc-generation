#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

BUILD_FLAG=()
if [[ "${1:-}" == "--skip-build" || "${1:-}" == "--no-build" ]]; then
    BUILD_FLAG=(--no-build)
fi

set +e
dotnet test ./mcp-doc-generation.sln --filter "Category=Keyless" "${BUILD_FLAG[@]}"
mcp_exit_code=$?
if [[ $mcp_exit_code -eq 0 ]]; then
    echo "MCP keyless suite: PASSED"
else
    echo "MCP keyless suite: FAILED"
fi

dotnet test ./skills-generation/skills-generation.slnx --filter "Category=Keyless" "${BUILD_FLAG[@]}"
skills_exit_code=$?
if [[ $skills_exit_code -eq 0 ]]; then
    echo "Skills keyless suite: PASSED"
else
    echo "Skills keyless suite: FAILED"
fi
set -e

if [[ $mcp_exit_code -eq 0 && $skills_exit_code -eq 0 ]]; then
    echo "Keyless verification overall: PASSED"
    exit 0
fi

echo "Keyless verification overall: FAILED"
exit 1
