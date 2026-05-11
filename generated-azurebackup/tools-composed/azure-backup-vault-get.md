---
ms.topic: reference
ms.date: 2026-05-11 14:39:33 UTC
mcp-cli.version: 3.0.0-beta.5+4637b2434cd6e8dcf285de245a71074bb00664db
generated: 2026-05-11 14:39:33 UTC
---

# get

<!-- @mcpcli azurebackup vault get -->

Retrieves backup vault information. When --vault and --resource-group are specified,
returns detailed information about a single vault including type, location, SKU, and
storage redundancy. When omitted, lists all backup vaults (RSV and Backup vaults) in
the subscription. Optionally filter by --vault-type ('rsv' or 'dpp') and/or
--resource-group to narrow the listing results.

<!-- Required parameters: 0 -  -->

Example prompts include:

- "List all backup vaults in the subscription."
- "Show all backup vaults with vault type 'rsv'."
- "Show all backup vaults in resource group 'prod-rg'."
- "Get details for vault 'contoso-backup' in resource group 'prod-rg'."
- "Show details for vault 'archive-vault' with vault type 'dpp'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Vault name** |  Optional | The name of the backup vault (Recovery Services vault or Backup vault). |
| **Vault type** |  Optional | The type of backup vault: 'rsv' (Recovery Services vault) or 'dpp' (Backup vault / Data Protection). Required for vault create; optional elsewhere (auto-detected if omitted). |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌

