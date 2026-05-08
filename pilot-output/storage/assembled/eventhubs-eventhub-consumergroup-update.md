Create or Update a Consumer Group. This tool will either create a Consumer Group resource 
or update a pre-existing Consumer Group resource within the specified Event Hub, depending 
on whether or not the specified Consumer Group already exists. This tool may modify existing 
configurations, and is considered to be destructive. 

The tool requires specifying the resource group, namespace name, event hub name, and consumer 
group name. Optionally, you can provide user metadata for the consumer group.

### Example CLI commands

Basic usage:

```azurecli
azmcp eventhubs eventhub consumergroup update
```

With parameters:

```azurecli
azmcp eventhubs eventhub consumergroup update --resource-group <resource-group> --namespace <namespace> --eventhub <eventhub> --consumer-group <consumer-group> --user-metadata <user-metadata>
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
| `--consumer-group` | string | The name of the consumer group within the Event Hub. |
| `--user-metadata` | string | User metadata for the consumer group. |

