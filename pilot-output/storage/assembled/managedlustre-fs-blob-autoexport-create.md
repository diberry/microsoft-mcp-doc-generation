Creates an auto export job for an Azure Managed Lustre filesystem to continuously export modified files to the linked blob storage container. The auto export job syncs changes from the Lustre filesystem to the configured HSM blob container. Use this to keep blob storage updated with changes in the filesystem.
Required options:
- filesystem-name: The name of the AMLFS filesystem
- resource-group: The resource group containing the filesystem
- subscription: The subscription containing the filesystem

### Example CLI commands

Basic usage:

```azurecli
azmcp managedlustre fs blob autoexport create
```

With parameters:

```azurecli
azmcp managedlustre fs blob autoexport create --resource-group <resource-group> --filesystem-name <filesystem-name> --job-name <job-name> --autoexport-prefix <autoexport-prefix> --admin-status <admin-status>
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
| `--filesystem-name` | string | - | The name of the Azure Managed Lustre filesystem |
| `--job-name` | string | - | The name of the autoexport job. If not specified, a timestamped name will be generated. |
| `--autoexport-prefix` | string | - | Blob path/prefix that gets auto exported from the cluster namespace. Default: '/'. Note: Only 1 prefix is supported for autoexport jobs. Example: --autoexport-prefix /data |
| `--admin-status` | string | - | The administrative status of the auto import job. Enable: job is active. Disable: disables the current active auto import job. Default: Enable. Allowed values: Enable, Disable. |

