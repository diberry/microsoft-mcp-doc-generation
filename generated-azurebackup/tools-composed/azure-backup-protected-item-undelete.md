---
ms.topic: reference
ms.date: 2026-05-11 14:39:33 UTC
mcp-cli.version: 3.0.0-beta.5+4637b2434cd6e8dcf285de245a71074bb00664db
generated: 2026-05-11 14:39:33 UTC
---

# undelete

<!-- @mcpcli azurebackup protecteditem undelete -->

Undeletes or restores a soft-deleted backup item to an active protection state.
Use this when a backup or protected item was accidentally deleted and needs to be recovered.
For RSV vaults: pass the datasource ARM resource ID as --datasource-id.
For DPP vaults: pass the datasource ARM resource ID as --datasource-id.
Optionally specify --container for RSV workload items (SQL/HANA).
The operation is asynchronous; use 'azurebackup job get' to monitor progress.

<!-- Required parameters: 3 - 'Datasource ID', 'Resource group', 'Vault name' -->

Example prompts include:

- "Undelete protected item with datasource ID '/subscriptions/11111111-1111-1111-1111-111111111111/resourceGroups/rg-prod/providers/Microsoft.Compute/virtualMachines/myvm', resource group 'rg-prod', and vault name 'backup-vault'."
- "Can you undelete the protectable item with datasource ID 'SAPHanaDatabase;instance01;db01' from resource group 'rg-sql' in vault name 'rsv-vault'?"
- "Undelete the protected item with datasource ID 'SAPHanaDatabase;instance02;db02', resource group 'rg-sap', vault name 'rsv-main', and container 'sap-container'."
- "Recover the soft-deleted protected item with datasource ID '/subscriptions/33333333-3333-3333-3333-333333333333/resourceGroups/rg-dev/providers/Microsoft.Compute/virtualMachines/test-vm' in resource group 'rg-dev' using vault name 'backup-vault-east'."
- "Please undelete the file-share protected item with datasource ID '/subscriptions/44444444-4444-4444-4444-444444444444/resourceGroups/rg-files/providers/Microsoft.Storage/storageAccounts/myfileshare/fileServices/default/shares/myshare', resource group 'rg-files', vault name 'dpp-vault', and vault type 'dpp'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Datasource ID** |  Required | The datasource identifier. For VM/FileShare/DPP workloads, use the ARM resource ID (for example, `'/subscriptions/.../virtualMachines/myvm'`). For RSV in-guest workloads (SQL/SAPHANA), use the protectable item name from 'protectableitem list' (for example, `'SAPHanaDatabase;instance;dbname'`). |
| **Resource group** |  Required | The name of the Azure resource group. This resource group is a logical container for Azure resources. |
| **Vault name** |  Required | The name of the backup vault (Recovery Services vault or Backup vault). |
| **Container name** |  Optional | The RSV protection container name. Only applicable for Recovery Services vaults. |
| **Vault type** |  Optional | The type of backup vault: 'rsv' (Recovery Services vault) or 'dpp' (Backup vault / Data Protection). Required for vault create; optional elsewhere (auto-detected if omitted). |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ✅ | Idempotent: ✅ | Open World: ❌ | Read Only: ❌ | Secret: ❌ | Local Required: ❌

