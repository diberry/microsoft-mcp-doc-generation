---
ms.topic: include
ms.date: 2026-05-11
mcp-cli.version: 3.0.0-beta.5+4637b2434cd6e8dcf285de245a71074bb00664db
generated: 2026-05-11 14:39:41 UTC
# [!INCLUDE [azurebackup policy create](../includes/tools/parameters/azure-backup-policy-create-parameters.md)]
# azmcp azurebackup policy create
---

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Policy** |  Required | The name of the backup policy. |
| **Resource group** |  Required | The name of the Azure resource group. This resource group is a logical container for Azure resources. |
| **Vault name** |  Required | The name of the backup vault (Recovery Services vault or Backup vault). |
| **Workload type** |  Required | Workload type: `VM`, `SQL`, `SAPHANA`, `SAPASE`, `AzureFileShare` (RSV types); AzureDisk, AzureBlob, AKS, ElasticSAN, PostgreSQLFlexible, ADLS, Azure Cosmos DB (DPP types). Also accepts aliases like AzureVM, SQLDatabase, and more. |
| **Daily retention days** |  Optional | Daily recovery point retention in days. Defaults to datasource-specific value if omitted. |
| **Schedule time** |  Optional | Backup time in UTC (for example, `'02:00'`). |
| **Vault type** |  Optional | The type of backup vault: 'rsv' (Recovery Services vault) or 'dpp' (Backup vault / Data Protection). Required for vault create; optional elsewhere (auto-detected if omitted). |
