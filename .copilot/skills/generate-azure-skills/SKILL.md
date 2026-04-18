# Generate Azure Skills Documentation

> Regenerate the Azure Skills markdown documentation files using the skills-generation pipeline, then deploy to the content repo.

## When to Use

Invoke this skill when the user says any of:
- "regenerate skills", "generate azure skills", "update skills files"
- "run skills pipeline", "rebuild skill docs"
- "generate skill for azure-storage" (single-skill variant)
- "deploy skills", "update skills content"

## Repository Layout

Three repos are involved:

| Repo | Path | Purpose |
|---|---|---|
| **microsoft-mcp-doc-generation** (this repo) | `C:\Users\diberry\project-dina\repos\project-azure-mcp-server\microsoft-mcp-doc-generation` | Generator code + templates |
| **azure-skills** (source) | `C:\Users\diberry\project-dina\repos\azure-skills` | Input: SKILL.md files + trigger tests |
| **azure-dev-docs-pr** (content target) | `C:\Users\diberry\project-dina\repos\project-azure-ai-tools\azure-dev-docs-pr` | Output destination for publishing to learn.microsoft.com |

**Source files** (input SKILL.md files) live at:
- Skills: `azure-skills/skills/{skill-name}/SKILL.md`
- Tests: `azure-skills/tests/{skill-name}/triggers.test.ts`

**Content files** (generated output) deploy to:
- `azure-dev-docs-pr/articles/azure-skills/skills/{skill-name}.md`

## Prerequisites

| Requirement | Details |
|---|---|
| .NET SDK | 9.0+ (builds `skills-generation.slnx`) |
| AI credentials (optional) | `FOUNDRY_API_KEY`, `FOUNDRY_ENDPOINT`, `FOUNDRY_MODEL_NAME` in environment or `docs-generation/.env` — omit for `--no-llm` mode |
| Working directory | Repository root (`microsoft-mcp-doc-generation/`) |
| Source repo clone | `azure-skills` cloned locally (see One-Time Setup) |
| Content repo clone | `azure-dev-docs-pr` cloned locally (see One-Time Setup) |

### One-time setup: Clone source repo

```bash
cd C:\Users\diberry\project-dina\repos
git clone --depth 1 https://github.com/microsoft/azure-skills.git
```

### One-time setup: Content repo

The content PR lives on `azure-dev-docs-pr` — should already be cloned at:
`C:\Users\diberry\project-dina\repos\project-azure-ai-tools\azure-dev-docs-pr`

## Step 0: Sync Source and Content Repos

**Always sync before generating** to ensure you have the latest skill definitions and content:

```bash
# Sync source repo (azure-skills)
cd C:\Users\diberry\project-dina\repos\azure-skills
git checkout main && git pull origin main

# Sync content repo (azure-dev-docs-pr)
cd C:\Users\diberry\project-dina\repos\project-azure-ai-tools\azure-dev-docs-pr
git checkout main && git pull origin main
```

## Step 1: Generate Skills

All commands run from the **microsoft-mcp-doc-generation** repository root.

### Generate all 24 skills (recommended)

```bash
./start-azure-skills.sh --no-llm \
  --source-path "C:\Users\diberry\project-dina\repos\azure-skills\skills" \
  --tests-path "C:\Users\diberry\project-dina\repos\azure-skills\tests"
```

### Generate a single skill

```bash
./start-azure-skills.sh azure-storage --no-llm \
  --source-path "C:\Users\diberry\project-dina\repos\azure-skills\skills" \
  --tests-path "C:\Users\diberry\project-dina\repos\azure-skills\tests"
```

### Quick options

```bash
# Dry run — parse and validate only, write nothing
./start-azure-skills.sh --dry-run \
  --source-path "C:\Users\diberry\project-dina\repos\azure-skills\skills" \
  --tests-path "C:\Users\diberry\project-dina\repos\azure-skills\tests"

# Force write even if validation fails
./start-azure-skills.sh --no-llm --force \
  --source-path "C:\Users\diberry\project-dina\repos\azure-skills\skills" \
  --tests-path "C:\Users\diberry\project-dina\repos\azure-skills\tests"
```

### PowerShell equivalent (Windows)

There is no separate `.ps1` wrapper. On Windows use Git Bash or WSL:

```powershell
bash ./start-azure-skills.sh --no-llm `
  --source-path "C:\Users\diberry\project-dina\repos\azure-skills\skills" `
  --tests-path "C:\Users\diberry\project-dina\repos\azure-skills\tests"
```

## Step 2: Deploy to Content Repo

After generation, copy files to azure-dev-docs-pr:

```powershell
$contentDir = "C:\Users\diberry\project-dina\repos\project-azure-ai-tools\azure-dev-docs-pr\articles\azure-skills\skills"
$genDir = "C:\Users\diberry\project-dina\repos\project-azure-mcp-server\microsoft-mcp-doc-generation\generated-skills"

# Remove old and copy new
Get-ChildItem "$contentDir\*.md" | Remove-Item
Get-ChildItem "$genDir\*.md" | ForEach-Object {
    Copy-Item $_.FullName "$contentDir\$($_.Name)"
}

# Commit
cd C:\Users\diberry\project-dina\repos\project-azure-ai-tools\azure-dev-docs-pr
git add articles/azure-skills/skills/
git commit -m "Regenerate skill pages

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
git push
```

## CLI Options Reference

The underlying CLI (`SkillsGen.Cli`) accepts these flags after `--`:

| Flag | Default | Description |
|---|---|---|
| `--no-llm` | off | Disable Azure OpenAI rewriting (use raw parsed content) |
| `--dry-run` | off | Parse and validate only — no files written |
| `--force` | off | Write output even when validation fails |
| `--out <dir>` | `../generated-skills/` | Override output directory |
| `--source` | `local` | Source mode: `local` or `github` |
| `--source-path` | `./skills-source/` | Path to SKILL.md source directory (use azure-skills path) |
| `--tests-path` | (none) | Path to tests directory for trigger files |
| `--verbose` | off | Verbose console output |

## What Gets Generated

Output lands in **`generated-skills/`** at the repository root (default `--out`):

```
generated-skills/
├── azure-storage.md          # One .md per skill (24 total)
├── azure-kubernetes.md
├── ...
├── generation-manifest.json  # Batch manifest with tier/status per skill
└── logs/                     # Generation logs
```

Each `.md` file contains the rendered skill documentation page produced from the Handlebars template at `skills-generation/templates/skill-page-template.hbs`.

## Verification Steps

After generation completes:

1. **Check exit code** — `0` means all skills passed validation; non-zero means at least one failed.
2. **Count output files** — expect 24 `.md` files:
   ```bash
   ls generated-skills/*.md | wc -l   # should be 24
   ```
3. **Spot-check a file** — open any `.md` and confirm it has YAML frontmatter, an H1 heading, and populated sections (description, triggers, tools, scenarios).
4. **Review console summary** — the CLI prints a pass/fail count:
   ```
   [skills-gen] Complete: 24/24 passed, 0 failed (12345ms)
   ```
5. **Check manifest** — `generated-skills/generation-manifest.json` lists every skill with its tier and validation status.

## Important Notes

- **NEVER use `--source github`** for iterative work. Each skill needs 2 API calls. With 24 skills, this exhausts GitHub API quota quickly. Always clone once and use `--source local`.
- **Always sync repos first** (Step 0) to avoid generating from stale data.
- **Acrolinx re-scores automatically** on every push to the content PR (~2-3 min).

## Troubleshooting

| Symptom | Fix |
|---|---|
| Build fails | Run `dotnet build skills-generation/skills-generation.slnx --configuration Release` manually to see errors |
| "No AI credentials found" warning | Set `FOUNDRY_API_KEY` and `FOUNDRY_ENDPOINT`, or pass `--no-llm` |
| "SKILL.md not found" | Verify `--source-path` points to `azure-skills/skills` and the repo is cloned |
| Validation failures | Review console WARN/ERROR lines; use `--force` to write anyway for inspection |
| Missing skills-inventory.json | Ensure `skills-generation/data/skills-inventory.json` exists (24 entries) |
| Stale content | Run Step 0 to sync both repos from remote main |
