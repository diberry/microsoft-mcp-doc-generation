#!/bin/bash
# Compare documentation generation before and after TextTransformation integration
# Usage: ./compare-generation.sh [baseline-dir] [current-dir]

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Directories to compare
BASELINE_DIR="${1:-baseline-output}"
CURRENT_DIR="${2:-generated}"

echo -e "${BLUE}======================================${NC}"
echo -e "${BLUE}Documentation Generation Comparison${NC}"
echo -e "${BLUE}======================================${NC}"
echo ""
echo -e "Baseline: ${YELLOW}${BASELINE_DIR}${NC}"
echo -e "Current:  ${YELLOW}${CURRENT_DIR}${NC}"
echo ""

# Check if directories exist
if [ ! -d "$BASELINE_DIR" ]; then
    echo -e "${RED}Error: Baseline directory not found: $BASELINE_DIR${NC}"
    echo "Generate baseline first with:"
    echo "  pwsh ./Generate.ps1"
    echo "  mv generated baseline-output"
    exit 1
fi

if [ ! -d "$CURRENT_DIR" ]; then
    echo -e "${RED}Error: Current directory not found: $CURRENT_DIR${NC}"
    echo "Generate current output first with:"
    echo "  pwsh ./Generate.ps1"
    exit 1
fi

# Function to count files in directory
count_files() {
    local dir=$1
    if [ -d "$dir" ]; then
        find "$dir" -type f | wc -l
    else
        echo "0"
    fi
}

# Function to compare file lists
compare_file_lists() {
    local subdir=$1
    local baseline_path="$BASELINE_DIR/$subdir"
    local current_path="$CURRENT_DIR/$subdir"
    
    echo -e "${BLUE}--- $subdir ---${NC}"
    
    if [ ! -d "$baseline_path" ]; then
        echo -e "${YELLOW}  Baseline directory not found${NC}"
        return
    fi
    
    if [ ! -d "$current_path" ]; then
        echo -e "${YELLOW}  Current directory not found${NC}"
        return
    fi
    
    local baseline_count=$(count_files "$baseline_path")
    local current_count=$(count_files "$current_path")
    
    echo -e "  Baseline files: ${YELLOW}$baseline_count${NC}"
    echo -e "  Current files:  ${YELLOW}$current_count${NC}"
    
    # Create temp file lists
    local baseline_list=$(mktemp)
    local current_list=$(mktemp)
    
    (cd "$baseline_path" && find . -type f | sort) > "$baseline_list"
    (cd "$current_path" && find . -type f | sort) > "$current_list"
    
    # Files only in baseline
    local only_baseline=$(comm -23 "$baseline_list" "$current_list" | wc -l)
    # Files only in current
    local only_current=$(comm -13 "$baseline_list" "$current_list" | wc -l)
    # Files in both
    local in_both=$(comm -12 "$baseline_list" "$current_list" | wc -l)
    
    if [ $only_baseline -gt 0 ]; then
        echo -e "  ${RED}Only in baseline: $only_baseline files${NC}"
        if [ $only_baseline -le 10 ]; then
            comm -23 "$baseline_list" "$current_list" | sed 's/^/    /'
        fi
    fi
    
    if [ $only_current -gt 0 ]; then
        echo -e "  ${RED}Only in current: $only_current files${NC}"
        if [ $only_current -le 10 ]; then
            comm -13 "$baseline_list" "$current_list" | sed 's/^/    /'
        fi
    fi
    
    if [ $only_baseline -eq 0 ] && [ $only_current -eq 0 ]; then
        echo -e "  ${GREEN}✓ File lists match ($in_both files)${NC}"
    fi
    
    rm "$baseline_list" "$current_list"
    echo ""
}

# Function to compare file content
compare_content() {
    local subdir=$1
    local sample_size=${2:-5}
    local baseline_path="$BASELINE_DIR/$subdir"
    local current_path="$CURRENT_DIR/$subdir"
    
    if [ ! -d "$baseline_path" ] || [ ! -d "$current_path" ]; then
        return
    fi
    
    echo -e "${BLUE}--- Content comparison: $subdir (sampling $sample_size files) ---${NC}"
    
    local files=$(cd "$baseline_path" && find . -type f -name "*.md" | sort | head -n $sample_size)
    local identical=0
    local different=0
    
    while IFS= read -r file; do
        if [ -f "$baseline_path/$file" ] && [ -f "$current_path/$file" ]; then
            if diff -q "$baseline_path/$file" "$current_path/$file" > /dev/null 2>&1; then
                ((identical++))
                echo -e "  ${GREEN}✓${NC} $file"
            else
                ((different++))
                echo -e "  ${RED}✗${NC} $file"
                # Show brief diff summary
                local added=$(diff "$baseline_path/$file" "$current_path/$file" | grep "^>" | wc -l)
                local removed=$(diff "$baseline_path/$file" "$current_path/$file" | grep "^<" | wc -l)
                echo -e "    ${YELLOW}+$added -$removed lines${NC}"
            fi
        fi
    done <<< "$files"
    
    echo -e "  Identical: ${GREEN}$identical${NC}, Different: ${RED}$different${NC}"
    echo ""
}

# Compare directory structure
echo -e "${BLUE}======================================${NC}"
echo -e "${BLUE}File Count Comparison${NC}"
echo -e "${BLUE}======================================${NC}"
echo ""

compare_file_lists "annotations"
compare_file_lists "parameters"
compare_file_lists "param-and-annotation"
compare_file_lists "multi-page"
compare_file_lists "tools"
compare_file_lists "common-general"

# Compare content samples
echo -e "${BLUE}======================================${NC}"
echo -e "${BLUE}Content Sample Comparison${NC}"
echo -e "${BLUE}======================================${NC}"
echo ""

compare_content "annotations" 3
compare_content "parameters" 3
compare_content "multi-page" 3

# Check for specific transformations
echo -e "${BLUE}======================================${NC}"
echo -e "${BLUE}Transformation Validation${NC}"
echo -e "${BLUE}======================================${NC}"
echo ""

check_transformation() {
    local pattern=$1
    local description=$2
    local baseline_count=$(grep -ro "$pattern" "$BASELINE_DIR" 2>/dev/null | wc -l || echo "0")
    local current_count=$(grep -ro "$pattern" "$CURRENT_DIR" 2>/dev/null | wc -l || echo "0")
    
    echo -e "  $description:"
    echo -e "    Baseline: ${YELLOW}$baseline_count${NC} occurrences"
    echo -e "    Current:  ${YELLOW}$current_count${NC} occurrences"
    
    if [ "$baseline_count" -eq "$current_count" ]; then
        echo -e "    ${GREEN}✓ Match${NC}"
    else
        local diff=$((current_count - baseline_count))
        if [ $diff -gt 0 ]; then
            echo -e "    ${RED}✗ +$diff occurrences${NC}"
        else
            echo -e "    ${RED}✗ $diff occurrences${NC}"
        fi
    fi
    echo ""
}

check_transformation "subscription ID" "subscription ID transformation"
check_transformation "resource group" "resource group transformation"
check_transformation "tenant ID" "tenant ID transformation"

# Summary
echo -e "${BLUE}======================================${NC}"
echo -e "${BLUE}Summary${NC}"
echo -e "${BLUE}======================================${NC}"
echo ""

TOTAL_BASELINE=$(count_files "$BASELINE_DIR")
TOTAL_CURRENT=$(count_files "$CURRENT_DIR")

echo -e "Total files:"
echo -e "  Baseline: ${YELLOW}$TOTAL_BASELINE${NC}"
echo -e "  Current:  ${YELLOW}$TOTAL_CURRENT${NC}"
echo ""

if [ "$TOTAL_BASELINE" -eq "$TOTAL_CURRENT" ]; then
    echo -e "${GREEN}✓ File count matches!${NC}"
else
    DIFF_COUNT=$((TOTAL_CURRENT - TOTAL_BASELINE))
    if [ $DIFF_COUNT -gt 0 ]; then
        echo -e "${RED}✗ $DIFF_COUNT more files in current${NC}"
    else
        DIFF_ABS=${DIFF_COUNT#-}
        echo -e "${RED}✗ $DIFF_ABS fewer files in current${NC}"
    fi
fi

echo ""
echo -e "${BLUE}Done!${NC}"
echo ""
echo "For detailed file-by-file diff, run:"
echo "  diff -r $BASELINE_DIR $CURRENT_DIR"
