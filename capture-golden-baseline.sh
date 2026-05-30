#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
NAMESPACE="${1:-advisor}"
STEPS="${2:-0,1,2,3,4,5,6}"
OUTPUT_DIR="$SCRIPT_DIR/generated-$NAMESPACE"
MANIFEST_DIR="$SCRIPT_DIR/mcp-tools/DocGeneration.PipelineRunner.Tests/Fixtures/GoldenSnapshot/$NAMESPACE"
EXTRA_FLAGS=(--skip-build --skip-changelog-gate --skip-npm-update)
DOTNET_BIN="${DOTNET_BIN:-dotnet}"

to_dotnet_path() {
  local value="$1"
  if [[ "$DOTNET_BIN" == *.exe ]] && command -v cygpath >/dev/null 2>&1; then
    cygpath -w "$value"
  elif [[ "$DOTNET_BIN" == *.exe ]] && command -v wslpath >/dev/null 2>&1; then
    wslpath -w "$value"
  else
    printf '%s\n' "$value"
  fi
}

if ! command -v "$DOTNET_BIN" >/dev/null 2>&1; then
  if [[ -x "/c/Program Files/dotnet/dotnet.exe" ]]; then
    DOTNET_BIN="/c/Program Files/dotnet/dotnet.exe"
  elif [[ -x "/mnt/c/Program Files/dotnet/dotnet.exe" ]]; then
    DOTNET_BIN="/mnt/c/Program Files/dotnet/dotnet.exe"
  else
    echo "dotnet executable not found. Set DOTNET_BIN to the full path if needed." >&2
    exit 1
  fi
fi

SOLUTION_PATH="$(to_dotnet_path "$SCRIPT_DIR/mcp-doc-generation.sln")"
PIPELINE_PROJECT_PATH="$(to_dotnet_path "$SCRIPT_DIR/mcp-tools/DocGeneration.PipelineRunner/DocGeneration.PipelineRunner.csproj")"
FINGERPRINT_PROJECT_PATH="$(to_dotnet_path "$SCRIPT_DIR/mcp-tools/DocGeneration.Tools.Fingerprint/DocGeneration.Tools.Fingerprint.csproj")"
OUTPUT_DIR_PATH="$(to_dotnet_path "$OUTPUT_DIR")"
MANIFEST_DIR_PATH="$(to_dotnet_path "$MANIFEST_DIR")"
REPO_ROOT_PATH="$(to_dotnet_path "$SCRIPT_DIR")"

if [[ "$STEPS" =~ ^(0,1,5|0,1|1|5)$ ]]; then
  EXTRA_FLAGS+=(--skip-env-validation)
fi

"$DOTNET_BIN" build "$SOLUTION_PATH" --configuration Release

rm -rf "$OUTPUT_DIR"

"$DOTNET_BIN" run --project "$PIPELINE_PROJECT_PATH" --configuration Release --no-build -- \
  --namespace "$NAMESPACE" \
  --steps "$STEPS" \
  --output "$OUTPUT_DIR_PATH" \
  "${EXTRA_FLAGS[@]}"

"$DOTNET_BIN" run --project "$FINGERPRINT_PROJECT_PATH" --configuration Release --no-build -- \
  golden capture \
  --namespace "$NAMESPACE" \
  --output "$MANIFEST_DIR_PATH" \
  --repo-root "$REPO_ROOT_PATH"
