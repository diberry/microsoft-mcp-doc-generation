Enables or configures backup protection for an Azure resource by creating a
protected item or backup instance. Protects VMs, disks, file shares, SQL databases,
SAP HANA databases, and other supported datasources.
For VMs: pass the VM ARM resource ID as --datasource-id.
For workloads (SQL/HANA): pass the protectable item name from 'protectableitem list'
as --datasource-id (e.g., 'SAPHanaDatabase;instance;dbname'), and specify --container.
Requires a backup policy name via --policy. The operation is asynchronous;
use 'azurebackup job get' to monitor the protection job progress.

### Example CLI commands

Basic usage:

```azurecli
azmcp azurebackup protecteditem protect
```

With parameters:

```azurecli
azmcp azurebackup protecteditem protect --resource-group <resource-group> --vault <vault> --vault-type <vault-type> --datasource-id <datasource-id> --policy <policy> --container <container> --datasource-type <datasource-type>
```

| Parameter | Type | Description |
|-----------|------|-------------|
| `--tenant` | string | The Microsoft Entra ID tenant ID or name. This can be either the GUID identifier or the display name of your Entra ID tenant. |
| `--auth-method` | string | Authentication method to use. Options: 'credential' (Azure CLI/managed identity), 'key' (access key), or 'connectionString'. |
| `--retry-delay` | string | Initial delay in seconds between retry attempts. For exponential backoff, this value is used as the base. |
| `--retry-max-delay` | string | Maximum delay in seconds between retries, regardless of the retry strategy. |
| `--retry-max-retries` | string | Maximum number of retry attempts for failed operations before giving up. |
| `--retry-mode` | string | Retry strategy to use. 'fixed' uses consistent delays, 'exponential' increases delay between attempts. |
| `--retry-network-timeout` | string | Network operation timeout in seconds. Operations taking longer than this will be cancelled. |
| `--subscription` | string | Specifies the Azure subscription to use. Accepts either a subscription ID (GUID) or display name. If not specified, the AZURE_SUBSCRIPTION_ID environment variable will be used instead. |
| `--resource-group` | string | The name of the Azure resource group. This is a logical container for Azure resources. |
| `--vault` | string | The name of the backup vault (Recovery Services vault or Backup vault). |
| `--vault-type` | string | The type of backup vault: 'rsv' (Recovery Services vault) or 'dpp' (Backup vault / Data Protection). Required for vault create; optional elsewhere (auto-detected if omitted). |
| `--datasource-id` | string | The datasource identifier. For VM/FileShare/DPP workloads, use the ARM resource ID (e.g., '/subscriptions/.../virtualMachines/myvm'). For RSV in-guest workloads (SQL/SAPHANA), use the protectable item name from 'protectableitem list' (e.g., 'SAPHanaDatabase;instance;dbname'). |
| `--policy` | string | The name of the backup policy. |
| `--container` | string | The RSV protection container name. Only applicable for Recovery Services vaults. |
| `--datasource-type` | string | The workload type hint: VM, SQL, SAPHANA, SAPASE, AzureFileShare (RSV types); AzureDisk, AzureBlob, AKS, ElasticSAN, PostgreSQLFlexible, ADLS, CosmosDB (DPP types). Also accepts aliases like AzureVM, SQLDatabase, etc. |

