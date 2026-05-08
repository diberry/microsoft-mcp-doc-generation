### [MCP Server](#tab/mcp-server)

This tool executes `azurebackup protecteditem undelete` via MCP Server.

See parameters below.

### [CLI](#tab/cli)

Undeletes or restores a soft-deleted backup item to an active protection state.
Use this when a backup or protected item was accidentally deleted and needs to be recovered.
For RSV vaults: pass the datasource ARM resource ID as --datasource-id.
For DPP vaults: pass the datasource ARM resource ID as --datasource-id.
Optionally specify --container for RSV workload items (SQL/HANA).
The operation is asynchronous; use 'azurebackup job get' to monitor progress.

### Example CLI commands

Basic usage:

```azurecli
azmcp azurebackup protecteditem undelete
```

With parameters:

```azurecli
azmcp azurebackup protecteditem undelete --resource-group <resource-group> --vault <vault> --vault-type <vault-type> --datasource-id <datasource-id> --container <container>
```

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `--tenant` | string | - | The Microsoft Entra ID tenant ID or name. This can be either the GUID identifier or the display name of your Entra ID tenant. |
| `--auth-method` | string | - | Authentication method to use. Options: 'credential' (Azure CLI/managed identity), 'key' (access key), or 'connectionString'. |
| `--retry-delay` | string | - | Initial delay in seconds between retry attempts. For exponential backoff, this value is used as the base. |
| `--retry-max-delay` | string | - | Maximum delay in seconds between retries, regardless of the retry strategy. |
| `--retry-max-retries` | string | - | Maximum number of retry attempts for failed operations before giving up. |
| `--retry-mode` | string | - | Retry strategy to use. 'fixed' uses consistent delays, 'exponential' increases delay between attempts. |
| `--retry-network-timeout` | string | - | Network operation timeout in seconds. Operations taking longer than this will be cancelled. |
| `--subscription` | string | - | Specifies the Azure subscription to use. Accepts either a subscription ID (GUID) or display name. If not specified, the AZURE_SUBSCRIPTION_ID environment variable will be used instead. |
| `--resource-group` | string | - | The name of the Azure resource group. This is a logical container for Azure resources. |
| `--vault` | string | - | The name of the backup vault (Recovery Services vault or Backup vault). |
| `--vault-type` | string | - | The type of backup vault: 'rsv' (Recovery Services vault) or 'dpp' (Backup vault / Data Protection). Required for vault create; optional elsewhere (auto-detected if omitted). |
| `--datasource-id` | string | - | The datasource identifier. For VM/FileShare/DPP workloads, use the ARM resource ID (e.g., '/subscriptions/.../virtualMachines/myvm'). For RSV in-guest workloads (SQL/SAPHANA), use the protectable item name from 'protectableitem list' (e.g., 'SAPHanaDatabase;instance;dbname'). |
| `--container` | string | - | The RSV protection container name. Only applicable for Recovery Services vaults. |

---
