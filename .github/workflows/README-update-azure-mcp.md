# Update @azure/mcp Workflow

## Overview

This workflow automatically checks for updates to the `@azure/mcp` package in the `test-npm-azure-mcp` directory and commits minor/patch updates directly to main. **Major version updates (breaking changes) are blocked and require manual review.**

## Triggers

### Scheduled
- Runs nightly at **9:00 AM UTC**
- Configurable via the cron expression in the workflow file

### Manual
- Can be triggered manually via GitHub Actions UI
- Go to: **Actions** → **Update @azure/mcp Version** → **Run workflow**

## What It Does

1. **Version Check**: Compares the current version in `package.json` with the latest version on npm
2. **Update**: If a new version is found, runs `npm install @azure/mcp@<version>` to update both `package.json` and `package-lock.json`
3. **Test**: Validates the installation by running `azmcp --version` and `azmcp --help`
4. **CLI Snapshot**: Creates a version-specific folder with `tools-list.json` and `cli-examples.md`
5. **Diff Generation**: Compares against the previous version and generates a diff file
6. **Coverage Audit**: Runs MCP tool coverage audit against published articles (if available)
7. **Breaking Change Check**: Detects major version changes (e.g., 3.0 → 4.0)
8. **Npm Audit**: Runs security audit at high severity level (blocks if vulnerabilities found)
9. **Breaking Change Gate**: **Blocks major version updates** — exits with error, requires manual PR
10. **Test Execution**: Runs `npm run build` and `npm run test` to satisfy branch protection
11. **Commit and Push**: Commits changes directly to `main` with detailed commit message

## Actions Used

All actions are pinned to their latest stable versions:

- `actions/checkout@v5` - Repository checkout
- `actions/setup-node@v4` - Node.js environment  
- `actions/upload-artifact@v4` - Artifact uploads (diffs, gap reports)
- `actions/github-script@v7` - Issue creation on failure

## Workflows

### Main Workflow: `update-azure-mcp.yml`
Checks for updates and commits directly to main (minor/patch only).

### Test Workflow: `test-azure-mcp-update.yml`
Runs on PRs to validate package updates work correctly.

## Configuration

### Branch Protection Requirements

For this workflow to push to `main`, branch protection must allow `github-actions[bot]`:

1. **Configure in GitHub**:
   - Go to: **Settings** → **Branches** → **Add rule**
   - Branch name pattern: `main` (or your default branch)
   - Enable:
     - ✅ Require status checks to pass: `build-and-test`
     - ✅ Allow specified actors to bypass: `github-actions[bot]`
   
   Alternatively, the workflow runs inline tests before committing to satisfy protection rules.

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
- `contents: write` - To commit and push to main
- `issues: write` - To create failure notification issues

These are configured in the workflow file and should work with the default `GITHUB_TOKEN`.

## Safety Gates

### Breaking Change Protection

Major version updates are **automatically blocked**:
- Detects version changes like 3.x → 4.x
- Exits with error and creates a GitHub issue
- Requires manual review and PR creation

### Security Gate

Runs `npm audit --audit-level=high`:
- Blocks commits if high/critical vulnerabilities found
- Creates GitHub issue with vulnerability details
- Requires manual resolution before update

## Testing

To test the workflow:

1. **Manual Trigger**: Run the workflow manually to verify it works
2. **Check Summary**: View the job summary for version information
3. **Verify Commit**: If an update occurs, check the commit in main history

## Troubleshooting

### No Commit Created

- Check the job summary to see if versions match
- Verify npm registry is accessible
- Check that the package `@azure/mcp` exists
- Check for breaking change gate (major version block)
- Review security audit results (may block commit)

### Permission Errors

- Ensure GitHub Actions has write permissions to the repository
- Check repository settings: **Settings** → **Actions** → **General** → **Workflow permissions**
- Verify `github-actions[bot]` is allowed to push to protected `main` branch

### Push Failures

If `git push` fails:
- Another commit may have been pushed during workflow execution (diverged branch)
- Branch protection rules may require status checks
- Check that the workflow has run inline tests before pushing

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

### Modifying Commit Message

Edit the commit message construction in the workflow to customize:
- Commit subject line
- Version change details
- CLI diff statistics


## Security

- Uses `GITHUB_TOKEN` (automatically provided)
- No custom secrets required
- Actions are pinned to specific versions (v5, v4, v7)
- Runs in isolated GitHub-hosted runner

## Related Files

- **Workflow**: `.github/workflows/update-azure-mcp.yml`
- **Target Package**: `test-npm-azure-mcp/package.json`
- **Lock File**: `test-npm-azure-mcp/package-lock.json`
