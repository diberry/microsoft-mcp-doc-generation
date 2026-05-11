---
ms.topic: reference
ms.date: 2026-05-11 14:39:33 UTC
mcp-cli.version: 3.0.0-beta.5+4637b2434cd6e8dcf285de245a71074bb00664db
generated: 2026-05-11 14:39:33 UTC
---

# enable-crr

<!-- @mcpcli azurebackup disasterrecovery enable-crr -->

Enables Cross-Region Restore on a geo-redundant storage (GRS)-enabled vault. This tool turns on cross-region restore so you can recover backed-up data from a paired region if the primary region becomes unavailable. Supported vault types include Recovery Services vault and Backup vault.

This tool is part of the Model Context Protocol (MCP) suite.

<!-- Required parameters: 2 - 'Resource group', 'Vault name' -->

Example prompts include:

- "Enable Cross-Region Restore (CRR) on vault 'rsv-backup-prod' in resource group 'rg-prod'."
- "Turn on CRR for vault 'contoso-rsv' in resource group 'rg-backups' with vault type 'rsv'."
- "Can you enable Cross-Region Restore for vault 'dpp-backup-eu' in resource group 'rg-europe' with vault type 'dpp'?"
- "Enable CRR for vault 'archive-vault' in resource group 'rg-archive'."
- "Enable Cross-Region Restore on vault 'site-recovery' in resource group 'rg-staging'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Resource group** |  Required | The name of the Azure resource group that contains the vault. |
| **Vault name** |  Required | The name of the backup vault. Use the vault name you created for backups. |
| **Vault type** |  Optional | The type of backup vault: `rsv` (Recovery Services vault) or `dpp` (Backup vault / Data Protection). Required for vault create; optional elsewhere (auto-detected if omitted). |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):
Destructive: ✅ | Idempotent: ✅ | Open World: ❌ | Read Only: ❌ | Secret: ❌ | Local Required: ❌

Examples

- Enable Cross-Region Restore for vault 'ContosoBackupVault' in resource group 'Contoso-RG'.
- Enable Cross-Region Restore for vault 'FinanceBackupVault' in resource group 'Finance-RG', specifying vault type 'rsv'.