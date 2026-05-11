### [MCP Server](#tab/mcp-server)

This tool executes `eventhubs eventhub consumergroup get` via MCP Server.

See parameters below.

### [CLI](#tab/cli)

Get consumer groups from Azure Event Hub. This command can either:

1) List all consumer groups in an Event Hub
2) Get a single consumer group by name

The EventHub, Namespace, and ResourceGroup parameters are required (for both get and list)
The Consumer Group parameter is only required for getting a specific consumer-group
When retrieving a single consumer group and when listing all available consumer groups, return all available metadata on the consumer group.

### Example CLI commands

Basic usage:

```azurecli
azmcp eventhubs eventhub consumergroup get
```

With parameters:

```azurecli
azmcp eventhubs eventhub consumergroup get --resource-group <resource-group> --namespace <namespace> --eventhub <eventhub> --consumer-group <consumer-group>
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

---
