Get Event Hubs from Azure namespace. This command can either:
1. List all Event Hubs in a namespace
2. Get a single Event Hub by name

When retrieving a single Event Hub or listing multiple Event Hubs, detailed information including
partition count, settings, and metadata is returned for all Event Hubs.

### Example CLI commands

Basic usage:

```azurecli
azmcp eventhubs eventhub get
```

With parameters:

```azurecli
azmcp eventhubs eventhub get --resource-group <resource-group> --namespace <namespace> --eventhub <eventhub>
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
| `--namespace` | string | The name of the Event Hubs namespace to retrieve. Must be used with --resource-group option. |
| `--eventhub` | string | The name of the Event Hub within the namespace. |

