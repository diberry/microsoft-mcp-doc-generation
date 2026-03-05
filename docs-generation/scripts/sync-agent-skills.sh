#!/bin/bash
# Sync Agent Skills data from the MicrosoftDocs/Agent-Skills GitHub repository.
#
# Usage:
#   ./sync-agent-skills.sh [output-dir]
#   ./sync-agent-skills.sh                                   # Default: docs-generation/skills-source/
#   ./sync-agent-skills.sh /path/to/output                   # Custom output directory
#
# What it does:
#   - Downloads CATALOG.md and BUNDLES.md from the Agent-Skills repo
#   - Writes sync-metadata.json with timestamp and source repo
#
# Prerequisites:
#   - curl

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"
source "$SCRIPT_DIR/bash-common.sh"

SOURCE_REPO="MicrosoftDocs/Agent-Skills"
RAW_BASE_URL="https://raw.githubusercontent.com/$SOURCE_REPO/main"

# Determine output directory
OUTPUT_DIR="${1:-$ROOT_DIR/docs-generation/skills-source}"

echo "==================================================================="
echo "Sync Agent Skills"
echo "==================================================================="
echo ""
echo "Source: $SOURCE_REPO"
echo "Output: $OUTPUT_DIR"
echo ""

# Prepare output directory
mkdir -p "$OUTPUT_DIR"

# Download CATALOG.md
echo "Downloading CATALOG.md..."
if curl -sS -f -o "$OUTPUT_DIR/CATALOG.md" "$RAW_BASE_URL/docs/CATALOG.md"; then
    echo "✓ CATALOG.md downloaded"
else
    echo "⛔ Failed to download CATALOG.md"
    exit 1
fi

# Download BUNDLES.md
echo "Downloading BUNDLES.md..."
if curl -sS -f -o "$OUTPUT_DIR/BUNDLES.md" "$RAW_BASE_URL/docs/BUNDLES.md"; then
    echo "✓ BUNDLES.md downloaded"
else
    echo "⛔ Failed to download BUNDLES.md"
    exit 1
fi

# Write sync-metadata.json
SYNC_TIMESTAMP="$(date -u +"%Y-%m-%dT%H:%M:%SZ")"
cat > "$OUTPUT_DIR/sync-metadata.json" <<EOF
{
  "syncTimestamp": "$SYNC_TIMESTAMP",
  "sourceRepo": "$SOURCE_REPO",
  "files": ["CATALOG.md", "BUNDLES.md"]
}
EOF
echo "✓ Wrote sync-metadata.json"

echo ""
echo "✓ Agent Skills sync complete!"
