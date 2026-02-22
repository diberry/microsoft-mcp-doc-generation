#!/bin/bash
# Shared bash helpers for the documentation generation pipeline.
# Source this file in bash scripts: source "$(dirname "${BASH_SOURCE[0]}")/bash-common.sh"

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
