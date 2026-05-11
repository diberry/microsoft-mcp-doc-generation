---
ms.topic: reference
ms.date: 2026-05-11 14:39:33 UTC
mcp-cli.version: 3.0.0-beta.5+4637b2434cd6e8dcf285de245a71074bb00664db
generated: 2026-05-11 14:39:33 UTC
---

# get

<!-- @mcpcli azurebackup vault get -->

Retrieves backup vault information. When you specify a vault name and a resource group, this tool returns detailed information about the vault, including type, location, SKU, and storage redundancy. When you omit those values, the tool lists all backup vaults in the subscription, including Recovery Services vaults and Backup vaults. You can filter results by vault type with values `rsv` or `dpp`, and by resource group to narrow the list.

<!-- Required parameters: 0 -  -->

Example prompts include:

- "List all backup vaults in the subscription."
- "Show all backup vaults with vault type 'rsv'."
- "Show all backup vaults in resource group 'prod-rg'."
- "Get details for vault 'contoso-backup' in resource group 'prod-rg'."
- "Show details for vault 'archive-vault' with vault type 'dpp'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Vault name** |  Optional | Name of the backup vault, either a Recovery Services vault or a Backup vault. |
| **Vault type** |  Optional | Type of backup vault: `rsv` for Recovery Services vault, or `dpp` for Backup vault (Data Protection). This value is required for vault create, and optional elsewhere; the tool auto-detects the type if you omit it. |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):
Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌