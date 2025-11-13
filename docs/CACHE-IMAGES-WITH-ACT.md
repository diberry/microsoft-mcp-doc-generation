# Caching Docker Images with Act

## Overview

When using **nektos/act** (which powers the **GitHub Local Actions** VS Code extension), you can enable offline mode to cache Docker images and actions instead of downloading them on every run.

## The Solution: Action Offline Mode

The setting that enables image caching is:

**`--action-offline-mode`**

## Benefits

When Action Offline Mode is enabled:

- ✅ Stops pulling existing images that are already cached
- ✅ Stops failing if an action has been cached and you cannot connect to GitHub
- ✅ Pulls only nonexistent actions and images
- ✅ Works offline if it has run at least once while online
- ✅ Eliminates unnecessary timeouts with unstable connections
- ✅ Helps work around rate limit problems
- ✅ Significantly speeds up workflow execution

## Configuration Methods

### Method 1: Via `.actrc` Configuration File

Create a `.actrc` file in your project root directory:

```
--action-offline-mode
```

This is the recommended approach as it persists the setting across all act runs in your project.

### Method 2: Via Command Line Flag

Run act with the flag each time:

```bash
act --action-offline-mode
```

### Method 3: Via GitHub Local Actions Extension (VS Code)

1. Open the **Settings** view in the GitHub Local Actions extension
2. Click the **Add Option** action
3. Select **action-offline-mode** from the available options
4. The setting will be applied to all workflow executions from the extension

## Important Notes

- Act must run **at least once while online** to cache the initial images and actions
- After the initial run, subsequent runs will use cached resources
- New actions or images not in the cache will still be pulled as needed
- This is particularly useful in:
  - Development environments with slow/unstable internet
  - Situations where you're hitting GitHub API rate limits
  - CI/CD pipelines where speed is critical

## VS Code Extension Reference

This repository uses the `SanjulaGanepola.github-local-actions` extension, which is configured in `.devcontainer/devcontainer.json`.

## Documentation References

- [nektos/act GitHub Repository](https://github.com/nektos/act)
- [nektos/act Action Offline Mode Documentation](https://nektosact.com/usage/index.html#action-offline-mode)
- [GitHub Local Actions Extension Documentation](https://sanjulaganepola.github.io/github-local-actions-docs/)
- [GitHub Local Actions Settings](https://sanjulaganepola.github.io/github-local-actions-docs/usage/settings)
