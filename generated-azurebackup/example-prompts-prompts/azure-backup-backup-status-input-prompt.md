---
ms.topic: include
ms.date: 2026-05-11
mcp-cli.version: 3.0.0-beta.5+4637b2434cd6e8dcf285de245a71074bb00664db
generated: 2026-05-11 14:40:00 UTC
userPrompt: |
  Generate exactly 5 diverse example prompts for the following Azure MCP Server tool:
  
  Tool Name: status
  Tool Command: azurebackup backup status
  Description: Checks the backup status of an Azure resource and returns whether it is protected,
  along with vault and policy details. Use this to verify if a VM, disk, storage account,
  or other datasource is currently backed up. Requires the datasource ARM resource ID
  and the Azure region (location) where the resource exists.
  Action: status
  Resource: backup
  
  Parameters:
  - Datasource ID (Required): The datasource identifier. For VM/FileShare/DPP workloads, use the ARM resource ID (for example, `'/subscriptions/.../virtualMachines/myvm'`). For RSV in-guest workloads (SQL/SAPHANA), use the protectable item name from 'protectableitem list' (for example, `'SAPHanaDatabase;instance;dbname'`).
  - Location (Required): The Azure region (for example, `'eastus'`, `'westus2'`).
  
  ## ⚠️ Required Parameter Summary
  
  This tool has **2 required parameter(s)**: Datasource ID, Location
  
  **CRITICAL — Every single example prompt MUST include ALL 2 of these required parameters.** Each parameter value MUST appear in single quotes with a concrete, realistic example value (e.g., 'my-value'). Before outputting your response, verify that every prompt contains all 2 required parameters. Your response will be rejected if any required parameter is missing from any prompt.
  
  Rules for optional parameters:
  - If `resource-group` is Required, include it. If Optional, omit it.
  - If a parameter is marked Optional, include it only when truly necessary for the action.
  - **For optional resource-identifying parameters** (like resource name, item ID, etc. that specify which specific item to get):
    - Generate examples both WITHOUT and WITH the parameter when appropriate
    - This demonstrates the tool's dual capability: list all vs. get one
  
  Return your response as a JSON object with this exact structure:
  {
    "azurebackup backup status": [
      "prompt 1 text here",
      "prompt 2 text here"
    ]
  }
  
  The array MUST contain exactly 5 prompts.
  
  
  Generate the prompts now.
# [!INCLUDE [azurebackup backup status](../includes/tools/example-prompts-prompts/azure-backup-backup-status-input-prompt.md)]
# azmcp azurebackup backup status
---

