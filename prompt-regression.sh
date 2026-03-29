#!/usr/bin/env bash
# Prompt Regression Testing Runner
# Seeds baselines, regenerates output, and runs regression comparison.
#
# Usage:
#   ./prompt-regression.sh seed              # Seed baselines from current output
#   ./prompt-regression.sh test [step]       # Regenerate and compare against baselines
#   ./prompt-regression.sh report            # Show last regression report
#
# Representative namespaces (small → large, all with tool-family + horizontal-articles):
#   applens, cloudarchitect, deploy, compute, fileshares

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
BASELINES_DIR="$REPO_ROOT/docs-generation/DocGeneration.PromptRegression.Tests/Baselines"
CANDIDATES_DIR="$REPO_ROOT/docs-generation/DocGeneration.PromptRegression.Tests/Candidates"
REPORTS_DIR="$REPO_ROOT/docs-generation/DocGeneration.PromptRegression.Tests/Reports"
FINGERPRINT_PROJECT="$REPO_ROOT/docs-generation/DocGeneration.Tools.Fingerprint"

# Representative namespaces for regression testing
NAMESPACES=(applens cloudarchitect deploy compute fileshares)

usage() {
    cat <<EOF
Prompt Regression Testing Runner

Usage:
  $0 seed              Seed baselines from current generated output
  $0 test [step]       Regenerate namespaces (optional: specific step) and compare
  $0 compare           Compare current output against baselines (no regeneration)
  $0 report            Show the last regression report
  $0 status            Show baseline and candidate file counts

Namespaces tested: ${NAMESPACES[*]}

Examples:
  $0 seed                    # First-time: capture current output as golden baselines
  $0 test 6                  # Regenerate Step 6 for all test namespaces and compare
  $0 test 4,6                # Regenerate Steps 4+6 and compare
  $0 compare                 # Compare without regenerating (after manual changes)
EOF
}

# Copy key articles from generated output to a target directory
copy_articles() {
    local ns="$1"
    local target_dir="$2"

    mkdir -p "$target_dir/$ns"

    # Tool-family article
    local tf_dir="$REPO_ROOT/generated-$ns/tool-family"
    if [ -d "$tf_dir" ]; then
        local tf_file
        tf_file=$(find "$tf_dir" -name "*.md" -type f | sort | head -1)
        if [ -n "$tf_file" ]; then
            cp "$tf_file" "$target_dir/$ns/tool-family.md"
        fi
    fi

    # Horizontal article
    local ha_dir="$REPO_ROOT/generated-$ns/horizontal-articles"
    if [ -d "$ha_dir" ]; then
        local ha_file
        ha_file=$(find "$ha_dir" -name "*.md" -type f | sort | head -1)
        if [ -n "$ha_file" ]; then
            cp "$ha_file" "$target_dir/$ns/horizontal-article.md"
        fi
    fi
}

cmd_seed() {
    echo "📸 Seeding baselines from current generated output..."
    echo "   Namespaces: ${NAMESPACES[*]}"
    echo ""

    local count=0
    for ns in "${NAMESPACES[@]}"; do
        if [ ! -d "$REPO_ROOT/generated-$ns" ]; then
            echo "   ⚠️  $ns: generated-$ns/ not found, skipping"
            continue
        fi

        copy_articles "$ns" "$BASELINES_DIR"
        local files
        files=$(find "$BASELINES_DIR/$ns" -name "*.md" -type f 2>/dev/null | wc -l)
        echo "   ✅ $ns: $files article(s) saved as baseline"
        count=$((count + files))
    done

    echo ""
    echo "✅ Seeded $count baseline files in $BASELINES_DIR"
    echo ""

    # Also take a fingerprint snapshot
    echo "📊 Taking fingerprint snapshot..."
    dotnet run --project "$FINGERPRINT_PROJECT" -- snapshot \
        --output "$BASELINES_DIR/fingerprint-baseline.json" \
        --repo-root "$REPO_ROOT" 2>/dev/null | grep -E "✅|Namespaces|Total"

    echo ""
    echo "Done. Commit baselines with: git add $BASELINES_DIR && git commit -m 'chore: Seed regression baselines'"
}

cmd_test() {
    local steps="${1:-4,6}"
    echo "🔄 Prompt regression test — regenerating with steps: $steps"
    echo "   Namespaces: ${NAMESPACES[*]}"
    echo ""

    # Regenerate each namespace
    for ns in "${NAMESPACES[@]}"; do
        echo "   🔄 Regenerating $ns (steps $steps)..."
        "$REPO_ROOT/start.sh" "$ns" "$steps" --skip-deps 2>&1 | tail -1
    done

    echo ""
    cmd_compare
}

cmd_compare() {
    echo "📊 Comparing current output against baselines..."
    echo ""

    mkdir -p "$CANDIDATES_DIR" "$REPORTS_DIR"

    # Copy current output as candidates
    for ns in "${NAMESPACES[@]}"; do
        if [ ! -d "$REPO_ROOT/generated-$ns" ]; then
            echo "   ⚠️  $ns: generated-$ns/ not found, skipping"
            continue
        fi
        copy_articles "$ns" "$CANDIDATES_DIR"
    done

    # Take candidate fingerprint
    dotnet run --project "$FINGERPRINT_PROJECT" -- snapshot \
        --output "$CANDIDATES_DIR/fingerprint-candidate.json" \
        --repo-root "$REPO_ROOT" 2>/dev/null | grep -E "✅|Namespaces|Total"

    # Generate fingerprint diff report
    local baseline_fp="$BASELINES_DIR/fingerprint-baseline.json"
    local candidate_fp="$CANDIDATES_DIR/fingerprint-candidate.json"

    if [ -f "$baseline_fp" ] && [ -f "$candidate_fp" ]; then
        echo ""
        echo "📋 Fingerprint diff report:"
        dotnet run --project "$FINGERPRINT_PROJECT" -- diff \
            --baseline "$baseline_fp" \
            --candidate "$candidate_fp" \
            --output "$REPORTS_DIR/fingerprint-diff.md" 2>/dev/null

        echo "   Saved to: $REPORTS_DIR/fingerprint-diff.md"
    fi

    # Run xUnit regression tests (uses BaselineManager to compare articles)
    echo ""
    echo "🧪 Running quality metric regression tests..."
    dotnet test "$REPO_ROOT/docs-generation/DocGeneration.PromptRegression.Tests/" \
        --no-build --verbosity quiet 2>&1 | tail -5

    # Show summary
    echo ""
    cmd_report
}

cmd_report() {
    echo "📋 Regression Report"
    echo "===================="

    if [ -f "$REPORTS_DIR/fingerprint-diff.md" ]; then
        echo ""
        cat "$REPORTS_DIR/fingerprint-diff.md"
    else
        echo "No report found. Run '$0 compare' or '$0 test' first."
    fi
}

cmd_status() {
    echo "📊 Regression Testing Status"
    echo ""

    echo "Baselines ($BASELINES_DIR):"
    for ns in "${NAMESPACES[@]}"; do
        local count
        count=$(find "$BASELINES_DIR/$ns" -name "*.md" -type f 2>/dev/null | wc -l)
        if [ "$count" -gt 0 ]; then
            echo "   ✅ $ns: $count file(s)"
        else
            echo "   ❌ $ns: no baselines"
        fi
    done

    echo ""
    echo "Candidates ($CANDIDATES_DIR):"
    for ns in "${NAMESPACES[@]}"; do
        local count
        count=$(find "$CANDIDATES_DIR/$ns" -name "*.md" -type f 2>/dev/null | wc -l)
        if [ "$count" -gt 0 ]; then
            echo "   📝 $ns: $count file(s)"
        else
            echo "   ⬜ $ns: no candidates"
        fi
    done

    echo ""
    if [ -f "$REPORTS_DIR/fingerprint-diff.md" ]; then
        echo "Latest report: $REPORTS_DIR/fingerprint-diff.md"
    else
        echo "No reports generated yet."
    fi
}

# Main
case "${1:-}" in
    seed)     cmd_seed ;;
    test)     cmd_test "${2:-4,6}" ;;
    compare)  cmd_compare ;;
    report)   cmd_report ;;
    status)   cmd_status ;;
    --help|-h|"") usage ;;
    *)        echo "❌ Unknown command: $1"; usage; exit 1 ;;
esac
