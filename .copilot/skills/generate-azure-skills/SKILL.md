# Generate Azure Skills Documentation

> Regenerate the Azure Skills markdown documentation files using the skills-generation pipeline.

## When to Use

Invoke this skill when the user says any of:
- "regenerate skills", "generate azure skills", "update skills files"
- "run skills pipeline", "rebuild skill docs"
- "generate skill for azure-storage" (single-skill variant)

## Prerequisites

| Requirement | Details |
|---|---|
| .NET SDK | 9.0+ (builds `skills-generation.slnx`) |
| AI credentials (optional) | `FOUNDRY_API_KEY`, `FOUNDRY_ENDPOINT`, `FOUNDRY_MODEL_NAME` in environment or `docs-generation/.env` — omit for `--no-llm` mode |
| Working directory | Repository root (`microsoft-mcp-doc-generation/`) |

## Commands

All commands run from the **repository root**.

### Generate all 24 skills

```bash
./start-azure-skills.sh
```

### Generate a single skill by name

```bash
./start-azure-skills.sh azure-storage
```

### Skip LLM rewriting (faster, no AI credentials needed)

```bash
./start-azure-skills.sh --no-llm
```

### Dry run — parse and validate only, write nothing

```bash
./start-azure-skills.sh --dry-run
```

### Combine options

```bash
# Single skill, no LLM
./start-azure-skills.sh azure-storage --no-llm

# Single skill, dry run
./start-azure-skills.sh azure-storage --dry-run

# All skills, no LLM, forced write even if validation fails
./start-azure-skills.sh --no-llm --force
```

### PowerShell equivalent (Windows)

There is no separate `.ps1` wrapper. On Windows use Git Bash or WSL:

```powershell
bash ./start-azure-skills.sh azure-storage --no-llm
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
| `--source-path` | `./skills-source/` | Path to local skills source directory |
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

## Troubleshooting

| Symptom | Fix |
|---|---|
| Build fails | Run `dotnet build skills-generation/skills-generation.slnx --configuration Release` manually to see errors |
| "No AI credentials found" warning | Set `FOUNDRY_API_KEY` and `FOUNDRY_ENDPOINT`, or pass `--no-llm` |
| Validation failures | Review console WARN/ERROR lines; use `--force` to write anyway for inspection |
| Missing skills-inventory.json | Ensure `skills-generation/data/skills-inventory.json` exists (24 entries) |
