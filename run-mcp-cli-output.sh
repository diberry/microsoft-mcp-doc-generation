#!/bin/bash
set -e

# Azure MCP CLI Output Generator - NPM-based Script
# Generates CLI output files using local test-npm-azure-mcp package

# Set up logging
mkdir -p generated/logs
LOG_FILE="generated/logs/run-mcp-cli-output.log"
exec > >(tee -a "$LOG_FILE") 2>&1
echo "=== Log started at $(date) ===" 

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

echo -e "${CYAN}========================================${NC}"
echo -e "${CYAN}Azure MCP CLI Output Generator${NC}"
echo -e "${CYAN}========================================${NC}"
echo ""

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --help|-h)
            echo "Usage: ./run-mcp-cli-output.sh [OPTIONS]"
            echo ""
            echo "Options:"
            echo "  --help,-h   Show this help message"
            echo ""
            echo "Examples:"
            echo "  ./run-mcp-cli-output.sh                    # Generate CLI output"
            echo ""
            echo "This script uses the local test-npm-azure-mcp package to generate:"
            echo "  • cli-output.json (tools list)"
            echo "  • cli-namespace.json (tools in namespace mode)"
            echo "  • cli-version.json (CLI version information)"
            exit 0
            ;;
        *)
            echo -e "${RED}Unknown option: $1${NC}"
            echo "Use --help for usage information"
            exit 1
            ;;
    esac
done

# Run the npm-based CLI output generator via PowerShell
echo -e "${BLUE}📝 Generating MCP CLI output files...${NC}"
echo -e "${YELLOW}Using: test-npm-azure-mcp package${NC}"
echo ""

if pwsh docs-generation/Get-McpCliOutput.ps1 -OutputPath generated/cli; then
    echo ""
    echo -e "${GREEN}========================================${NC}"
    echo -e "${GREEN}✅ CLI output generated successfully!${NC}"
    echo -e "${GREEN}========================================${NC}"
    echo ""
    
    # Validate and show generated files
    CLI_OUTPUT_FILE="generated/cli/cli-output.json"
    NAMESPACE_FILE="generated/cli/cli-namespace.json"
    VERSION_FILE="generated/cli/cli-version.json"
    
    if [ -f "$CLI_OUTPUT_FILE" ] && [ -f "$NAMESPACE_FILE" ] && [ -f "$VERSION_FILE" ]; then
        OUTPUT_SIZE=$(du -h "$CLI_OUTPUT_FILE" | cut -f1)
        NAMESPACE_SIZE=$(du -h "$NAMESPACE_FILE" | cut -f1)
        VERSION_SIZE=$(du -h "$VERSION_FILE" | cut -f1)
        
        TOTAL_SIZE=$(du -sh generated/cli | cut -f1)
        
        echo -e "${CYAN}📄 Generated files:${NC}"
        echo "  • cli-output.json (${OUTPUT_SIZE})"
        echo "  • cli-namespace.json (${NAMESPACE_SIZE})"
        echo "  • cli-version.json (${VERSION_SIZE})"
        echo ""
        echo -e "${CYAN}Total size: ${TOTAL_SIZE}${NC}"
        echo ""
        
        # Show version info
        if command -v jq &> /dev/null; then
            VERSION=$(jq -r '.version' "$VERSION_FILE" 2>/dev/null || echo "unknown")
            TIMESTAMP=$(jq -r '.timestamp' "$VERSION_FILE" 2>/dev/null || echo "unknown")
            
            echo -e "${CYAN}Version Information:${NC}"
            echo "  • CLI Version: $VERSION"
            echo "  • Generated: $TIMESTAMP"
        fi
        
        echo ""
        echo -e "${GREEN}🎉 Ready for documentation generation!${NC}"
        echo -e "${CYAN}Next step: ./run-docker.sh --skip-cli-generation${NC}"
    else
        echo -e "${YELLOW}⚠️  Some output files may be missing${NC}"
        ls -lh generated/cli/
    fi
    
else
    echo ""
    echo -e "${RED}❌ CLI output generation failed${NC}"
    echo ""
    echo "Troubleshooting:"
    echo "  1. Ensure PowerShell 7+ is installed and accessible via 'pwsh'"
    echo "  2. Check that test-npm-azure-mcp has node_modules installed"
    echo "  3. Run: cd test-npm-azure-mcp && npm install"
    echo "  4. Then retry: ./run-mcp-cli-output.sh"
    exit 1
fi
