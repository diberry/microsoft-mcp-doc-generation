# Azure Skills Documentation Generator

Generates customer-facing markdown documentation for Azure Skills used in GitHub Copilot. Produces one article per skill covering prerequisites, trigger prompts, MCP tools, decision guidance, and workflows.

## Quick start

```bash
# From the repository root
./start-azure-skills.sh
```

Requires a `.env` file in `skills-generation/` with Azure OpenAI credentials:

```env
FOUNDRY_API_KEY=<your-key>
FOUNDRY_ENDPOINT=<your-endpoint>
FOUNDRY_MODEL_NAME=gpt-4o
```

## Architecture

```
SkillPipelineOrchestrator
  ├── Fetch      → GitHubSkillFetcher / LocalSkillFetcher
  ├── Parse      → SkillMarkdownParser + TriggerTestParser
  ├── Assess     → TierAssessor (Tier 1 / Tier 2 scoring)
  ├── Rewrite    → AzureOpenAiRewriter (LLM intro polish)
  ├── Generate   → SkillPageGenerator (Handlebars template)
  ├── Post-proc  → AcrolinxPostProcessor (contractions, URLs, acronyms)
  └── Validate   → SkillPageValidator (sections, frontmatter, word count)
```

**Tier assessment** scores each skill on 5 questions (Q1–Q5) covering service count, use-for scenarios, trigger prompts, description depth, and Azure service references. Tier 1 (score ≥ 4) gets comprehensive articles; Tier 2 gets essential-only articles.

## Projects

| Project | Purpose |
|---------|---------|
| `SkillsGen.Cli` | CLI entry point (`dotnet run -- --help`) |
| `SkillsGen.Core` | Pipeline logic, parsers, generators, validators |
| `SkillsGen.Core.Tests` | xUnit tests with NSubstitute + FluentAssertions |

## CLI usage

```bash
# Generate all skills from inventory
dotnet run --project SkillsGen.Cli -- --inventory data/skills-inventory.json --output ./generated-skills

# Generate a single skill
dotnet run --project SkillsGen.Cli -- --skill azure-storage --output ./generated-skills

# Dry run (no file writes)
dotnet run --project SkillsGen.Cli -- --inventory data/skills-inventory.json --dry-run

# Force output despite validation errors
dotnet run --project SkillsGen.Cli -- --skill azure-storage --force
```

## Development

```bash
# Build
dotnet build skills-generation/skills-generation.slnx

# Test
dotnet test skills-generation/skills-generation.slnx

# Build Release
dotnet build skills-generation/skills-generation.slnx --configuration Release
```

### Key directories

| Directory | Contents |
|-----------|----------|
| `data/` | Skills inventory, Acrolinx rules, acronyms |
| `prompts/` | LLM system and user prompt templates |
| `templates/` | Handlebars page template |

### LLM retry logic

`AzureOpenAiRewriter` uses exponential backoff (5 retries: 1s → 2s → 4s → 8s → 16s) on rate-limit errors (HTTP 429). Non-rate-limit errors fail immediately.
