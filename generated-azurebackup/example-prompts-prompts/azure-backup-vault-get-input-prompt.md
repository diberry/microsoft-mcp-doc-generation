---
ms.topic: include
ms.date: 2026-05-11
mcp-cli.version: 3.0.0-beta.5+4637b2434cd6e8dcf285de245a71074bb00664db
generated: 2026-05-11 14:44:03 UTC
userPrompt: |
  Generate exactly 5 diverse example prompts for the following Azure MCP Server tool:
  
  Tool Name: get
  Tool Command: azurebackup vault get
  Description: Retrieves backup vault information. When --vault and --resource-group are specified,
  returns detailed information about a single vault including type, location, SKU, and
  storage redundancy. When omitted, lists all backup vaults (RSV and Backup vaults) in
  the subscription. Optionally filter by --vault-type ('rsv' or 'dpp') and/or
  --resource-group to narrow the listing results.
  Action: get
  Resource: vault
  
  Parameters:
  - Vault name (Optional): The name of the backup vault (Recovery Services vault or Backup vault).
  - Vault type (Optional): The type of backup vault: 'rsv' (Recovery Services vault) or 'dpp' (Backup vault / Data Protection). Required for vault create; optional elsewhere (auto-detected if omitted).
  
  ## ⚠️ Required Parameter Summary
  
  This tool has **0 required parameter(s)**: (none)
  
  **CRITICAL — Every single example prompt MUST include ALL 0 of these required parameters.** Each parameter value MUST appear in single quotes with a concrete, realistic example value (e.g., 'my-value'). Before outputting your response, verify that every prompt contains all 0 required parameters. Your response will be rejected if any required parameter is missing from any prompt.
  
  Rules for optional parameters:
  - If `resource-group` is Required, include it. If Optional, omit it.
  - If a parameter is marked Optional, include it only when truly necessary for the action.
  - **For optional resource-identifying parameters** (like resource name, item ID, etc. that specify which specific item to get):
    - Generate examples both WITHOUT and WITH the parameter when appropriate
    - This demonstrates the tool's dual capability: list all vs. get one
  
  Return your response as a JSON object with this exact structure:
  {
    "azurebackup vault get": [
      "prompt 1 text here",
      "prompt 2 text here"
    ]
  }
  
  The array MUST contain exactly 5 prompts.
  
  
  Generate the prompts now.
# [!INCLUDE [azurebackup vault get](../includes/tools/example-prompts-prompts/azure-backup-vault-get-input-prompt.md)]
# azmcp azurebackup vault get
---

