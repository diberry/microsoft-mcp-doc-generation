List or get Azure Virtual Machine (VM) configuration and properties in a resource group. By default, returns VM details including name, location, size, provisioning state, and OS type. When retrieving a specific VM with --vm-name and --instance-view, the response also includes power state (running/stopped/deallocated). Use this tool to retrieve VM configuration details.

### Example CLI commands

Basic usage:

```azurecli
azmcp compute vm get
```

With parameters:

```azurecli
azmcp compute vm get --resource-group <resource-group> --vm-name <vm-name> --instance-view <instance-view>
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
| `--vm-name` | string | - | The name of the virtual machine |
| `--instance-view` | string | - | Include instance view details (only available when retrieving a specific VM) |

