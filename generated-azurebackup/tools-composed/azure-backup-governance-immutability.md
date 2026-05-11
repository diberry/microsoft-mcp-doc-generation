---
ms.topic: reference
ms.date: 2026-05-11 14:39:33 UTC
mcp-cli.version: 3.0.0-beta.5+4637b2434cd6e8dcf285de245a71074bb00664db
generated: 2026-05-11 14:39:33 UTC
---

# immutability

<!-- @mcpcli azurebackup governance immutability -->

Configures the immutability state for a backup vault. States include 'Disabled', 'Enabled',
or 'Locked'. Warning: 'Locked' state is irreversible.

<!-- Required parameters: 3 - 'Immutability state', 'Resource group', 'Vault name' -->

Example prompts include:

- "Set immutability state to 'Enabled' for backup vault 'rsv-prod-vault' in resource group 'rg-backup-prod'."
- "Set immutability state to 'Locked' for vault 'backupVault01' in resource group 'prod-rg' with vault type 'rsv'."
- "Set immutability state to 'Disabled' for vault 'data-protection' in resource group 'rg-dev'."
- "What happens if you set immutability state to 'Locked' for vault 'archive-vault' in resource group 'rg-archive'?"
- "Apply immutability state 'Enabled' to backup vault 'daily-backup' in resource group 'rg-daily' with vault type 'dpp'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Immutability state** |  Required | Immutability state: `Disabled`, `Enabled`, or `Locked` (irreversible). |
| **Resource group** |  Required | The name of the Azure resource group. This resource group is a logical container for Azure resources. |
| **Vault name** |  Required | The name of the backup vault (Recovery Services vault or Backup vault). |
| **Vault type** |  Optional | The type of backup vault: 'rsv' (Recovery Services vault) or 'dpp' (Backup vault / Data Protection). Required for vault create; optional elsewhere (auto-detected if omitted). |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ✅ | Idempotent: ✅ | Open World: ❌ | Read Only: ❌ | Secret: ❌ | Local Required: ❌

