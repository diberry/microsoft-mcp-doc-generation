---
name: "Skills Generation Run & Deploy"
description: "Generate skill pages and deploy to the content PR"
domain: "deployment"
confidence: "high"
source: "manual"
tools:
  - name: "powershell"
    description: "Run generation pipeline and copy files"
    when: "To generate and deploy skill pages"
  - name: "github-mcp-server-pull_request_read"
    description: "Check content PR status"
    when: "To verify deployment"
---

## Context

Generated skill pages live in `generated-skills/` locally and are copied to the content PR branch on `azure-dev-docs-pr` for publishing to learn.microsoft.com.

## When to Use

- After fixing generator code and need to regenerate
- When deploying updated skill pages to the content PR
- First-time setup of the source data clone

## Prerequisites

### One-time: Clone source repo

```bash
cd C:\Users\diberry\project-dina\repos
git clone --depth 1 https://github.com/microsoft/GitHub-Copilot-for-Azure.git
```

This gives local access to SKILL.md files and trigger tests with zero API calls.

### One-time: Clone content repo

The content PR lives on `diberry/azure-dev-docs-pr`, branch `squad/ai-dev-tools-docset`:

```bash
cd C:\Users\diberry\project-dina\repos
# Already cloned as azure-dev-docs-pr-diberry
```

## Full Generation Workflow

### Step 1: Build

```bash
cd microsoft-mcp-doc-generation/skills-generation
dotnet build skills-generation.slnx --configuration Release
```

### Step 2: Run tests

```bash
dotnet test skills-generation.slnx
# Expected: 152 passed, 0 failed
```

### Step 3: Generate all 24 skills

```bash
dotnet run --project SkillsGen.Cli --configuration Release -- \
  generate-skills --all --no-llm --source local \
  --source-path "C:\Users\diberry\project-dina\repos\GitHub-Copilot-for-Azure\plugin\skills" \
  --tests-path "C:\Users\diberry\project-dina\repos\GitHub-Copilot-for-Azure\tests" \
  --out "../generated-skills/"
# Expected: 24/24 passed, ~3-5 seconds
```

### Step 4: Run Vale lint

```bash
pwsh scripts/lint-vale.ps1 -TargetDir ../generated-skills/
# Expected: ~21 issues (mostly false positives)
```

### Step 5: Deploy to content PR

```powershell
cd C:\Users\diberry\project-dina\repos\azure-dev-docs-pr-diberry
git checkout squad/ai-dev-tools-docset

# Remove old and copy new
Get-ChildItem "articles/azure-skills/skills/*.md" | Remove-Item
$genDir = "C:\Users\diberry\project-dina\repos\microsoft-mcp-doc-generation\generated-skills"
Get-ChildItem "$genDir\*.md" | ForEach-Object {
    Copy-Item $_.FullName "articles/azure-skills/skills/$($_.Name)"
}

# Commit and push
git add articles/azure-skills/skills/
git commit -m "Regenerate skill pages

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
git push origin squad/ai-dev-tools-docset
```

### Step 6: Wait for Acrolinx

Acrolinx re-scores automatically on every push to the content PR (~2-3 min). Check the PR comments for the latest scorecard.

## Single Skill Generation

For iterating on one skill:

```bash
dotnet run --project SkillsGen.Cli --configuration Release -- \
  generate-skill azure-storage --no-llm --source local \
  --source-path "C:\Users\diberry\project-dina\repos\GitHub-Copilot-for-Azure\plugin\skills" \
  --tests-path "C:\Users\diberry\project-dina\repos\GitHub-Copilot-for-Azure\tests" \
  --out "../generated-skills/"
```

## CLI Options Reference

| Flag | Default | Description |
|------|---------|-------------|
| `--source` | `local` | `local` or `github` |
| `--source-path` | `./skills-source/` | Path to `plugin/skills/` directory |
| `--tests-path` | (none) | Path to `tests/` directory for trigger files |
| `--out` | `./generated-skills/` | Output directory |
| `--no-llm` | false | Skip LLM rewriting (use template-only output) |
| `--dry-run` | false | Parse and validate without writing |
| `--force` | false | Write even if validation fails |
| `--verbose` | false | Detailed logging |

## Important: Rate Limit Prevention

**NEVER use `--source github` for iterative work.** Each skill needs 2 API calls (SKILL.md + triggers.test.ts). With 24 skills and retries, this exhausts the GitHub API quota quickly. Always clone once and use `--source local`.
