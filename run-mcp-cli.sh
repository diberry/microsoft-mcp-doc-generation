#!/bin/bash
# Azure MCP CLI Helper Script
# Provides easy access to Azure MCP Server CLI via npm

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
MCP_PROJECT="${SCRIPT_DIR}/test-npm-azure-mcp"

# Colors for output
setup_colors() {
    if [ -t 1 ] && command -v tput >/dev/null 2>&1; then
        RED=$(tput setaf 1 2>/dev/null || echo '')
        GREEN=$(tput setaf 2 2>/dev/null || echo '')
        YELLOW=$(tput setaf 3 2>/dev/null || echo '')
        CYAN=$(tput setaf 6 2>/dev/null || echo '')
        BOLD=$(tput bold 2>/dev/null || echo '')
        NC=$(tput sgr0 2>/dev/null || echo '')
    else
        RED=''
        GREEN=''
        YELLOW=''
        CYAN=''
        BOLD=''
        NC=''
    fi
}

print_usage() {
    cat << EOF
${CYAN}Azure MCP CLI Helper${NC}

${YELLOW}Usage:${NC}
  ./run-mcp-cli.sh [OPTIONS] [COMMAND] [ARGS...]

${YELLOW}Options:${NC}
  --install            Install dependencies (npm install)
  --help               Show this help message

${YELLOW}Examples:${NC}
  ${GREEN}# Show MCP CLI help${NC}
  ./run-mcp-cli.sh -- --help

  ${GREEN}# List all available tools${NC}
  ./run-mcp-cli.sh tools list

  ${GREEN}# List tools with JSON output${NC}
  ./run-mcp-cli.sh tools list --output tools.json

  ${GREEN}# Get CLI version${NC}
  ./run-mcp-cli.sh -- --version

  ${GREEN}# Install dependencies first${NC}
  ./run-mcp-cli.sh --install

${YELLOW}Common Commands:${NC}
  tools list           List all available MCP tools
  -- --help            Show MCP CLI help
  -- --version         Show MCP CLI version

${YELLOW}Note:${NC}
  This script uses the local test-npm-azure-mcp package.
  All arguments are passed directly to: npx azmcp [ARGS]

EOF
}

setup_colors

# Parse arguments
INSTALL=false
COMMAND_ARGS=()

while [[ $# -gt 0 ]]; do
    case $1 in
        --install)
            INSTALL=true
            shift
            ;;
        --help)
            if [ ${#COMMAND_ARGS[@]} -eq 0 ]; then
                print_usage
                exit 0
            else
                COMMAND_ARGS+=("$1")
                shift
            fi
            ;;
        *)
            COMMAND_ARGS+=("$1")
            shift
            ;;
    esac
done

# Verify npm project exists
if [ ! -d "$MCP_PROJECT" ]; then
    printf "${RED}❌ Project not found at: $MCP_PROJECT${NC}\n"
    exit 1
fi

# Install dependencies if requested or if node_modules doesn't exist
if [ "$INSTALL" = true ] || [ ! -d "$MCP_PROJECT/node_modules" ]; then
    printf "${CYAN}Installing dependencies...${NC}\n"
    cd "$MCP_PROJECT"
    npm install --silent
    cd "$SCRIPT_DIR"
    printf "${GREEN}✅ Dependencies installed${NC}\n"
    echo ""
fi

# Run command if arguments provided
if [ ${#COMMAND_ARGS[@]} -gt 0 ]; then
    cd "$MCP_PROJECT"
    npx azmcp "${COMMAND_ARGS[@]}"
else
    # No arguments, show help
    print_usage
fi
