---
name: "PR Documentation Update"
description: "Update CHANGELOG and user-facing documentation for every PR before merge"
domain: "documentation"
confidence: "high"
source: "manual"
tools:
  - name: "view"
    description: "Read existing documentation files"
    when: "To understand current state of docs before updating"
  - name: "edit"
    description: "Update documentation files"
    when: "To add changelog entries and update user docs"
  - name: "github-mcp-server-pull_request_read"
    description: "Read PR details and changed files"
    when: "To understand what changed and needs documenting"
---

## Context

Every PR must update documentation before merge (AD-004, AD-026). This skill defines **what** to update and **where**. It runs AFTER code changes are complete but BEFORE the PR review.

## When to Run

- **Trigger**: Before requesting team review on any PR
- **Blocking**: PR cannot be reviewed until docs are updated
- **Exemptions**: Test-only PRs and internal refactors (must state exemption in PR comment)

## Step 1: Determine Documentation Impact

Read the PR diff and classify the change:

| Change Type | CHANGELOG | Docs Update | README |
|-------------|:---------:|:-----------:|:------:|
| New feature / tool | ✅ Required | ✅ Required | ✅ If public-facing |
| Bug fix (behavior change) | ✅ Required | ✅ Update affected docs | ❌ |
| Bug fix (internal) | ✅ Required | ❌ | ❌ |
| Pipeline step change | ✅ Required | ✅ ARCHITECTURE.md | ❌ |
| Prompt change | ✅ Required | ✅ Note in relevant step docs | ❌ |
| Config change | ✅ Required | ✅ Update config reference | ❌ |
| Script/CI change | ✅ Required | ✅ START-SCRIPTS.md or CI docs | ❌ |
| Docs-only change | ❌ | ✅ (that's the PR itself) | ❌ |
| Test-only change | ❌ Exempt | ❌ Exempt | ❌ |
| Internal refactor | ❌ Exempt | ❌ Exempt | ❌ |

## Step 2: Update CHANGELOG.md

Add an entry under `## [Unreleased]` in the appropriate subsection:

```markdown
### Added      — New features, tools, capabilities
### Changed    — Changes to existing functionality
### Fixed      — Bug fixes
### Removed    — Removed features
### Deprecated — Soon-to-be-removed features
```

**Entry format:**
```markdown
- **Brief description** — One sentence explaining what and why. (PR #NNN, Issue #NNN)
```

**Rules:**
- Write from user perspective, not developer perspective
- Include PR number and issue number if applicable
- Be specific: "Added Azure AD branding detection" not "Updated regex patterns"
- Group related changes into a single entry

## Step 3: Update User-Facing Documentation

Route to the appropriate documentation file(s) based on what changed:

### Documentation Routing Table

| What Changed | Update This File | Section to Update |
|-------------|-----------------|-------------------|
| New CLI tool or command | `docs/PROJECT-GUIDE.md` | "Tools" or relevant section |
| New pipeline step or step change | `docs/ARCHITECTURE.md` | Step description |
| Start script options | `docs/START-SCRIPTS.md` | Command reference |
| AI prompt behavior | `docs/tool-generation-and-ai-improvements.md` | Relevant strategy section |
| Quality/compliance change | `docs/acrolinx-compliance-strategy.md` | Relevant rule section |
| Test infrastructure | `docs/test-strategy.md` | Coverage or framework section |
| Fingerprint/regression tool | `docs/FINGERPRINTING.md` | Relevant section |
| Config file format | `docs/PROJECT-GUIDE.md` | Configuration section |
| New dependency or prereq | `README.md` | Prerequisites section |
| New output file or directory | `README.md` | Output Structure section |
| Common error or fix | `docs/PROJECT-GUIDE.md` | Troubleshooting section |
| Breaking change | `README.md` + `docs/QUICK-START.md` | Migration note |

### What to Write

- **New feature**: Add a brief description of what it does, when to use it, and a minimal example
- **Behavior change**: Update any docs that describe the old behavior
- **Bug fix**: If users might encounter the old bug, add a note in troubleshooting
- **Config change**: Update the config reference with new fields/values

## Step 4: Update README Navigation (if applicable)

If a new documentation file was created, add it to the appropriate category in the README's Documentation section:

```markdown
## Documentation

### {Category}

| Document | Description |
|----------|-------------|
| [docs/NEW-FILE.md](docs/NEW-FILE.md) | Brief description |
```

## Step 5: Verify

Before marking docs as complete:

1. All new docs links in README are valid files
2. CHANGELOG entry is under `## [Unreleased]`
3. No stale references to old behavior remain in updated docs
4. Examples in docs are copy-pasteable and correct

## Anti-Patterns

- ❌ Skipping CHANGELOG for "small" changes
- ❌ Writing developer-facing changelog entries ("refactored regex") instead of user-facing ("improved branding detection")
- ❌ Updating CHANGELOG but not the actual documentation
- ❌ Adding a new doc file without linking it from README
- ❌ Leaving outdated examples in docs after a behavior change

## Related Decisions

- **AD-004**: PR Documentation Requirement (merge-blocking)
- **AD-026**: PR Documentation Skill — CHANGELOG + docs update required for every PR
