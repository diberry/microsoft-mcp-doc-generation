#!/bin/bash

# Script to generate example prompts using generative AI
# This script calls the Generate-ExamplePrompts.sh script in the docs-generation directory

set -e

# Get the directory where this script is located
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Change to the docs-generation directory and run the generator
cd "$SCRIPT_DIR/docs-generation"
./Generate-ExamplePrompts.sh "$@"
