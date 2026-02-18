#chmod +x scripts/copilot-agent-verify.sh

#!/usr/bin/env bash
set -euo pipefail

REPO=${1:-}
MODE=${2:-verify}

if [[ -z "$REPO" ]]; then
  echo "Usage: $0 OWNER/REPO [apply]"
  exit 1
fi

command -v gh >/dev/null 2>&1 || { echo "GitHub CLI (gh) is required"; exit 2; }
command -v jq >/dev/null 2>&1 || { echo "jq is required"; exit 2; }

ACTIONS_JSON=$(gh api repos/$REPO/actions/permissions)
WF_JSON=$(gh api repos/$REPO/actions/permissions/workflow)

ACTIONS_ENABLED=$(echo "$ACTIONS_JSON" | jq -r '.enabled')
WF_DEFAULT=$(echo "$WF_JSON" | jq -r '.default_workflow_permissions')
WF_APPROVE=$(echo "$WF_JSON" | jq -r '.can_approve_pull_request_reviews')

FIX_NEEDED=0

if [[ "$ACTIONS_ENABLED" != "true" ]]; then
  echo "GitHub Actions are disabled"
  FIX_NEEDED=1
fi

if [[ "$WF_DEFAULT" != "write" ]]; then
  echo "Workflow permissions are not set to write"
  FIX_NEEDED=1
fi

if [[ "$MODE" != "apply" ]]; then
  echo "Verify mode complete"
  [[ "$FIX_NEEDED" -eq 1 ]] && echo "Run again with 'apply' to fix"
  exit 0
fi

if [[ "$ACTIONS_ENABLED" != "true" ]]; then
  gh api -X PUT repos/$REPO/actions/permissions     -f enabled=true     -f allowed_actions=all
fi

if [[ "$WF_DEFAULT" != "write" || "$WF_APPROVE" != "true" ]]; then
  gh api -X PUT repos/$REPO/actions/permissions/workflow     -f default_workflow_permissions=write     -f can_approve_pull_request_reviews=true
fi

echo "Repository settings updated successfully"
