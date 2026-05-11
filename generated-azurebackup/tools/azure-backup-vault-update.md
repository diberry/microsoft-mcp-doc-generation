---
ms.topic: reference
ms.date: 2026-05-11 14:39:33 UTC
mcp-cli.version: 3.0.0-beta.5+4637b2434cd6e8dcf285de245a71074bb00664db
generated: 2026-05-11 14:39:33 UTC
---

# update

<!-- @mcpcli azurebackup vault update -->

This tool updates vault-level settings for a Recovery Services vault or Backup vault used by Azure Backup, including storage redundancy, soft delete, immutability, and managed identity.

<!-- Required parameters: 2 - `Resource group`, `Vault name` -->

Example prompts include:

- "Update vault 'backup-main' in resource group 'rg-prod' to set redundancy 'GeoRedundant'."
- "Enable immutability state 'Enabled' and set identity type 'SystemAssigned' for vault 'rsv-primary' in resource group 'rg-backup'."
- "Turn soft delete 'AlwaysOn' with soft delete retention days '30' for vault 'vault-archive' in resource group 'rg-archive'."
- "Add tags '{"env":"prod","owner":"backup-team"}' and set identity type 'UserAssigned' for vault 'secure-vault' in resource group 'rg-security'."
- "Can you update vault 'dpp-recovery' in resource group 'rg-disaster' to set immutability state 'Locked', redundancy 'ReadAccessGeoZoneRedundant', and vault type 'rsv'?"

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Resource group** |  Required | The name of the Azure resource group that contains the vault. |
| **Vault name** |  Required | The name of the vault, such as a Recovery Services vault or Backup vault. |
| **Identity type** |  Optional | Managed identity type: `SystemAssigned`, `UserAssigned`, or `None`. |
| **Immutability state** |  Optional | Immutability state: `Disabled`, `Enabled`, or `Locked` (irreversible). |
| **Redundancy** |  Optional | Storage redundancy: `GeoRedundant`, `LocallyRedundant`, `ZoneRedundant`, or `ReadAccessGeoZoneRedundant`. |
| **Soft delete** |  Optional | Soft delete state: `AlwaysOn`, `On`, or `Off`. |
| **Soft delete retention days** |  Optional | Soft delete retention period in days (14–180). |
| **Tags** |  Optional | Resource tags as a JSON object of key-value pairs. |
| **Vault type** |  Optional | The type of backup vault: `rsv` (Recovery Services vault) or `dpp` (Backup vault / Data Protection). Required for vault create; optional elsewhere (auto-detected if omitted). |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):
Destructive: ✅ | Idempotent: ✅ | Open World: ❌ | Read Only: ❌ | Secret: ❌ | Local Required: ❌

Examples

For example, update vault 'contoso-vault' in resource group 'contoso-rg' to use redundancy 'GeoRedundant' and enable soft delete 'On' with retention days '30'.

For example, assign a system-assigned managed identity to vault 'prod-backup' in resource group 'prod-rg' and set immutability state to 'Enabled'.