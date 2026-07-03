#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
TEST_ROOT="$REPO_ROOT/.test-artifacts/run-focus-version-pin"

fail() {
    echo "FAIL: $*" >&2
    exit 1
}

assert_file_contains() {
    local file="$1"
    local expected="$2"
    grep -F -- "$expected" "$file" >/dev/null || fail "Expected '$expected' in $file"
}

assert_file_not_contains() {
    local file="$1"
    local unexpected="$2"
    if grep -F -- "$unexpected" "$file" >/dev/null; then
        fail "Did not expect '$unexpected' in $file"
    fi
}

setup_fixture() {
    rm -rf "$TEST_ROOT"
    mkdir -p "$TEST_ROOT/bin"
    cp "$REPO_ROOT/run-focus.sh" "$TEST_ROOT/run-focus.sh"

    cat > "$TEST_ROOT/start.sh" <<'EOF'
#!/usr/bin/env bash
set -euo pipefail
printf '%s\n' "$@" >> "$TEST_ROOT/start-args.log"
EOF
    chmod +x "$TEST_ROOT/start.sh"

    cat > "$TEST_ROOT/bin/dotnet" <<'EOF'
#!/usr/bin/env bash
set -euo pipefail
printf '%s\n' "$*" >> "$TEST_ROOT/dotnet.log"
if [[ "${1-}" == "tool" && "${2-}" == "update" ]]; then
    if [[ "${DOTNET_UPDATE_EXIT:-0}" == "0" ]]; then
        exit 0
    fi
    exit "${DOTNET_UPDATE_EXIT}"
fi
if [[ "${1-}" == "tool" && "${2-}" == "install" ]]; then
    exit "${DOTNET_INSTALL_EXIT:-0}"
fi
exit 0
EOF
    chmod +x "$TEST_ROOT/bin/dotnet"
}

run_fixture() {
    local scenario="$1"
    shift

    rm -f "$TEST_ROOT/dotnet.log" "$TEST_ROOT/start-args.log"
    (
        export TEST_ROOT
        export PATH="$TEST_ROOT/bin:$PATH"
        export DOTNET_UPDATE_EXIT="${DOTNET_UPDATE_EXIT:-0}"
        export DOTNET_INSTALL_EXIT="${DOTNET_INSTALL_EXIT:-0}"
        cd "$TEST_ROOT"
        bash ./run-focus.sh "$@"
    ) >"$TEST_ROOT/$scenario.out" 2>&1
}

test_update_path() {
    setup_fixture
    DOTNET_UPDATE_EXIT=0 DOTNET_INSTALL_EXIT=99 run_fixture update-path cosmos 4 --dry-run

    assert_file_contains "$TEST_ROOT/dotnet.log" "tool update --global azure.mcp --version 3.0.0-beta.15"
    assert_file_not_contains "$TEST_ROOT/dotnet.log" "tool install --global azure.mcp --version 3.0.0-beta.15"

    cat > "$TEST_ROOT/expected-start-args.log" <<'EOF'
cosmos
4
--skip-npm-update
--dry-run
EOF
    cmp -s "$TEST_ROOT/start-args.log" "$TEST_ROOT/expected-start-args.log" || fail "Unexpected start.sh args for update path"
}

test_install_fallback_path() {
    setup_fixture
    DOTNET_UPDATE_EXIT=1 DOTNET_INSTALL_EXIT=0 run_fixture install-fallback storage --skip-deps

    assert_file_contains "$TEST_ROOT/dotnet.log" "tool update --global azure.mcp --version 3.0.0-beta.15"
    assert_file_contains "$TEST_ROOT/dotnet.log" "tool install --global azure.mcp --version 3.0.0-beta.15"

    cat > "$TEST_ROOT/expected-start-args.log" <<'EOF'
storage
--skip-npm-update
--skip-deps
EOF
    cmp -s "$TEST_ROOT/start-args.log" "$TEST_ROOT/expected-start-args.log" || fail "Unexpected start.sh args for install fallback"
}

test_update_path
test_install_fallback_path

rm -rf "$TEST_ROOT"
echo "PASS: run-focus version pin tests"
