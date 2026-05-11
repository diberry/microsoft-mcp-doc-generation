---
ms.topic: reference
ms.date: 2026-05-11 14:39:33 UTC
mcp-cli.version: 3.0.0-beta.5+4637b2434cd6e8dcf285de245a71074bb00664db
generated: 2026-05-11 14:39:33 UTC
---

# list

<!-- @mcpcli azurebackup protectableitem list -->

Lists items that can be backed up (protectable items) in a Recovery Services vault,
such as SQL databases and SAP HANA databases discovered on registered VMs.
Use this to find databases and workloads available for backup protection.
Only supported for RSV vaults; DPP datasources are protected by ARM resource ID directly.
Filter results by --workload-type (e.g., SQL, SAPHana) or --container.

<!-- Required parameters: 2 - 'Resource group', 'Vault name' -->

Example prompts include:

- "List protectable items in resource group 'rg-prod' and vault name 'rsv-prod-vault'."
- "Get protectable items in resource group 'rg-backup' for vault name 'backupvault-eastus' filtered by workload type 'SQL'."
- "What protectable items exist in resource group 'rg-devops' and vault name 'dpp-backup-01' for vault type 'dpp' and workload type 'AzureBlob'?"
- "List protectable items in resource group 'rg-prod' and vault name 'rsv-prod-vault' for container 'vm-container-01'."
- "Show protectable items in resource group 'rg-backup' and vault name 'rsv-prod-vault' for container 'hana-container' filtered by workload type 'SAPHANA'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Resource group** |  Required | The name of the Azure resource group. This resource group is a logical container for Azure resources. |
| **Vault name** |  Required | The name of the backup vault (Recovery Services vault or Backup vault). |
| **Container name** |  Optional | The RSV protection container name. Only applicable for Recovery Services vaults. |
| **Vault type** |  Optional | The type of backup vault: 'rsv' (Recovery Services vault) or 'dpp' (Backup vault / Data Protection). Required for vault create; optional elsewhere (auto-detected if omitted). |
| **Workload type** |  Optional | Workload type: `VM`, `SQL`, `SAPHANA`, `SAPASE`, `AzureFileShare` (RSV types); AzureDisk, AzureBlob, AKS, ElasticSAN, PostgreSQLFlexible, ADLS, Azure Cosmos DB (DPP types). Also accepts aliases like AzureVM, SQLDatabase, and more. |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌

