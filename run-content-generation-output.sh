#!/bin/bash
set -e

# Azure MCP Content Generation - Documentation Generator
# This script generates documentation from existing CLI output files
# Prerequisites: CLI output files must exist in generated/cli/

# Set up logging
mkdir -p generated/logs
LOG_FILE="generated/logs/run-content-generation-output.log"
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
echo -e "${CYAN}Azure MCP Content Generator${NC}"
echo -e "${CYAN}========================================${NC}"
echo ""

# Check if Docker is installed
if ! command -v docker &> /dev/null; then
    echo -e "${RED}❌ Docker is not installed${NC}"
    echo "Please install Docker Desktop or Docker Engine"
    echo "Visit: https://docs.docker.com/get-docker/"
    exit 1
fi

# Check if Docker daemon is running
if ! docker info &> /dev/null; then
    echo -e "${RED}❌ Docker daemon is not running${NC}"
    echo "Please start Docker Desktop or the Docker service"
    exit 1
fi

echo -e "${GREEN}✅ Docker is ready${NC}"
echo ""

# Check if running with sudo (not recommended)
if [ "$EUID" -eq 0 ]; then
    echo -e "${RED}❌ Do not run this script with sudo${NC}"
    echo -e "${YELLOW}The script will handle Docker permissions automatically${NC}"
    echo -e "${YELLOW}Run without sudo: ./run-content-generation-output.sh${NC}"
    exit 1
fi

# Get user/group IDs for non-root container execution
USER_ID=$(id -u)
GROUP_ID=$(id -g)
echo -e "${BLUE}Building for user: ${USER_ID}:${GROUP_ID}${NC}"

# Parse command line arguments
BUILD_ONLY=false
NO_CACHE=false
INTERACTIVE=false
SKIP_BUILD=false

while [[ $# -gt 0 ]]; do
    case $1 in
        --build-only)
            BUILD_ONLY=true
            shift
            ;;
        --no-cache)
            NO_CACHE=true
            shift
            ;;
        --interactive|-i)
            INTERACTIVE=true
            shift
            ;;
        --skip-build)
            SKIP_BUILD=true
            shift
            ;;
        --help|-h)
            echo "Usage: ./run-content-generation-output.sh [OPTIONS]"
            echo ""
            echo "Description:"
            echo "  Generates documentation from existing CLI output files."
            echo "  Requires: generated/cli/cli-output.json"
            echo "            generated/cli/cli-namespace.json"
            echo "            generated/cli/cli-version.json"
            echo ""
            echo "Options:"
            echo "  --build-only      Build the Docker image without running"
            echo "  --skip-build      Skip building, use existing image"
            echo "  --no-cache        Build without using Docker cache"
            echo "  --interactive,-i  Start interactive shell in container"
            echo "  --help,-h         Show this help message"
            echo ""
            echo "Examples:"
            echo "  ./run-content-generation-output.sh              # Generate docs"
            echo "  ./run-content-generation-output.sh --skip-build # Use existing image"
            echo "  ./run-content-generation-output.sh --no-cache   # Rebuild from scratch"
            echo "  ./run-content-generation-output.sh -i           # Debug shell"
            echo ""
            echo "Prerequisites:"
            echo "  • Generate CLI output first: ./run-mcp-cli-output.sh"
            exit 0
            ;;
        *)
            echo -e "${RED}Unknown option: $1${NC}"
            echo "Use --help for usage information"
            exit 1
            ;;
    esac
done

# Validate that required CLI output files exist
echo -e "${BLUE}🔍 Validating CLI output files...${NC}"
CLI_OUTPUT_FILE="generated/cli/cli-output.json"
NAMESPACE_FILE="generated/cli/cli-namespace.json"
VERSION_FILE="generated/cli/cli-version.json"

if [ ! -f "$CLI_OUTPUT_FILE" ] || [ ! -f "$NAMESPACE_FILE" ] || [ ! -f "$VERSION_FILE" ]; then
    echo -e "${RED}❌ CLI output files not found${NC}"
    echo ""
    echo "Required files:"
    echo "  • $CLI_OUTPUT_FILE"
    echo "  • $NAMESPACE_FILE"
    echo "  • $VERSION_FILE"
    echo ""
    echo -e "${YELLOW}Run this first: ./run-mcp-cli-output.sh${NC}"
    exit 1
fi

# Additional validation: check file sizes
CLI_SIZE=$(wc -c < "$CLI_OUTPUT_FILE")
NS_SIZE=$(wc -c < "$NAMESPACE_FILE")
VER_SIZE=$(wc -c < "$VERSION_FILE")

if [ "$CLI_SIZE" -lt 100 ] || [ "$NS_SIZE" -lt 100 ] || [ "$VER_SIZE" -lt 50 ]; then
    echo -e "${RED}❌ CLI output files are too small (possibly empty)${NC}"
    echo ""
    echo "File sizes:"
    echo "  • $CLI_OUTPUT_FILE: $CLI_SIZE bytes"
    echo "  • $NAMESPACE_FILE: $NS_SIZE bytes"
    echo "  • $VERSION_FILE: $VER_SIZE bytes"
    echo ""
    echo -e "${YELLOW}Regenerate CLI files: ./run-mcp-cli-output.sh${NC}"
    exit 1
fi

echo -e "${GREEN}✅ CLI output files validated${NC}"
echo "  • cli-output.json: $(numfmt --to=iec-i --suffix=B $CLI_SIZE)"
echo "  • cli-namespace.json: $(numfmt --to=iec-i --suffix=B $NS_SIZE)"
echo "  • cli-version.json: $(numfmt --to=iec-i --suffix=B $VER_SIZE)"
echo ""

# Build the Docker image (unless skipped)
if [ "$SKIP_BUILD" = false ]; then
    echo -e "${BLUE}📦 Building Docker image...${NC}"
    echo ""

    # Remove existing image and build cache if --no-cache is specified
    if [ "$NO_CACHE" = true ]; then
        echo -e "${YELLOW}🗑️  Removing existing Docker image and build cache...${NC}"
        docker rmi azure-mcp-docgen:latest 2>/dev/null || echo -e "${CYAN}   (No existing image to remove)${NC}"
        docker builder prune -f --filter "label!=keep-cache" 2>/dev/null || true
        echo -e "${GREEN}✅ Cache cleared${NC}"
        echo ""
    fi

    BUILD_ARGS="--build-arg USER_ID=${USER_ID} --build-arg GROUP_ID=${GROUP_ID}"
    if [ "$NO_CACHE" = true ]; then
        BUILD_ARGS="${BUILD_ARGS} --no-cache"
    fi

    if docker build ${BUILD_ARGS} -t azure-mcp-docgen:latest -f docker/Dockerfile .; then
        echo ""
        echo -e "${GREEN}✅ Docker image built successfully${NC}"
        
        # Show image info
        IMAGE_SIZE=$(docker images azure-mcp-docgen:latest --format "{{.Size}}")
        echo -e "${CYAN}Image size: ${IMAGE_SIZE}${NC}"
    else
        echo ""
        echo -e "${RED}❌ Docker build failed${NC}"
        exit 1
    fi
else
    echo -e "${YELLOW}⏭️  Skipping build (--skip-build flag set)${NC}"
    
    # Verify image exists
    if ! docker image inspect azure-mcp-docgen:latest &>/dev/null; then
        echo -e "${RED}❌ Docker image 'azure-mcp-docgen:latest' not found${NC}"
        echo -e "${YELLOW}Run without --skip-build to build the image${NC}"
        exit 1
    fi
    echo -e "${GREEN}✅ Using existing Docker image${NC}"
    echo ""
fi

# Exit if build-only mode
if [ "$BUILD_ONLY" = true ]; then
    echo ""
    echo -e "${CYAN}Build-only mode: Skipping container run${NC}"
    exit 0
fi

# Clean previous documentation output (preserve CLI files)
echo -e "${YELLOW}🗑️  Cleaning previous documentation output...${NC}"
if [ -d "generated" ]; then
    # Preserve CLI directory, remove everything else
    find generated -mindepth 1 -maxdepth 1 ! -name 'cli' -exec rm -rf {} + 2>/dev/null || true
    echo -e "${GREEN}✅ Previous documentation output removed (CLI files preserved)${NC}"
else
    mkdir -p generated
    echo -e "${GREEN}✅ Output directory created${NC}"
fi

# Pre-create directories with proper permissions
mkdir -p generated/tools generated/example-prompts generated/logs
chmod -R u+rwX,go+rX generated/ 2>/dev/null || true
echo ""

# Interactive mode
if [ "$INTERACTIVE" = true ]; then
    echo -e "${BLUE}🔧 Starting interactive debug shell...${NC}"
    echo -e "${YELLOW}CLI files are mounted at: /output/cli${NC}"
    echo -e "${YELLOW}Run inside container: ${NC}pwsh ./Generate-MultiPageDocs.ps1"
    echo -e "${YELLOW}Exit with: ${NC}exit"
    echo ""
    docker run --rm -it \
        -v "$(pwd)/generated:/output" \
        --user "${USER_ID}:${GROUP_ID}" \
        --env SKIP_CLI_GENERATION="true" \
        --entrypoint /bin/bash \
        azure-mcp-docgen:latest
    exit 0
fi

# Load .env file if it exists
ENV_FILE="docs-generation/.env"
ENV_ARGS=""
if [ -f "$ENV_FILE" ]; then
    echo -e "${CYAN}📄 Loading credentials from $ENV_FILE${NC}"
    # Export variables from .env file
    while IFS='=' read -r key value; do
        # Skip comments and empty lines
        [[ $key =~ ^#.*$ ]] && continue
        [[ -z $key ]] && continue
        # Remove quotes and whitespace
        value=$(echo "$value" | sed -e 's/^"//' -e 's/"$//' -e 's/^[[:space:]]*//' -e 's/[[:space:]]*$//')
        ENV_ARGS="$ENV_ARGS --env $key=$value"
    done < "$ENV_FILE"
    echo -e "${GREEN}✅ Credentials loaded${NC}"
else
    echo -e "${YELLOW}⚠️  No .env file found at $ENV_FILE${NC}"
    echo -e "${YELLOW}   Example prompts will not be generated${NC}"
fi
echo ""

# Run the documentation generator
echo -e "${BLUE}📝 Running documentation generator...${NC}"
echo -e "${YELLOW}Output directory: $(pwd)/generated${NC}"
echo ""

if docker run --rm \
    -v "$(pwd)/generated:/output" \
    --user "${USER_ID}:${GROUP_ID}" \
    --env SKIP_CLI_GENERATION="true" \
    --env MCP_SERVER_PATH="/mcp/servers/Azure.Mcp.Server/src" \
    $ENV_ARGS \
    azure-mcp-docgen:latest; then
    
    echo ""
    echo -e "${GREEN}========================================${NC}"
    echo -e "${GREEN}✅ Documentation generated successfully!${NC}"
    echo -e "${GREEN}========================================${NC}"
    echo ""
    
    # Show summary of generated files
    if [ -d "generated/tools" ]; then
        FILE_COUNT=$(find generated/tools -name "*.md" -type f | wc -l)
        echo -e "${CYAN}📄 Generated ${FILE_COUNT} markdown files${NC}"
        echo ""
        echo -e "${CYAN}Output location:${NC}"
        echo "  $(pwd)/generated/"
        echo ""
        
        # Show first few files
        echo -e "${CYAN}Sample files:${NC}"
        find generated/tools -name "*.md" -type f | sort | head -5 | while read file; do
            SIZE=$(du -h "$file" | cut -f1)
            echo "  • $(basename "$file") (${SIZE})"
        done
        
        if [ $FILE_COUNT -gt 5 ]; then
            echo "  ... and $((FILE_COUNT - 5)) more files"
        fi
        
        echo ""
        echo -e "${GREEN}🎉 Ready to use!${NC}"
    else
        echo -e "${YELLOW}⚠️  No tools directory found${NC}"
        echo "Generated files:"
        ls -lh generated/ 2>/dev/null || echo "  (empty)"
    fi
    
else
    echo ""
    echo -e "${RED}❌ Documentation generation failed${NC}"
    echo ""
    echo "Troubleshooting:"
    echo "  1. Check Docker logs above for errors"
    echo "  2. Verify CLI files are valid: cat $VERSION_FILE"
    echo "  3. Try rebuilding: ./run-content-generation-output.sh --no-cache"
    echo "  4. Try interactive mode: ./run-content-generation-output.sh --interactive"
    echo "  5. Regenerate CLI files: ./run-mcp-cli-output.sh"
    echo "  6. Check available disk space"
    echo "  7. Ensure Docker has enough memory (8GB recommended)"
    exit 1
fi
