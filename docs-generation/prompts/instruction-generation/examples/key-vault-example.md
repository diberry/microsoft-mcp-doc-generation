# =============================================================================
# EXAMPLE: Key Vault User Prompt (Filled In)
# =============================================================================
# This shows how to fill in the user prompt template for Azure Key Vault.
# =============================================================================

## TASK

Analyze the following GitHub PR review comments JSON and generate a service-specific instruction file for **Azure Key Vault** (key-vault).

## OPERATION MODE

**CREATE**: Generate a new instruction file from scratch

## SOURCE INFORMATION

- **Service Name**: Azure Key Vault
- **Service Slug**: key-vault (for filename: key-vault-instructions.md)
- **PR Number**: 8229
- **PR URL**: https://github.com/MicrosoftDocs/azure-dev-docs-pr/pull/8229
- **Service Team**: Key Vault SDK Team

## PR REVIEW COMMENTS JSON

```json
{
  "pr": {
    "number": 8229,
    "title": "Update azure-key-vault.md with example prompts",
    "url": "https://github.com/MicrosoftDocs/azure-dev-docs-pr/pull/8229"
  },
  "reviewComments": [
    {
      "user": { "login": "vcolin7" },
      "author_association": "MEMBER",
      "body": "```suggestion\n- **Create RSA key**: \"Create a new RSA key named 'app-encryption-key' in my key vault 'mykeyvault'\"\n- **Generate EC key**: \"Generate a new EC key called 'signing-key' in key vault 'security-kv'\"\n```"
    },
    {
      "user": { "login": "vcolin7" },
      "author_association": "MEMBER",
      "body": "I would word things a bit differently in some of these:\n```suggestion\n- **Get key details**: \"Show me details of the key 'app-encryption-key' in my key vault 'mykeyvault'\"\n```"
    },
    {
      "user": { "login": "vcolin7" },
      "author_association": "MEMBER",
      "body": "Also, you cannot technically retrieve a whole key from the Key Vault service like this. You only retrieve the key properties/details, so saying \"retrieve key\" would be inaccurate."
    },
    {
      "user": { "login": "vcolin7" },
      "author_association": "MEMBER",
      "body": "We can consolidate a couple of these. Also added small corrections as this operation is only valid on managed HSMs but not key vaults:\n```suggestion\n- **Get account settings**:\n  - \"Get the account settings for my managed HSM 'myhsm'\"\n  - \"Show me the account settings for managed HSM 'contoso-hsm'\"\n```"
    },
    {
      "user": { "login": "vcolin7" },
      "author_association": "MEMBER",
      "body": "I removed the app config example because we should encourage users to utilize the App Configuration service for such cases."
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

For Azure Key Vault, pay special attention to:
- Terminology (lowercase "key vault" vs "Key Vault")
- Managed HSM vs standard key vault distinctions (some operations only work on Managed HSM)
- Key/Secret/Certificate operation nuances (e.g., can't "retrieve" a key, only properties)
- Avoiding app configuration storage in secrets (use App Configuration service instead)
- Word order: "key vault 'name'" not "'name' Key Vault"

---

**Generate the instruction file now based on the JSON content above.**
