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
    set -a
    source .env
    set +a
    echo "✅ Environment variables loaded and exported"
fi

./Generate-ExamplePrompts.sh "$@"
