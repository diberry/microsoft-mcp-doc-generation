---
ms.topic: include
ms.date: 05/11/2026
mcp-cli.version: 3.0.0-beta.5+4637b2434cd6e8dcf285de245a71074bb00664db
---
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--resource-group` | string | Yes | The name of the Azure resource group. This is a logical container for Azure resources. |
| `--vault` | string | Yes | The name of the backup vault (Recovery Services vault or Backup vault). |
| `--vault-type` | string | No | The type of backup vault: 'rsv' (Recovery Services vault) or 'dpp' (Backup vault / Data Protection). Required for vault create; optional elsewhere (auto-detected if omitted). |
| `--policy` | string | Yes | The name of the backup policy. |
| `--workload-type` | string | Yes | Workload type: VM, SQL, SAPHANA, SAPASE, AzureFileShare (RSV types); AzureDisk, AzureBlob, AKS, ElasticSAN, PostgreSQLFlexible, ADLS, CosmosDB (DPP types). Also accepts aliases like AzureVM, SQLDatabase, etc. |
| `--schedule-time` | string | No | Backup time in UTC (e.g., '02:00'). |
| `--daily-retention-days` | string | No | Daily recovery point retention in days. Defaults to datasource-specific value if omitted. |
