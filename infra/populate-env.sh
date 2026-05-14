#!/usr/bin/env bash
# populate-env.sh — Bash wrapper for populate-env.ps1
# Derives mcp-tools/.env from deployed Azure resources via az CLI.
#
# Usage:
#   ./infra/populate-env.sh -EnvironmentName mcpdocs
#   ./infra/populate-env.sh -EnvironmentName mcpdocs -UseDefaultCredential
#   ./infra/populate-env.sh -ResourceGroup rg-mcpdocs -EnvironmentName mcpdocs

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Forward all arguments to PowerShell script via pwsh -File
pwsh -File "$SCRIPT_DIR/populate-env.ps1" "$@"
