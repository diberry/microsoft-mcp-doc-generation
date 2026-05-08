Show all available Event Grid subscriptions with optional topic filtering. This tool displays active event subscriptions including webhook endpoints, event filters, and delivery retry policies. Use this when you need to show, list, or get Event Grid subscriptions for topics. Requires either topic name OR subscription. If only topic is provided, searches all accessible subscriptions for a topic with that name. Resource group and location filters can be applied, but only when used with a subscription or topic.

### Example CLI commands

Basic usage:

```azurecli
azmcp eventgrid subscription list
```

With parameters:

```azurecli
azmcp eventgrid subscription list --resource-group <resource-group> --topic <topic> --location <location>
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
| `--topic` | string | - | The name of the Event Grid topic. |
| `--location` | string | - | The Azure region to filter resources by (e.g., 'eastus', 'westus2'). |

