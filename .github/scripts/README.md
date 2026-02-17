# Repository Configuration Scripts

This directory contains scripts to programmatically configure repository settings for automated dependency updates.

## Available Scripts

### 1. Branch Protection Configuration
**Script**: `configure-branch-protection.sh`

Configures branch protection rules to enable auto-merge for automated dependency update PRs.

**Usage:**
```bash
./.github/scripts/configure-branch-protection.sh [branch-name]
```

**Configures:**
- Required status check: `test-azure-mcp`
- 0 required approvals (auto-merge compatible)
- Prevents force pushes

### 2. GitHub Actions Permissions
**Script**: `configure-actions-permissions.sh`

Configures GitHub Actions workflow permissions for the repository.

**Usage:**
```bash
./.github/scripts/configure-actions-permissions.sh
```

**Configures:**
- Default workflow permissions: Read and write
- Allows GitHub Actions to create and approve pull requests

---

## Branch Protection Configuration

This script programmatically configures branch protection rules for your repository to enable auto-merge for automated dependency update PRs.

## Prerequisites

- **GitHub CLI (`gh`)** installed and authenticated
  - Install: https://cli.github.com/
  - Authenticate: `gh auth login`
- **Admin access** to the repository

## Usage

```bash
# Configure protection for main branch (default)
./.github/scripts/configure-branch-protection.sh

# Configure protection for a specific branch
./.github/scripts/configure-branch-protection.sh develop
```

## What It Does

The script configures the following branch protection rules:

### Required Status Checks
- ✅ Requires the `test-azure-mcp` status check to pass
- ✅ Strict status checks (branch must be up-to-date)

### Pull Request Requirements
- ✅ Require pull request before merging
- ✅ Dismiss stale reviews when new commits are pushed
- ✅ **0 required approving reviews** (allows auto-merge)

### Branch Restrictions
- ✅ Prevent force pushes
- ✅ Prevent branch deletion
- ❌ No admin enforcement (admins can bypass)

## Auto-merge Compatibility

The configuration is specifically designed to work with auto-merge:

1. **No required approvals**: PRs can merge automatically without human approval
2. **Required status check**: PRs must pass the `test-azure-mcp` check first
3. **Auto-merge step in workflow**: The workflow includes a step to enable auto-merge

## Testing

After running the script:

1. View your branch protection rules:
   ```
   https://github.com/OWNER/REPO/settings/branch_protection_rules
   ```

2. Trigger the update workflow manually to test:
   - Go to **Actions** → **Update @azure/mcp Version** → **Run workflow**
   - Watch for the PR to be created and auto-merged

## Status Check Name

The script configures a required status check named **`test-azure-mcp`**. This must match the check name in your workflow:

```yaml
jobs:
  test-azure-mcp:  # This name becomes the status check name
    runs-on: ubuntu-latest
    steps:
      # ... test steps
```

If you change the job name in the workflow, update this script accordingly.

## Troubleshooting

### "Not authenticated" Error
```bash
gh auth login
```

### "Insufficient permissions" Error
You need admin access to the repository to configure branch protection rules.

### Status Check Not Found
The `test-azure-mcp` check must run at least once before branch protection can require it. Run the workflow once, then configure branch protection.

### Auto-merge Not Working
1. Verify branch protection is configured: Check repository settings
2. Verify status check passes: Check PR status
3. Verify auto-merge is enabled: Check PR auto-merge indicator

## Modifying the Configuration

Edit the script to customize:

- **Required approvals**: Change `"required_approving_review_count":0` to require reviews
- **Status check names**: Update `"contexts":["test-azure-mcp"]` array
- **Admin enforcement**: Change `enforce_admins=false` to `true`
- **Other settings**: See [GitHub API documentation](https://docs.github.com/en/rest/branches/branch-protection)

## Reverting Changes

To remove branch protection:

```bash
gh api \
  --method DELETE \
  "/repos/OWNER/REPO/branches/BRANCH/protection"
```

Or use the GitHub web UI: **Settings** → **Branches** → **Delete rule**
