---
name: azure-skills-content
description: >-
  Synchronization and drift detection for Azure Skills documentation — keeps
  published articles in azure-dev-docs-pr aligned with canonical SKILL.md
  sources in microsoft/azure-skills.
domain: content-generation
confidence: medium
source: earned — synthesized from content sync and drift detection workflows
status: active
category: documentation
---
# Azure Skills Content

Synchronization workflow and weekly drift detection for keeping Azure Skills documentation in `azure-dev-docs-pr` aligned with canonical source skills in `microsoft/azure-skills`.

---

## When to Use This Skill

Invoke this skill when you need to:

- **Sync content updates** from `microsoft/azure-skills` source skills to published documentation in `azure-dev-docs-pr`
- **Identify version gaps** between source SKILL.md files and their corresponding docs articles
- **Apply surgical fixes** to documentation articles (update specific sections without rewriting the entire file)
- **Validate content compliance** with Microsoft Learn docs standards (no absolute URLs, correct metadata, etc.)
- **Track sync status** across multiple skill articles (batch operations)

**Trigger phrases:**
- "Sync the [skill-name] documentation from azure-skills"
- "Update [article-name].md to version X.Y.Z from source"
- "Check version gaps between azure-skills and azure-dev-docs"
- "Apply surgical fixes to [skill-name] article"
- "Run the azure skills freshness check"
- "What skills are stale?"
- "Azure skills drift report"
- "Check source vs published for azure skills"
- "Check azure skills count"
- "Update skills count"

---

## Workflow: Version Gap Check

**Purpose:** Determine if a docs article is out of sync with its source skill.

### Steps:

1. **Locate source skill:**
   - Clone/pull `microsoft/azure-skills` repo
   - Navigate to `skills/{skill-name}/SKILL.md`

2. **Extract source version:**
   - Read SKILL.md frontmatter
   - Find `ms.custom: copilot-skill, version=X.Y.Z`
   - Store as `source_version`

3. **Locate docs article:**
   - Clone/pull `microsoftdocs/azure-dev-docs-pr` (internal `-pr` repo)
   - Navigate to `articles/ai/copilot-skills/{skill-name}.md`

4. **Extract docs version:**
   - Read article frontmatter
   - Find `ms.custom: copilot-skill, version=X.Y.Z`
   - Store as `docs_version`

5. **Compare versions:**
   - If `source_version > docs_version`: **Gap exists** (sync required)
   - If `source_version == docs_version`: **In sync** (no action)
   - If `docs_version > source_version`: **Anomaly** (investigate)

### Output:

Return structured report:

```markdown
## Version Gap Report

| Article | Docs Version | Source Version | Status | Action |
|---------|--------------|----------------|--------|--------|
| microsoft-foundry.md | 1.0.0 | 1.1.2 | ⚠️ Gap | Sync required |
| azure-kusto.md | 1.0.0 | 1.1.0 | ⚠️ Gap | Sync required |
| azure-storage.md | 1.1.2 | 1.1.2 | ✅ Synced | None |
```

---

## Workflow: Skills Count Verification

**Purpose:** Verify that hardcoded skill counts in overview pages stay in sync with the actual number of published skill reference articles.

**Trigger:** Part of every sync operation AND weekly drift detection.

### Steps:

1. **Count published skill articles**
   - Clone/pull `microsoftdocs/azure-dev-docs-pr` (internal `-pr` repo)
   - Count `.md` files in `articles/azure-skills/skills/` directory
   - Record as `published_count`

2. **Search for hardcoded counts**
   - Search in these files for skill count references:
     - `articles/azure-mcp-server/overview.md` (look for "packages N+ reusable Azure skills" pattern)
     - `articles/ai-developer-tools/overview.md` (search for any skills count reference)
     - `articles/azure-skills/overview.md` (search for any skills count reference)
   - Extract all referenced counts: `[count_1, count_2, ...]`

3. **Compare counts**
   - If any referenced count ≠ `published_count`: **Mismatch found**
   - If all referenced counts == `published_count`: **All in sync**

4. **Update mismatched counts**
   - For each file with a stale count:
     - Update the hardcoded count to match `published_count`
     - Update `ms.date` in frontmatter to current date
     - Create focused PR with "Update skill count" in title

5. **Report results**
   ```markdown
   ## Skills Count Verification Report
   
   **Published skills:** {published_count}
   **Last verified:** {CURRENT_DATETIME}
   
   | File | Current Count | Status | Action |
   |------|---------------|--------|--------|
   | articles/azure-mcp-server/overview.md | 19 | ✅ Match | None |
   | articles/ai-developer-tools/overview.md | 19 | ✅ Match | None |
   | articles/azure-skills/overview.md | 19 | ✅ Match | None |
   ```

---

## Workflow: Surgical Fix Application

**Purpose:** Update specific sections of a docs article without full rewrite.

### Steps:

#### 1. Read Source SKILL.md

- Clone `microsoft/azure-skills` (if not already available)
- Read `skills/{skill-name}/SKILL.md` in full
- Identify new/changed sections by comparing with docs article

#### 2. Identify Delta

Compare source vs. docs to find:
- **New sections** (e.g., "MCP Tools", "Sub-Skills")
- **Updated content** (e.g., expanded descriptions, new examples)
- **Removed content** (deprecated patterns, obsolete prerequisites)

#### 3. Plan Surgical Changes

For each delta, document:
- **Location:** Line numbers or section heading in docs article
- **Action:** Insert, replace, or delete
- **Content:** Exact markdown to apply

Example plan:
```markdown
### Change 1: Add Sub-Skills Table
- **Location:** After line 24 (description), before "## When to Use"
- **Action:** Insert
- **Content:** [markdown table with 11 sub-skills]

### Change 2: Update Version Metadata
- **Location:** Line 8 (frontmatter)
- **Action:** Replace
- **Old:** `ms.custom: copilot-skill, version=1.0.0`
- **New:** `ms.custom: copilot-skill, version=1.1.2`
```

#### 4. Apply Changes

Use precise `edit` tool calls:
- Match exact `old_str` including whitespace
- Apply changes sequentially (top-to-bottom of file)
- Preserve doc-specific sections (see "Preservation Rules" below)

#### 5. Update Metadata

Always update these frontmatter fields:
- **`ms.custom`:** Sync version to source (`version=X.Y.Z`)
- **`ms.date`:** Set to today's date (format: `MM/DD/YYYY`)

#### 6. Validate Syntax

Run automated checks:
- **No absolute URLs:** Search for `https://learn.microsoft.com/` patterns
  - Replace with relative paths: `/azure/developer/...`, `/entra/...`
- **Markdown lint:** Check table formatting, heading hierarchy
- **Build validation:** Run `npm run build` in azure-dev-docs-pr to catch `docs-link-absolute` errors

---

## Preservation Rules (CRITICAL)

When syncing content from source to docs, **preserve these doc-specific sections:**

### 1. "Next steps" Section

**Location:** Typically at end of article  
**Purpose:** Cross-links to related Learn content, tutorials, quickstarts  
**Rule:** Keep existing links unless they're broken or redundant with new source content

Example:
```markdown
## Next steps

- [Azure AI Foundry documentation](/azure/ai-services/ai-foundry/)
- [Deploy your first model](/azure/ai-services/ai-foundry/quickstart-deploy)
```

**Action:** Do NOT delete unless source SKILL.md provides replacement links

### 2. "See also" / "Related content" Section

**Location:** Often near end of article  
**Purpose:** Links to related skills, concepts, or external resources  
**Rule:** Preserve unless source explicitly deprecates the references

### 3. Microsoft Learn Metadata

**Location:** Frontmatter (YAML)  
**Purpose:** SEO, routing, build configuration  
**Rule:** Only update `ms.custom` and `ms.date`. Do NOT modify:
- `title`
- `description`
- `ms.service`
- `ms.topic`
- `author` / `ms.author`

### 4. Code Comments in Examples

**Location:** Within code blocks  
**Purpose:** Explain Learn-specific context (e.g., subscription IDs, resource naming)  
**Rule:** Keep comments that clarify Learn tutorials/quickstarts, even if not in source

---

## URL Validation Rules (HARD REQUIREMENT)

Microsoft Learn build system **rejects absolute URLs** to learn.microsoft.com domains.

### Prohibited Patterns:

❌ `https://learn.microsoft.com/azure/developer/intro`  
❌ `https://learn.microsoft.com/entra/agent-id/overview`  
❌ `[Link text](https://learn.microsoft.com/path/to/page)`

### Required Patterns:

✅ `/azure/developer/intro`  
✅ `/entra/agent-id/overview`  
✅ `[Link text](/path/to/page)`

### Exception:

External links (non-Learn domains) may use full URLs:
- ✅ `https://github.com/microsoft/azure-skills`
- ✅ `https://aka.ms/azurecli`

### Validation Command:

Search for violations before committing:
```bash
grep -n "https://learn.microsoft.com" articles/ai/copilot-skills/*.md
```

If matches found: Convert to relative paths.

---

## Batch Sync Workflow

**Purpose:** Sync multiple articles in a prioritized sequence.

**NOTE:** Skills count verification should always run as part of batch sync operations to ensure overview pages remain current.

### Steps:

1. **Run version gap check** (see "Version Gap Check" workflow above)

2. **Run skills count verification** (see "Skills Count Verification" workflow above)
   - This ensures overview references are accurate before publishing batch updates

3. **Prioritize articles:**
   - **HIGH:** Significant content gaps (new sections, major rewrites)
   - **MEDIUM:** Version bumps with minor updates
   - **LOW:** Metadata-only changes

3. **Create execution plan:**
   - Document changes for each HIGH priority article
   - Include line numbers, exact diffs, validation steps
   - Store plan in `projects/azure-ai-tools/content-fixes/{issue-number}-plan.md`

4. **Execute HIGH priority first:**
   - Apply surgical fixes one article at a time
   - Validate after each article (build, lint, URL check)
   - Create PRs targeting `microsoftdocs/azure-dev-docs-pr`

5. **Queue MEDIUM/LOW priority:**
   - Track in backlog issue (e.g., #174 absorbed into #169)
   - Execute in subsequent PRs to avoid merge conflicts

---

## Repository Structure

### Source of Truth: `microsoft/azure-skills`

```
microsoft/azure-skills/
├── skills/
│   ├── microsoft-foundry/
│   │   └── SKILL.md          ← Canonical source
│   ├── azure-kusto/
│   │   └── SKILL.md
│   ├── azure-storage/
│   │   └── SKILL.md
│   └── ... (other skills)
```

### Published Docs: `microsoftdocs/azure-dev-docs-pr`

```
azure-dev-docs-pr/
├── articles/
│   ├── ai/
│   │   ├── copilot-skills/
│   │   │   ├── microsoft-foundry.md  ← Sync target
│   │   │   ├── azure-kusto.md
│   │   │   ├── azure-storage.md
│   │   │   └── ... (other skills)
```

**CRITICAL:** Always target the **`-pr` (internal) repo**, never the public mirror.

---

## Workflow: Weekly Drift Detection

**Purpose:** Compare commit history in source SKILL.md files against published articles to identify what got stale.

**Cadence:** Run weekly (typically Monday).

**NOTE:** Skills count verification should always run as part of weekly drift detection to catch overview page staleness.


### Weekly audit workflow

1. **Set parameters:**
   - `lookback_days`: How far back to check (default: 30)
   - `exclude_open_prs`: Whether to skip skills with open PRs (default: true)
   - `include_auto_syncs`: Whether automated github-actions[bot] syncs count as material changes (default: false — only flag if sync introduces actual content differences)

2. **Fetch source commit history** for the lookback window:
   ```bash
   # Plugin skills
   gh api "repos/microsoft/azure-skills/commits?path=skills/&since={lookback_start_ISO}" --paginate --jq '.[].sha'

   # Per-skill granularity
   gh api "repos/microsoft/azure-skills/commits?path=skills/{skill-name}/SKILL.md&since={lookback_start_ISO}" --jq '.[] | {sha: .sha, date: .commit.committer.date, message: .commit.message, author: .commit.author.name}'

   # Standalone skills
   gh api "repos/microsoft/azure-skills/commits?path=skills/{skill-name}/SKILL.md&since={lookback_start_ISO}" --jq '.[] | {sha: .sha, date: .commit.committer.date, message: .commit.message}'
   ```

3. **Classify commits** by materiality:
   - **MATERIAL:** Human author commits, content rewrites, new capabilities, anti-pattern fixes, version bumps
   - **COSMETIC:** Automated syncs with no net content change, formatting-only, CI file changes
   - To distinguish: fetch the actual diff for automated syncs and check if SKILL.md content changed

4. **Enumerate published articles:**
   ```bash
   gh api "repos/MicrosoftDocs/azure-dev-docs-pr/contents/articles/azure-skills/skills" --jq '.[].name' | sed 's/\.md$//' | sort
   ```

5. **Exclude skills with open PRs** (if configured):
   ```bash
   gh pr list --repo MicrosoftDocs/azure-dev-docs-pr --state open --json number,title,files --limit 50
   # Filter for PRs touching articles/azure-skills/skills/{skill-name}.md
   ```

6. **For each skill with material commits:** Compare current source SKILL.md content against published article. Classify drift severity:

### Drift severity classification

| Severity | Signal | Action |
|----------|--------|--------|
| 🔴 CRITICAL | Structural rewrite (>50% content change), version bump, new sub-skills added, skill split/merged | Full article regeneration required |
| 🟡 MEDIUM | Anti-pattern fixes, capability additions, workflow changes, prerequisite updates | Targeted article update — diff and patch |
| 🟢 LOW | Single automated sync, minor wording, metadata-only | Monitor — may not need article change |

### Output format

```markdown
## Azure Skills Content Drift Report — {date}

**Parameters:** lookback={N} days, exclude_open_prs={yes/no}, auto_syncs={included/excluded}
**Source:** microsoft/azure-skills (commit range: {oldest_sha}..{newest_sha})

### 🔴 Critical Drift (requires full regeneration)

| Skill | Commits | Last change | Change summary |
|-------|---------|-------------|----------------|

### 🟡 Medium Drift (targeted update needed)

| Skill | Commits | Last change | Change summary |
|-------|---------|-------------|----------------|

### 🟢 Low Drift (monitor only)

| Skill | Commits | Last change | Change summary |
|-------|---------|-------------|----------------|

### ⏭️ Excluded (open PR covers changes)

| Skill | PR # | PR title |
|-------|------|----------|
```

---

## Common Pitfalls

### ❌ Pitfall 1: Full File Replacement

**Problem:** Overwriting entire docs article with source SKILL.md loses Learn-specific sections.  
**Solution:** Use surgical edits (insert, replace specific sections only).

### ❌ Pitfall 2: Absolute URLs

**Problem:** Copying links from source that use `https://learn.microsoft.com/...` format.  
**Solution:** Convert to relative paths during sync.

### ❌ Pitfall 3: Branch from Local Main

**Problem:** Branching from local `main` includes unpushed commits, bloating PR diffs.  
**Solution:** Always branch from `origin/main`:
```bash
git checkout -b pr/sync-foundry origin/main
```

### ❌ Pitfall 4: Targeting Public Repo

**Problem:** Creating PRs against `Azure/azure-dev-docs` (public mirror).  
**Solution:** Target `microsoftdocs/azure-dev-docs-pr` (internal repo).

### ❌ Pitfall 5: Ignoring Build Validation

**Problem:** Merging PRs without running `npm run build`, causing CI failures.  
**Solution:** Always validate locally before pushing.

---

## Tools and Commands

### Clone Repos

```bash
# Source skills
gh repo clone microsoft/azure-skills

# Target docs (internal)
gh repo clone microsoftdocs/azure-dev-docs-pr
```

### Version Extraction

```bash
# Source version
grep "ms.custom" microsoft/azure-skills/skills/microsoft-foundry/SKILL.md

# Docs version
grep "ms.custom" azure-dev-docs-pr/articles/ai/copilot-skills/microsoft-foundry.md
```

### URL Validation

```bash
# Check for absolute URLs
grep -rn "https://learn.microsoft.com" azure-dev-docs-pr/articles/ai/copilot-skills/
```

### Build Validation

```bash
cd azure-dev-docs-pr
npm install
npm run build
```

### Create PR

```bash
git checkout -b pr/sync-foundry origin/main
git add articles/ai/copilot-skills/microsoft-foundry.md
git commit -m "Sync microsoft-foundry.md to v1.1.2 from azure-skills

Fixes #169

- Add sub-skills table (11 workflows)
- Update description with comprehensive overview
- Remove deprecated PowerShell prerequisites
- Update version metadata to 1.1.2

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"

git push origin pr/sync-foundry
gh pr create --title "Sync microsoft-foundry skill to v1.1.2" --body "Addresses #169"
```

---

## Success Criteria

A sync operation is complete when:

- ✅ Docs article version matches source SKILL.md version
- ✅ New sections from source are present in docs article
- ✅ Doc-specific sections (Next steps, See also) are preserved
- ✅ No absolute URLs (`https://learn.microsoft.com/...`) remain
- ✅ `ms.date` updated to current date
- ✅ Build validation passes (`npm run build` succeeds)
- ✅ PR created targeting `microsoftdocs/azure-dev-docs-pr` (internal)

---

## Related Skills

- `azure-ai-tools-skills` — gap analysis (what's missing?)
- `content-source-validation` — per-article accuracy validation
- `azure-ai-tools-mcp` — decision gate for MCP content work

---

## References

- **Source Repository:** https://github.com/microsoft/azure-skills
- **Target Repository:** https://github.com/microsoftdocs/azure-dev-docs-pr (internal)
- **Learn Authoring Guide:** https://review.learn.microsoft.com/help/contribute/
- **Docs Validation Rules:** https://review.learn.microsoft.com/help/contribute/validation-reference/
