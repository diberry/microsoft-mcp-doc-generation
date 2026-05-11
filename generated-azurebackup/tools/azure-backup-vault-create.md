---
ms.topic: reference
ms.date: 2026-05-11 14:39:33 UTC
mcp-cli.version: 3.0.0-beta.5+4637b2434cd6e8dcf285de245a71074bb00664db
generated: 2026-05-11 14:39:33 UTC
---

# create

<!-- @mcpcli azurebackup vault create -->

This tool, part of the Model Context Protocol (MCP), creates a new backup vault in Azure. Specify the vault type as `rsv` for a Recovery Services vault or `dpp` for a Backup vault (Data Protection). This tool returns the created vault details.

<!-- Required parameters: 3 - 'Location', 'Resource group', 'Vault name' -->

Example prompts include:

- "Create vault 'backup-vault-prod' in resource group 'rg-backup-prod' at location 'eastus'."
- "Can you create a Recovery Services vault named 'rsv-prod' in resource group 'rg-recovery' at location 'westus2' with vault type 'rsv' and storage type 'GeoRedundant'?"
- "Create vault 'data-protect-vault' in resource group 'rg-dataprotection' at location 'centralus' with SKU 'Standard' and storage type 'LocallyRedundant'."
- "Provision a Backup vault named 'dpp-backup-01' in resource group 'rg-backup-staging' at location 'eastus2' with vault type 'dpp'."
- "Create vault 'vault-backup-eu' in resource group 'rg-eu-prod' at location 'northeurope' with SKU 'Premium'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Location** |  Required | The Azure region, for example `eastus` or `westus2`. |
| **Resource group** |  Required | The name of the Azure resource group, a logical container for Azure resources. |
| **Vault name** |  Required | The name of the backup vault, either a Recovery Services vault or a Backup vault (Data Protection). |
| **SKU** |  Optional | The vault SKU. |
| **Storage type** |  Optional | Storage redundancy: `GeoRedundant`, `LocallyRedundant`, or `ZoneRedundant`. |
| **Vault type** |  Optional | The type of backup vault: `rsv` (Recovery Services vault) or `dpp` (Backup vault / Data Protection). Required for vault create; optional elsewhere (auto-detected if omitted). |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):
Destructive: ✅ | Idempotent: ❌ | Open World: ❌ | Read Only: ❌ | Secret: ❌ | Local Required: ❌