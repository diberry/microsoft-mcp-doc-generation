---
ms.topic: reference
ms.date: 2026-05-11 14:39:33 UTC
mcp-cli.version: 3.0.0-beta.5+4637b2434cd6e8dcf285de245a71074bb00664db
generated: 2026-05-11 14:39:33 UTC
---

# get

<!-- @mcpcli azurebackup protecteditem get -->

This tool retrieves protected item information from a backup vault. When `Protected item` is specified, this tool returns detailed information about a single backup instance, including protection status, data source details, policy assignment, and last backup time. When `Container name` is specified, this tool targets items in a Recovery Services vault (RSV) container. When `Protected item` is omitted, this tool lists all protected items in the vault.

Get details for protected item 'sales-db-backup' in resource group 'rg-production' and vault 'contoso-rsv'.  
List all protected items in resource group 'rg-test' and vault 'test-backup-vault'.

<!-- Required parameters: 2 - 'Resource group', 'Vault name' -->

Example prompts include:

- "List all protected items in resource group 'rg-prod' from vault 'backup-vault'."
- "Get details for protected item 'vm-prod-01' in resource group 'rg-prod' from vault 'backup-vault'."
- "Show protected items in resource group 'rg-staging' for vault 'rsv-vault' with container 'rsv-container1'."
- "Retrieve protected item 'sql-db-02' from container 'rsv-sql' in resource group 'rg-database' and vault 'data-protect-vault' with vault type 'rsv'."
- "What protected items are in vault 'protection-vault' within resource group 'rg-backup' when vault type is 'dpp'?"

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Resource group** |  Required | The name of the Azure resource group. This resource group is a logical container for Azure resources. |
| **Vault name** |  Required | The name of the backup vault (Recovery Services vault or Backup vault). |
| **Container name** |  Optional | The RSV protection container name. Only applicable for Recovery Services vaults. |
| **Protected item** |  Optional | The name of the protected item or backup instance. |
| **Vault type** |  Optional | The type of backup vault: `rsv` (Recovery Services vault) or `dpp` (Backup vault / Data Protection). Required for vault create; optional elsewhere (auto-detected if omitted). |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):
Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌ |