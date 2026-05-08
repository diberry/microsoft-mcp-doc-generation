Delete, remove, or destroy an Azure Virtual Machine Scale Set (VMSS) and all its VM instances.
Use this to permanently remove a scale set that is no longer needed.
Equivalent to 'az vmss delete'. This operation is irreversible and all VMSS instances will be lost.
Use --force-deletion to force delete the VMSS even if it is in a running or failed state
(passes forceDeletion=true to the Azure API).
Do not use this to delete a single VM (use VM delete instead).

### Example CLI commands

Basic usage:

```azurecli
azmcp compute vmss delete
```

With parameters:

```azurecli
azmcp compute vmss delete --resource-group <resource-group> --vmss-name <vmss-name> --force-deletion <force-deletion>
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
| `--vmss-name` | string | - | The name of the virtual machine scale set |
| `--force-deletion` | string | - | Force delete the resource even if it is in a running or failed state (passes forceDeletion=true to the Azure API) |

