Delete Event Hubs namespace. This tool will delete a pre-existing Namespace from the 
specified resource group. This tool will remove existing configurations, and is 
considered to be destructive.

WARNING: This operation is irreversible. All Event Hubs, Consumer Groups, and
configurations within the namespace will be permanently deleted.

The namespace must exist in the specified resource group. If the namespace is not found,
an error will be returned.

### Example CLI commands

Basic usage:

```azurecli
azmcp eventhubs namespace delete
```

With parameters:

```azurecli
azmcp eventhubs namespace delete --resource-group <resource-group> --namespace <namespace>
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

