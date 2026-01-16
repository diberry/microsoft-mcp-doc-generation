# =============================================================================
# EXAMPLE: Managed Lustre User Prompt (Filled In)
# =============================================================================
# This shows how to fill in the user prompt template for Azure Managed Lustre.
# =============================================================================

## TASK

Analyze the following GitHub PR review comments JSON and generate a service-specific instruction file for **Azure Managed Lustre** (managed-lustre).

## OPERATION MODE

**CREATE**: Generate a new instruction file from scratch

## SOURCE INFORMATION

- **Service Name**: Azure Managed Lustre
- **Service Slug**: managed-lustre (for filename: managed-lustre-instructions.md)
- **PR Number**: 8353
- **PR URL**: https://github.com/MicrosoftDocs/azure-dev-docs-pr/pull/8353
- **Service Team**: Azure Managed Lustre Team

## PR REVIEW COMMENTS JSON

```json
{
  "pr": {
    "number": 8353,
    "title": "Update managed-lustre.md with example prompts",
    "url": "https://github.com/MicrosoftDocs/azure-dev-docs-pr/pull/8353"
  },
  "reviewComments": [
    {
      "user": { "login": "wolfgang-desalvador" },
      "author_association": "MEMBER",
      "body": "Can we use as example names \"TrainingDataFs\" and \"rg-training-lustre\" for instance? Just to align examples to the Lustre typical workloads"
    },
    {
      "user": { "login": "wolfgang-desalvador" },
      "author_association": "MEMBER",
      "body": "Same as above: as the name for the filesystem and the resource group can we use something more linked to HPC/training?"
    },
    {
      "user": { "login": "wolfgang-desalvador" },
      "author_association": "MEMBER",
      "body": "```suggestion\n- **Get autoimport jobs**: \"Get the autoimport jobs for filesystem 'TrainingDataFs' in resource group 'rg-training-lustre'\"\n```\nI would use \"jobs\" or \"tasks\" in the prompts instead of \"settings\". It resonates more with typical Lustre users in HPC and training"
    },
    {
      "user": { "login": "wolfgang-desalvador" },
      "author_association": "MEMBER",
      "body": "I would suggest adding an example where you use the prefix and the conflict resolution mode as they are optional parameters but commonly used"
    },
    {
      "user": { "login": "wolfgang-desalvador" },
      "author_association": "MEMBER",
      "body": "```suggestion\n- **List autoexport jobs**: \"Show me the autoexport jobs for filesystem 'TrainingOutputFs' in resource group 'rg-training'\"\n```\nSame feedback here - use \"jobs\" not \"configuration\" or \"settings\""
    }
  ]
}
```

## OUTPUT REQUIREMENTS

1. Generate a complete markdown instruction file
2. Follow the standard format with these sections:
   - Header with service name and PR source
   - TERMINOLOGY REQUIREMENTS
   - Service-specific sections as needed
   - PROMPT STRUCTURE GUIDELINES
   - EXAMPLE CORRECTIONS FROM PR REVIEW
3. Only include rules that are clearly supported by reviewer comments
4. Ignore automated bot comments (Acrolinx, PoliCheck, Learn Build Service, etc.)
5. Focus on comments from reviewers with author_association "MEMBER" or "CONTRIBUTOR"

## ANALYSIS FOCUS

For Azure Managed Lustre, pay special attention to:
- HPC and AI/ML training workload context (typical Lustre use cases)
- Use "jobs" or "tasks" for auto-import/auto-export, NOT "settings" or "configuration"
- Naming conventions: Use HPC/training-focused names (TrainingDataFs, rg-training, etc.)
- Include examples with optional parameters (prefix, conflict resolution mode)
- Domain-appropriate resource group naming (rg-training, rg-hpc, etc.)

---

**Generate the instruction file now based on the JSON content above.**
