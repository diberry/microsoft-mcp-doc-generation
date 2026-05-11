---
ms.topic: reference
ms.date: 2026-05-11 14:39:33 UTC
mcp-cli.version: 3.0.0-beta.5+4637b2434cd6e8dcf285de245a71074bb00664db
generated: 2026-05-11 14:39:33 UTC
---

# create

<!-- @mcpcli azurebackup policy create -->

This tool, part of the Model Context Protocol (MCP), creates a backup policy for a specified workload type and applies schedule and retention rules.

<!-- Required parameters: 4 - `Policy`, `Resource group`, `Vault name`, `Workload type` -->

Example prompts include:

- "Create backup policy 'daily-vm-policy' in resource group 'rg-backup-prod' with vault name 'rsv-prod-vault' for workload type 'VM' and schedule time '02:00' with daily retention days '30'."
- "Set up policy 'sql-weekly-retention' for workload type 'SQL' in resource group 'rg-sql-prod' using vault name 'rsv-sql-vault' with daily retention days '7'."
- "Create policy 'aks-backup-policy' in resource group 'rg-aks' on vault name 'dpp-aks-vault' for workload type 'AKS' with vault type 'dpp'."
- "Can you create backup policy 'azureblob-monthly' in resource group 'rg-storage' using vault name 'rsv-storage-vault' for workload type 'AzureBlob'?"
- "Create backup policy 'saphana-daily' with workload type 'SAPHANA' in resource group 'rg-sap' and vault name 'rsv-sap-vault'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Policy** |  Required | The name of the backup policy. |
| **Resource group** |  Required | The name of the Azure resource group. This resource group is a logical container for Azure resources. |
| **Vault name** |  Required | The name of the backup vault, such as a Recovery Services vault or a Backup vault. |
| **Workload type** |  Required | Workload type: `VM`, `SQL`, `SAPHANA`, `SAPASE`, `AzureFileShare` (RSV types); `AzureDisk`, `AzureBlob`, `Azure Kubernetes Service (AKS)`, `ElasticSAN`, `PostgreSQLFlexible`, `Azure Data Lake Storage (ADLS)`, `Azure Cosmos DB` (DPP types). Also accepts aliases like `AzureVM`, `SQLDatabase`, and other supported values. |
| **Daily retention days** |  Optional | Number of days to retain daily recovery points. If omitted, the tool uses the datasource-specific default. |
| **Schedule time** |  Optional | Backup time in UTC, for example `02:00`. |
| **Vault type** |  Optional | Type of backup vault: `rsv` (Recovery Services vault) or `dpp` (Backup vault / Data Protection). Required when creating a vault; optional otherwise. If omitted, the tool auto-detects the vault type. |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):
Destructive: ✅ | Idempotent: ❌ | Open World: ❌ | Read Only: ❌ | Secret: ❌ | Local Required: ❌ |