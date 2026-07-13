#!/bin/bash
# merge-namespaces-smoke.sh — Smoke test for the SHIPPING merge-namespaces.sh (#706).
#
# merge-namespaces.sh (bash + inline-Node) is the code that actually runs in
# production during start.sh, but it had zero automated coverage — the typed
# NamespaceMerger.cs twin is unit-tested but not wired into the pipeline. This
# test locks the shipping merge behavior directly against the AD-011 rules for
# BOTH the canonical (plain) and the -cli tab variant:
#   * primary frontmatter/overview/related content is kept
#   * every member's tool H2 sections are concatenated in mergeOrder
#   * tool_count is updated to the merged total
#   * CLI tab markers (#### [Azure MCP CLI] / #### [MCP Server]) are preserved
#
# Self-contained: builds an isolated fixture under mktemp and drives the real
# script through the MERGE_ROOT_DIR / MERGE_BRAND_MAP test seams. Requires bash
# + node (both present on the ubuntu-latest CI runner used by pester-tests).
#
# Exit code 0 = pass; non-zero = a failed assertion (message on stderr).

set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
MERGE_SCRIPT="$REPO_ROOT/merge-namespaces.sh"

fail() { echo "SMOKE FAIL: $*" >&2; exit 1; }

[[ -f "$MERGE_SCRIPT" ]] || fail "merge-namespaces.sh not found at $MERGE_SCRIPT"
command -v node >/dev/null 2>&1 || fail "node is required but not installed"

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

# ── Brand mapping with one merge group: alpha (primary) + beta (secondary) ──
cat > "$TMP/brand-map.json" <<'JSON'
[
  { "mcpServerName": "alpha", "fileName": "azure-alpha", "mergeGroup": "smoke-group", "mergeOrder": 1, "mergeRole": "primary" },
  { "mcpServerName": "beta",  "fileName": "azure-beta",  "mergeGroup": "smoke-group", "mergeOrder": 2, "mergeRole": "secondary" }
]
JSON

mkdir -p "$TMP/generated-alpha/tool-family" "$TMP/generated-beta/tool-family"

# ── Canonical (plain) variant fixtures ──
cat > "$TMP/generated-alpha/tool-family/azure-alpha.md" <<'MD'
---
title: Alpha tools
tool_count: 1
---
# Alpha tools

Overview paragraph for alpha.

## Alpha tool one
<!-- @mcpcli alpha one -->
Alpha tool one body.

## Related content

- Alpha related link
MD

cat > "$TMP/generated-beta/tool-family/azure-beta.md" <<'MD'
---
title: Beta tools
tool_count: 2
---
# Beta tools

Overview paragraph for beta.

## Beta tool one
<!-- @mcpcli beta one -->
Beta tool one body.

## Beta tool two
<!-- @mcpcli beta two -->
Beta tool two body.

## Related content

- Beta related link
MD

# ── CLI-tab variant fixtures (tab markers must survive the merge) ──
cat > "$TMP/generated-alpha/tool-family/azure-alpha-cli.md" <<'MD'
---
title: Alpha tools
tool_count: 1
---
# Alpha tools

Overview paragraph for alpha.

## Alpha tool one
<!-- @mcpcli alpha one -->
#### [Azure MCP CLI](#tab/azure-mcp-cli)
alpha cli content
#### [MCP Server](#tab/mcp-server)
alpha mcp content

## Related content

- Alpha related link
MD

cat > "$TMP/generated-beta/tool-family/azure-beta-cli.md" <<'MD'
---
title: Beta tools
tool_count: 2
---
# Beta tools

Overview paragraph for beta.

## Beta tool one
<!-- @mcpcli beta one -->
#### [Azure MCP CLI](#tab/azure-mcp-cli)
beta one cli
#### [MCP Server](#tab/mcp-server)
beta one mcp

## Beta tool two
<!-- @mcpcli beta two -->
#### [Azure MCP CLI](#tab/azure-mcp-cli)
beta two cli
#### [MCP Server](#tab/mcp-server)
beta two mcp

## Related content

- Beta related link
MD

# ── Run the SHIPPING script against the isolated fixture ──
MERGE_ROOT_DIR="$TMP" MERGE_BRAND_MAP="$TMP/brand-map.json" bash "$MERGE_SCRIPT" \
    || fail "merge-namespaces.sh exited non-zero"

CANON="$TMP/generated-alpha/tool-family/azure-alpha.md"
CLI="$TMP/generated-alpha/tool-family/azure-alpha-cli.md"

[[ -f "$CANON" ]] || fail "merged canonical article not written: $CANON"
[[ -f "$CLI" ]]   || fail "merged -cli article not written: $CLI"

# ── Assertion helpers ──
assert_contains()     { grep -qF -- "$2" "$1" || fail "$3 (expected to find: $2)"; }
assert_not_contains() { grep -qF -- "$2" "$1" && fail "$3 (did not expect: $2)"; return 0; }
line_of()             { grep -nF -- "$2" "$1" | head -1 | cut -d: -f1; }
assert_before() {
    local file="$1" first="$2" second="$3" msg="$4"
    local a b
    a="$(line_of "$file" "$first")"; b="$(line_of "$file" "$second")"
    [[ -n "$a" && -n "$b" && "$a" -lt "$b" ]] || fail "$msg ('$first' at ${a:-none}, '$second' at ${b:-none})"
}
assert_count() {
    local file="$1" pat="$2" want="$3" msg="$4" got
    got="$(grep -cF -- "$pat" "$file")"
    [[ "$got" == "$want" ]] || fail "$msg (found $got, expected $want of: $pat)"
}

# ── Canonical variant: AD-011 rules ──
assert_contains "$CANON" "tool_count: 3" "canonical tool_count not updated to merged total"
assert_contains "$CANON" "# Alpha tools" "canonical missing primary H1"
assert_contains "$CANON" "Overview paragraph for alpha." "canonical missing primary overview"
assert_contains "$CANON" "## Alpha tool one" "canonical missing primary tool section"
assert_contains "$CANON" "## Beta tool one" "canonical missing secondary tool one"
assert_contains "$CANON" "## Beta tool two" "canonical missing secondary tool two"
assert_before   "$CANON" "## Alpha tool one" "## Beta tool one" "canonical tool order wrong (primary must precede secondary)"
assert_before   "$CANON" "## Beta tool one" "## Beta tool two" "canonical secondary tools out of order"
assert_contains "$CANON" "- Alpha related link" "canonical missing primary related content"
assert_not_contains "$CANON" "- Beta related link" "canonical must use primary related content only"
assert_not_contains "$CANON" "Overview paragraph for beta." "canonical must not include secondary overview"

# ── CLI-tab variant: same rules + tab markers preserved ──
assert_contains "$CLI" "tool_count: 3" "-cli tool_count not updated to merged total"
assert_contains "$CLI" "## Alpha tool one" "-cli missing primary tool section"
assert_contains "$CLI" "## Beta tool one" "-cli missing secondary tool one"
assert_contains "$CLI" "## Beta tool two" "-cli missing secondary tool two"
assert_before   "$CLI" "## Alpha tool one" "## Beta tool one" "-cli tool order wrong"
assert_count    "$CLI" "#### [Azure MCP CLI](#tab/azure-mcp-cli)" 3 "-cli lost Azure MCP CLI tab markers"
assert_count    "$CLI" "#### [MCP Server](#tab/mcp-server)" 3 "-cli lost MCP Server tab markers"
assert_contains "$CLI" "- Alpha related link" "-cli missing primary related content"
assert_not_contains "$CLI" "- Beta related link" "-cli must use primary related content only"

echo "SMOKE PASS: merge-namespaces.sh merges canonical + -cli variants per AD-011"
exit 0
