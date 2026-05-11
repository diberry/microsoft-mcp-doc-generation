---
ms.topic: include
ms.date: 2026-05-11
mcp-cli.version: 3.0.0-beta.5+4637b2434cd6e8dcf285de245a71074bb00664db
generated: 2026-05-11 14:41:50 UTC
userPrompt: |
  Generate exactly 5 diverse example prompts for the following Azure MCP Server tool:
  
  Tool Name: get
  Tool Command: azurebackup policy get
  Description: Retrieves backup policy information. When --policy is specified, returns detailed
  information about a single policy including datasource types and protected items count.
  When omitted, lists all backup policies configured in the vault.
  Action: get
  Resource: policy
  
  Parameters:
  - Resource group (Required): The name of the Azure resource group. This resource group is a logical container for Azure resources.
  - Vault name (Required): The name of the backup vault (Recovery Services vault or Backup vault).
  - Policy (Optional): The name of the backup policy.
  - Vault type (Optional): The type of backup vault: 'rsv' (Recovery Services vault) or 'dpp' (Backup vault / Data Protection). Required for vault create; optional elsewhere (auto-detected if omitted).
  
  ## ⚠️ Required Parameter Summary
  
  This tool has **2 required parameter(s)**: Resource group, Vault name
  
  **CRITICAL — Every single example prompt MUST include ALL 2 of these required parameters.** Each parameter value MUST appear in single quotes with a concrete, realistic example value (e.g., 'my-value'). Before outputting your response, verify that every prompt contains all 2 required parameters. Your response will be rejected if any required parameter is missing from any prompt.
  
  Rules for optional parameters:
  - If `resource-group` is Required, include it. If Optional, omit it.
  - If a parameter is marked Optional, include it only when truly necessary for the action.
  - **For optional resource-identifying parameters** (like resource name, item ID, etc. that specify which specific item to get):
    - Generate examples both WITHOUT and WITH the parameter when appropriate
    - This demonstrates the tool's dual capability: list all vs. get one
  
  Return your response as a JSON object with this exact structure:
  {
    "azurebackup policy get": [
      "prompt 1 text here",
      "prompt 2 text here"
    ]
  }
  
  The array MUST contain exactly 5 prompts.
  
  
  Generate the prompts now.
# [!INCLUDE [azurebackup policy get](../includes/tools/example-prompts-prompts/azure-backup-policy-get-input-prompt.md)]
# azmcp azurebackup policy get
---

