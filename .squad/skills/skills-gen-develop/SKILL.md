---
name: "Skills Generation Development"
description: "Develop, test, and iterate on the skills-generation pipeline"
domain: "development"
confidence: "high"
source: "manual"
tools:
  - name: "powershell"
    description: "Build, test, and run the pipeline"
    when: "To build, test, or generate skills"
  - name: "view"
    description: "Read source files"
    when: "To understand pipeline code"
  - name: "edit"
    description: "Modify pipeline code"
    when: "To fix issues or add features"
---

## Context

The skills-generation pipeline is an independent .NET project in `skills-generation/` that generates 24 Azure Skills documentation pages. It has its own solution (`skills-generation.slnx`), test project, and start script.

## When to Use

- Adding a new feature to the skills pipeline
- Fixing a bug in parsing, generation, or post-processing
- Improving Acrolinx compliance scores
- Adding a new skill to the inventory
- Updating the Handlebars template

## Key Principles

1. **TDD required** — write failing tests first, then implement (AD-007, AD-010)
2. **Never edit generated files** — fix the generator code, then regenerate
3. **Local source only** — always use `--source local` with a cloned repo to avoid GitHub API rate limits
4. **Vale before push** — run Vale lint locally before pushing to catch style issues

## Build and Test

```bash
# Build
dotnet build skills-generation/skills-generation.slnx --configuration Release

# Test (152 tests, 82.9% line coverage)
dotnet test skills-generation/skills-generation.slnx

# Test with coverage
dotnet test skills-generation/skills-generation.slnx --collect:"XPlat Code Coverage" --results-directory skills-generation/TestResults
pwsh skills-generation/scripts/check-coverage.ps1 -ResultsDir skills-generation/TestResults
```

## Generate Skills

```bash
# Prerequisites: clone source repo once
git clone --depth 1 https://github.com/microsoft/GitHub-Copilot-for-Azure.git

# Generate all 24 skills (no LLM, local source, ~3 seconds)
dotnet run --project skills-generation/SkillsGen.Cli --configuration Release -- \
  generate-skills --all --no-llm --source local \
  --source-path ./GitHub-Copilot-for-Azure/plugin/skills \
  --tests-path ./GitHub-Copilot-for-Azure/tests \
  --out ./generated-skills/

# Generate single skill
dotnet run --project skills-generation/SkillsGen.Cli --configuration Release -- \
  generate-skill azure-storage --no-llm --source local \
  --source-path ./GitHub-Copilot-for-Azure/plugin/skills \
  --tests-path ./GitHub-Copilot-for-Azure/tests \
  --out ./generated-skills/
```

## Run Vale Lint

```bash
# After generating, lint the output
pwsh skills-generation/scripts/lint-vale.ps1 -TargetDir ./generated-skills/
```

## Architecture

```
SkillsGen.Cli (entry point)
  → SkillPipelineOrchestrator
    → ISkillSourceFetcher (Local or GitHub)
    → SkillMarkdownParser (SKILL.md → SkillData)
    → TriggerTestParser (triggers.test.ts → TriggerData)
    → TierAssessor (5-question decision tree → Tier 1/2)
    → ILlmRewriter (AzureOpenAi or NoOp for --no-llm)
    → SkillPageGenerator (Handlebars template → markdown)
    → AcrolinxPostProcessor (static replacements, acronyms, contractions, term wrapping)
    → SkillPageValidator (7 validation gates)
    → Output to generated-skills/
```

## Key Files

| File | Purpose |
|------|---------|
| `SkillsGen.Core/Parsers/SkillMarkdownParser.cs` | Parse SKILL.md → SkillData (description cleaning, list extraction, display name derivation) |
| `SkillsGen.Core/Generation/SkillPageGenerator.cs` | Build template context, NaturalizeItems for bullet points, FixAcronymCasing |
| `SkillsGen.Core/PostProcessing/AcrolinxPostProcessor.cs` | Static replacements, contractions, acronym expansion, URL normalization, technical term wrapping |
| `templates/skill-page-template.hbs` | Handlebars template — uses `{{{triple-curly}}}` for raw output (no HTML escaping) |
| `data/skills-inventory.json` | Canonical list of 24 skills with display names and categories |
| `data/static-text-replacement.json` | 42 text replacements for Acrolinx compliance |
| `.vale.ini` | Vale prose linter config with Microsoft style guide |

## Common Development Tasks

### Adding a new skill
1. Add entry to `data/skills-inventory.json`
2. Ensure SKILL.md exists in the source repo
3. Regenerate — the pipeline handles everything else

### Fixing an Acrolinx issue
1. Check if it's a **terminology** issue → add to `static-text-replacement.json`
2. Check if it's a **spelling** issue → add to `WrapTechnicalTerms()` in AcrolinxPostProcessor
3. Check if it's a **clarity** issue → fix in template or NaturalizeItems
4. Regenerate and run Vale locally to verify

### Updating the template
1. Edit `templates/skill-page-template.hbs`
2. Remember: use `{{{var}}}` not `{{var}}` (triple-curly = raw markdown, double-curly = HTML-escaped)
3. Regenerate and check output

### Adding a prerequisite detection
1. Edit `BuildPrerequisites()` in `SkillPipelineOrchestrator.cs`
2. Add file extension → tool mapping or body keyword → resource mapping
3. Add test in orchestrator tests
