---
ms.topic: include
ms.date: 2026-05-11
mcp-cli.version: 3.0.0-beta.5+4637b2434cd6e8dcf285de245a71074bb00664db
generated: 2026-05-11 14:43:11 UTC
userPrompt: |
  Generate exactly 5 diverse example prompts for the following Azure MCP Server tool:
  
  Tool Name: undelete
  Tool Command: azurebackup protecteditem undelete
  Description: Undeletes or restores a soft-deleted backup item to an active protection state.
  Use this when a backup or protected item was accidentally deleted and needs to be recovered.
  For RSV vaults: pass the datasource ARM resource ID as --datasource-id.
  For DPP vaults: pass the datasource ARM resource ID as --datasource-id.
  Optionally specify --container for RSV workload items (SQL/HANA).
  The operation is asynchronous; use 'azurebackup job get' to monitor progress.
  Action: undelete
  Resource: protecteditem
  
  Parameters:
  - Datasource ID (Required): The datasource identifier. For VM/FileShare/DPP workloads, use the ARM resource ID (for example, `'/subscriptions/.../virtualMachines/myvm'`). For RSV in-guest workloads (SQL/SAPHANA), use the protectable item name from 'protectableitem list' (for example, `'SAPHanaDatabase;instance;dbname'`).
  - Resource group (Required): The name of the Azure resource group. This resource group is a logical container for Azure resources.
  - Vault name (Required): The name of the backup vault (Recovery Services vault or Backup vault).
  - Container name (Optional): The RSV protection container name. Only applicable for Recovery Services vaults.
  - Vault type (Optional): The type of backup vault: 'rsv' (Recovery Services vault) or 'dpp' (Backup vault / Data Protection). Required for vault create; optional elsewhere (auto-detected if omitted).
  
  ## ⚠️ Required Parameter Summary
  
  This tool has **3 required parameter(s)**: Datasource ID, Resource group, Vault name
  
  **CRITICAL — Every single example prompt MUST include ALL 3 of these required parameters.** Each parameter value MUST appear in single quotes with a concrete, realistic example value (e.g., 'my-value'). Before outputting your response, verify that every prompt contains all 3 required parameters. Your response will be rejected if any required parameter is missing from any prompt.
  
  Rules for optional parameters:
  - If `resource-group` is Required, include it. If Optional, omit it.
  - If a parameter is marked Optional, include it only when truly necessary for the action.
  - **For optional resource-identifying parameters** (like resource name, item ID, etc. that specify which specific item to get):
    - Generate examples both WITHOUT and WITH the parameter when appropriate
    - This demonstrates the tool's dual capability: list all vs. get one
  
  Return your response as a JSON object with this exact structure:
  {
    "azurebackup protecteditem undelete": [
      "prompt 1 text here",
      "prompt 2 text here"
    ]
  }
  
  The array MUST contain exactly 5 prompts.
  
  
  Generate the prompts now.
# [!INCLUDE [azurebackup protecteditem undelete](../includes/tools/example-prompts-prompts/azure-backup-protected-item-undelete-input-prompt.md)]
# azmcp azurebackup protecteditem undelete
---

