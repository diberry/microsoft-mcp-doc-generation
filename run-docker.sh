#!/bin/bash
set -e

# Azure MCP Documentation Generator - Quick Run Script
# This script makes it easy to generate documentation locally using Docker

# Set up logging
mkdir -p generated/logs
LOG_FILE="generated/logs/run-docker.log"
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

# Check if running with sudo (not recommended)
if [ "$EUID" -eq 0 ]; then
    echo -e "${RED}❌ Do not run this script with sudo${NC}"
    echo -e "${YELLOW}The script will handle Docker permissions automatically${NC}"
    echo -e "${YELLOW}Run without sudo: ./run-docker.sh${NC}"
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
SKIP_CLI_GENERATION=false

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
        --skip-cli-generation)
            SKIP_CLI_GENERATION=true
            shift
            ;;
        --help|-h)
            echo "Usage: ./run-docker.sh [OPTIONS]"
            echo ""
            echo "Options:"
            echo "  --build-only           Build the Docker image without running"
            echo "  --no-cache             Build without using Docker cache"
            echo "  --skip-cli-generation  Skip CLI output generation (requires existing files)"
            echo "  --interactive,-i       Start interactive shell in container"
            echo "  --help,-h              Show this help message"
            echo ""
            echo "Workflow:"
            echo "  By default, this script generates CLI output first, then documentation."
            echo "  Use --skip-cli-generation to skip CLI generation if files already exist."
            echo ""
            echo "Examples:"
            echo "  ./run-docker.sh                            # Generate CLI output + docs"
            echo "  ./run-docker.sh --skip-cli-generation      # Generate docs only (requires CLI files)"
            echo "  ./run-docker.sh --build-only               # Just build the image"
            echo "  ./run-docker.sh --no-cache                 # Rebuild from scratch"
            echo "  ./run-docker.sh --interactive              # Start debug shell"
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

# Exit if build-only mode
if [ "$BUILD_ONLY" = true ]; then
    echo ""
    echo -e "${CYAN}Build-only mode: Skipping container run${NC}"
    exit 0
fi

# Clean and create output directory (but preserve CLI files if skipping CLI generation)
echo ""
if [ "$SKIP_CLI_GENERATION" = false ]; then
    echo -e "${YELLOW}🗑️  Cleaning previous output...${NC}"
    if [ -d "generated" ]; then
        rm -rf generated 2>/dev/null || true
        echo -e "${GREEN}✅ Previous output removed${NC}"
    fi
    
    # Pre-create directories with proper permissions
    mkdir -p generated/cli generated/tools generated/example-prompts generated/logs
    chmod -R u+rwX,go+rX generated/ 2>/dev/null || true
    echo -e "${GREEN}✅ Output directory ready${NC}"
else
    echo -e "${YELLOW}🗑️  Cleaning previous documentation output (preserving CLI files)...${NC}"
    if [ -d "generated/multi-page" ]; then
        rm -rf generated/multi-page 2>/dev/null || true
    fi
    if [ -d "generated/tools" ]; then
        rm -rf generated/tools 2>/dev/null || true
    fi
    if [ -d "generated/example-prompts" ]; then
        rm -rf generated/example-prompts 2>/dev/null || true
    fi
    # Ensure directories exist
    mkdir -p generated/cli generated/tools generated/example-prompts generated/logs
    chmod -R u+rwX,go+rX generated/ 2>/dev/null || true
    echo -e "${GREEN}✅ Output directories ready (CLI files preserved)${NC}"
fi

# Interactive mode
if [ "$INTERACTIVE" = true ]; then
    echo ""
    echo -e "${BLUE}🔧 Starting interactive debug shell...${NC}"
    if [ "$SKIP_CLI_GENERATION" = true ]; then
        echo -e "${YELLOW}CLI generation will be skipped. If files are missing, run:${NC}"
        echo -e "${YELLOW}  pwsh docs-generation/Get-McpCliOutput.ps1 -OutputPath generated/cli${NC}"
    fi
    echo -e "${YELLOW}Run inside container: ${NC}pwsh ./Generate-MultiPageDocs.ps1"
    echo -e "${YELLOW}Exit with: ${NC}exit"
    echo ""
    docker run --rm -it \
        -v "$(pwd)/generated:/output" \
        --user "${USER_ID}:${GROUP_ID}" \
        --env SKIP_CLI_GENERATION="$SKIP_CLI_GENERATION" \
        --entrypoint /bin/bash \
        azure-mcp-docgen:latest
    exit 0
fi

# Step 1: Generate CLI output files (unless skipped)
if [ "$SKIP_CLI_GENERATION" = false ]; then
    echo ""
    echo -e "${BLUE}📝 Step 1: Generating MCP CLI output files...${NC}"
    echo ""
    
    # Run CLI output generation using npm-based approach
    # Export the npm project path so the PowerShell script can find it
    export NPM_PROJECT_PATH="$(pwd)/test-npm-azure-mcp"
    if pwsh docs-generation/Get-McpCliOutput.ps1 -OutputPath generated/cli; then
        echo -e "${GREEN}✅ CLI output files generated${NC}"
    else
        echo -e "${RED}❌ Failed to generate CLI output files${NC}"
        exit 1
    fi
else
    echo ""
    echo -e "${YELLOW}⏭️  Skipping CLI generation (--skip-cli-generation flag set)${NC}"
    echo ""
    
    # Validate that required files exist
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
        echo "Run: ./run-docker.sh to generate them"
        exit 1
    fi
    
    echo -e "${GREEN}✅ CLI output files found${NC}"
fi

# Step 2: Load .env file if it exists
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

# Step 3: Run the documentation generator
echo ""
echo -e "${BLUE}📝 Step 2: Running documentation generator...${NC}"
echo -e "${YELLOW}Output directory: $(pwd)/generated${NC}"
echo ""

if docker run --rm \
    -v "$(pwd)/generated:/output" \
    --user "${USER_ID}:${GROUP_ID}" \
    --env SKIP_CLI_GENERATION="true" \
    $ENV_ARGS \
    azure-mcp-docgen:latest; then
    
    echo ""
    echo -e "${GREEN}========================================${NC}"
    echo -e "${GREEN}✅ Documentation generated successfully!${NC}"
    echo -e "${GREEN}========================================${NC}"
    echo ""
    
    # Show summary of generated files
    if [ -d "generated/multi-page" ]; then
        FILE_COUNT=$(find generated/multi-page -name "*.md" -type f | wc -l)
        echo -e "${CYAN}📄 Generated ${FILE_COUNT} markdown files${NC}"
        echo ""
        echo -e "${CYAN}Output location:${NC}"
        echo "  $(pwd)/generated/"
        echo ""
        
        # Show first few files
        echo -e "${CYAN}Sample files:${NC}"
        find generated/multi-page -name "*.md" -type f | sort | head -5 | while read file; do
            SIZE=$(du -h "$file" | cut -f1)
            echo "  • $(basename "$file") (${SIZE})"
        done
        
        if [ $FILE_COUNT -gt 5 ]; then
            echo "  ... and $((FILE_COUNT - 5)) more files"
        fi
        
        echo ""
        echo -e "${GREEN}🎉 Ready to use!${NC}"
    else
        echo -e "${YELLOW}⚠️  No multi-page directory found${NC}"
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
