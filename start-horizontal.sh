#!/bin/bash
# Generate horizontal Azure service articles using AI
# This script calls the PowerShell generator for horizontal articles

set -e

echo "========================================"
echo "Azure MCP Horizontal Article Generator"
echo "========================================"
echo ""

# Load environment variables from .env file if it exists
ENV_FILE="./docs-generation/.env"
if [[ -f "$ENV_FILE" ]]; then
    echo "Loading environment variables from $ENV_FILE"
    set -a
    source "$ENV_FILE"
    set +a
    echo ""
fi

# Check for required environment variables
required_vars=("FOUNDRY_API_KEY" "FOUNDRY_ENDPOINT" "FOUNDRY_MODEL_NAME" "FOUNDRY_MODEL_API_VERSION")
missing_vars=()

for var in "${required_vars[@]}"; do
    if [[ -z "${!var}" ]]; then
        missing_vars+=("$var")
    fi
done

if [[ ${#missing_vars[@]} -gt 0 ]]; then
    echo "ERROR: Missing required environment variables:"
    for var in "${missing_vars[@]}"; do
        echo "  - $var"
    done
    echo ""
    echo "Set these variables before running this script."
    exit 1
fi

# Check if CLI output exists
if [[ ! -f "generated/cli/cli-output.json" ]]; then
    echo "ERROR: CLI output not found at generated/cli/cli-output.json"
    echo ""
    echo "Please run ./start.sh first to generate the CLI output."
    exit 1
fi

# Run the PowerShell generator
cd docs-generation
pwsh ./Generate-HorizontalArticles.ps1 "$@"
