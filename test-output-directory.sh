#!/bin/bash
# Integration test for output directory naming based on namespace parameter
# Tests the feature where start.sh uses generated-<namespace> when a specific namespace is provided
#
# This test verifies that:
# 1. start.sh sets OUTPUT_DIR based on namespace parameter
# 2. start-only.sh accepts and uses the output directory parameter
# 3. generate-tool-family.sh passes the output directory to PowerShell
# 4. All output messages use the dynamic directory path
#
# Usage:
#   bash test-output-directory.sh

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Colors for output
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo "=================================================================="
echo "Integration Test: Output Directory Naming"
echo "=================================================================="
echo ""

# Track test results
TESTS_PASSED=0
TESTS_FAILED=0

# Helper function to check if directory contains expected structure
check_output_directory() {
    local dir="$1"
    local namespace="$2"
    
    echo "  Checking directory: $dir"
    
    # Check if base directory exists
    if [[ ! -d "$dir" ]]; then
        echo -e "  ${RED}✗ FAIL: Directory does not exist: $dir${NC}"
        return 1
    fi
    
    # Check if CLI metadata exists
    if [[ ! -f "$dir/cli/cli-version.json" ]]; then
        echo -e "  ${YELLOW}⚠ WARNING: CLI version file missing (expected for Step 1 test)${NC}"
    fi
    
    # Check if tool-family directory exists
    if [[ ! -d "$dir/tool-family" ]]; then
        echo -e "  ${YELLOW}⚠ WARNING: tool-family directory missing (expected for Step 1 test)${NC}"
    fi
    
    echo -e "  ${GREEN}✓ PASS: Directory structure looks correct${NC}"
    return 0
}

# Test 1: Verify start.sh script has OUTPUT_DIR logic
echo "Test 1: Verify OUTPUT_DIR logic exists in start.sh"
if grep -q "OUTPUT_DIR=" "$ROOT_DIR/start.sh"; then
    if grep -q "generated-\$NAMESPACE_ARG" "$ROOT_DIR/start.sh"; then
        echo -e "${GREEN}✓ PASS: start.sh contains OUTPUT_DIR logic with namespace suffix${NC}"
        TESTS_PASSED=$((TESTS_PASSED + 1))
    else
        echo -e "${RED}✗ FAIL: start.sh missing namespace suffix logic${NC}"
        TESTS_FAILED=$((TESTS_FAILED + 1))
    fi
else
    echo -e "${RED}✗ FAIL: start.sh missing OUTPUT_DIR variable${NC}"
    TESTS_FAILED=$((TESTS_FAILED + 1))
fi
echo ""

# Test 2: Verify start-only.sh accepts output directory parameter
echo "Test 2: Verify start-only.sh accepts OUTPUT_DIR parameter"
if grep -q 'OUTPUT_DIR="\${3:-' "$ROOT_DIR/docs-generation/scripts/start-only.sh"; then
    echo -e "${GREEN}✓ PASS: start-only.sh accepts OUTPUT_DIR as third parameter${NC}"
    TESTS_PASSED=$((TESTS_PASSED + 1))
else
    echo -e "${RED}✗ FAIL: start-only.sh does not accept OUTPUT_DIR parameter${NC}"
    TESTS_FAILED=$((TESTS_FAILED + 1))
fi
echo ""

# Test 3: Verify generate-tool-family.sh passes output directory
echo "Test 3: Verify generate-tool-family.sh handles OUTPUT_DIR"
if grep -q 'OUTPUT_DIR="\${3:-' "$ROOT_DIR/docs-generation/scripts/generate-tool-family.sh"; then
    if grep -q "OutputPath.*OUTPUT_DIR" "$ROOT_DIR/docs-generation/scripts/generate-tool-family.sh"; then
        echo -e "${GREEN}✓ PASS: generate-tool-family.sh accepts and uses OUTPUT_DIR${NC}"
        TESTS_PASSED=$((TESTS_PASSED + 1))
    else
        echo -e "${RED}✗ FAIL: generate-tool-family.sh doesn't pass OUTPUT_DIR to PowerShell${NC}"
        TESTS_FAILED=$((TESTS_FAILED + 1))
    fi
else
    echo -e "${RED}✗ FAIL: generate-tool-family.sh doesn't accept OUTPUT_DIR parameter${NC}"
    TESTS_FAILED=$((TESTS_FAILED + 1))
fi
echo ""

# Test 4: Verify output message uses dynamic directory
echo "Test 4: Verify start.sh output message uses OUTPUT_DIR"
if grep -q '\$OUTPUT_DIR/tool-family/' "$ROOT_DIR/start.sh"; then
    echo -e "${GREEN}✓ PASS: start.sh output message uses dynamic OUTPUT_DIR${NC}"
    TESTS_PASSED=$((TESTS_PASSED + 1))
else
    echo -e "${RED}✗ FAIL: start.sh output message doesn't use OUTPUT_DIR${NC}"
    TESTS_FAILED=$((TESTS_FAILED + 1))
fi
echo ""

# Summary
echo "=================================================================="
echo "Test Summary"
echo "=================================================================="
echo -e "Tests passed: ${GREEN}$TESTS_PASSED${NC}"
echo -e "Tests failed: ${RED}$TESTS_FAILED${NC}"
echo ""

if [[ $TESTS_FAILED -eq 0 ]]; then
    echo -e "${GREEN}✓ All tests passed!${NC}"
    exit 0
else
    echo -e "${RED}✗ Some tests failed${NC}"
    exit 1
fi
