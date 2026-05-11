---
ms.topic: include
ms.date: 2026-05-11
mcp-cli.version: 3.0.0-beta.5+4637b2434cd6e8dcf285de245a71074bb00664db
generated: 2026-05-11 14:39:41 UTC
# [!INCLUDE [azurebackup vault update](../includes/tools/parameters/azure-backup-vault-update-parameters.md)]
# azmcp azurebackup vault update
---

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Resource group** |  Required | The name of the Azure resource group. This resource group is a logical container for Azure resources. |
| **Vault name** |  Required | The name of the backup vault (Recovery Services vault or Backup vault). |
| **Identity type** |  Optional | Managed identity type: `SystemAssigned`, `UserAssigned`, or `None`. |
| **Immutability state** |  Optional | Immutability state: `Disabled`, `Enabled`, or `Locked` (irreversible). |
| **Redundancy** |  Optional | Storage redundancy: `GeoRedundant`, `LocallyRedundant`, `ZoneRedundant`, or `ReadAccessGeoZoneRedundant`. |
| **Soft delete** |  Optional | Soft delete state: `AlwaysOn`, `On`, or `Off`. |
| **Soft delete retention days** |  Optional | Soft delete retention period (14-180 days). |
| **Tags** |  Optional | Resource tags as JSON key-value object. |
| **Vault type** |  Optional | The type of backup vault: 'rsv' (Recovery Services vault) or 'dpp' (Backup vault / Data Protection). Required for vault create; optional elsewhere (auto-detected if omitted). |
