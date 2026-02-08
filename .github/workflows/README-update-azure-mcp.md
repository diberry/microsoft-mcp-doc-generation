# Update @azure/mcp Workflow

## Overview

This workflow automatically checks for updates to the `@azure/mcp` package in the `test-npm-azure-mcp` directory and creates a pull request when a new version is available.

## Triggers

### Scheduled
- Runs nightly at **9:00 AM UTC**
- Configurable via the cron expression in the workflow file

### Manual
- Can be triggered manually via GitHub Actions UI
- Go to: **Actions** → **Update @azure/mcp Version** → **Run workflow**

## What It Does

1. **Version Check**: Compares the current version in `package.json` with the latest version on npm
2. **Update**: If a new version is found, runs `npm install @azure/mcp@latest` to update both `package.json` and `package-lock.json`
3. **Clean Up Old PRs**: Automatically closes any previous automated PRs to ensure only one PR exists at a time
4. **Pull Request**: Creates a PR with:
   - Clear description of the version change
   - Updated package files
   - Labels: `dependencies`, `automated`
   - Assigned to the repository owner

## Actions Used

All actions are pinned to their latest stable versions:

- `actions/checkout@v5` - Repository checkout
- `actions/setup-node@v4` - Node.js environment
- `peter-evans/create-pull-request@v7` - PR creation

## Configuration

### Changing Schedule

Edit the cron expression in the workflow file:

```yaml
schedule:
  - cron: '0 9 * * *'  # Nightly at 9 AM UTC
```

Common schedules:
- Weekly: `'0 9 * * 1'` (Monday at 9 AM UTC)
- Twice weekly: `'0 9 * * 1,4'` (Monday & Thursday)
- Monthly: `'0 9 1 * *'` (1st of each month)

### Permissions

The workflow requires:
- `contents: write` - To commit changes
- `pull-requests: write` - To create PRs

These are configured in the workflow file and should work with the default `GITHUB_TOKEN`.

## Testing

To test the workflow:

1. **Manual Trigger**: Run the workflow manually to verify it works
2. **Check Summary**: View the job summary for version information
3. **Verify PR**: If an update is available, check the created PR

### PR Management Behavior

The workflow ensures only **one automated PR exists at a time**:
- Before creating a new PR, it automatically closes any existing PRs created by this workflow
- Closed PRs include a comment explaining they were superseded by a newer version
- This prevents PR stack-up when multiple versions are released between runs

## Troubleshooting

### No PR Created

- Check the job summary to see if versions match
- Verify npm registry is accessible
- Check that the package `@azure/mcp` exists

### Permission Errors

- Ensure GitHub Actions has write permissions to the repository
- Check repository settings: **Settings** → **Actions** → **General** → **Workflow permissions**

### Version Detection Issues

The workflow extracts version strings by:
1. Reading from `package.json` using Node.js
2. Removing `^` or `~` prefixes for comparison
3. Querying npm registry for latest version

If issues occur, check:
- `package.json` format is valid
- npm registry connectivity
- Package name is correct (`@azure/mcp`)

## Maintenance

### Updating Action Versions

When new versions of actions are released, update the workflow file:

1. Check for updates: https://github.com/marketplace/actions
2. Update version tags in the workflow
3. Test the workflow

### Modifying PR Content

Edit the `body` section of the `create-pull-request` step to customize:
- PR description
- Verification checklist
- Additional context

## Security

- Uses `GITHUB_TOKEN` (automatically provided)
- No custom secrets required
- Actions are pinned to specific versions (v5, v4, v7)
- Runs in isolated GitHub-hosted runner

## Related Files

- **Workflow**: `.github/workflows/update-azure-mcp.yml`
- **Target Package**: `test-npm-azure-mcp/package.json`
- **Lock File**: `test-npm-azure-mcp/package-lock.json`
