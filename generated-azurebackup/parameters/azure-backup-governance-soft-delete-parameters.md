---
ms.topic: include
ms.date: 2026-05-11
mcp-cli.version: 3.0.0-beta.5+4637b2434cd6e8dcf285de245a71074bb00664db
generated: 2026-05-11 14:39:41 UTC
# [!INCLUDE [azurebackup governance soft-delete](../includes/tools/parameters/azure-backup-governance-soft-delete-parameters.md)]
# azmcp azurebackup governance soft-delete
---

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Resource group** |  Required | The name of the Azure resource group. This resource group is a logical container for Azure resources. |
| **Soft delete** |  Required | Soft delete state: `AlwaysOn`, `On`, or `Off`. |
| **Vault name** |  Required | The name of the backup vault (Recovery Services vault or Backup vault). |
| **Soft delete retention days** |  Optional | Soft delete retention period (14-180 days). |
| **Vault type** |  Optional | The type of backup vault: 'rsv' (Recovery Services vault) or 'dpp' (Backup vault / Data Protection). Required for vault create; optional elsewhere (auto-detected if omitted). |
