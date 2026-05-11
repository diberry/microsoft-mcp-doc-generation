---
ms.topic: reference
ms.date: 2026-05-11 14:39:33 UTC
mcp-cli.version: 3.0.0-beta.5+4637b2434cd6e8dcf285de245a71074bb00664db
generated: 2026-05-11 14:39:33 UTC
---

# get

<!-- @mcpcli azurebackup job get -->

Model Context Protocol (MCP) tool. This tool retrieves backup job information. When the job is specified, this tool returns detailed information about a single job, including operation type, status, start and end times, error codes, and data source details. When the job is omitted, this tool lists all backup jobs in the vault.

<!-- Required parameters: 2 - 'Resource group', 'Vault name' -->

Example prompts include:

- "List all backup jobs in vault 'backup-vault-prod' within resource group 'rg-backup-prod'."
- "What backup jobs are in vault 'daily-backups' for resource group 'rg-data'?"
- "Get details for job '7f3a2c1d-9b2e-4a6f-8c5d-1a2b3c4d5e6f' in vault 'backup-vault-prod' within resource group 'rg-backup-prod', vault type 'rsv'."
- "Show me the status of backup job 'job-20240512-001' in vault 'weekly-rsv' in resource group 'rg-backup-test'."
- "List all backup jobs for vault 'company-dpp' in resource group 'rg-enterprise', vault type 'dpp'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Resource group** |  Required | The name of the Azure resource group. |
| **Vault name** |  Required | The name of the vault, such as a Recovery Services vault or a Backup vault. |
| **Job** |  Optional | The backup job ID. |
| **Vault type** |  Optional | The type of backup vault: `rsv` (Recovery Services vault) or `dpp` (Backup vault / Data Protection). Required for vault create; optional elsewhere (auto-detected if omitted). |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):
Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌