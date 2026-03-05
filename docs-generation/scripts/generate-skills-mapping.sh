#!/bin/bash
# generate-skills-mapping.sh — Bootstrap a skills-to-namespace mapping JSON
#
# Fuzzy-matches MCP namespace data (from brand-to-server-mapping.json)
# against Agent Skills directory names (from skills-source/).
# Produces a v1 mapping that needs manual curation afterwards.
#
# Usage:
#   ./generate-skills-mapping.sh [skills-source-dir] [output-file]
#   ./generate-skills-mapping.sh                                        # defaults
#   ./generate-skills-mapping.sh /path/to/skills /path/to/output.json   # custom
#
# Prerequisites: jq

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"
source "$SCRIPT_DIR/bash-common.sh"

# --- Inputs ---
BRAND_MAPPING="$SCRIPT_DIR/../data/brand-to-server-mapping.json"
SKILLS_DIR="${1:-$SCRIPT_DIR/../skills-source}"
OUTPUT_FILE="${2:-$SCRIPT_DIR/../data/skills-to-namespace-mapping.json}"

# --- Validate prerequisites ---
if ! command -v jq &>/dev/null; then
    echo "ERROR: jq is required but not found. Install it first." >&2
    exit 1
fi

if [[ ! -f "$BRAND_MAPPING" ]]; then
    echo "ERROR: brand-to-server-mapping.json not found at $BRAND_MAPPING" >&2
    exit 1
fi

if [[ ! -d "$SKILLS_DIR" ]]; then
    echo "ERROR: Skills source directory not found at $SKILLS_DIR" >&2
    echo "       Sync skills first, then re-run this script." >&2
    exit 1
fi

# --- Collect skill directory names ---
SKILL_NAMES=()
while IFS= read -r dir; do
    name="$(basename "$dir")"
    # Skip hidden directories
    if [[ "$name" == .* ]]; then continue; fi
    SKILL_NAMES+=("$name")
done < <(find "$SKILLS_DIR" -mindepth 1 -maxdepth 1 -type d | sort)

if [[ ${#SKILL_NAMES[@]} -eq 0 ]]; then
    echo "ERROR: No skill directories found in $SKILLS_DIR" >&2
    exit 1
fi

echo "Found ${#SKILL_NAMES[@]} skill directories in $(basename "$SKILLS_DIR")/"

# --- Build a lookup set for quick membership checks ---
# We track which skills have been claimed by at least one namespace
declare -A CLAIMED_SKILLS

# --- Read namespaces from brand mapping ---
NAMESPACE_COUNT=$(jq 'length' "$BRAND_MAPPING")
MATCHED_NS=0
TOTAL_MATCHED_SKILLS=0
UNMATCHED_NS_LIST=()

# Build the output JSON array incrementally
OUTPUT="["

for (( i=0; i<NAMESPACE_COUNT; i++ )); do
    MCP_NS=$(jq -r ".[$i].mcpServerName" "$BRAND_MAPPING")
    BRAND=$(jq -r ".[$i].brandName" "$BRAND_MAPPING")
    FILENAME=$(jq -r ".[$i].fileName" "$BRAND_MAPPING")

    MATCHES=()

    for skill in "${SKILL_NAMES[@]}"; do

        # 1. Exact match: fileName == skill directory name
        if [[ "$FILENAME" == "$skill" ]]; then
            MATCHES+=("$skill")
            CLAIMED_SKILLS["$skill"]=1
            continue
        fi

        # 2. Partial match: strip "azure-" prefix from both and compare substrings
        FILE_CORE="${FILENAME#azure-}"
        SKILL_CORE="${skill#azure-}"

        # Skip very short cores to avoid false positives (e.g., "get", "sql")
        if [[ ${#FILE_CORE} -le 2 ]] || [[ ${#SKILL_CORE} -le 2 ]]; then
            continue
        fi

        # Check if fileName core contains skill core or vice versa
        if [[ "$FILE_CORE" == *"$SKILL_CORE"* ]] || [[ "$SKILL_CORE" == *"$FILE_CORE"* ]]; then
            MATCHES+=("$skill")
            CLAIMED_SKILLS["$skill"]=1
        fi
    done

    # Build skills JSON array for this namespace
    SKILLS_JSON="[]"
    if [[ ${#MATCHES[@]} -gt 0 ]]; then
        SKILLS_JSON="["
        for (( m=0; m<${#MATCHES[@]}; m++ )); do
            if [[ $m -gt 0 ]]; then SKILLS_JSON+=","; fi
            REL="primary"
            # If multiple matches, first is primary, rest are related
            if [[ $m -gt 0 ]]; then REL="related"; fi
            SKILLS_JSON+=$(printf '{"name":"%s","relationship":"%s"}' "${MATCHES[$m]}" "$REL")
        done
        SKILLS_JSON+="]"
        MATCHED_NS=$(( MATCHED_NS + 1 ))
    else
        UNMATCHED_NS_LIST+=("$MCP_NS")
    fi

    # Append entry (add comma separator for all but first)
    if [[ $i -gt 0 ]]; then OUTPUT+=","; fi
    OUTPUT+=$(jq -n \
        --arg ns "$MCP_NS" \
        --arg brand "$BRAND" \
        --argjson skills "$SKILLS_JSON" \
        '{mcpNamespace: $ns, brandName: $brand, skills: $skills}')
done

# --- Collect unmatched skills into "other" category ---
OTHER_SKILLS=()
for skill in "${SKILL_NAMES[@]}"; do
    if [[ -z "${CLAIMED_SKILLS[$skill]+_}" ]]; then
        OTHER_SKILLS+=("$skill")
    fi
done

# Append the "other" entry
OTHER_JSON="[]"
if [[ ${#OTHER_SKILLS[@]} -gt 0 ]]; then
    OTHER_JSON="["
    for (( s=0; s<${#OTHER_SKILLS[@]}; s++ )); do
        if [[ $s -gt 0 ]]; then OTHER_JSON+=","; fi
        OTHER_JSON+=$(printf '{"name":"%s","relationship":"standalone"}' "${OTHER_SKILLS[$s]}")
    done
    OTHER_JSON+="]"
fi

OUTPUT+=","
OUTPUT+=$(jq -n \
    --argjson skills "$OTHER_JSON" \
    '{mcpNamespace: "other", brandName: "Other Azure Services", skills: $skills}')

OUTPUT+="]"

# --- Pretty-print and write output ---
mkdir -p "$(dirname "$OUTPUT_FILE")"
echo "$OUTPUT" | jq '.' > "$OUTPUT_FILE"

# --- Summary ---
echo ""
echo "=== Skills Mapping Summary ==="
echo "Matched ${#CLAIMED_SKILLS[@]} skills to $MATCHED_NS namespaces, ${#OTHER_SKILLS[@]} skills unmatched → Other"
echo "Output: $OUTPUT_FILE"

if [[ ${#UNMATCHED_NS_LIST[@]} -gt 0 ]]; then
    echo ""
    echo "⚠️  Unmatched namespaces (no skills found — review manually):"
    for ns in "${UNMATCHED_NS_LIST[@]}"; do
        echo "   - $ns"
    done
fi

if [[ ${#OTHER_SKILLS[@]} -gt 0 ]]; then
    echo ""
    echo "ℹ️  Unmatched skills placed in 'other' (review for possible namespace links):"
    for skill in "${OTHER_SKILLS[@]}"; do
        echo "   - $skill"
    done
fi

echo ""
echo "NOTE: This is a BOOTSTRAP mapping. Known tricky cases that need manual curation:"
echo "  - search → azure-cognitive-search (rebranded to AI Search)"
echo "  - compute → azure-virtual-machines (different naming)"
echo "  - storage → azure-blob-storage, azure-files, azure-table-storage, azure-queue-storage"
echo "  - kusto → azure-data-explorer"
echo "  - foundry → azure-microsoft-foundry"
echo "  - redis → azure-cache-redis AND azure-managed-redis"
echo ""
echo "Done."
