---
ms.topic: reference
ms.date: 2026-05-11 14:39:33 UTC
mcp-cli.version: 3.0.0-beta.5+4637b2434cd6e8dcf285de245a71074bb00664db
generated: 2026-05-11 14:39:33 UTC
---

# soft-delete

<!-- @mcpcli azurebackup governance soft-delete -->

This tool configures soft delete settings for a backup vault. You set the state to `AlwaysOn`, `On`, or `Off`, and you can optionally specify the retention period in days (14-180). Soft delete helps prevent accidental data loss by retaining deleted recovery points for the configured retention period so you can recover backups if deletion occurs.

This tool is part of the Model Context Protocol (MCP) tools for managing Azure backup resources.

<!-- Required parameters: 3 - 'Resource group', 'Soft delete', 'Vault name' -->

Example prompts include:

- "Enable soft delete 'AlwaysOn' for vault name 'backup-vault-prod' in resource group 'rg-prod'."
- "Set soft delete 'On' with soft delete retention days '30' for vault name 'rsv-prod' in resource group 'rg-backup'."
- "Turn soft delete 'Off' for vault name 'dpp-archive' in resource group 'rg-archives'?"
- "Configure soft delete 'On' for vault name 'my-recovery-vault' in resource group 'prod-rg' with vault type 'rsv' and soft delete retention days '90'."
- "Can you set soft delete 'AlwaysOn' on vault name 'backup-vault-staging' in resource group 'rg-staging' with vault type 'dpp'?"

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Resource group** |  Required | The name of the Azure resource group. This resource group is a logical container for Azure resources. |
| **Soft delete** |  Required | Soft delete state: `AlwaysOn`, `On`, or `Off`. |
| **Vault name** |  Required | The name of the backup vault (Recovery Services vault or Backup vault). |
| **Soft delete retention days** |  Optional | Soft delete retention period (14-180 days). |
| **Vault type** |  Optional | The type of backup vault: 'rsv' (Recovery Services vault) or 'dpp' (Backup vault / Data Protection). Required for vault create; optional elsewhere (auto-detected if omitted). |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):
Destructive: ✅ | Idempotent: ✅ | Open World: ❌ | Read Only: ❌ | Secret: ❌ | Local Required: ❌



Examples

- Configure soft delete to 'AlwaysOn' for vault 'ProdRecoveryVault' in resource group 'rg-prod' with retention 30 days.
- Set soft delete to 'Off' for vault 'TestVault' in resource group 'rg-test'.