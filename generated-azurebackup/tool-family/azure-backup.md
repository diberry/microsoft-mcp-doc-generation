---

title: Azure MCP Server tools for Azure Backup
description: Use Azure MCP Server tools to manage Azure Backup resources with natural language prompts from your IDE.
ms.service: azure-mcp-server
ms.topic: concept-article
tool_count: 16
mcp-cli.version: 3.0.0-beta.5+4637b2434cd6e8dcf285de245a71074bb00664db
author: diberry
ms.author: diberry
ms.reviewer: mbaldwin
ms.date: 05/11/2026
ai-usage: ai-generated
ms.custom: build-2025
content_well_notification:
  - AI-contribution
---

# Azure MCP Server tools for Azure Backup

The Azure MCP Server lets you manage Azure Backup resources, including: create, enable-crr, find-unprotected, get, immutability, list, protect, soft-delete, status, undelete, and update, with natural language prompts.

Azure Backup is an Azure service that provides cloud-based capabilities for your applications. For more information, see [Azure Backup documentation](/azure/azurebackup/).

[!INCLUDE [tip-about-params](../includes/tools/parameter-consideration.md)]


## Create policy
#### [MCP Server](#tab/mcp-server)


<!-- @mcpcli azurebackup policy create -->

This tool, part of the Model Context Protocol (MCP), creates a backup policy for a specified workload type and applies schedule and retention rules.

Example prompts include:

- "Create backup policy 'daily-vm-policy' in resource group 'rg-backup-prod' with vault name 'rsv-prod-vault' for workload type 'virtual machine (VM)' and schedule time '02:00' with daily retention days '30'."
- "Set up policy 'sql-weekly-retention' for workload type 'SQL' in resource group 'rg-sql-prod' using vault name 'rsv-sql-vault' with daily retention days '7'."
- "Create policy 'aks-backup-policy' in resource group 'rg-aks' on vault name 'dpp-aks-vault' for workload type 'Azure Kubernetes Service (AKS)' with vault type 'dpp'."
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

Destructive: ✅ | Idempotent: ❌ | Open World: ❌ | Read Only: ❌ | Secret: ❌ | Local Required: ❌

#### [CLI](#tab/cli)

Creates a backup policy for a specified workload type with schedule and retention rules.

**Example CLI command**

```azurecli
azmcp azurebackup policy create \
  --resource-group <resource-group> \
  --vault <vault> \
  --policy <policy> \
  --workload-type <workload-type> \
  [--vault-type <vault-type>] \
  [--schedule-time <schedule-time>] \
  [--daily-retention-days <daily-retention-days>]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--resource-group` | string | Yes | The name of the Azure resource group. This is a logical container for Azure resources. |
| `--vault` | string | Yes | The name of the backup vault (Recovery Services vault or Backup vault). |
| `--vault-type` | string | No | The type of backup vault: 'rsv' (Recovery Services vault) or 'dpp' (Backup vault / Data Protection). Required for vault create; optional elsewhere (auto-detected if omitted). |
| `--policy` | string | Yes | The name of the backup policy. |
| `--workload-type` | string | Yes | Workload type: VM, SQL, SAPHANA, SAPASE, AzureFileShare (RSV types); AzureDisk, AzureBlob, AKS, ElasticSAN, PostgreSQLFlexible, ADLS, CosmosDB (DPP types). Also accepts aliases like AzureVM, SQLDatabase, etc. |
| `--schedule-time` | string | No | Backup time in UTC (e.g., '02:00'). |
| `--daily-retention-days` | string | No | Daily recovery point retention in days. Defaults to datasource-specific value if omitted. |

---

## Create vault
#### [MCP Server](#tab/mcp-server)


<!-- @mcpcli azurebackup vault create -->

This tool, part of the Model Context Protocol (MCP), creates a new backup vault in Azure. Specify the vault type as `rsv` for a Recovery Services vault or `dpp` for a Backup vault (Data Protection). This tool returns the created vault details.

Example prompts include:

- "Create vault 'backup-vault-prod' in resource group 'rg-backup-prod' at location 'eastus'."
- "Can you create a Recovery Services vault named 'rsv-prod' in resource group 'rg-recovery' at location 'westus2' with vault type 'rsv' and storage type 'GeoRedundant'?"
- "Create vault 'data-protect-vault' in resource group 'rg-dataprotection' at location 'centralus' with SKU 'Standard' and storage type 'LocallyRedundant'."
- "Provision a Backup vault named 'dpp-backup-01' in resource group 'rg-backup-staging' at location 'eastus2' with vault type 'dpp'."
- "Create vault 'vault-backup-eu' in resource group 'rg-eu-prod' at location 'northeurope' with SKU 'Premium'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Location** |  Required | The Azure region, for example `eastus` or `westus2`. |
| **Resource group** |  Required | The name of the Azure resource group, a logical container for Azure resources. |
| **Vault name** |  Required | The name of the backup vault, either a Recovery Services vault or a Backup vault (Data Protection). |
| **SKU** |  Optional | The vault SKU. |
| **Storage type** |  Optional | Storage redundancy: `GeoRedundant`, `LocallyRedundant`, or `ZoneRedundant`. |
| **Vault type** |  Optional | The type of backup vault: `rsv` (Recovery Services vault) or `dpp` (Backup vault / Data Protection). Required for vault create; optional elsewhere (auto-detected if omitted). |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ✅ | Idempotent: ❌ | Open World: ❌ | Read Only: ❌ | Secret: ❌ | Local Required: ❌

#### [CLI](#tab/cli)

Creates a new backup vault. Specify --vault-type as 'rsv' for a Recovery Services vault
or 'dpp' for a Backup vault (Data Protection). Returns the created vault details.

**Example CLI command**

```azurecli
azmcp azurebackup vault create \
  --resource-group <resource-group> \
  --vault <vault> \
  --location <location> \
  [--vault-type <vault-type>] \
  [--sku <sku>] \
  [--storage-type <storage-type>]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--resource-group` | string | Yes | The name of the Azure resource group. This is a logical container for Azure resources. |
| `--vault` | string | Yes | The name of the backup vault (Recovery Services vault or Backup vault). |
| `--vault-type` | string | No | The type of backup vault: 'rsv' (Recovery Services vault) or 'dpp' (Backup vault / Data Protection). Required for vault create; optional elsewhere (auto-detected if omitted). |
| `--location` | string | Yes | The Azure region (e.g., 'eastus', 'westus2'). |
| `--sku` | string | No | The vault SKU. |
| `--storage-type` | string | No | Storage redundancy: 'GeoRedundant', 'LocallyRedundant', or 'ZoneRedundant'. |

---

## Enable crr disaster recovery
#### [MCP Server](#tab/mcp-server)


<!-- @mcpcli azurebackup disasterrecovery enable-crr -->

Enables Cross-Region Restore on a geo-redundant storage (GRS)-enabled vault. This tool turns on cross-region restore so you can recover backed-up data from a paired region if the primary region becomes unavailable. Supported vault types include Recovery Services vault and Backup vault.

This tool is part of the Model Context Protocol (MCP) suite.

Example prompts include:

- "Enable Cross-Region Restore (CRR) on vault 'rsv-backup-prod' in resource group 'rg-prod'."
- "Turn on CRR for vault 'contoso-rsv' in resource group 'rg-backups' with vault type 'rsv'."
- "Can you enable Cross-Region Restore for vault 'dpp-backup-eu' in resource group 'rg-europe' with vault type 'dpp'?"
- "Enable CRR for vault 'archive-vault' in resource group 'rg-archive'."
- "Enable Cross-Region Restore on vault 'site-recovery' in resource group 'rg-staging'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Resource group** |  Required | The name of the Azure resource group that contains the vault. |
| **Vault name** |  Required | The name of the backup vault. Use the vault name you created for backups. |
| **Vault type** |  Optional | The type of backup vault: `rsv` (Recovery Services vault) or `dpp` (Backup vault / Data Protection). Required for vault create; optional elsewhere (auto-detected if omitted). |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ✅ | Idempotent: ✅ | Open World: ❌ | Read Only: ❌ | Secret: ❌ | Local Required: ❌

Examples

- Enable Cross-Region Restore for vault 'ContosoBackupVault' in resource group 'Contoso-RG'.
- Enable Cross-Region Restore for vault 'FinanceBackupVault' in resource group 'Finance-RG', specifying vault type 'rsv'.

#### [CLI](#tab/cli)

Enables Cross-Region Restore on a GRS-enabled vault.

**Example CLI command**

```azurecli
azmcp azurebackup disasterrecovery enable-crr \
  --resource-group <resource-group> \
  --vault <vault> \
  [--vault-type <vault-type>]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--resource-group` | string | Yes | The name of the Azure resource group. This is a logical container for Azure resources. |
| `--vault` | string | Yes | The name of the backup vault (Recovery Services vault or Backup vault). |
| `--vault-type` | string | No | The type of backup vault: 'rsv' (Recovery Services vault) or 'dpp' (Backup vault / Data Protection). Required for vault create; optional elsewhere (auto-detected if omitted). |

---

## Find unprotected governance
#### [MCP Server](#tab/mcp-server)


<!-- @mcpcli azurebackup governance find-unprotected -->

This Model Context Protocol (MCP) tool scans your subscription to find Azure resources that aren't protected by any Azure Backup policy. You can filter results by resource type, resource group, or tags. Use the results to identify resources that need backup configuration and help meet recovery and compliance objectives.

Examples

- For example, find unprotected virtual machines in resource group 'rg-prod'.
- For example, find unprotected storage accounts tagged 'environment=production'.

Example prompts include:

- "Find all Azure resources that aren't protected by any backup policy."
- "List unprotected resources filtered by resource type 'Microsoft.Compute/virtualMachines,Microsoft.Sql/servers/databases'."
- "Show unprotected Azure resources with tag 'environment=production'."
- "Scan for unprotected resources of resource type 'Microsoft.Storage/storageAccounts' with tag 'environment=staging'."
- "Which 'Microsoft.Sql/servers/databases' with tag 'backup=true' aren't protected by a backup policy?"

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Resource type filter** |  Optional | Resource types to filter (comma-separated). |
| **Tag filter** |  Optional | Tag-based filter in key=value format (for example, `'environment=production'`). |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌

#### [CLI](#tab/cli)

Scans the subscription to find Azure resources that are not currently protected by any
backup policy. Optionally filter by resource type, resource group, or tags.

**Example CLI command**

```azurecli
azmcp azurebackup governance find-unprotected \
  [--resource-type-filter <resource-type-filter>] \
  [--resource-group <resource-group>] \
  [--tag-filter <tag-filter>]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--resource-type-filter` | string | No | Resource types to filter (comma-separated). |
| `--resource-group` | string | No | The name of the Azure resource group. This is a logical container for Azure resources. |
| `--tag-filter` | string | No | Tag-based filter in key=value format (e.g., 'environment=production'). |

---

## Get job
#### [MCP Server](#tab/mcp-server)


<!-- @mcpcli azurebackup job get -->

Model Context Protocol (MCP) tool. This tool retrieves backup job information. When the job is specified, this tool returns detailed information about a single job, including operation type, status, start and end times, error codes, and data source details. When the job is omitted, this tool lists all backup jobs in the vault.

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

#### [CLI](#tab/cli)

Retrieves backup job information. When --job is specified, returns detailed information
about a single job including operation type, status, start/end times, error codes, and
datasource details. When omitted, lists all backup jobs in the vault.

**Example CLI command**

```azurecli
azmcp azurebackup job get \
  --resource-group <resource-group> \
  --vault <vault> \
  [--vault-type <vault-type>] \
  [--job <job>]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--resource-group` | string | Yes | The name of the Azure resource group. This is a logical container for Azure resources. |
| `--vault` | string | Yes | The name of the backup vault (Recovery Services vault or Backup vault). |
| `--vault-type` | string | No | The type of backup vault: 'rsv' (Recovery Services vault) or 'dpp' (Backup vault / Data Protection). Required for vault create; optional elsewhere (auto-detected if omitted). |
| `--job` | string | No | The backup job ID. |

---

## Get policy
#### [MCP Server](#tab/mcp-server)


<!-- @mcpcli azurebackup policy get -->

This tool retrieves backup policy information for the Model Context Protocol (MCP). It lists all backup policies configured in the specified vault, or returns detailed information for a single policy. When `Policy` is specified, this tool returns details such as datasource types and the count of protected items. When `Policy` is omitted, it lists all backup policies in the vault.

Examples

- Get detailed information for policy 'DailyBackupPolicy' in resource group 'rg-prod' and vault 'contoso-backup-vault'.
- List all backup policies in resource group 'rg-prod' and vault 'contoso-backup-vault'.

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

#### [CLI](#tab/cli)

Retrieves backup policy information. When --policy is specified, returns detailed
information about a single policy including datasource types and protected items count.
When omitted, lists all backup policies configured in the vault.

**Example CLI command**

```azurecli
azmcp azurebackup policy get \
  --resource-group <resource-group> \
  --vault <vault> \
  [--vault-type <vault-type>] \
  [--policy <policy>]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--resource-group` | string | Yes | The name of the Azure resource group. This is a logical container for Azure resources. |
| `--vault` | string | Yes | The name of the backup vault (Recovery Services vault or Backup vault). |
| `--vault-type` | string | No | The type of backup vault: 'rsv' (Recovery Services vault) or 'dpp' (Backup vault / Data Protection). Required for vault create; optional elsewhere (auto-detected if omitted). |
| `--policy` | string | No | The name of the backup policy. |

---

## Get protectable items
#### [MCP Server](#tab/mcp-server)


<!-- @mcpcli azurebackup protectableitem list -->

This tool lists protectable items in a Recovery Services vault, such as SQL databases and SAP HANA databases that the vault discovers on registered virtual machines. Use this tool to find databases and workloads that the vault can protect. This tool supports Recovery Services vaults (RSV) only. Backup vault (DPP) data sources use Azure Resource Manager (ARM) resource IDs for protection. Filter results by workload type, for example `SQL` or `SAPHANA`, or by container name.

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

Examples

- List protectable items in resource group 'prod-rg' and vault 'contoso-rsv', filter by workload type 'SQL'.
- List protectable SAP HANA databases in resource group 'prod-rg' and vault 'contoso-rsv', filter by container 'vm-container-01'.

#### [CLI](#tab/cli)

Lists items that can be backed up (protectable items) in a Recovery Services vault,
such as SQL databases and SAP HANA databases discovered on registered VMs.
Use this to find databases and workloads available for backup protection.
Only supported for RSV vaults; DPP datasources are protected by ARM resource ID directly.
Filter results by --workload-type (e.g., SQL, SAPHana) or --container.

**Example CLI command**

```azurecli
azmcp azurebackup protectableitem list \
  --resource-group <resource-group> \
  --vault <vault> \
  [--vault-type <vault-type>] \
  [--workload-type <workload-type>] \
  [--container <container>]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--resource-group` | string | Yes | The name of the Azure resource group. This is a logical container for Azure resources. |
| `--vault` | string | Yes | The name of the backup vault (Recovery Services vault or Backup vault). |
| `--vault-type` | string | No | The type of backup vault: 'rsv' (Recovery Services vault) or 'dpp' (Backup vault / Data Protection). Required for vault create; optional elsewhere (auto-detected if omitted). |
| `--workload-type` | string | No | Workload type: VM, SQL, SAPHANA, SAPASE, AzureFileShare (RSV types); AzureDisk, AzureBlob, AKS, ElasticSAN, PostgreSQLFlexible, ADLS, CosmosDB (DPP types). Also accepts aliases like AzureVM, SQLDatabase, etc. |
| `--container` | string | No | The RSV protection container name. Only applicable for Recovery Services vaults. |

---

## Get protected item
#### [MCP Server](#tab/mcp-server)


<!-- @mcpcli azurebackup protecteditem get -->

This tool retrieves protected item information from a backup vault. When `Protected item` is specified, this tool returns detailed information about a single backup instance, including protection status, data source details, policy assignment, and last backup time. When `Container name` is specified, this tool targets items in a Recovery Services vault (RSV) container. When `Protected item` is omitted, this tool lists all protected items in the vault.

Get details for protected item 'sales-db-backup' in resource group 'rg-production' and vault 'contoso-rsv'.  
List all protected items in resource group 'rg-test' and vault 'test-backup-vault'.

Example prompts include:

- "List all protected items in resource group 'rg-prod' from vault 'backup-vault'."
- "Get details for protected item 'vm-prod-01' in resource group 'rg-prod' from vault 'backup-vault'."
- "Show protected items in resource group 'rg-staging' for vault 'rsv-vault' with container 'rsv-container1'."
- "Retrieve protected item 'sql-db-02' from container 'rsv-sql' in resource group 'rg-database' and vault 'data-protect-vault' with vault type 'rsv'."
- "What protected items are in vault 'protection-vault' within resource group 'rg-backup' when vault type is 'dpp'?"

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Resource group** |  Required | The name of the Azure resource group. This resource group is a logical container for Azure resources. |
| **Vault name** |  Required | The name of the backup vault (Recovery Services vault or Backup vault). |
| **Container name** |  Optional | The RSV protection container name. Only applicable for Recovery Services vaults. |
| **Protected item** |  Optional | The name of the protected item or backup instance. |
| **Vault type** |  Optional | The type of backup vault: `rsv` (Recovery Services vault) or `dpp` (Backup vault / Data Protection). Required for vault create; optional elsewhere (auto-detected if omitted). |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌

#### [CLI](#tab/cli)

Retrieves protected item information. When --protected-item is specified, returns
detailed information about a single backup instance including protection status,
datasource details, policy assignment, and last backup time. Specify --container
for RSV workload items. When --protected-item is omitted, lists all protected items
(backup instances) in the vault.

**Example CLI command**

```azurecli
azmcp azurebackup protecteditem get \
  --resource-group <resource-group> \
  --vault <vault> \
  [--vault-type <vault-type>] \
  [--protected-item <protected-item>] \
  [--container <container>]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--resource-group` | string | Yes | The name of the Azure resource group. This is a logical container for Azure resources. |
| `--vault` | string | Yes | The name of the backup vault (Recovery Services vault or Backup vault). |
| `--vault-type` | string | No | The type of backup vault: 'rsv' (Recovery Services vault) or 'dpp' (Backup vault / Data Protection). Required for vault create; optional elsewhere (auto-detected if omitted). |
| `--protected-item` | string | No | The name of the protected item or backup instance. |
| `--container` | string | No | The RSV protection container name. Only applicable for Recovery Services vaults. |

---

## Get recovery point
#### [MCP Server](#tab/mcp-server)


<!-- @mcpcli azurebackup recoverypoint get -->

This tool retrieves recovery point information for a protected item. When you specify the `recovery point` ID, this tool returns detailed information about that recovery point, including recovery time and type. When you omit the `recovery point`, this tool lists all available recovery points for the protected item. This tool is part of the Model Context Protocol (MCP) tools.

Example prompts include:

- "List all recovery points for protected item 'vm-backup-01' in resource group 'rg-prod' and vault name 'backup-vault'."
- "Get recovery point 'rp-20250501T120000Z' for protected item 'fileserver-01' in resource group 'rg-file-prod' and vault name 'dpp-vault' with vault type 'dpp'."
- "Show details for recovery point 'a1b2c3d4-ef56-7890-ab12-34567890cdef' of protected item 'sql-db-01' in resource group 'rg-db' vault name 'rsv-backup' container 'rsv-container-1'."
- "What recovery points exist for protected item 'appservice-backup' in resource group 'rg-web' vault name 'rsv-central' container 'rsv-web-cont' vault type 'rsv'?"
- "Retrieve recovery point '2026-04-10T08:30:00Z' for protected item 'vm-prod-02' in resource group 'rg-infra' vault name 'backup-vault-main'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Protected item** |  Required | The name of the protected item or backup instance. |
| **Resource group** |  Required | The name of the Azure resource group. |
| **Vault name** |  Required | The name of the backup vault (Recovery Services vault or Data Protection backup vault). |
| **Container name** |  Optional | The Recovery Services vault protection container name. Applies only to Recovery Services vaults. |
| **Recovery point** |  Optional | The recovery point ID. |
| **Vault type** |  Optional | The type of backup vault: `rsv` for Recovery Services vault, or `dpp` for Data Protection backup vault. Required for vault create; optional elsewhere, auto-detected if omitted. |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌

#### [CLI](#tab/cli)

Retrieves recovery point information for a protected item. When --recovery-point is
specified, returns detailed information about a single recovery point including time
and type. When omitted, lists all available recovery points for the protected item.

**Example CLI command**

```azurecli
azmcp azurebackup recoverypoint get \
  --resource-group <resource-group> \
  --vault <vault> \
  --protected-item <protected-item> \
  [--vault-type <vault-type>] \
  [--container <container>] \
  [--recovery-point <recovery-point>]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--resource-group` | string | Yes | The name of the Azure resource group. This is a logical container for Azure resources. |
| `--vault` | string | Yes | The name of the backup vault (Recovery Services vault or Backup vault). |
| `--vault-type` | string | No | The type of backup vault: 'rsv' (Recovery Services vault) or 'dpp' (Backup vault / Data Protection). Required for vault create; optional elsewhere (auto-detected if omitted). |
| `--protected-item` | string | Yes | The name of the protected item or backup instance. |
| `--container` | string | No | The RSV protection container name. Only applicable for Recovery Services vaults. |
| `--recovery-point` | string | No | The recovery point ID. |

---

## Get vault
#### [MCP Server](#tab/mcp-server)


<!-- @mcpcli azurebackup vault get -->

Retrieves backup vault information. When you specify a vault name and a resource group, this tool returns detailed information about the vault, including type, location, SKU, and storage redundancy. When you omit those values, the tool lists all backup vaults in the subscription, including Recovery Services vaults and Backup vaults. You can filter results by vault type with values `rsv` or `dpp`, and by resource group to narrow the list.

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

#### [CLI](#tab/cli)

Retrieves backup vault information. When --vault and --resource-group are specified,
returns detailed information about a single vault including type, location, SKU, and
storage redundancy. When omitted, lists all backup vaults (RSV and Backup vaults) in
the subscription. Optionally filter by --vault-type ('rsv' or 'dpp') and/or
--resource-group to narrow the listing results.

**Example CLI command**

```azurecli
azmcp azurebackup vault get \
  [--resource-group <resource-group>] \
  [--vault <vault>] \
  [--vault-type <vault-type>]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--resource-group` | string | No | The name of the Azure resource group. This is a logical container for Azure resources. |
| `--vault` | string | No | The name of the backup vault (Recovery Services vault or Backup vault). |
| `--vault-type` | string | No | The type of backup vault: 'rsv' (Recovery Services vault) or 'dpp' (Backup vault / Data Protection). Required for vault create; optional elsewhere (auto-detected if omitted). |

---

## Immutability governance
#### [MCP Server](#tab/mcp-server)


<!-- @mcpcli azurebackup governance immutability -->

This Model Context Protocol (MCP) tool configures the immutability state for a backup vault in Azure Backup, such as a Recovery Services vault or a Backup vault. States include `Disabled`, `Enabled`, and `Locked`. Warning: `Locked` is irreversible.

Example prompts include:

- "Set immutability state to 'Enabled' for backup vault 'rsv-prod-vault' in resource group 'rg-backup-prod'."
- "Set immutability state to 'Locked' for vault 'backupVault01' in resource group 'prod-rg' with vault type 'rsv'."
- "Set immutability state to 'Disabled' for vault 'data-protection' in resource group 'rg-dev'."
- "What happens if you set immutability state to 'Locked' for vault 'archive-vault' in resource group 'rg-archive'?"
- "Apply immutability state 'Enabled' to backup vault 'daily-backup' in resource group 'rg-daily' with vault type 'dpp'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Immutability state** |  Required | Immutability state: `Disabled`, `Enabled`, or `Locked` (irreversible). |
| **Resource group** |  Required | The name of the Azure resource group. This resource group is a logical container for Azure resources. |
| **Vault name** |  Required | The name of the backup vault, such as a Recovery Services vault or a Backup vault. |
| **Vault type** |  Optional | The type of backup vault: `rsv` (Recovery Services vault) or `dpp` (Backup vault / Data Protection). Required for vault create; optional elsewhere. The tool auto-detects the type if you omit it. |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ✅ | Idempotent: ✅ | Open World: ❌ | Read Only: ❌ | Secret: ❌ | Local Required: ❌

Examples

For example, set immutability to 'Enabled' for vault 'contosoBackupVault' in resource group 'contoso-rg'.

For example, set immutability to 'Locked' for Recovery Services vault 'rsv-prod-vault' in resource group 'rg-prod-backup'.

#### [CLI](#tab/cli)

Configures the immutability state for a backup vault. States include 'Disabled', 'Enabled',
or 'Locked'. Warning: 'Locked' state is irreversible.

**Example CLI command**

```azurecli
azmcp azurebackup governance immutability \
  --resource-group <resource-group> \
  --vault <vault> \
  --immutability-state <immutability-state> \
  [--vault-type <vault-type>]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--resource-group` | string | Yes | The name of the Azure resource group. This is a logical container for Azure resources. |
| `--vault` | string | Yes | The name of the backup vault (Recovery Services vault or Backup vault). |
| `--vault-type` | string | No | The type of backup vault: 'rsv' (Recovery Services vault) or 'dpp' (Backup vault / Data Protection). Required for vault create; optional elsewhere (auto-detected if omitted). |
| `--immutability-state` | string | Yes | Immutability state: 'Disabled', 'Enabled', or 'Locked' (irreversible). |

---

## Protect protected item
#### [MCP Server](#tab/mcp-server)


<!-- @mcpcli azurebackup protecteditem protect -->

Enables or configures backup protection for an Azure resource by creating a protected item or backup instance. This tool is part of the Model Context Protocol (MCP) toolset. It protects virtual machines, managed disks, file shares, SQL Server databases, SAP HANA databases, and other supported data sources.

For virtual machines, specify the VM ARM resource ID as the datasource ID. For in-guest workloads such as SQL Server and SAP HANA, specify the protectable item name returned by the protectableitem list tool, for example `SAPHanaDatabase;instance;dbname`, and provide the container when required. Specify the backup policy by name. The operation runs asynchronously, so monitor the protection job with the azurebackup job get tool.

Example prompts include:

- "Protect datasource ID '/subscriptions/12345678-90ab-cdef-1234-567890abcdef/resourceGroups/prod-rg/providers/Microsoft.Compute/virtualMachines/webapp-prod' with policy 'DailyBackup' in resource group 'prod-rg' and vault 'rsv-prod'."
- "Protect datasource ID '/subscriptions/12345678-90ab-cdef-1234-567890abcdef/resourceGroups/rg-prod/providers/Microsoft.Compute/disks/dataDisk1' with policy 'HourlyBackup' in resource group 'rg-prod' and vault 'dpp-backup', datasource type 'AzureDisk'."
- "Protect datasource ID 'SAPHanaDatabase;instance01;SalesDB' with policy 'SAPHanaPolicy' in resource group 'sap-rg' and vault 'rsv-sap', container 'saphana-cont'."
- "Protect datasource ID '/subscriptions/12345678-90ab-cdef-1234-567890abcdef/resourceGroups/files-rg/providers/Microsoft.Storage/storageAccounts/mystorage/fileServices/default/shares/backups' with policy 'FileSharePolicy' in resource group 'files-rg' and vault 'rsv-files', container 'files-container'."
- "Can you protect datasource ID '/subscriptions/12345678-90ab-cdef-1234-567890abcdef/resourceGroups/test-rg/providers/Microsoft.Compute/virtualMachines/db-staging' using policy 'WeeklyFull' in resource group 'test-rg' and vault 'backup-vault'?"

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Datasource ID** |  Required | The datasource identifier. For VM, FileShare, and DPP workloads, use the ARM resource ID (for example, `'/subscriptions/.../virtualMachines/myvm'`). For Recovery Services vault (RSV) in-guest workloads such as SQL Server and SAP HANA, use the protectable item name returned by the protectableitem list tool (for example, `SAPHanaDatabase;instance;dbname`). |
| **Policy** |  Required | The name of the backup policy. |
| **Resource group** |  Required | The name of the Azure resource group. This resource group is a logical container for Azure resources. |
| **Vault name** |  Required | The name of the backup vault, such as a Recovery Services vault or a Backup vault. |
| **Container name** |  Optional | The RSV protection container name. Only applicable for Recovery Services vaults. |
| **Datasource type** |  Optional | The workload type hint: `VM`, `SQL`, `SAPHANA`, `SAPASE`, `AzureFileShare` (RSV types); `AzureDisk`, `AzureBlob`, `AKS`, `ElasticSAN`, `PostgreSQLFlexible`, `ADLS`, `Azure Cosmos DB` (DPP types). Also accepts common aliases such as AzureVM and SQLDatabase. |
| **Vault type** |  Optional | The type of backup vault: `rsv` (Recovery Services vault) or `dpp` (Backup vault / Data Protection). Required for vault create; optional elsewhere (auto-detected if omitted). |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ✅ | Idempotent: ❌ | Open World: ❌ | Read Only: ❌ | Secret: ❌ | Local Required: ❌

#### [CLI](#tab/cli)

Enables or configures backup protection for an Azure resource by creating a
protected item or backup instance. Protects VMs, disks, file shares, SQL databases,
SAP HANA databases, and other supported datasources.
For VMs: pass the VM ARM resource ID as --datasource-id.
For workloads (SQL/HANA): pass the protectable item name from 'protectableitem list'
as --datasource-id (e.g., 'SAPHanaDatabase;instance;dbname'), and specify --container.
Requires a backup policy name via --policy. The operation is asynchronous;
use 'azurebackup job get' to monitor the protection job progress.

**Example CLI command**

```azurecli
azmcp azurebackup protecteditem protect \
  --resource-group <resource-group> \
  --vault <vault> \
  --datasource-id <datasource-id> \
  --policy <policy> \
  [--vault-type <vault-type>] \
  [--container <container>] \
  [--datasource-type <datasource-type>]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--resource-group` | string | Yes | The name of the Azure resource group. This is a logical container for Azure resources. |
| `--vault` | string | Yes | The name of the backup vault (Recovery Services vault or Backup vault). |
| `--vault-type` | string | No | The type of backup vault: 'rsv' (Recovery Services vault) or 'dpp' (Backup vault / Data Protection). Required for vault create; optional elsewhere (auto-detected if omitted). |
| `--datasource-id` | string | Yes | The datasource identifier. For VM/FileShare/DPP workloads, use the ARM resource ID (e.g., '/subscriptions/.../virtualMachines/myvm'). For RSV in-guest workloads (SQL/SAPHANA), use the protectable item name from 'protectableitem list' (e.g., 'SAPHanaDatabase;instance;dbname'). |
| `--policy` | string | Yes | The name of the backup policy. |
| `--container` | string | No | The RSV protection container name. Only applicable for Recovery Services vaults. |
| `--datasource-type` | string | No | The workload type hint: VM, SQL, SAPHANA, SAPASE, AzureFileShare (RSV types); AzureDisk, AzureBlob, AKS, ElasticSAN, PostgreSQLFlexible, ADLS, CosmosDB (DPP types). Also accepts aliases like AzureVM, SQLDatabase, etc. |

---

## Soft delete governance
#### [MCP Server](#tab/mcp-server)


<!-- @mcpcli azurebackup governance soft-delete -->

This tool configures soft delete settings for a backup vault. You set the state to `AlwaysOn`, `On`, or `Off`, and you can optionally specify the retention period in days (14-180). Soft delete helps prevent accidental data loss by retaining deleted recovery points for the configured retention period so you can recover backups if deletion occurs.

This tool is part of the Model Context Protocol (MCP) tools for managing Azure backup resources.

Example prompts include:

- "Enable soft delete 'AlwaysOn' for vault name 'backup-vault-prod' in resource group 'rg-prod'."
- "Set soft delete 'On' with soft delete retention days '30' for vault name 'rsv-prod' in resource group 'rg-backup'."
- "Turn soft delete 'Off' for vault name 'dpp-archive' in resource group 'rg-archives'?"
- "Configure soft delete 'On' for vault name 'my-recovery-vault' in resource group 'prod-rg' with vault type 'rsv' and soft delete retention days '90'."
- "Can you set soft delete 'AlwaysOn' on vault name 'backup-vault-staging' in resource group 'rg-staging' with vault type 'dpp'?"

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Resource group** |  Required | The name of the Azure resource group. This resource group is a logical container for Azure resources. |
| **Soft delete** |  Required | Soft delete state: `AlwaysOn`, `On`, or `Off`. |
| **Vault name** |  Required | The name of the backup vault (Recovery Services vault or Backup vault). |
| **Soft delete retention days** |  Optional | Soft delete retention period (14-180 days). |
| **Vault type** |  Optional | The type of backup vault: 'rsv' (Recovery Services vault) or 'dpp' (Backup vault / Data Protection). Required for vault create; optional elsewhere (auto-detected if omitted). |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ✅ | Idempotent: ✅ | Open World: ❌ | Read Only: ❌ | Secret: ❌ | Local Required: ❌

Examples

- Configure soft delete to 'AlwaysOn' for vault 'ProdRecoveryVault' in resource group 'rg-prod' with retention 30 days.
- Set soft delete to 'Off' for vault 'TestVault' in resource group 'rg-test'.

#### [CLI](#tab/cli)

Configures the soft delete settings for a backup vault. Set the state to 'AlwaysOn', 'On',
or 'Off', and optionally specify the retention period in days (14-180).

**Example CLI command**

```azurecli
azmcp azurebackup governance soft-delete \
  --resource-group <resource-group> \
  --vault <vault> \
  --soft-delete <soft-delete> \
  [--vault-type <vault-type>] \
  [--soft-delete-retention-days <soft-delete-retention-days>]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--resource-group` | string | Yes | The name of the Azure resource group. This is a logical container for Azure resources. |
| `--vault` | string | Yes | The name of the backup vault (Recovery Services vault or Backup vault). |
| `--vault-type` | string | No | The type of backup vault: 'rsv' (Recovery Services vault) or 'dpp' (Backup vault / Data Protection). Required for vault create; optional elsewhere (auto-detected if omitted). |
| `--soft-delete` | string | Yes | Soft delete state: 'AlwaysOn', 'On', or 'Off'. |
| `--soft-delete-retention-days` | string | No | Soft delete retention period (14-180 days). |

---

## Status backup
#### [MCP Server](#tab/mcp-server)


<!-- @mcpcli azurebackup backup status -->

This tool checks the backup status of an Azure resource and returns whether the resource is protected, along with the backup vault and policy details from Azure Backup. You can verify protection for a virtual machine, managed disk, storage account, or other data source. The tool requires the datasource ARM resource ID and the Azure region where the resource exists.

For example, check the backup status of a virtual machine by providing its resource ID '/subscriptions/11111111-1111-1111-1111-111111111111/resourceGroups/myResourceGroup/providers/Microsoft.Compute/virtualMachines/myVM' and Location 'eastus'.

Example prompts include:

- "Check backup status for datasource ID '/subscriptions/12345678-1234-1234-1234-123456789012/resourceGroups/rg-prod/providers/Microsoft.Compute/virtualMachines/webapp-prod' in location 'eastus'."
- "Is datasource ID '/subscriptions/12345678-1234-1234-1234-123456789012/resourceGroups/rg-backup/providers/Microsoft.Compute/disks/db-disk' protected in location 'westus2'?"
- "Show backup protection details for datasource ID '/subscriptions/abcdefab-0000-1111-2222-333344445555/resourceGroups/rg-storage/providers/Microsoft.Storage/storageAccounts/mystorageacct' in location 'eastus2'."
- "Verify backup status for datasource ID 'SAPHanaDatabase;instance;sapprd' in location 'centralus'."
- "I need the backup status for datasource ID '/subscriptions/22222222-2222-3333-4444-555566667777/resourceGroups/prod-rg/providers/Microsoft.RecoveryServices/vaults/my-vault/backupFabrics/Azure/protectionContainers/iaasvmcontainer;iaasvmcontainerv2;prod-rg;app-server/protectableItems/app-server' in location 'eastus'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Datasource ID** |  Required | The datasource identifier. For VM/FileShare/DPP workloads, use the ARM resource ID (for example, `'/subscriptions/.../virtualMachines/myvm'`). For RSV in-guest workloads (SQL/SAPHANA), use the protectable item name from 'protectableitem list' (for example, `'SAPHanaDatabase;instance;dbname'`). |
| **Location** |  Required | The Azure region (for example, `'eastus'`, `'westus2'`). |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌

#### [CLI](#tab/cli)

Checks the backup status of an Azure resource and returns whether it is protected,
along with vault and policy details. Use this to verify if a VM, disk, storage account,
or other datasource is currently backed up. Requires the datasource ARM resource ID
and the Azure region (location) where the resource exists.

**Example CLI command**

```azurecli
azmcp azurebackup backup status \
  --datasource-id <datasource-id> \
  --location <location>
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--datasource-id` | string | Yes | The datasource identifier. For VM/FileShare/DPP workloads, use the ARM resource ID (e.g., '/subscriptions/.../virtualMachines/myvm'). For RSV in-guest workloads (SQL/SAPHANA), use the protectable item name from 'protectableitem list' (e.g., 'SAPHanaDatabase;instance;dbname'). |
| `--location` | string | Yes | The Azure region (e.g., 'eastus', 'westus2'). |

---

## Undelete protected item
#### [MCP Server](#tab/mcp-server)


<!-- @mcpcli azurebackup protecteditem undelete -->

This tool undeletes or restores a soft-deleted backup item to an active protection state. Recover an accidentally deleted backup or protected item. The operation runs asynchronously, so monitor progress by checking the job status in the vault's job list.

Example prompts include:

- "Undelete protected item with datasource ID '/subscriptions/11111111-1111-1111-1111-111111111111/resourceGroups/rg-prod/providers/Microsoft.Compute/virtualMachines/myvm', resource group 'rg-prod', and vault name 'backup-vault'."
- "Can you undelete the protectable item with datasource ID 'SAPHanaDatabase;instance01;db01' from resource group 'rg-sql' in vault name 'rsv-vault'?"
- "Undelete the protected item with datasource ID 'SAPHanaDatabase;instance02;db02', resource group 'rg-sap', vault name 'rsv-main', and container 'sap-container'."
- "Recover the soft-deleted protected item with datasource ID '/subscriptions/33333333-3333-3333-3333-333333333333/resourceGroups/rg-dev/providers/Microsoft.Compute/virtualMachines/test-vm' in resource group 'rg-dev' using vault name 'backup-vault-east'."
- "Please undelete the file-share protected item with datasource ID '/subscriptions/44444444-4444-4444-4444-444444444444/resourceGroups/rg-files/providers/Microsoft.Storage/storageAccounts/myfileshare/fileServices/default/shares/myshare', resource group 'rg-files', vault name 'dpp-vault', and vault type 'dpp'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Datasource ID** |  Required | The datasource identifier. For VM/FileShare/DPP workloads, use the ARM resource ID (for example, `'/subscriptions/.../virtualMachines/myvm'`). For RSV in-guest workloads (SQL/SAPHANA), use the protectable item name from 'protectableitem list' (for example, `'SAPHanaDatabase;instance;dbname'`). |
| **Resource group** |  Required | The name of the Azure resource group. This resource group is a logical container for Azure resources. |
| **Vault name** |  Required | The name of the backup vault (Recovery Services vault or Backup vault). |
| **Container name** |  Optional | The RSV protection container name. Only applicable for Recovery Services vaults. |
| **Vault type** |  Optional | The type of backup vault: 'rsv' (Recovery Services vault) or 'dpp' (Backup vault / Data Protection). Required for vault create; optional elsewhere (auto-detected if omitted). |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ✅ | Idempotent: ✅ | Open World: ❌ | Read Only: ❌ | Secret: ❌ | Local Required: ❌

#### [CLI](#tab/cli)

Undeletes or restores a soft-deleted backup item to an active protection state.
Use this when a backup or protected item was accidentally deleted and needs to be recovered.
For RSV vaults: pass the datasource ARM resource ID as --datasource-id.
For DPP vaults: pass the datasource ARM resource ID as --datasource-id.
Optionally specify --container for RSV workload items (SQL/HANA).
The operation is asynchronous; use 'azurebackup job get' to monitor progress.

**Example CLI command**

```azurecli
azmcp azurebackup protecteditem undelete \
  --resource-group <resource-group> \
  --vault <vault> \
  --datasource-id <datasource-id> \
  [--vault-type <vault-type>] \
  [--container <container>]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--resource-group` | string | Yes | The name of the Azure resource group. This is a logical container for Azure resources. |
| `--vault` | string | Yes | The name of the backup vault (Recovery Services vault or Backup vault). |
| `--vault-type` | string | No | The type of backup vault: 'rsv' (Recovery Services vault) or 'dpp' (Backup vault / Data Protection). Required for vault create; optional elsewhere (auto-detected if omitted). |
| `--datasource-id` | string | Yes | The datasource identifier. For VM/FileShare/DPP workloads, use the ARM resource ID (e.g., '/subscriptions/.../virtualMachines/myvm'). For RSV in-guest workloads (SQL/SAPHANA), use the protectable item name from 'protectableitem list' (e.g., 'SAPHanaDatabase;instance;dbname'). |
| `--container` | string | No | The RSV protection container name. Only applicable for Recovery Services vaults. |

---

## Update vault
#### [MCP Server](#tab/mcp-server)


<!-- @mcpcli azurebackup vault update -->

This tool updates vault-level settings for a Recovery Services vault or Backup vault used by Azure Backup, including storage redundancy, soft delete, immutability, and managed identity.

Example prompts include:

- "Update vault 'backup-main' in resource group 'rg-prod' to set redundancy 'GeoRedundant'."
- "Enable immutability state 'Enabled' and set identity type 'SystemAssigned' for vault 'rsv-primary' in resource group 'rg-backup'."
- "Turn soft delete 'AlwaysOn' with soft delete retention days '30' for vault 'vault-archive' in resource group 'rg-archive'."
- "Add tags '{"env":"prod","owner":"backup-team"}' and set identity type 'UserAssigned' for vault 'secure-vault' in resource group 'rg-security'."
- "Can you update vault 'dpp-recovery' in resource group 'rg-disaster' to set immutability state 'Locked', redundancy 'ReadAccessGeoZoneRedundant', and vault type 'rsv'?"

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Resource group** |  Required | The name of the Azure resource group that contains the vault. |
| **Vault name** |  Required | The name of the vault, such as a Recovery Services vault or Backup vault. |
| **Identity type** |  Optional | Managed identity type: `SystemAssigned`, `UserAssigned`, or `None`. |
| **Immutability state** |  Optional | Immutability state: `Disabled`, `Enabled`, or `Locked` (irreversible). |
| **Redundancy** |  Optional | Storage redundancy: `GeoRedundant`, `LocallyRedundant`, `ZoneRedundant`, or `ReadAccessGeoZoneRedundant`. |
| **Soft delete** |  Optional | Soft delete state: `AlwaysOn`, `On`, or `Off`. |
| **Soft delete retention days** |  Optional | Soft delete retention period in days (14–180). |
| **Tags** |  Optional | Resource tags as a JSON object of key-value pairs. |
| **Vault type** |  Optional | The type of backup vault: `rsv` (Recovery Services vault) or `dpp` (Backup vault / Data Protection). Required for vault create; optional elsewhere (auto-detected if omitted). |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ✅ | Idempotent: ✅ | Open World: ❌ | Read Only: ❌ | Secret: ❌ | Local Required: ❌

Examples

For example, update vault 'contoso-vault' in resource group 'contoso-rg' to use redundancy 'GeoRedundant' and enable soft delete 'On' with retention days '30'.

For example, assign a system-assigned managed identity to vault 'prod-backup' in resource group 'prod-rg' and set immutability state to 'Enabled'.

#### [CLI](#tab/cli)

Updates vault-level settings including storage redundancy, soft delete, immutability, and managed identity.

**Example CLI command**

```azurecli
azmcp azurebackup vault update \
  --resource-group <resource-group> \
  --vault <vault> \
  [--vault-type <vault-type>] \
  [--redundancy <redundancy>] \
  [--soft-delete <soft-delete>] \
  [--soft-delete-retention-days <soft-delete-retention-days>] \
  [--immutability-state <immutability-state>] \
  [--identity-type <identity-type>] \
  [--tags <tags>]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--resource-group` | string | Yes | The name of the Azure resource group. This is a logical container for Azure resources. |
| `--vault` | string | Yes | The name of the backup vault (Recovery Services vault or Backup vault). |
| `--vault-type` | string | No | The type of backup vault: 'rsv' (Recovery Services vault) or 'dpp' (Backup vault / Data Protection). Required for vault create; optional elsewhere (auto-detected if omitted). |
| `--redundancy` | string | No | Storage redundancy: 'GeoRedundant', 'LocallyRedundant', 'ZoneRedundant', or 'ReadAccessGeoZoneRedundant'. |
| `--soft-delete` | string | No | Soft delete state: 'AlwaysOn', 'On', or 'Off'. |
| `--soft-delete-retention-days` | string | No | Soft delete retention period (14-180 days). |
| `--immutability-state` | string | No | Immutability state: 'Disabled', 'Enabled', or 'Locked' (irreversible). |
| `--identity-type` | string | No | Managed identity type: 'SystemAssigned', 'UserAssigned', or 'None'. |
| `--tags` | string | No | Resource tags as JSON key-value object. |

---

## Related content

- [What are the Azure MCP Server tools?](index.md)
- [Get started using Azure MCP Server](../get-started.md)
