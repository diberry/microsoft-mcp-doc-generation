#!/bin/bash

# Script to generate example prompts using generative AI
# This script calls the Generate-ExamplePrompts.sh script in the docs-generation directory

set -e

# Get the directory where this script is located
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Change to the docs-generation directory
cd "$SCRIPT_DIR/docs-generation"

# Load .env file if it exists and export variables
if [ -f ".env" ]; then
    echo "📄 Loading .env file..."
    echo "📄 .env file contents:"
    cat .env
    echo ""
    
    set -a
    source .env
    set +a
    
    echo "✅ Environment variables loaded and exported"
    echo "📊 Environment check after loading:"
    echo "  FOUNDRY_API_KEY: ${FOUNDRY_API_KEY:+SET (${#FOUNDRY_API_KEY} chars)} | Value: ${FOUNDRY_API_KEY}"
    echo "  FOUNDRY_ENDPOINT: ${FOUNDRY_ENDPOINT:+SET} | Value: ${FOUNDRY_ENDPOINT}"
    echo "  FOUNDRY_MODEL_NAME: ${FOUNDRY_MODEL_NAME:+SET} | Value: ${FOUNDRY_MODEL_NAME}"
    echo "  FOUNDRY_MODEL_API_VERSION: ${FOUNDRY_MODEL_API_VERSION:+SET} | Value: ${FOUNDRY_MODEL_API_VERSION}"
    echo ""
else
    echo "❌ .env file not found at: $(pwd)/.env"
    exit 1
fi

./Generate-ExamplePrompts.sh "$@"
