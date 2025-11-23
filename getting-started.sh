#!/bin/bash

################################################################################
# Getting Started Script
# 
# This script guides you through the three-step documentation generation process
# for Azure MCP tools.
################################################################################

set -e  # Exit on error

# Colors for output
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo ""
echo "=========================================="
echo "🚀 Azure MCP Documentation Generator"
echo "=========================================="
echo ""
echo "This process has three steps:"
echo ""
echo "  1️⃣  Extract tool metadata from MCP CLI"
echo "  2️⃣  Generate markdown documentation"
echo "  3️⃣  Generate AI example prompts"
echo ""

# Step 1: MCP CLI Output
echo ""
echo -e "${BLUE}=========================================="
echo "Step 1: Extract MCP CLI Tool Metadata"
echo -e "==========================================${NC}"
echo ""
echo "This will run the MCP CLI to extract tool information."
echo ""
read -p "Press Enter to run ./run-mcp-cli-output.sh or Ctrl+C to exit..."
echo ""

./run-mcp-cli-output.sh

if [ $? -eq 0 ]; then
    echo ""
    echo -e "${GREEN}✅ Step 1 complete!${NC}"
    echo ""
else
    echo ""
    echo -e "${YELLOW}⚠️  Step 1 failed. Please check the error messages above.${NC}"
    exit 1
fi

# Step 2: Content Generation
echo ""
echo -e "${BLUE}=========================================="
echo "Step 2: Generate Markdown Documentation"
echo -e "==========================================${NC}"
echo ""
echo "This will generate markdown documentation files from the extracted metadata."
echo ""
read -p "Press Enter to run ./run-content-generation-output.sh or Ctrl+C to exit..."
echo ""

./run-content-generation-output.sh

if [ $? -eq 0 ]; then
    echo ""
    echo -e "${GREEN}✅ Step 2 complete!${NC}"
    echo ""
else
    echo ""
    echo -e "${YELLOW}⚠️  Step 2 failed. Please check the error messages above.${NC}"
    exit 1
fi

# Step 3: Generative AI
echo ""
echo -e "${BLUE}=========================================="
echo "Step 3: Generate AI Example Prompts"
echo -e "==========================================${NC}"
echo ""
echo "This will use Generative AI to create example prompts for each tool."
echo ""
read -p "Press Enter to run ./run-generative-ai-output.sh or Ctrl+C to exit..."
echo ""

./run-generative-ai-output.sh

if [ $? -eq 0 ]; then
    echo ""
    echo -e "${GREEN}✅ Step 3 complete!${NC}"
    echo ""
else
    echo ""
    echo -e "${YELLOW}⚠️  Step 3 failed. Please check the error messages above.${NC}"
    exit 1
fi

# All done!
echo ""
echo "=========================================="
echo -e "${GREEN}🎉 All steps completed successfully!${NC}"
echo "=========================================="
echo ""
echo "📄 Your documentation is ready in:"
echo "   ./generated/"
echo ""
echo "Generated files include:"
echo "   • Markdown documentation (*.md)"
echo "   • Tool metadata reports"
echo "   • AI-generated example prompts"
echo ""
echo "🚀 Ready to use!"
echo ""
