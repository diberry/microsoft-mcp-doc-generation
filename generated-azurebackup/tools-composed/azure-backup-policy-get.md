---
ms.topic: reference
ms.date: 2026-05-11 14:39:33 UTC
mcp-cli.version: 3.0.0-beta.5+4637b2434cd6e8dcf285de245a71074bb00664db
generated: 2026-05-11 14:39:33 UTC
---

# get

<!-- @mcpcli azurebackup policy get -->

Retrieves backup policy information. When --policy is specified, returns detailed
information about a single policy including datasource types and protected items count.
When omitted, lists all backup policies configured in the vault.

<!-- Required parameters: 2 - 'Resource group', 'Vault name' -->

Example prompts include:

- "List all backup policies in resource group 'rg-prod' for vault 'my-recovery-vault'."
- "Get backup policy 'daily-backup-policy' from vault 'backupvault01' in resource group 'rg-backup'."
- "Show details for policy 'SQLPolicy' in vault 'protection-vault' within resource group 'rg-recovery'."
- "Show me all backup policies in resource group 'rg-test' for vault 'dpp-backup' with vault type 'dpp'."
- "Retrieve policy 'file-share-policy' from vault 'rsv-prod' in resource group 'rg-prod-east'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Resource group** |  Required | The name of the Azure resource group. This resource group is a logical container for Azure resources. |
| **Vault name** |  Required | The name of the backup vault (Recovery Services vault or Backup vault). |
| **Policy** |  Optional | The name of the backup policy. |
| **Vault type** |  Optional | The type of backup vault: 'rsv' (Recovery Services vault) or 'dpp' (Backup vault / Data Protection). Required for vault create; optional elsewhere (auto-detected if omitted). |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌

