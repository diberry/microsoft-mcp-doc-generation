---
ms.topic: reference
ms.date: 2026-05-11 14:39:33 UTC
mcp-cli.version: 3.0.0-beta.5+4637b2434cd6e8dcf285de245a71074bb00664db
generated: 2026-05-11 14:39:33 UTC
---

# enable-crr

<!-- @mcpcli azurebackup disasterrecovery enable-crr -->

Enables Cross-Region Restore on a GRS-enabled vault.

<!-- Required parameters: 2 - 'Resource group', 'Vault name' -->

Example prompts include:

- "Enable Cross-Region Restore (CRR) on vault 'rsv-backup-prod' in resource group 'rg-prod'."
- "Turn on CRR for vault 'contoso-rsv' in resource group 'rg-backups' with vault type 'rsv'."
- "Can you enable Cross-Region Restore for vault 'dpp-backup-eu' in resource group 'rg-europe' with vault type 'dpp'?"
- "Enable CRR for vault 'archive-vault' in resource group 'rg-archive'."
- "Enable Cross-Region Restore on vault 'site-recovery' in resource group 'rg-staging'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Resource group** |  Required | The name of the Azure resource group. This resource group is a logical container for Azure resources. |
| **Vault name** |  Required | The name of the backup vault (Recovery Services vault or Backup vault). |
| **Vault type** |  Optional | The type of backup vault: 'rsv' (Recovery Services vault) or 'dpp' (Backup vault / Data Protection). Required for vault create; optional elsewhere (auto-detected if omitted). |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ✅ | Idempotent: ✅ | Open World: ❌ | Read Only: ❌ | Secret: ❌ | Local Required: ❌

