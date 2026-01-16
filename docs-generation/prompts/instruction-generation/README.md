# PR Review to Service-Specific Instructions Generator

This directory contains prompts for automatically generating or updating service-specific Copilot instruction files from GitHub PR review comments.

## Files

| File | Purpose |
|------|---------|
| [system-prompt.md](system-prompt.md) | System prompt that instructs the AI how to analyze PR reviews and extract rules |
| [user-prompt-template.md](user-prompt-template.md) | User prompt template with variables to fill in |
| [examples/](examples/) | Example filled-in prompts for reference |

## Quick Start

### 1. Collect PR Review Comments

First, extract the PR review comments to a JSON file. The JSON should have this structure:

```json
{
  "pr": {
    "number": 8229,
    "title": "Azure Key Vault documentation update",
    "url": "https://github.com/MicrosoftDocs/azure-dev-docs-pr/pull/8229"
  },
  "issueComments": [...],
  "reviewComments": [...]
}
```

Save this to `generated/pr-review-feedback/{service-name}.json`.

### 2. Prepare the User Prompt

Copy the `user-prompt-template.md` and replace the variables:

| Variable | Example Value |
|----------|---------------|
| `{{SERVICE_NAME}}` | Azure Key Vault |
| `{{SERVICE_SLUG}}` | key-vault |
| `{{PR_NUMBER}}` | 8229 |
| `{{PR_URL}}` | https://github.com/MicrosoftDocs/azure-dev-docs-pr/pull/8229 |
| `{{TEAM_NAME}}` | Key Vault SDK |
| `{{PR_REVIEW_JSON}}` | (paste the JSON content) |
| `{{OPERATION_MODE}}` | CREATE or UPDATE |
| `{{SERVICE_FOCUS_AREAS}}` | (list specific areas to focus on) |

### 3. Run the Generation

Use the system prompt and your filled-in user prompt with your preferred AI assistant:

```
System: [content of system-prompt.md]
User: [content of your filled-in user prompt]
```

### 4. Save the Output

Save the generated instruction file to:
```
docs-generation/prompts/service-specific/{service-slug}-instructions.md
```

## What Gets Extracted

The prompts are designed to extract these types of service-specific rules:

### Terminology Requirements
- Words/phrases that must be used or avoided
- Case sensitivity (e.g., "key vault" not "Key Vault")
- Word order preferences

### Technical Accuracy
- Operation limitations (e.g., "can't retrieve keys, only properties")
- Resource type distinctions (e.g., Managed HSM vs standard vault)

### Naming Conventions
- Example resource names appropriate for the service domain
- Resource group naming patterns

### Parameter Patterns
- How to demonstrate optional parameters
- Natural language vs. technical parameter syntax

## What Gets Ignored

The prompts filter out:

- ❌ Automated bot comments (Acrolinx, PoliCheck, Learn Build Service)
- ❌ Generic documentation style feedback
- ❌ Formatting preferences unrelated to the service
- ❌ Comments without clear actionable rules

## Example Services

See the `examples/` directory for complete examples:

- **Key Vault**: Terminology (lowercase), HSM distinction, key operations
- **Managed Lustre**: HPC context, job terminology, naming conventions

## Updating Existing Instructions

To update an existing instruction file:

1. Set `{{OPERATION_MODE}}` to `UPDATE`
2. Include the existing file content in `{{EXISTING_INSTRUCTIONS}}`
3. The AI will merge new rules with existing ones

## Best Practices

1. **Review the output**: Always review generated instructions before committing
2. **Be conservative**: It's better to miss a rule than create an incorrect one
3. **Cite sources**: Keep PR URLs as references in the instruction files
4. **Iterate**: If the first pass misses something, add specific focus areas and regenerate
