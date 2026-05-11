---
ms.topic: include
ms.date: 2026-05-11
mcp-cli.version: 3.0.0-beta.5+4637b2434cd6e8dcf285de245a71074bb00664db
generated: 2026-05-11 14:40:31 UTC
userPrompt: |
  Generate exactly 5 diverse example prompts for the following Azure MCP Server tool:
  
  Tool Name: find-unprotected
  Tool Command: azurebackup governance find-unprotected
  Description: Scans the subscription to find Azure resources that are not currently protected by any
  backup policy. Optionally filter by resource type, resource group, or tags.
  Action: find-unprotected
  Resource: governance
  
  Parameters:
  - Resource type filter (Optional): Resource types to filter (comma-separated).
  - Tag filter (Optional): Tag-based filter in key=value format (for example, `'environment=production'`).
  
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
    "azurebackup governance find-unprotected": [
      "prompt 1 text here",
      "prompt 2 text here"
    ]
  }
  
  The array MUST contain exactly 5 prompts.
  
  
  Generate the prompts now.
# [!INCLUDE [azurebackup governance find-unprotected](../includes/tools/example-prompts-prompts/azure-backup-governance-find-unprotected-input-prompt.md)]
# azmcp azurebackup governance find-unprotected
---

