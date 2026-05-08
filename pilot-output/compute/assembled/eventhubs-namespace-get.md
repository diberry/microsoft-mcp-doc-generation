Get Event Hubs namespaces from Azure. This command supports three modes of operation:
1. List all Event Hubs namespaces in a subscription 
2. List all Event Hubs namespaces in a specific resource group 
3. Get a single namespace by name 

When retrieving a single namespace, detailed information including SKU, settings, and metadata 
is returned. When listing namespaces, the same detailed information is returned for all 
namespaces in the specified scope.

The --resource-group parameter is optional for listing operations but required when getting a specific namespace.

### Example CLI commands

Basic usage:

```azurecli
azmcp eventhubs namespace get
```

With parameters:

```azurecli
azmcp eventhubs namespace get --resource-group <resource-group> --namespace <namespace>
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
| `--namespace` | string | - | The name of the Event Hubs namespace to retrieve. Must be used with --resource-group option. |

