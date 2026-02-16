#!/usr/bin/env bash
# copilot-verify.sh — Verify (and optionally fix) GitHub repository settings
# required for GitHub Copilot / GitHub Actions workflows.
#
# Checks:
#   1. GitHub Actions are enabled on the repo
#   2. Workflow default permissions are set to "write" (needed for PR operations)
#   3. Workflows can approve pull request reviews
#
# Usage:
#   ./copilot-verify.sh OWNER/REPO          # Verify only (read-only)
#   ./copilot-verify.sh OWNER/REPO apply    # Verify and fix any issues
#
# Prerequisites: gh (GitHub CLI), jq

set -euo pipefail

# Parse arguments: repo (required) and mode (default: verify)
REPO=${1:-}
MODE=${2:-verify}

if [[ -z "$REPO" ]]; then
  echo "Usage: $0 OWNER/REPO [apply]"
  exit 1
fi

# Ensure required CLI tools are available
command -v gh >/dev/null 2>&1 || { echo "GitHub CLI (gh) is required"; exit 2; }
command -v jq >/dev/null 2>&1 || { echo "jq is required"; exit 2; }

# Fetch current repository settings via GitHub API
ACTIONS_JSON=$(gh api repos/$REPO/actions/permissions)
WF_JSON=$(gh api repos/$REPO/actions/permissions/workflow)

# Extract individual settings from the API responses
ACTIONS_ENABLED=$(echo "$ACTIONS_JSON" | jq -r '.enabled')
WF_DEFAULT=$(echo "$WF_JSON" | jq -r '.default_workflow_permissions')
WF_APPROVE=$(echo "$WF_JSON" | jq -r '.can_approve_pull_request_reviews')

FIX_NEEDED=0

# Check 1: Actions must be enabled
if [[ "$ACTIONS_ENABLED" != "true" ]]; then
  echo "GitHub Actions are disabled"
  FIX_NEEDED=1
fi

# Check 2: Workflow token needs write permissions for PR comments, commits, etc.
if [[ "$WF_DEFAULT" != "write" ]]; then
  echo "Workflow permissions are not set to write"
  FIX_NEEDED=1
fi

# In verify-only mode, report status and exit without making changes
if [[ "$MODE" != "apply" ]]; then
  echo "Verify mode complete"
  [[ "$FIX_NEEDED" -eq 1 ]] && echo "Run again with 'apply' to fix"
  exit 0
fi

# --- Apply mode: fix any issues found ---

# Enable GitHub Actions with all actions allowed
if [[ "$ACTIONS_ENABLED" != "true" ]]; then
  gh api -X PUT repos/$REPO/actions/permissions     -F enabled=true     -f allowed_actions=all
fi

# Set workflow permissions to write and allow PR review approvals
if [[ "$WF_DEFAULT" != "write" || "$WF_APPROVE" != "true" ]]; then
  gh api -X PUT repos/$REPO/actions/permissions/workflow     -f default_workflow_permissions=write     -F can_approve_pull_request_reviews=true
fi

echo "Repository settings updated successfully"
