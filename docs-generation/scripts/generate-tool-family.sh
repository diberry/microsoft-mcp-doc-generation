#!/bin/bash
# Wrapper script to call Generate-ToolFamily.ps1 from bash
# This allows you to use bash syntax while invoking the PowerShell script

# Example usage:
#   ./generate-tool-family.sh advisor 1
#   ./generate-tool-family.sh advisor 1,2
#   ./generate-tool-family.sh advisor 1,2,3

if [ $# -lt 1 ]; then
    echo "Usage: $0 <ToolFamily> [steps] [output-dir]"
    echo ""
    echo "Examples:"
    echo "  $0 advisor                       # Run all steps"
    echo "  $0 advisor 1                     # Run only step 1"
    echo "  $0 advisor 1,2                   # Run steps 1 and 2"
    echo "  $0 advisor 1,2,3                 # Run steps 1, 2, and 3"
    echo "  $0 storage 1,3,5                 # Run steps 1, 3, and 5"
    echo "  $0 advisor 1,2,3 /path/to/output # Custom output directory"
    exit 1
fi

# Detect OS: Windows Git Bash (MSYS/MINGW/CYGWIN) adds \r to command output
IS_WINDOWS=false
case "$(uname -s)" in
    MINGW*|MSYS*|CYGWIN*) IS_WINDOWS=true ;;
esac

# Strip \r from string on Windows, no-op on Unix
strip_cr() {
    if $IS_WINDOWS; then
        tr -d '\r'
    else
        cat
    fi
}

TOOL_FAMILY="$(echo "$1" | strip_cr)"
STEPS="${2:-1,2,3,4,5}"
OUTPUT_DIR="${3:-}"

# Convert comma-separated steps to PowerShell array syntax
STEPS_ARRAY="@($(echo $STEPS | tr ',' ','))"

# Get the directory where this script is located
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Build PowerShell command with optional OutputPath
if [[ -n "$OUTPUT_DIR" ]]; then
    # Run the PowerShell script with SkipBuild and custom OutputPath
    pwsh -File "$SCRIPT_DIR/Generate-ToolFamily.ps1" -ToolFamily "$TOOL_FAMILY" -Steps "$STEPS" -SkipBuild -OutputPath "$OUTPUT_DIR"
else
    # Run the PowerShell script with SkipBuild (build should be done by orchestrator)
    # Use -File instead of -Command to avoid Unix-style path issues in Git Bash on Windows
    pwsh -File "$SCRIPT_DIR/Generate-ToolFamily.ps1" -ToolFamily "$TOOL_FAMILY" -Steps "$STEPS" -SkipBuild
fi
