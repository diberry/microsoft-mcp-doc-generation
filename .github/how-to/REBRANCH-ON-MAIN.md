# Rebranch unstaged changes onto origin/main

Move your uncommitted (unstaged/untracked) changes to a new branch based on the latest `origin/main`.

## Steps

```bash
# 1. Stash all changes including untracked files
git stash --include-untracked

# 2. Fetch the latest main
git fetch origin main

# 3. Create a new branch from origin/main
git checkout -b <your-branch-name> origin/main

# 4. Restore your changes
git stash pop
```

## Example

```bash
git stash --include-untracked
git fetch origin main
git checkout -b copilot/my-feature origin/main
git stash pop
```

## Notes

- If `git stash pop` reports merge conflicts, resolve them manually, then `git stash drop` to clean up the stash entry.
- If you already have a branch with the same name, delete it first with `git branch -D <branch-name>` or pick a different name.
- This works whether you're currently on `main` or any other branch.
