#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PS_SCRIPT="$SCRIPT_DIR/echo-content-impact.ps1"

if command -v pwsh >/dev/null 2>&1; then
  POWERSHELL_BIN="pwsh"
  PS_SCRIPT_ARG="$PS_SCRIPT"
  ARGS=("$@")
elif command -v pwsh.exe >/dev/null 2>&1; then
  POWERSHELL_BIN="pwsh.exe"
  PS_SCRIPT_ARG="$(wslpath -w "$PS_SCRIPT")"
  ARGS=()
  for arg in "$@"; do
    if [[ "$arg" == /* ]]; then
      ARGS+=("$(wslpath -w "$arg")")
    else
      ARGS+=("$arg")
    fi
  done
else
  echo "echo-content-impact.sh requires PowerShell 7 (pwsh or pwsh.exe) on PATH." >&2
  exit 1
fi

"$POWERSHELL_BIN" -NoProfile -File "$PS_SCRIPT_ARG" "${ARGS[@]}"
