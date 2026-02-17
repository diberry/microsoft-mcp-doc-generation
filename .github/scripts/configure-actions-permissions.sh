#!/bin/bash
# Script to configure GitHub Actions permissions for the repository
# Requires: GitHub CLI (gh) to be installed and authenticated
# Usage: ./configure-actions-permissions.sh

set -e

REPO_OWNER="${GITHUB_REPOSITORY_OWNER:-$(gh repo view --json owner -q .owner.login)}"
REPO_NAME="${GITHUB_REPOSITORY_NAME:-$(gh repo view --json name -q .name)}"

echo "=========================================="
echo "Configuring GitHub Actions Permissions"
echo "=========================================="
echo "Repository: $REPO_OWNER/$REPO_NAME"
echo ""

# Check if gh CLI is installed
if ! command -v gh &> /dev/null; then
    echo "❌ Error: GitHub CLI (gh) is not installed"
    echo "Install from: https://cli.github.com/"
    exit 1
fi

# Check if authenticated
if ! gh auth status &> /dev/null; then
    echo "❌ Error: Not authenticated with GitHub CLI"
    echo "Run: gh auth login"
    exit 1
fi

echo "Configuring workflow permissions..."
echo ""

# Set workflow permissions to read and write
echo "Setting workflow permissions to 'read and write'..."
gh api \
  --method PUT \
  -H "Accept: application/vnd.github+json" \
  -H "X-GitHub-Api-Version: 2022-11-28" \
  "/repos/$REPO_OWNER/$REPO_NAME/actions/permissions/workflow" \
  -f default_workflow_permissions='write' \
  -F can_approve_pull_request_reviews=true

echo "✅ Workflow permissions configured successfully!"
echo ""
echo "Configured settings:"
echo "  ✓ Default workflow permissions: Read and write"
echo "  ✓ Allow GitHub Actions to create and approve pull requests: Enabled"
echo ""
echo "To view the settings:"
echo "  https://github.com/$REPO_OWNER/$REPO_NAME/settings/actions"
echo ""
echo "⚠️  Note: These permissions allow workflows to:"
echo "  - Create and update pull requests"
echo "  - Commit changes to the repository"
echo "  - Create issues"
echo "  - Add labels and assignees"
