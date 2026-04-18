#!/usr/bin/env bash
# Wrapper to run verify-published.ps1 via pwsh -File
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
pwsh -File "$SCRIPT_DIR/verify-published.ps1" "$@"
