---
ms.topic: include
ms.date: 2026-05-11
mcp-cli.version: 3.0.0-beta.5+4637b2434cd6e8dcf285de245a71074bb00664db
generated: 2026-05-11 14:41:04 UTC
userPrompt: |
  Generate exactly 5 diverse example prompts for the following Azure MCP Server tool:
  
  Tool Name: soft-delete
  Tool Command: azurebackup governance soft-delete
  Description: Configures the soft delete settings for a backup vault. Set the state to 'AlwaysOn', 'On',
  or 'Off', and optionally specify the retention period in days (14-180).
  Action: soft-delete
  Resource: governance
  
  Parameters:
  - Resource group (Required): The name of the Azure resource group. This resource group is a logical container for Azure resources.
  - Soft delete (Required): Soft delete state: `AlwaysOn`, `On`, or `Off`.
  - Vault name (Required): The name of the backup vault (Recovery Services vault or Backup vault).
  - Soft delete retention days (Optional): Soft delete retention period (14-180 days).
  - Vault type (Optional): The type of backup vault: 'rsv' (Recovery Services vault) or 'dpp' (Backup vault / Data Protection). Required for vault create; optional elsewhere (auto-detected if omitted).
  
  ## ⚠️ Required Parameter Summary
  
  This tool has **3 required parameter(s)**: Resource group, Soft delete, Vault name
  
  **CRITICAL — Every single example prompt MUST include ALL 3 of these required parameters.** Each parameter value MUST appear in single quotes with a concrete, realistic example value (e.g., 'my-value'). Before outputting your response, verify that every prompt contains all 3 required parameters. Your response will be rejected if any required parameter is missing from any prompt.
  
  Rules for optional parameters:
  - If `resource-group` is Required, include it. If Optional, omit it.
  - If a parameter is marked Optional, include it only when truly necessary for the action.
  - **For optional resource-identifying parameters** (like resource name, item ID, etc. that specify which specific item to get):
    - Generate examples both WITHOUT and WITH the parameter when appropriate
    - This demonstrates the tool's dual capability: list all vs. get one
  
  Return your response as a JSON object with this exact structure:
  {
    "azurebackup governance soft-delete": [
      "prompt 1 text here",
      "prompt 2 text here"
    ]
  }
  
  The array MUST contain exactly 5 prompts.
  
  
  Generate the prompts now.
# [!INCLUDE [azurebackup governance soft-delete](../includes/tools/example-prompts-prompts/azure-backup-governance-soft-delete-input-prompt.md)]
# azmcp azurebackup governance soft-delete
---

