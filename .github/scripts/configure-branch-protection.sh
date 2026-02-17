#!/bin/bash
# Script to configure branch protection rules for the repository
# Requires: GitHub CLI (gh) to be installed and authenticated
# Usage: ./configure-branch-protection.sh [branch-name]

set -e

BRANCH="${1:-main}"
REPO_OWNER="${GITHUB_REPOSITORY_OWNER:-$(gh repo view --json owner -q .owner.login)}"
REPO_NAME="${GITHUB_REPOSITORY_NAME:-$(gh repo view --json name -q .name)}"

echo "=========================================="
echo "Configuring Branch Protection Rules"
echo "=========================================="
echo "Repository: $REPO_OWNER/$REPO_NAME"
echo "Branch: $BRANCH"
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

echo "Configuring branch protection for '$BRANCH'..."
echo ""

# Create or update branch protection rule using GitHub API
gh api \
  --method PUT \
  -H "Accept: application/vnd.github+json" \
  -H "X-GitHub-Api-Version: 2022-11-28" \
  "/repos/$REPO_OWNER/$REPO_NAME/branches/$BRANCH/protection" \
  -f required_status_checks='{"strict":true,"contexts":["test-azure-mcp"]}' \
  -f enforce_admins=false \
  -f required_pull_request_reviews='{"dismiss_stale_reviews":true,"require_code_owner_reviews":false,"required_approving_review_count":0}' \
  -f restrictions=null \
  -f required_linear_history=false \
  -f allow_force_pushes=false \
  -f allow_deletions=false \
  -f block_creations=false \
  -f required_conversation_resolution=false \
  -f lock_branch=false \
  -f allow_fork_syncing=true

echo ""
echo "✅ Branch protection rules configured successfully!"
echo ""
echo "Configured settings:"
echo "  ✓ Require pull request before merging"
echo "  ✓ Require status checks to pass: test-azure-mcp"
echo "  ✓ Dismiss stale reviews when new commits are pushed"
echo "  ✓ 0 required approving reviews (auto-merge compatible)"
echo "  ✓ Prevent force pushes"
echo ""
echo "To view the settings:"
echo "  https://github.com/$REPO_OWNER/$REPO_NAME/settings/branch_protection_rules"
echo ""
echo "⚠️  Note: Auto-merge will now work for PRs that pass the 'test-azure-mcp' check"
