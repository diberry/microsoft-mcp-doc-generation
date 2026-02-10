#!/bin/bash
# Azure MCP Documentation Generator
# Single consolidated CLI for all documentation generation tasks

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# Helper functions
function print_info() { echo -e "${CYAN}INFO: $1${NC}"; }
function print_success() { echo -e "${GREEN}SUCCESS: $1${NC}"; }
function print_warning() { echo -e "${YELLOW}WARNING: $1${NC}"; }
function print_error() { echo -e "${RED}ERROR: $1${NC}"; }

function show_help() {
    cat << 'EOF'
Azure MCP Documentation Generator
==================================

USAGE:
    ./generate.sh <command> [options]

COMMANDS:
    reports               Generate CLI analysis report and common-general files
                         This runs for every command automatically
    
    tool <name>          Generate all files for a single tool
                         Example: ./generate.sh tool "keyvault get"
    
    family <namespace>   Generate all files for a tool family/namespace
                         Example: ./generate.sh family keyvault
                         Example: ./generate.sh family storage
    
    all                  Generate files for all tool families
                         This is a full documentation generation run
    
    help                 Show this help message

OPTIONS:
    --no-clean          Don't clean the generated/ directory before running
    --steps <list>      Run only specific steps (for 'family' command)
                       Example: --steps 1,2,3
    
EXAMPLES:
    # Full documentation generation (all tools)
    ./generate.sh all

    # Generate documentation for a single tool family
    ./generate.sh family keyvault

    # Generate documentation for a single tool family with specific steps
    ./generate.sh family storage --steps 1,2,3

    # Generate only reports (CLI analysis)
    ./generate.sh reports

    # Keep existing generated files and add to them
    ./generate.sh family advisor --no-clean

WORKFLOW:
    Every command automatically:
    1. Generates CLI metadata (npm run commands)
    2. Creates CLI analysis report (reports/cli-analysis-report.md)
    3. Generates common-general files
    4. Then executes the specific command requested

PREREQUISITES:
    - Node.js + npm (for MCP CLI metadata)
    - PowerShell (pwsh)
    - .NET SDK (for generator projects)
    - Azure OpenAI env vars in docs-generation/.env (for AI steps)

OUTPUT:
    All generated files are written to: ./generated/

MORE INFO:
    See README.md and docs-generation/README.md for detailed information

EOF
}

function setup_directories() {
    print_info "Setting up output directories..."
    mkdir -p "$ROOT_DIR/generated/cli"
    mkdir -p "$ROOT_DIR/generated/common-general"
    mkdir -p "$ROOT_DIR/generated/tools"
    mkdir -p "$ROOT_DIR/generated/example-prompts"
    mkdir -p "$ROOT_DIR/generated/annotations"
    mkdir -p "$ROOT_DIR/generated/parameters"
    mkdir -p "$ROOT_DIR/generated/logs"
    mkdir -p "$ROOT_DIR/generated/reports"
    print_success "✓ Output directories ready"
}

function generate_cli_metadata() {
    print_info "Generating CLI metadata..."
    cd "$ROOT_DIR/test-npm-azure-mcp"
    npm install
    npm run --silent get:version > "$ROOT_DIR/generated/cli/cli-version.json"
    npm run --silent get:tools-json > "$ROOT_DIR/generated/cli/cli-output.json"
    npm run --silent get:tools-namespace > "$ROOT_DIR/generated/cli/cli-namespace.json"
    print_success "✓ Generated CLI metadata"
    cd "$ROOT_DIR"
}

function generate_reports() {
    print_info "Generating CLI analysis report and common files..."
    cd "$ROOT_DIR/docs-generation"
    
    # Generate CLI analyzer report
    pwsh -Command './scripts/Invoke-CliAnalyzer.ps1 -OutputPath "../generated" -HtmlOnly $true'
    
    # Generate common-general files
    pwsh ./scripts/Generate-Common.ps1 -OutputPath "../generated"
    
    print_success "✓ Generated reports and common files"
    cd "$ROOT_DIR"
}

function generate_tool() {
    local tool_name="$1"
    print_info "Generating documentation for tool: $tool_name"
    
    # TODO: Implement single tool generation
    # This would need a new PowerShell script or modification to existing ones
    print_error "Single tool generation not yet implemented"
    print_info "Use 'family' command to generate all tools in a namespace"
    exit 1
}

function generate_family() {
    local namespace="$1"
    local steps="${2:-1,2,3,4,5}"
    
    print_info "Generating documentation for tool family: $namespace (steps: $steps)"
    cd "$ROOT_DIR/docs-generation"
    ./generate-tool-family.sh "$namespace" "$steps"
    print_success "✓ Generated tool family: $namespace"
    cd "$ROOT_DIR"
}

function generate_all() {
    print_info "Generating documentation for all tool families..."
    cd "$ROOT_DIR/docs-generation"
    pwsh ./Generate.ps1 -OutputPath "../generated"
    cd "$ROOT_DIR"
    
    # Run verification and summary
    print_info "Verifying generated documentation..."
    cd "$ROOT_DIR/verify-quantity"
    npm install
    node index.js > "$ROOT_DIR/verify.md"
    cd "$ROOT_DIR"
    
    print_info "Generating summary..."
    cd "$ROOT_DIR/summary-generator"
    npm install
    node generate-summary.js
    cd "$ROOT_DIR"
    
    print_success "✓ Generated all documentation"
}

# Parse arguments
COMMAND=""
NO_CLEAN=false
STEPS="1,2,3,4,5"
TARGET=""

if [[ $# -eq 0 ]]; then
    show_help
    exit 0
fi

while [[ $# -gt 0 ]]; do
    case $1 in
        help|--help|-h)
            show_help
            exit 0
            ;;
        reports|tool|family|all)
            COMMAND="$1"
            shift
            if [[ "$COMMAND" == "tool" || "$COMMAND" == "family" ]] && [[ $# -gt 0 ]] && [[ ! "$1" =~ ^-- ]]; then
                TARGET="$1"
                shift
            fi
            ;;
        --no-clean)
            NO_CLEAN=true
            shift
            ;;
        --steps)
            if [[ $# -lt 2 ]]; then
                print_error "Missing value for --steps"
                exit 1
            fi
            STEPS="$2"
            shift 2
            ;;
        *)
            print_error "Unknown option: $1"
            echo ""
            show_help
            exit 1
            ;;
    esac
done

# Validate command
if [[ -z "$COMMAND" ]]; then
    print_error "No command specified"
    echo ""
    show_help
    exit 1
fi

# Validate target for tool and family commands
if [[ "$COMMAND" == "tool" || "$COMMAND" == "family" ]] && [[ -z "$TARGET" ]]; then
    print_error "Missing target for '$COMMAND' command"
    echo ""
    show_help
    exit 1
fi

# Main execution
echo "========================================"
echo "Azure MCP Documentation Generator"
echo "========================================"
echo ""

# Clean up if requested
if [[ "$NO_CLEAN" == false ]]; then
    print_info "Cleaning generated/ directory..."
    rm -rf "$ROOT_DIR/generated/"
    print_success "✓ Cleaned generated/ directory"
fi

# Setup directories
setup_directories

# Generate CLI metadata (always)
generate_cli_metadata

# Generate reports and common files (always)
generate_reports

# Execute the specific command
case $COMMAND in
    reports)
        print_success "✓ Reports generated successfully"
        print_info "Output: $ROOT_DIR/generated/reports/cli-analysis-report.md"
        ;;
    tool)
        generate_tool "$TARGET"
        ;;
    family)
        generate_family "$TARGET" "$STEPS"
        ;;
    all)
        generate_all
        ;;
esac

echo ""
print_success "==============================================="
print_success "Documentation generation completed successfully!"
print_success "==============================================="
print_info "Output location: $ROOT_DIR/generated/"
