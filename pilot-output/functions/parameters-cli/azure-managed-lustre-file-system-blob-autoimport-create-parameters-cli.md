---
ms.topic: include
ms.date: 05/08/2026
mcp-cli.version: 1.0.0-pilot
---
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
| `--filesystem-name` | string | The name of the Azure Managed Lustre filesystem |
| `--job-name` | string | The name of the autoimport job. If not specified, a timestamped name will be generated. |
| `--conflict-resolution-mode` | string | How the auto import job handles conflicts. Fail: stop immediately on conflict. Skip: pass over the conflict. OverwriteIfDirty: delete and re-import if conflicting type, dirty, or currently released. OverwriteAlways: extends OverwriteIfDirty to include releasing restored but not dirty files. Default: Skip. Allowed values: Fail, Skip, OverwriteIfDirty, OverwriteAlways. |
| `--autoimport-prefixes` | string | Array of blob paths/prefixes that get auto imported to the cluster namespace. Default: '/'. Maximum: 100 paths. Example: --autoimport-prefixes /data --autoimport-prefixes /logs |
| `--admin-status` | string | The administrative status of the auto import job. Enable: job is active. Disable: disables the current active auto import job. Default: Enable. Allowed values: Enable, Disable. |
| `--enable-deletions` | string | Whether to enable deletions during auto import. This only affects overwrite-dirty mode. Default: false. |
| `--maximum-errors` | string | Total non-conflict-oriented errors (e.g., OS errors) that import will tolerate before exiting with failure. -1: infinite. 0: exit immediately on any error. |
