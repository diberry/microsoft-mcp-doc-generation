---
ms.topic: reference
ms.date: 2026-05-11 14:39:33 UTC
mcp-cli.version: 3.0.0-beta.5+4637b2434cd6e8dcf285de245a71074bb00664db
generated: 2026-05-11 14:39:33 UTC
---

# protect

<!-- @mcpcli azurebackup protecteditem protect -->

Enables or configures backup protection for an Azure resource by creating a
protected item or backup instance. Protects VMs, disks, file shares, SQL databases,
SAP HANA databases, and other supported datasources.
For VMs: pass the VM ARM resource ID as --datasource-id.
For workloads (SQL/HANA): pass the protectable item name from 'protectableitem list'
as --datasource-id (e.g., 'SAPHanaDatabase;instance;dbname'), and specify --container.
Requires a backup policy name via --policy. The operation is asynchronous;
use 'azurebackup job get' to monitor the protection job progress.

<!-- Required parameters: 4 - 'Datasource ID', 'Policy', 'Resource group', 'Vault name' -->

Example prompts include:

- "Protect datasource ID '/subscriptions/12345678-90ab-cdef-1234-567890abcdef/resourceGroups/prod-rg/providers/Microsoft.Compute/virtualMachines/webapp-prod' with policy 'DailyBackup' in resource group 'prod-rg' and vault 'rsv-prod'."
- "Protect datasource ID '/subscriptions/12345678-90ab-cdef-1234-567890abcdef/resourceGroups/rg-prod/providers/Microsoft.Compute/disks/dataDisk1' with policy 'HourlyBackup' in resource group 'rg-prod' and vault 'dpp-backup', datasource type 'AzureDisk'."
- "Protect datasource ID 'SAPHanaDatabase;instance01;SalesDB' with policy 'SAPHanaPolicy' in resource group 'sap-rg' and vault 'rsv-sap', container 'saphana-cont'."
- "Protect datasource ID '/subscriptions/12345678-90ab-cdef-1234-567890abcdef/resourceGroups/files-rg/providers/Microsoft.Storage/storageAccounts/mystorage/fileServices/default/shares/backups' with policy 'FileSharePolicy' in resource group 'files-rg' and vault 'rsv-files', container 'files-container'."
- "Can you protect datasource ID '/subscriptions/12345678-90ab-cdef-1234-567890abcdef/resourceGroups/test-rg/providers/Microsoft.Compute/virtualMachines/db-staging' using policy 'WeeklyFull' in resource group 'test-rg' and vault 'backup-vault'?"

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Datasource ID** |  Required | The datasource identifier. For VM/FileShare/DPP workloads, use the ARM resource ID (for example, `'/subscriptions/.../virtualMachines/myvm'`). For RSV in-guest workloads (SQL/SAPHANA), use the protectable item name from 'protectableitem list' (for example, `'SAPHanaDatabase;instance;dbname'`). |
| **Policy** |  Required | The name of the backup policy. |
| **Resource group** |  Required | The name of the Azure resource group. This resource group is a logical container for Azure resources. |
| **Vault name** |  Required | The name of the backup vault (Recovery Services vault or Backup vault). |
| **Container name** |  Optional | The RSV protection container name. Only applicable for Recovery Services vaults. |
| **Datasource type** |  Optional | The workload type hint: `VM`, `SQL`, `SAPHANA`, `SAPASE`, `AzureFileShare` (RSV types); AzureDisk, AzureBlob, AKS, ElasticSAN, PostgreSQLFlexible, ADLS, Azure Cosmos DB (DPP types). Also accepts aliases like AzureVM, SQLDatabase, and more. |
| **Vault type** |  Optional | The type of backup vault: 'rsv' (Recovery Services vault) or 'dpp' (Backup vault / Data Protection). Required for vault create; optional elsewhere (auto-detected if omitted). |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ✅ | Idempotent: ❌ | Open World: ❌ | Read Only: ❌ | Secret: ❌ | Local Required: ❌

