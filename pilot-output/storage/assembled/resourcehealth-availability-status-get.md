Get the Azure Resource Health availability status for a specific resource or all resources in a subscription or resource group. Use this tool when asked about the availability status, health status, or Resource Health of an Azure resource (e.g. virtual machine, storage account). Reports whether a resource is Available, Unavailable, Degraded, or Unknown, including the reason and details. This is the correct tool for questions like 'What is the availability status of VM X?' or 'Is resource Y healthy?'.

### Example CLI commands

Basic usage:

```azurecli
azmcp resourcehealth availability-status get
```

With parameters:

```azurecli
azmcp resourcehealth availability-status get --resourceId <resourceId> --resource-group <resource-group>
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
| `--resourceId` | string | - | The Azure resource ID to get health status for (e.g., /subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.Compute/virtualMachines/{vm}). |
| `--resource-group` | string | - | The name of the Azure resource group. This is a logical container for Azure resources. |

