#!/bin/bash
set -e

# Generate Example Prompts - Standalone Script
# This script generates example prompts from existing CLI output JSON

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

# === CONFIGURATION - Update paths here if needed ===
CLI_OUTPUT="$SCRIPT_DIR/../generated/cli/cli-output.json"
TOOLS_DIR="$SCRIPT_DIR/../generated/tools"
OUTPUT_DIR="$SCRIPT_DIR/../generated/example-prompts"
PROMPTS_INPUT_DIR="$SCRIPT_DIR/../generated/prompts-for-example-tool-prompts"
PROJECT_PATH="$SCRIPT_DIR/CSharpGenerator/CSharpGenerator.csproj"
# ===================================================

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo -e "${BLUE}=== Generate Example Prompts ===${NC}"
echo ""

# Check if cli-output.json exists
if [ ! -f "$CLI_OUTPUT" ]; then
    echo -e "${RED}❌ CLI output not found: $CLI_OUTPUT${NC}"
    echo "Please run Generate-MultiPageDocs.ps1 first to generate the CLI output"
    exit 1
fi

echo -e "${GREEN}✅ Found CLI output: $CLI_OUTPUT${NC}"
echo -e "${BLUE}   Full path: $(realpath "$CLI_OUTPUT")${NC}"

# Verify environment variables (loaded by parent script)
echo -e "${BLUE}=== Environment Variables Check ===${NC}"
echo -e "${BLUE}Full environment (FOUNDRY_* only):${NC}"
# Mask API key in environment display
env | grep FOUNDRY_ | sed 's/\(FOUNDRY_API_KEY=\)\(.\{6\}\)\(.*\)$/\1\2...[MASKED]/' || echo "  (no FOUNDRY_* variables found in environment)"
echo ""

echo -e "${BLUE}Verifying required variables:${NC}"
# Mask API key - show only first 6 characters
MASKED_KEY="${FOUNDRY_API_KEY:0:6}...[MASKED]"
echo "  FOUNDRY_API_KEY: ${FOUNDRY_API_KEY:+SET (${#FOUNDRY_API_KEY} chars)} | Value: ${MASKED_KEY}"
echo "  FOUNDRY_ENDPOINT: ${FOUNDRY_ENDPOINT:+SET} | Value: ${FOUNDRY_ENDPOINT}"
echo "  FOUNDRY_MODEL_NAME: ${FOUNDRY_MODEL_NAME:+SET} | Value: ${FOUNDRY_MODEL_NAME}"
echo "  FOUNDRY_MODEL_API_VERSION: ${FOUNDRY_MODEL_API_VERSION:+SET} | Value: ${FOUNDRY_MODEL_API_VERSION}"
echo ""

if [ -z "$FOUNDRY_API_KEY" ] || [ -z "$FOUNDRY_ENDPOINT" ] || [ -z "$FOUNDRY_MODEL_NAME" ]; then
    echo -e "${RED}❌ Missing required environment variables${NC}"
    echo "Required: FOUNDRY_API_KEY, FOUNDRY_ENDPOINT, FOUNDRY_MODEL_NAME"
    echo ""
    echo "These should be loaded by run-generative-ai-output.sh from .env file"
    exit 1
fi
echo -e "${GREEN}✅ Environment variables verified${NC}"

# Create output directory
mkdir -p "$OUTPUT_DIR"

# Clean output directory before regenerating
echo -e "${YELLOW}🧹 Cleaning output directory...${NC}"
rm -rf "$OUTPUT_DIR"/*
echo -e "${GREEN}✅ Output directory cleaned${NC}"

# Clean prompts-for-example-tool-prompts directory
echo -e "${YELLOW}🧹 Cleaning prompts-for-example-tool-prompts directory...${NC}"
rm -rf "$PROMPTS_INPUT_DIR"/*
echo -e "${GREEN}✅ prompts-for-example-tool-prompts directory cleaned${NC}"

echo -e "${GREEN}✅ Output directory: $OUTPUT_DIR${NC}"
echo -e "${BLUE}   Full path: $(realpath "$OUTPUT_DIR")${NC}"
echo ""

# Build C# generator if needed
echo -e "${BLUE}Building C# generator...${NC}"
echo -e "${BLUE}   Project: $(realpath "$PROJECT_PATH")${NC}"
echo -e "${BLUE}   Tools dir: $(realpath "$TOOLS_DIR")${NC}"
dotnet build "$PROJECT_PATH" --configuration Release --nologo --verbosity quiet
if [ $? -ne 0 ]; then
    echo -e "${RED}❌ Build failed${NC}"
    exit 1
fi
echo -e "${GREEN}✅ Build successful${NC}"
echo ""

# Run example prompt generation
echo -e "${BLUE}Generating example prompts...${NC}"
dotnet run --project "$PROJECT_PATH" --configuration Release --no-build -- \
    generate-docs \
    "$CLI_OUTPUT" \
    "$TOOLS_DIR" \
    --annotations \
    --example-prompts \
    --no-service-options

if [ $? -eq 0 ]; then
    echo ""
    echo -e "${GREEN}========================================${NC}"
    echo -e "${GREEN}✅ Example prompts generated!${NC}"
    echo -e "${GREEN}========================================${NC}"
    echo ""
    
    # Count generated files
    FILE_COUNT=$(find "$OUTPUT_DIR" -name "*.md" -type f 2>/dev/null | wc -l)
    echo -e "${BLUE}📄 Generated ${FILE_COUNT} example prompt files${NC}"
    echo -e "${BLUE}📁 Location: $OUTPUT_DIR${NC}"
    echo ""
    
    # Show first few files
    if [ $FILE_COUNT -gt 0 ]; then
        echo -e "${BLUE}Sample files:${NC}"
        find "$OUTPUT_DIR" -name "*.md" -type f | sort | head -5 | while read file; do
            SIZE=$(stat -f%z "$file" 2>/dev/null || stat -c%s "$file" 2>/dev/null || echo "0")
            SIZE_KB=$((SIZE / 1024))
            echo "  • $(basename "$file") (${SIZE_KB}KB)"
        done
        
        if [ $FILE_COUNT -gt 5 ]; then
            echo "  ... and $((FILE_COUNT - 5)) more files"
        fi
    fi
else
    echo ""
    echo -e "${RED}❌ Example prompt generation failed${NC}"
    exit 1
fi
