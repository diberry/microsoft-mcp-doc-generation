---
ms.topic: reference
ms.date: 2026-05-11 14:39:33 UTC
mcp-cli.version: 3.0.0-beta.5+4637b2434cd6e8dcf285de245a71074bb00664db
generated: 2026-05-11 14:39:33 UTC
---

# find-unprotected

<!-- @mcpcli azurebackup governance find-unprotected -->

This Model Context Protocol (MCP) tool scans your subscription to find Azure resources that aren't protected by any Azure Backup policy. You can filter results by resource type, resource group, or tags. Use the results to identify resources that need backup configuration and help meet recovery and compliance objectives.

Examples

- For example, find unprotected virtual machines in resource group 'rg-prod'.
- For example, find unprotected storage accounts tagged 'environment=production'.

<!-- Required parameters: 0 -  -->

Example prompts include:

- "Find all Azure resources that aren't protected by any backup policy."
- "List unprotected resources filtered by resource type 'Microsoft.Compute/virtualMachines,Microsoft.Sql/servers/databases'."
- "Show unprotected Azure resources with tag 'environment=production'."
- "Scan for unprotected resources of resource type 'Microsoft.Storage/storageAccounts' with tag 'environment=staging'."
- "Which 'Microsoft.Sql/servers/databases' with tag 'backup=true' aren't protected by a backup policy?"

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Resource type filter** |  Optional | Resource types to filter (comma-separated). |
| **Tag filter** |  Optional | Tag-based filter in key=value format (for example, `'environment=production'`). |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):
Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌