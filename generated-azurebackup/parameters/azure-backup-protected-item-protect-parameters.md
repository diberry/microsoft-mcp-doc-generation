---
ms.topic: include
ms.date: 2026-05-11
mcp-cli.version: 3.0.0-beta.5+4637b2434cd6e8dcf285de245a71074bb00664db
generated: 2026-05-11 14:39:41 UTC
# [!INCLUDE [azurebackup protecteditem protect](../includes/tools/parameters/azure-backup-protected-item-protect-parameters.md)]
# azmcp azurebackup protecteditem protect
---

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Datasource ID** |  Required | The datasource identifier. For VM/FileShare/DPP workloads, use the ARM resource ID (for example, `'/subscriptions/.../virtualMachines/myvm'`). For RSV in-guest workloads (SQL/SAPHANA), use the protectable item name from 'protectableitem list' (for example, `'SAPHanaDatabase;instance;dbname'`). |
| **Policy** |  Required | The name of the backup policy. |
| **Resource group** |  Required | The name of the Azure resource group. This resource group is a logical container for Azure resources. |
| **Vault name** |  Required | The name of the backup vault (Recovery Services vault or Backup vault). |
| **Container name** |  Optional | The RSV protection container name. Only applicable for Recovery Services vaults. |
| **Datasource type** |  Optional | The workload type hint: `VM`, `SQL`, `SAPHANA`, `SAPASE`, `AzureFileShare` (RSV types); AzureDisk, AzureBlob, AKS, ElasticSAN, PostgreSQLFlexible, ADLS, Azure Cosmos DB (DPP types). Also accepts aliases like AzureVM, SQLDatabase, and more. |
| **Vault type** |  Optional | The type of backup vault: 'rsv' (Recovery Services vault) or 'dpp' (Backup vault / Data Protection). Required for vault create; optional elsewhere (auto-detected if omitted). |
