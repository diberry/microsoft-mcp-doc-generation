#!/bin/bash
set -e

# Azure MCP CLI Output Generator - Docker Wrapper Script
# Builds and runs container to generate CLI output files for documentation

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

# Parse command line arguments
BUILD_ONLY=false
NO_CACHE=false
MCP_BRANCH="main"
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
        --branch)
            MCP_BRANCH="$2"
            shift 2
            ;;
        --skip-build)
            SKIP_BUILD=true
            shift
            ;;
        --help|-h)
            echo "Usage: ./run-mcp-cli-output.sh [OPTIONS]"
            echo ""
            echo "Options:"
            echo "  --build-only      Build the Docker image without running"
            echo "  --no-cache        Build without using Docker cache"
            echo "  --branch BRANCH   Use specific Microsoft/MCP branch (default: main)"
            echo "  --skip-build      Skip building, use existing image"
            echo "  --help,-h         Show this help message"
            echo ""
            echo "Examples:"
            echo "  ./run-mcp-cli-output.sh                    # Build and generate CLI output"
            echo "  ./run-mcp-cli-output.sh --build-only       # Just build the image"
            echo "  ./run-mcp-cli-output.sh --no-cache         # Rebuild from scratch"
            echo "  ./run-mcp-cli-output.sh --branch feature   # Use specific MCP branch"
            echo "  ./run-mcp-cli-output.sh --skip-build       # Use existing image"
            exit 0
            ;;
        *)
            echo -e "${RED}Unknown option: $1${NC}"
            echo "Use --help for usage information"
            exit 1
            ;;
    esac
done

# Build or check for Docker image
IMAGE_NAME="azure-mcp-cli-output:latest"

if [ "$SKIP_BUILD" = false ]; then
    echo -e "${BLUE}📦 Building Docker image...${NC}"
    echo -e "${YELLOW}MCP Branch: ${MCP_BRANCH}${NC}"
    echo ""
    
    # Remove existing image if --no-cache is specified
    if [ "$NO_CACHE" = true ]; then
        echo -e "${YELLOW}🗑️  Removing existing Docker image...${NC}"
        docker rmi $IMAGE_NAME 2>/dev/null || echo -e "${CYAN}   (No existing image to remove)${NC}"
        echo ""
    fi
    
    BUILD_ARGS="--build-arg MCP_BRANCH=${MCP_BRANCH}"
    if [ "$NO_CACHE" = true ]; then
        BUILD_ARGS="${BUILD_ARGS} --no-cache"
    fi
    
    if docker build ${BUILD_ARGS} -t $IMAGE_NAME -f Dockerfile.mcp-cli-output .; then
        echo ""
        echo -e "${GREEN}✅ Docker image built successfully${NC}"
        
        # Show image info
        IMAGE_SIZE=$(docker images $IMAGE_NAME --format "{{.Size}}")
        echo -e "${CYAN}Image size: ${IMAGE_SIZE}${NC}"
    else
        echo ""
        echo -e "${RED}❌ Docker build failed${NC}"
        exit 1
    fi
else
    # Check if image exists
    if ! docker image inspect $IMAGE_NAME >/dev/null 2>&1; then
        echo -e "${RED}❌ Image $IMAGE_NAME not found${NC}"
        echo "Run without --skip-build to build the image first"
        exit 1
    fi
    echo -e "${GREEN}✅ Using existing Docker image: $IMAGE_NAME${NC}"
fi

# Exit if build-only mode
if [ "$BUILD_ONLY" = true ]; then
    echo ""
    echo -e "${CYAN}Build-only mode: Skipping CLI output generation${NC}"
    exit 0
fi

# Create output directory
echo ""
echo -e "${YELLOW}📁 Preparing output directory...${NC}"
mkdir -p generated/cli
echo -e "${GREEN}✅ Output directory ready: ./generated/cli${NC}"

# Run the CLI output generator
echo ""
echo -e "${BLUE}📝 Generating MCP CLI output files...${NC}"
echo -e "${YELLOW}Output directory: $(pwd)/generated/cli${NC}"
echo ""

if docker run --rm \
    -v "$(pwd)/generated/cli:/output/cli" \
    $IMAGE_NAME; then
    
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
            BRANCH=$(jq -r '.mcpBranch' "$VERSION_FILE" 2>/dev/null || echo "unknown")
            
            echo -e "${CYAN}Version Information:${NC}"
            echo "  • CLI Version: $VERSION"
            echo "  • MCP Branch: $BRANCH"
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
    echo "  1. Check Docker logs above for errors"
    echo "  2. Try rebuilding: ./run-mcp-cli-output.sh --no-cache"
    echo "  3. Verify Docker has enough memory (4GB recommended)"
    echo "  4. Check available disk space"
    exit 1
fi
