# SkillsRelevance

Collects and summarizes GitHub Copilot skills relevant to a specified Azure service or MCP namespace.

## Purpose

This tool helps documentation teams discover which GitHub Copilot skills are relevant to specific Azure services. It fetches skill definitions from three public repositories, analyzes their relevance to the requested service, and generates markdown documentation in the `generated/skills-relevance/` directory.

## Skill Sources

| Source | Repository | Path |
|--------|-----------|------|
| GitHub Awesome Copilot | [github/awesome-copilot](https://github.com/github/awesome-copilot) | `skills/` |
| Microsoft Skills | [microsoft/skills](https://github.com/microsoft/skills) | root |
| GitHub Copilot for Azure | [microsoft/GitHub-Copilot-for-Azure](https://github.com/microsoft/GitHub-Copilot-for-Azure) | `plugin/skills/` |

## Usage

```bash
dotnet run --project SkillsRelevance -- <service-name> [options]
```

### Arguments

| Argument | Description | Example |
|----------|-------------|---------|
| `<service-name>` | Azure service name or MCP namespace | `aks`, `storage`, `keyvault` |

### Options

| Option | Description | Default |
|--------|-------------|---------|
| `--output-path <path>` | Output directory | `../../generated/skills-relevance` |
| `--min-score <0.0-1.0>` | Minimum relevance score | `0.1` |
| `--all-skills` | Include all skills regardless of score | false |

### Environment Variables

| Variable | Description |
|----------|-------------|
| `GITHUB_TOKEN` | GitHub personal access token (recommended to avoid API rate limits) |

### Examples

```bash
# Analyze skills for Azure Kubernetes Service
dotnet run --project SkillsRelevance -- aks

# Analyze skills for Azure Storage with custom output
dotnet run --project SkillsRelevance -- storage --output-path ./generated/skills-relevance

# Analyze with higher minimum relevance threshold
dotnet run --project SkillsRelevance -- keyvault --min-score 0.3

# Include all skills (not just relevant ones)
dotnet run --project SkillsRelevance -- azure --all-skills
```

## Output

The tool generates markdown files in the output directory:

```
generated/skills-relevance/
├── index.md                              # Index of all generated files
└── {service-name}-skills-relevance.md   # Per-service skill summary
```

Each service file includes:
- Summary table of all relevant skills
- Per-skill sections with:
  - Skill name and source location
  - Azure services the skill interacts with
  - Skill purpose and description
  - Last updated date
  - Tags and categories
  - Best practices (if available in skill content)
  - Troubleshooting tips (if available in skill content)
  - Why the skill was considered relevant

## Architecture

| File | Purpose |
|------|---------|
| `Program.cs` | CLI entry point, orchestrates fetch → analyze → write |
| `Models/SkillInfo.cs` | Data model for a single skill with all metadata |
| `Models/SkillSource.cs` | Configuration for a skill source repository |
| `Models/GitHubFileEntry.cs` | GitHub API response model |
| `Services/GitHubSkillsFetcher.cs` | Fetches skills via GitHub REST API |
| `Services/SkillContentParser.cs` | Parses YAML/Markdown/JSON skill file content |
| `Services/SkillRelevanceAnalyzer.cs` | Scores skills for relevance to a service |
| `Output/SkillsMarkdownWriter.cs` | Generates markdown output files |

## Dependencies

- `.NET 9.0`
- `Shared` project (for `LogFileHelper`)
- GitHub REST API (unauthenticated or with `GITHUB_TOKEN`)

## Running Tests

```bash
dotnet test SkillsRelevance.Tests/
```
