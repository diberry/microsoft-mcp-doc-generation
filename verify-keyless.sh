#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

dotnet test ./mcp-doc-generation.sln --filter "Category=Keyless"
dotnet test ./skills-generation/skills-generation.slnx --filter "Category=Keyless"
