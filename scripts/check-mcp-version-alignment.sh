#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="${1:-$(cd "$SCRIPT_DIR/.." && pwd)}"
NAMESPACE_CHECK="${NAMESPACE_CHECK:-resilience}"

read_trimmed() {
  local path="$1"
  if [[ -f "$path" ]]; then
    tr -d '\r\n' < "$path"
  else
    printf ''
  fi
}

parse_sort_key() {
  local ver="$1"
  local base="${ver%%+*}"

  if [[ "$base" =~ ^([0-9]+)\.([0-9]+)\.([0-9]+)-beta\.([0-9]+)$ ]]; then
    printf '%06d.%06d.%06d.%06d|%s\n' "${BASH_REMATCH[1]}" "${BASH_REMATCH[2]}" "${BASH_REMATCH[3]}" "${BASH_REMATCH[4]}" "$ver"
    return
  fi

  if [[ "$base" =~ ^([0-9]+)\.([0-9]+)\.([0-9]+)$ ]]; then
    printf '%06d.%06d.%06d.%06d|%s\n' "${BASH_REMATCH[1]}" "${BASH_REMATCH[2]}" "${BASH_REMATCH[3]}" "999999" "$ver"
    return
  fi
}

get_latest_snapshot() {
  local metadata_dir="$1"
  if [[ ! -d "$metadata_dir" ]]; then
    printf ''
    return
  fi

  local best_key=''
  local best_ver=''
  local d
  while IFS= read -r -d '' d; do
    local name
    name="$(basename "$d")"
    local parsed
    parsed="$(parse_sort_key "$name" || true)"
    [[ -z "$parsed" ]] && continue
    local key="${parsed%%|*}"
    local ver="${parsed#*|}"
    if [[ -z "$best_key" || "$key" > "$best_key" ]]; then
      best_key="$key"
      best_ver="$ver"
    fi
  done < <(find "$metadata_dir" -mindepth 1 -maxdepth 1 -type d -print0)

  printf '%s' "$best_ver"
}

get_dotnet_tool_version() {
  if ! command -v dotnet >/dev/null 2>&1; then
    printf ''
    return
  fi

  local line
  line="$(dotnet tool list --global 2>/dev/null | awk '$1==\"azure.mcp\" {print; exit}')"
  if [[ -z "$line" ]]; then
    printf ''
    return
  fi

  awk '{print $2}' <<< "$line"
}

contains_namespace() {
  local json_file="$1"
  local namespace="$2"

  if [[ ! -f "$json_file" ]]; then
    printf 'missing-file'
    return
  fi

  if grep -q "\"mcpServerName\"[[:space:]]*:[[:space:]]*\"${namespace}\"" "$json_file"; then
    printf 'present'
  else
    printf 'missing'
  fi
}

TOOL_VERSION_PATH="$REPO_ROOT/mcp-tool-version.txt"
TRACKED_VERSION_PATH="$REPO_ROOT/mcp-cli-metadata/tracked-version.txt"
METADATA_DIR="$REPO_ROOT/mcp-cli-metadata"
BRAND_MAPPING_PATH="$REPO_ROOT/mcp-tools/data/brand-to-server-mapping.json"

MCP_TOOL_VERSION="$(read_trimmed "$TOOL_VERSION_PATH")"
TRACKED_VERSION="$(read_trimmed "$TRACKED_VERSION_PATH")"
GLOBAL_TOOL_VERSION="$(get_dotnet_tool_version)"
LATEST_SNAPSHOT_VERSION="$(get_latest_snapshot "$METADATA_DIR")"
BRAND_COVERAGE_STATE="$(contains_namespace "$BRAND_MAPPING_PATH" "$NAMESPACE_CHECK")"

printf '%-45s | %s\n' "mcp-tool-version.txt" "${MCP_TOOL_VERSION:-<missing>}"
printf '%-45s | %s\n' "mcp-cli-metadata/tracked-version.txt" "${TRACKED_VERSION:-<missing>}"
printf '%-45s | %s\n' "dotnet global tool azure.mcp" "${GLOBAL_TOOL_VERSION:-<not-installed>}"
printf '%-45s | %s\n' "latest local mcp-cli-metadata folder" "${LATEST_SNAPSHOT_VERSION:-<none>}"
printf '%-45s | %s\n' "brand mapping namespace '${NAMESPACE_CHECK}'" "${BRAND_COVERAGE_STATE}"

reasons=()

[[ -z "$MCP_TOOL_VERSION" ]] && reasons+=("mcp-tool-version.txt missing")
[[ -z "$TRACKED_VERSION" ]] && reasons+=("tracked-version.txt missing")
[[ -z "$GLOBAL_TOOL_VERSION" ]] && reasons+=("dotnet global tool azure.mcp not installed")
[[ -z "$LATEST_SNAPSHOT_VERSION" ]] && reasons+=("no local mcp-cli-metadata version folders")
[[ "$BRAND_COVERAGE_STATE" != "present" ]] && reasons+=("brand mapping missing namespace '$NAMESPACE_CHECK'")

TARGET_VERSION="$MCP_TOOL_VERSION"
if [[ -n "$MCP_TOOL_VERSION" && -n "$TRACKED_VERSION" && "$MCP_TOOL_VERSION" != "$TRACKED_VERSION" ]]; then
  reasons+=("mcp-tool-version.txt ($MCP_TOOL_VERSION) != tracked-version.txt ($TRACKED_VERSION)")
fi
if [[ -n "$TARGET_VERSION" && -n "$GLOBAL_TOOL_VERSION" && "$TARGET_VERSION" != "$GLOBAL_TOOL_VERSION" ]]; then
  reasons+=("mcp-tool-version.txt ($TARGET_VERSION) != dotnet azure.mcp ($GLOBAL_TOOL_VERSION)")
fi

if [[ -n "$TARGET_VERSION" && -n "$LATEST_SNAPSHOT_VERSION" ]]; then
  LATEST_BASE="${LATEST_SNAPSHOT_VERSION%%+*}"
  if [[ "$TARGET_VERSION" != "$LATEST_BASE" && "$TARGET_VERSION" != "$LATEST_SNAPSHOT_VERSION" ]]; then
    reasons+=("mcp-tool-version.txt ($TARGET_VERSION) not represented by latest snapshot folder ($LATEST_SNAPSHOT_VERSION)")
  fi
fi

if [[ ${#reasons[@]} -eq 0 ]]; then
  printf 'ALIGNED at %s\n' "$TARGET_VERSION"
  exit 0
fi

printf 'MISALIGNED with reasons: %s\n' "$(IFS='; '; echo "${reasons[*]}")"
exit 1