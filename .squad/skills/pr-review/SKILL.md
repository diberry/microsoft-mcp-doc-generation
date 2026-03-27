---
name: "Squad PR Review"
description: "Run full Squad team review on a pull request before merge"
domain: "code-review"
confidence: "high"
source: "manual"
tools:
  - name: "github-mcp-server-pull_request_read"
    description: "Read PR details, diff, and files"
    when: "To understand what changed in the PR"
  - name: "view"
    description: "Read source files for detailed review"
    when: "To review specific implementation details"
---

## Context

Every PR in this repository MUST receive a full Squad team review before merging (AD-005). No exceptions, regardless of PR size. This skill defines the review process.

## When to Run

- **Trigger**: When a PR is ready for review (author says "review", "ready for review", or asks to merge)
- **Blocking**: PR cannot be merged until this review is posted
- **Scope**: ALL PRs — features, fixes, docs, config changes, even single-line changes

## Review Process

### Step 1: Read the PR

1. Use `github-mcp-server-pull_request_read` with method `get` to get PR metadata
2. Use method `get_files` to list changed files
3. Use method `get_diff` to read the actual changes
4. Use `view` to read the full content of changed files when diff context is insufficient

### Step 2: Read Team Charters

Each reviewer's perspective is defined by their charter:
- `.squad/agents/avery/charter.md` — Lead/Architect
- `.squad/agents/morgan/charter.md` — C# Generator Developer
- `.squad/agents/quinn/charter.md` — DevOps/Scripts Engineer
- `.squad/agents/sage/charter.md` — AI/Prompt Engineer
- `.squad/agents/parker/charter.md` — QA/Tester
- `.squad/agents/reeve/charter.md` — Documentation Engineer

Also read `.squad/decisions.md` for project decisions that apply.

### Step 3: Determine Relevant Reviewers

Not every reviewer reviews every PR. Route based on changed files:

| Changed files | Required reviewers |
|--------------|-------------------|
| `*.cs` (non-test) | Morgan + Avery |
| `*.Tests/*.cs` | Parker + Avery |
| `*.ps1`, `*.sh`, `Dockerfile`, `*.yml` | Quinn + Avery |
| `prompts/`, `GenerativeAI/` | Sage + Avery |
| `docs/`, `README.md`, `*.md` (non-generated) | Reeve + Avery |
| Any code change | Parker (test coverage review) |
| Any PR | Reeve (documentation gate per AD-004) |

**Avery always reviews.** Parker and Reeve always review code changes.

### Step 4: Generate Reviews

For each relevant reviewer, provide a review in this format:

```
### {Name} ({Role}) — {APPROVED or CHANGES REQUESTED}

{2-3 sentences reviewing from their domain perspective}

**Issues found:** (if any)
- {specific issue with file and line reference}
```

**Review guidelines:**
- Only flag genuine issues that matter — no style nitpicks
- Reference specific files and line numbers
- Be thorough but fair
- APPROVED means no blocking issues
- CHANGES REQUESTED means there are issues that must be fixed before merge

### Step 5: Post Review as PR Comment

Post the complete review as a single PR comment using `gh pr comment`.

Format:
```
## Squad Team Review — PR #{number}

{Individual reviews from each relevant reviewer}

---

**Summary: {X} APPROVED, {Y} CHANGES REQUESTED.**
{If changes requested: "Address {reviewer} blockers before merge."}
{If all approved: "Ready to merge."}
```

## Anti-Patterns

- ❌ Merging without posting a review
- ❌ Posting reviews retroactively after merge
- ❌ Skipping reviews for "trivial" changes
- ❌ Only reviewing large PRs
- ❌ Rubber-stamp reviews with no substance

## Examples

**Small fix (2 files, URL change):**
Still gets Avery (architecture) + Reeve (docs) + relevant domain reviewer.

**Large feature (10+ files, new test project):**
Gets full team: Avery + Morgan + Parker + Sage + Reeve.

## Related Decisions

- **AD-004**: PR Documentation Requirement (merge-blocking)
- **AD-005**: All Work Must Go Through PRs
- **AD-007**: TDD Mandatory
- **AD-010**: Test Coverage Depth
