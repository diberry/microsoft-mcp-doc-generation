#!/bin/bash
set -e

# Quick Example Prompts Regeneration Script
# Run this from the workspace root to regenerate example prompts

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

echo -e "${CYAN}========================================${NC}"
echo -e "${CYAN}Example Prompts Regeneration${NC}"
echo -e "${CYAN}========================================${NC}"
echo ""

# Check if CLI output exists
CLI_OUTPUT="generated/cli/cli-output.json"
if [ ! -f "$CLI_OUTPUT" ]; then
    echo -e "${RED}❌ CLI output not found: $CLI_OUTPUT${NC}"
    echo ""
    echo "Please generate CLI output first:"
    echo "  pwsh docs-generation/Get-McpCliOutput.ps1 -OutputPath generated/cli"
    exit 1
fi
echo -e "${GREEN}✅ Found CLI output${NC}"

# Load credentials from .env file
ENV_FILE="docs-generation/.env"
if [ ! -f "$ENV_FILE" ]; then
    echo -e "${RED}❌ Missing .env file: $ENV_FILE${NC}"
    echo ""
    echo "Create one with:"
    echo "  cd docs-generation"
    echo "  cp sample.env .env"
    echo "  # Edit .env with your Azure OpenAI credentials"
    exit 1
fi

echo -e "${CYAN}📄 Loading credentials from $ENV_FILE${NC}"
# Export variables from .env file
while IFS='=' read -r key value; do
    # Skip comments and empty lines
    [[ $key =~ ^#.*$ ]] && continue
    [[ -z $key ]] && continue
    # Remove quotes and whitespace
    value=$(echo "$value" | sed -e 's/^"//' -e 's/"$//' -e 's/^[[:space:]]*//' -e 's/[[:space:]]*$//')
    export "$key=$value"
done < "$ENV_FILE"

# Verify credentials
if [ -z "$FOUNDRY_API_KEY" ] || [ -z "$FOUNDRY_ENDPOINT" ] || [ -z "$FOUNDRY_MODEL_NAME" ]; then
    echo -e "${RED}❌ Missing required credentials in .env file${NC}"
    echo "Required: FOUNDRY_API_KEY, FOUNDRY_ENDPOINT, FOUNDRY_MODEL_NAME"
    exit 1
fi

MASKED_KEY="${FOUNDRY_API_KEY:0:6}***"
echo -e "${GREEN}✅ Credentials loaded${NC}"
echo -e "${BLUE}   API Key: $MASKED_KEY${NC}"
echo -e "${BLUE}   Endpoint: $FOUNDRY_ENDPOINT${NC}"
echo -e "${BLUE}   Model: $FOUNDRY_MODEL_NAME${NC}"
echo ""

# Clean output directory
echo -e "${YELLOW}🧹 Cleaning previous example prompts...${NC}"
rm -rf generated/example-prompts/*
mkdir -p generated/example-prompts
echo -e "${GREEN}✅ Ready for generation${NC}"
echo ""

# Build C# generator
echo -e "${BLUE}📦 Building generator...${NC}"
cd docs-generation
dotnet build CSharpGenerator/CSharpGenerator.csproj --configuration Release --nologo --verbosity quiet
if [ $? -ne 0 ]; then
    echo -e "${RED}❌ Build failed${NC}"
    exit 1
fi
echo -e "${GREEN}✅ Build complete${NC}"
echo ""

# Run example prompt generation ONLY
echo -e "${BLUE}🤖 Generating example prompts with Azure OpenAI...${NC}"
echo -e "${YELLOW}   This may take a few minutes (199 tools × ~5-10 sec each)${NC}"
echo ""

dotnet run --project CSharpGenerator/CSharpGenerator.csproj --configuration Release -- \
    generate-docs \
    ../generated/cli/cli-output.json \
    ../generated/tools \
    --example-prompts

if [ $? -eq 0 ]; then
    cd ..
    echo ""
    echo -e "${GREEN}========================================${NC}"
    echo -e "${GREEN}✅ Example prompts generated!${NC}"
    echo -e "${GREEN}========================================${NC}"
    echo ""
    
    # Count generated files
    FILE_COUNT=$(find generated/example-prompts -name "*.md" -type f 2>/dev/null | wc -l)
    if [ $FILE_COUNT -gt 0 ]; then
        echo -e "${CYAN}📄 Generated $FILE_COUNT example prompt files${NC}"
        echo ""
        echo -e "${CYAN}Sample files:${NC}"
        find generated/example-prompts -name "*.md" -type f | sort | head -5 | while read file; do
            SIZE=$(du -h "$file" | cut -f1)
            echo "  • $(basename "$file") (${SIZE})"
        done
        
        if [ $FILE_COUNT -gt 5 ]; then
            echo "  ... and $((FILE_COUNT - 5)) more files"
        fi
        echo ""
        echo -e "${GREEN}🎉 Ready to use!${NC}"
    else
        echo -e "${YELLOW}⚠️  No files generated - check logs above for errors${NC}"
    fi
else
    cd ..
    echo ""
    echo -e "${RED}❌ Example prompt generation failed${NC}"
    echo ""
    echo "Check the output above for error details"
    exit 1
fi
