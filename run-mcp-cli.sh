#!/bin/bash
# Azure MCP CLI Container Helper Script
# Provides easy access to Azure MCP Server CLI via Docker

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
IMAGE_NAME="azure-mcp-cli:latest"
MCP_BRANCH="${MCP_BRANCH:-main}"
USE_COLOR=true

# Colors for output (check if terminal supports colors)
setup_colors() {
    if [ "$USE_COLOR" = true ] && [ -t 1 ] && command -v tput >/dev/null 2>&1; then
        # Terminal supports colors
        RED=$(tput setaf 1 2>/dev/null || echo '')
        GREEN=$(tput setaf 2 2>/dev/null || echo '')
        YELLOW=$(tput setaf 3 2>/dev/null || echo '')
        CYAN=$(tput setaf 6 2>/dev/null || echo '')
        BOLD=$(tput bold 2>/dev/null || echo '')
        NC=$(tput sgr0 2>/dev/null || echo '')
    else
        # No color support
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
${CYAN}Azure MCP CLI Container Helper${NC}

${YELLOW}Usage:${NC}
  ./run-mcp-cli.sh [OPTIONS] [COMMAND] [ARGS...]

${YELLOW}Options:${NC}
  --build              Build the Docker image first
  --no-cache           Build without using cache
  --branch <name>      Use specific MCP branch (default: main)
  --shell              Open interactive shell in container
  --no-color           Disable colored output
  --help               Show this help message

${YELLOW}Examples:${NC}
  ${GREEN}# Show MCP CLI help${NC}
  ./run-mcp-cli.sh --help

  ${GREEN}# List all available tools${NC}
  ./run-mcp-cli.sh tools list

  ${GREEN}# List tools with JSON output${NC}
  ./run-mcp-cli.sh tools list --output tools.json

  ${GREEN}# List namespaces${NC}
  ./run-mcp-cli.sh tools list-namespaces

  ${GREEN}# Build image first, then run command${NC}
  ./run-mcp-cli.sh --build tools list

  ${GREEN}# Use different MCP branch${NC}
  ./run-mcp-cli.sh --branch feature-branch tools list

  ${GREEN}# Open interactive shell for debugging${NC}
  ./run-mcp-cli.sh --shell

${YELLOW}Common Commands:${NC}
  tools list              List all available MCP tools
  tools list-namespaces   List all tool namespaces
  --help                  Show MCP CLI help
  --version               Show MCP CLI version

EOF
}

build_image() {
    local no_cache=""
    if [ "$1" = "--no-cache" ]; then
        no_cache="--no-cache"
    fi

    printf "${CYAN}Building Azure MCP CLI container...${NC}\n"
    docker build \
        ${no_cache} \
        --build-arg MCP_BRANCH="${MCP_BRANCH}" \
        -t "${IMAGE_NAME}" \
        -f docker/Dockerfile.cli \
        .

    printf "${GREEN}✅ Docker image built successfully${NC}\n"
    docker images "${IMAGE_NAME}"
}

run_shell() {
    printf "${CYAN}Opening interactive shell in Azure MCP CLI container...${NC}\n"
    printf "${YELLOW}MCP Server is located at: /mcp/servers/Azure.Mcp.Server/src${NC}\n"
    printf "${YELLOW}Run 'dotnet run -- [command]' to execute MCP CLI${NC}\n\n"
    
    docker run \
        --rm \
        -it \
        --entrypoint /bin/bash \
        "${IMAGE_NAME}"
}

run_command() {
    # Check if image exists
    if ! docker image inspect "${IMAGE_NAME}" >/dev/null 2>&1; then
        printf "${YELLOW}Image not found. Building...${NC}\n"
        build_image
    fi

    # Run the MCP CLI with provided arguments
    docker run \
        --rm \
        "${IMAGE_NAME}" \
        "$@"
}

# Setup colors first
setup_colors

# Parse arguments
BUILD=false
NO_CACHE=false
SHELL=false
COMMAND_ARGS=()

while [[ $# -gt 0 ]]; do
    case $1 in
        --build)
            BUILD=true
            shift
            ;;
        --no-cache)
            NO_CACHE=true
            shift
            ;;
        --no-color)
            USE_COLOR=false
            setup_colors
            shift
            ;;
        --branch)
            MCP_BRANCH="$2"
            shift 2
            ;;
        --shell)
            SHELL=true
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

# Execute based on options
if [ "$BUILD" = true ]; then
    if [ "$NO_CACHE" = true ]; then
        build_image --no-cache
    else
        build_image
    fi
fi

if [ "$SHELL" = true ]; then
    run_shell
    exit 0
fi

# Run command if arguments provided
if [ ${#COMMAND_ARGS[@]} -gt 0 ]; then
    run_command "${COMMAND_ARGS[@]}"
else
    # No arguments, show help
    print_usage
fi
