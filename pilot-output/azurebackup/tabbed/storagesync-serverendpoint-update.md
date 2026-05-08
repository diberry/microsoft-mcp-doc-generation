### [MCP Server](#tab/mcp-server)

This tool executes `storagesync serverendpoint update` via MCP Server.

See parameters below.

### [CLI](#tab/cli)

Update properties of a server endpoint.

### Example CLI commands

Basic usage:

```azurecli
azmcp storagesync serverendpoint update
```

With parameters:

```azurecli
azmcp storagesync serverendpoint update --resource-group <resource-group> --name <name> --sync-group-name <sync-group-name> --server-endpoint-name <server-endpoint-name> --cloud-tiering <cloud-tiering> --volume-free-space-percent <volume-free-space-percent> --tier-files-older-than-days <tier-files-older-than-days> --local-cache-mode <local-cache-mode>
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
| `--name` | string | - | The name of the storage sync service |
| `--sync-group-name` | string | - | The name of the sync group |
| `--server-endpoint-name` | string | - | The name of the server endpoint |
| `--cloud-tiering` | string | - | Enable cloud tiering on this endpoint |
| `--volume-free-space-percent` | string | - | Volume free space percentage to maintain (1-99, default 20) |
| `--tier-files-older-than-days` | string | - | Archive files not accessed for this many days |
| `--local-cache-mode` | string | - | Local cache mode: DownloadNewAndModifiedFiles, UpdateLocallyCachedFiles |

---
