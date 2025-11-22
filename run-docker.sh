#!/bin/bash
set -e

# Azure MCP Documentation Generator - Quick Run Script
# This script makes it easy to generate documentation locally using Docker

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

echo -e "${CYAN}========================================${NC}"
echo -e "${CYAN}Azure MCP Documentation Generator${NC}"
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
INTERACTIVE=false

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
        --interactive|-i)
            INTERACTIVE=true
            shift
            ;;
        --help|-h)
            echo "Usage: ./run-docker.sh [OPTIONS]"
            echo ""
            echo "Options:"
            echo "  --build-only      Build the Docker image without running"
            echo "  --no-cache        Build without using Docker cache"
            echo "  --branch BRANCH   Use specific Microsoft/MCP branch (default: main)"
            echo "  --interactive,-i  Start interactive shell in container"
            echo "  --help,-h         Show this help message"
            echo ""
            echo "Examples:"
            echo "  ./run-docker.sh                           # Build and run"
            echo "  ./run-docker.sh --build-only              # Just build the image"
            echo "  ./run-docker.sh --no-cache                # Rebuild from scratch"
            echo "  ./run-docker.sh --branch feature-branch   # Use specific MCP branch"
            echo "  ./run-docker.sh --interactive             # Start debug shell"
            exit 0
            ;;
        *)
            echo -e "${RED}Unknown option: $1${NC}"
            echo "Use --help for usage information"
            exit 1
            ;;
    esac
done

# Build the Docker image
echo -e "${BLUE}📦 Building Docker image...${NC}"
echo -e "${YELLOW}MCP Branch: ${MCP_BRANCH}${NC}"
echo ""

BUILD_ARGS="--build-arg MCP_BRANCH=${MCP_BRANCH}"
if [ "$NO_CACHE" = true ]; then
    BUILD_ARGS="${BUILD_ARGS} --no-cache"
fi

if docker build ${BUILD_ARGS} -t azure-mcp-docgen:latest .; then
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

# Exit if build-only mode
if [ "$BUILD_ONLY" = true ]; then
    echo ""
    echo -e "${CYAN}Build-only mode: Skipping container run${NC}"
    exit 0
fi

# Create output directory
mkdir -p generated

# Interactive mode
if [ "$INTERACTIVE" = true ]; then
    echo ""
    echo -e "${BLUE}🔧 Starting interactive debug shell...${NC}"
    echo -e "${YELLOW}Run inside container: ${NC}pwsh ./Generate-MultiPageDocs.ps1"
    echo -e "${YELLOW}Exit with: ${NC}exit"
    echo ""
    docker run --rm -it \
        -v "$(pwd)/generated:/output" \
        --entrypoint /bin/bash \
        azure-mcp-docgen:latest
    exit 0
fi

# Run the documentation generator
echo ""
echo -e "${BLUE}📝 Running documentation generator...${NC}"
echo -e "${YELLOW}Output directory: $(pwd)/generated${NC}"
echo ""

if docker run --rm \
    -v "$(pwd)/generated:/output" \
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
    echo "  2. Try rebuilding: ./run-docker.sh --no-cache"
    echo "  3. Try interactive mode: ./run-docker.sh --interactive"
    echo "  4. Check available disk space"
    echo "  5. Ensure Docker has enough memory (8GB recommended)"
    exit 1
fi
