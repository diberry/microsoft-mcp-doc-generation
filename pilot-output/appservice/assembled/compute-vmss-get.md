List or get Azure Virtual Machine Scale Sets (VMSS) and their instances in a subscription or resource group. Returns scale set details including name, location, SKU, capacity, upgrade policy, and individual VM instance information.

### Example CLI commands

Basic usage:

```azurecli
azmcp compute vmss get
```

With parameters:

```azurecli
azmcp compute vmss get --resource-group <resource-group> --vmss-name <vmss-name> --instance-id <instance-id>
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
| `--vmss-name` | string | The name of the virtual machine scale set |
| `--instance-id` | string | The instance ID of the virtual machine in the scale set |

