# =============================================================================
# USER PROMPT TEMPLATE: PR Review to Service-Specific Instructions Generator
# =============================================================================
# Copy this template and fill in the variables to generate or update
# a service-specific instruction file from PR review comments.
# =============================================================================

## TASK

Analyze the following GitHub PR review comments JSON and generate a service-specific instruction file for **{{SERVICE_NAME}}** ({{SERVICE_SLUG}}).

## OPERATION MODE

{{OPERATION_MODE}}

<!-- Use one of these operation modes:
- **CREATE**: Generate a new instruction file from scratch
- **UPDATE**: Update an existing instruction file with new rules (existing file content provided below)
-->

## SOURCE INFORMATION

- **Service Name**: {{SERVICE_NAME}}
- **Service Slug**: {{SERVICE_SLUG}} (for filename: {{SERVICE_SLUG}}-instructions.md)
- **PR Number**: {{PR_NUMBER}}
- **PR URL**: {{PR_URL}}
- **Service Team**: {{TEAM_NAME}}

## PR REVIEW COMMENTS JSON

```json
{{PR_REVIEW_JSON}}
```

{{#if EXISTING_INSTRUCTIONS}}
## EXISTING INSTRUCTION FILE (for UPDATE mode)

```markdown
{{EXISTING_INSTRUCTIONS}}
```

### Update Instructions
When updating, please:
1. Preserve existing rules that are still valid
2. Add new rules from the new PR review
3. Remove or modify rules that conflict with new feedback
4. Add the new PR URL to the source references
{{/if}}

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

For {{SERVICE_NAME}}, pay special attention to:
{{SERVICE_FOCUS_AREAS}}

<!-- Example focus areas:
For Key Vault:
- Terminology (lowercase "key vault" vs "Key Vault")
- Managed HSM vs standard key vault distinctions
- Key/Secret/Certificate operation nuances

For Managed Lustre:
- HPC/Training workload terminology
- Auto-import/auto-export job terminology (not "settings")
- Naming conventions for filesystems and resource groups
-->

---

**Generate the instruction file now based on the JSON content above.**
