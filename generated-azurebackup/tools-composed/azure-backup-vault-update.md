---
ms.topic: reference
ms.date: 2026-05-11 14:39:33 UTC
mcp-cli.version: 3.0.0-beta.5+4637b2434cd6e8dcf285de245a71074bb00664db
generated: 2026-05-11 14:39:33 UTC
---

# update

<!-- @mcpcli azurebackup vault update -->

Updates vault-level settings including storage redundancy, soft delete, immutability, and managed identity.

<!-- Required parameters: 2 - 'Resource group', 'Vault name' -->

Example prompts include:

- "Update vault 'backup-main' in resource group 'rg-prod' to set redundancy 'GeoRedundant'."
- "Enable immutability state 'Enabled' and set identity type 'SystemAssigned' for vault 'rsv-primary' in resource group 'rg-backup'."
- "Turn soft delete 'AlwaysOn' with soft delete retention days '30' for vault 'vault-archive' in resource group 'rg-archive'."
- "Add tags '{"env":"prod","owner":"backup-team"}' and set identity type 'UserAssigned' for vault 'secure-vault' in resource group 'rg-security'."
- "Can you update vault 'dpp-recovery' in resource group 'rg-disaster' to set immutability state 'Locked', redundancy 'ReadAccessGeoZoneRedundant', and vault type 'rsv'?"

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

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ✅ | Idempotent: ✅ | Open World: ❌ | Read Only: ❌ | Secret: ❌ | Local Required: ❌

