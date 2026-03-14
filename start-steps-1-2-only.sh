#!/bin/bash
# Development shortcut: run only pipeline Steps 1-2 and capture output locally.
#
# Usage:
#   ./start-steps-1-2-only.sh
#
# Notes:
#   - Convenience helper for local development/debugging only
#   - Delegates to ./start.sh 1,2
#   - Writes combined stdout/stderr to ./start-log.txt

bash ./start.sh 1,2 2>&1 | tee start-log.txt