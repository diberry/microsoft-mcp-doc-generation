---
ms.topic: reference
ms.date: 2026-05-11 14:39:33 UTC
mcp-cli.version: 3.0.0-beta.5+4637b2434cd6e8dcf285de245a71074bb00664db
generated: 2026-05-11 14:39:33 UTC
---

# get

<!-- @mcpcli azurebackup recoverypoint get -->

Retrieves recovery point information for a protected item. When --recovery-point is
specified, returns detailed information about a single recovery point including time
and type. When omitted, lists all available recovery points for the protected item.

<!-- Required parameters: 3 - 'Protected item', 'Resource group', 'Vault name' -->

Example prompts include:

- "List all recovery points for protected item 'vm-backup-01' in resource group 'rg-prod' and vault name 'backup-vault'."
- "Get recovery point 'rp-20250501T120000Z' for protected item 'fileserver-01' in resource group 'rg-file-prod' and vault name 'dpp-vault' with vault type 'dpp'."
- "Show details for recovery point 'a1b2c3d4-ef56-7890-ab12-34567890cdef' of protected item 'sql-db-01' in resource group 'rg-db' vault name 'rsv-backup' container 'rsv-container-1'."
- "What recovery points exist for protected item 'appservice-backup' in resource group 'rg-web' vault name 'rsv-central' container 'rsv-web-cont' vault type 'rsv'?"
- "Retrieve recovery point '2026-04-10T08:30:00Z' for protected item 'vm-prod-02' in resource group 'rg-infra' vault name 'backup-vault-main'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Protected item** |  Required | The name of the protected item or backup instance. |
| **Resource group** |  Required | The name of the Azure resource group. This resource group is a logical container for Azure resources. |
| **Vault name** |  Required | The name of the backup vault (Recovery Services vault or Backup vault). |
| **Container name** |  Optional | The RSV protection container name. Only applicable for Recovery Services vaults. |
| **Recovery point** |  Optional | The recovery point ID. |
| **Vault type** |  Optional | The type of backup vault: 'rsv' (Recovery Services vault) or 'dpp' (Backup vault / Data Protection). Required for vault create; optional elsewhere (auto-detected if omitted). |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌

