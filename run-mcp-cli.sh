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
${CYAN}${BOLD}════════════════════════════════════════════════════════════${NC}
${CYAN}${BOLD}  Azure MCP CLI Container Helper - WRAPPER SCRIPT HELP${NC}
${CYAN}${BOLD}════════════════════════════════════════════════════════════${NC}

${YELLOW}Usage:${NC}
  ./run-mcp-cli.sh [WRAPPER_OPTIONS] [--] [MCP_COMMAND] [MCP_ARGS...]

${YELLOW}Wrapper Options (for this script only):${NC}
  --build              Build the Docker image first
  --no-cache           Build without using cache
  --branch <name>      Use specific MCP branch (default: main)
  --shell              Open interactive shell in container
  --no-color           Disable colored output
  --help               Show ${BOLD}this wrapper script help${NC}

${YELLOW}Separator:${NC}
  --                   Pass all remaining arguments directly to MCP CLI

${YELLOW}Examples:${NC}
  ${GREEN}# Show this wrapper script help (wrapper only)${NC}
  ./run-mcp-cli.sh --help

  ${GREEN}# Show MCP CLI help (calls MCP CLI in Docker)${NC}
  ./run-mcp-cli.sh -- --help

  ${GREEN}# List all MCP tools (calls: azmcp tools list)${NC}
  ./run-mcp-cli.sh tools list

  ${GREEN}# List just tool names (calls: azmcp tools list --name-only)${NC}
  ./run-mcp-cli.sh tools list --name-only

  ${GREEN}# List tool namespaces (calls: azmcp tools list --namespace-mode)${NC}
  ./run-mcp-cli.sh tools list --namespace-mode

  ${GREEN}# Get MCP CLI version (calls: azmcp --version)${NC}
  ./run-mcp-cli.sh -- --version

  ${GREEN}# Wrapper option + MCP command: build image, then list tools${NC}
  ./run-mcp-cli.sh --build -- tools list

  ${GREEN}# Wrapper option: use different MCP git branch${NC}
  ./run-mcp-cli.sh --branch feature-branch -- tools list

  ${GREEN}# Wrapper option: open shell inside container (no MCP command)${NC}
  ./run-mcp-cli.sh --shell

${YELLOW}Common MCP CLI Commands (all run inside Docker):${NC}
  tools list                      List all MCP tools (full JSON)
  tools list --name-only          List just tool names (concise)
  tools list --namespace-mode     List service namespaces
  --help                          Show MCP CLI help
  --version                       Show MCP CLI version

${YELLOW}What Runs Where?${NC}
  ${BOLD}Wrapper only:${NC}     --help, --build, --no-cache, --branch, --shell
  ${BOLD}Inside Docker:${NC}    Everything else (passed to: azmcp [command])

${YELLOW}Tip:${NC} Use ${BOLD}--${NC} to clearly separate wrapper options from MCP CLI arguments

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
        -f Dockerfile.cli \
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

    # Show header for MCP CLI output
    printf "${CYAN}${BOLD}════════════════════════════════════════════════════════════${NC}\n"
    printf "${CYAN}${BOLD}  MCP CLI OUTPUT${NC}\n"
    printf "${CYAN}${BOLD}════════════════════════════════════════════════════════════${NC}\n\n"

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
PASSTHROUGH=false

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
            if [ ${#COMMAND_ARGS[@]} -eq 0 ] && [ "$PASSTHROUGH" = false ]; then
                print_usage
                exit 0
            else
                COMMAND_ARGS+=("$1")
                shift
            fi
            ;;
        --)
            # All remaining arguments go to MCP CLI
            PASSTHROUGH=true
            shift
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
