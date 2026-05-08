Creates a one-time import job for an Azure Managed Lustre filesystem to import files from the linked blob storage container. The import job performs a one-time sync of data from the configured HSM blob container to the Lustre filesystem. Use this to import specific prefixes or all data from blob storage into the filesystem at a point in time.
Required options:
- filesystem-name: The name of the AMLFS filesystem
Optional options:
- job-name: Name for the import job (auto-generated if not provided)
- conflict-resolution-mode: How to handle conflicting files (Fail, Skip, OverwriteIfDirty, OverwriteAlways, default: Fail)
- import-prefixes: Blob prefixes to import (default: imports all data from root '/')
- maximum-errors: Maximum errors allowed before job failure (-1: infinite, 0: fail on first error, default: use service default)

### Example CLI commands

Basic usage:

```azurecli
azmcp managedlustre fs blob import create
```

With parameters:

```azurecli
azmcp managedlustre fs blob import create --resource-group <resource-group> --filesystem-name <filesystem-name> --job-name <job-name> --conflict-resolution-mode <conflict-resolution-mode> --import-prefixes <import-prefixes> --maximum-errors <maximum-errors>
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
| `--job-name` | string | - | The name of the autoimport job. If not specified, a timestamped name will be generated. |
| `--conflict-resolution-mode` | string | - | How the auto import job handles conflicts. Fail: stop immediately on conflict. Skip: pass over the conflict. OverwriteIfDirty: delete and re-import if conflicting type, dirty, or currently released. OverwriteAlways: extends OverwriteIfDirty to include releasing restored but not dirty files. Default: Skip. Allowed values: Fail, Skip, OverwriteIfDirty, OverwriteAlways. |
| `--import-prefixes` | string | - | Array of blob paths/prefixes to import from blob storage. Default: '/'. Maximum: 100 paths. Example: --import-prefixes /data --import-prefixes /logs |
| `--maximum-errors` | string | - | Total non-conflict-oriented errors (e.g., OS errors) that import will tolerate before exiting with failure. -1: infinite. 0: exit immediately on any error. |

