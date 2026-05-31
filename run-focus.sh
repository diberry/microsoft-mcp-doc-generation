#!/usr/bin/env bash
# run-focus.sh — Quickly run specific problematic namespace combinations through the pipeline.
#
# Usage:
#   ./run-focus.sh <target> [steps] [--skip-deps] [--dry-run]
#   ./run-focus.sh --list
#   ./run-focus.sh --help
#
# Examples:
#   ./run-focus.sh monitor-workbooks          # Run monitor + workbooks, then merge
#   ./run-focus.sh storage 3,4                # Run storage, steps 3 and 4 only
#   ./run-focus.sh appconfig --skip-deps      # Run appconfig, skip dep validation
#   ./run-focus.sh all-problematic            # Full focus regression pass

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Colors
GREEN='\033[0;32m'
YELLOW='\033[0;33m'
RED='\033[0;31m'
RESET='\033[0m'

# ── helpers ──────────────────────────────────────────────────────────────────

info()    { echo -e "${YELLOW}[run-focus] $*${RESET}"; }
success() { echo -e "${GREEN}[run-focus] $*${RESET}"; }
error()   { echo -e "${RED}[run-focus] ERROR: $*${RESET}" >&2; }

print_header() {
    local label="$1"
    echo ""
    echo -e "${YELLOW}==================================================================${RESET}"
    echo -e "${YELLOW}  Focus: ${label}${RESET}"
    echo -e "${YELLOW}==================================================================${RESET}"
    echo ""
}

usage() {
    cat <<EOF
Usage: ./run-focus.sh <target> [steps] [flags...]
       ./run-focus.sh --list
       ./run-focus.sh --help

Arguments:
  <target>   Named focus target (see --list for all targets)
  [steps]    Optional step filter, e.g. 3,4  (passed to start.sh)
  [flags]    Any extra flags passed through to start.sh, e.g. --skip-deps

Flags:
  --list     Print all available focus targets with descriptions
  --help     Show this help message
EOF
}

list_targets() {
    cat <<EOF

Available focus targets:

  monitor-workbooks   monitor + workbooks (sequential, then merge)
                      Merge group — two namespaces combine into one article

  storage             storage
                      Parameter filtering anomaly, CLI version mismatch

  functions           functions
                      Coverage expansion, new tools added

  appconfig           appconfig
                      --account param coverage edge cases

  cosmos              cosmos
                      Reference namespace for @mcpcli tests

  all-problematic     All of the above, sequential
                      Full focus regression pass

EOF
}

# Run a single namespace through start.sh, passing optional STEPS and extra flags.
# Usage: run_namespace <namespace> [steps] [extra flags...]
run_namespace() {
    local ns="$1"
    shift
    print_header "$ns"
    info "Running: ./start.sh $ns $*"
    bash "$SCRIPT_DIR/start.sh" "$ns" "$@"
    success "$ns complete"
}

# ── argument parsing ──────────────────────────────────────────────────────────

TARGET=""
STEPS=""
EXTRA_FLAGS=()

if [[ $# -eq 0 ]]; then
    usage
    exit 1
fi

case "$1" in
    --help|-h)
        usage
        exit 0
        ;;
    --list)
        list_targets
        exit 0
        ;;
    *)
        TARGET="$1"
        shift
        ;;
esac

# Next arg is steps if it looks like digits/commas
if [[ $# -gt 0 && "$1" =~ ^[1-6](,[1-6])*$ ]]; then
    STEPS="$1"
    shift
fi

# Remaining args are pass-through flags
while [[ $# -gt 0 ]]; do
    EXTRA_FLAGS+=("$1")
    shift
done

# Build the args array to forward to start.sh
START_ARGS=()
[[ -n "$STEPS" ]] && START_ARGS+=("$STEPS")
START_ARGS+=("${EXTRA_FLAGS[@]+"${EXTRA_FLAGS[@]}"}")

# ── dispatch ──────────────────────────────────────────────────────────────────

run_monitor_workbooks() {
    run_namespace "monitor"   "${START_ARGS[@]+"${START_ARGS[@]}"}"
    run_namespace "workbooks" "${START_ARGS[@]+"${START_ARGS[@]}"}"

    if [[ -f "$SCRIPT_DIR/merge-namespaces.sh" ]]; then
        print_header "merge: monitor + workbooks"
        info "Running merge-namespaces.sh"
        bash "$SCRIPT_DIR/merge-namespaces.sh"
        success "Merge complete"
    else
        info "merge-namespaces.sh not found — skipping merge step"
    fi
}

case "$TARGET" in
    monitor-workbooks)
        run_monitor_workbooks
        ;;
    storage)
        run_namespace "storage" "${START_ARGS[@]+"${START_ARGS[@]}"}"
        ;;
    functions)
        run_namespace "functions" "${START_ARGS[@]+"${START_ARGS[@]}"}"
        ;;
    appconfig)
        run_namespace "appconfig" "${START_ARGS[@]+"${START_ARGS[@]}"}"
        ;;
    cosmos)
        run_namespace "cosmos" "${START_ARGS[@]+"${START_ARGS[@]}"}"
        ;;
    all-problematic)
        info "Running full focus regression pass (5 targets)"
        OVERALL_EXIT=0
        run_namespace "monitor"   "${START_ARGS[@]+"${START_ARGS[@]}"}" || OVERALL_EXIT=$?
        run_namespace "workbooks" "${START_ARGS[@]+"${START_ARGS[@]}"}" || OVERALL_EXIT=$?
        if [[ -f "$SCRIPT_DIR/merge-namespaces.sh" ]]; then
            print_header "merge: monitor + workbooks"
            info "Running merge-namespaces.sh"
            bash "$SCRIPT_DIR/merge-namespaces.sh" || OVERALL_EXIT=$?
            success "Merge complete"
        else
            info "merge-namespaces.sh not found — skipping merge step"
        fi
        run_namespace "storage"   "${START_ARGS[@]+"${START_ARGS[@]}"}" || OVERALL_EXIT=$?
        run_namespace "functions" "${START_ARGS[@]+"${START_ARGS[@]}"}" || OVERALL_EXIT=$?
        run_namespace "appconfig" "${START_ARGS[@]+"${START_ARGS[@]}"}" || OVERALL_EXIT=$?
        run_namespace "cosmos"    "${START_ARGS[@]+"${START_ARGS[@]}"}" || OVERALL_EXIT=$?
        if [[ $OVERALL_EXIT -ne 0 ]]; then
            error "One or more targets failed in all-problematic pass (exit $OVERALL_EXIT)"
            exit $OVERALL_EXIT
        fi
        ;;
    *)
        error "Unknown target: '$TARGET'"
        echo ""
        list_targets
        exit 1
        ;;
esac

echo ""
success "run-focus.sh finished: target='$TARGET'"
