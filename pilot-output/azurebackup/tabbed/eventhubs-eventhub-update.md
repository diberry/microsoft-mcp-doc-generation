### [MCP Server](#tab/mcp-server)

This tool executes `eventhubs eventhub update` via MCP Server.

See parameters below.

### [CLI](#tab/cli)

Create or update an Event Hub within an Azure Event Hubs namespace. This command can either:
1. Create a new Event Hub if it doesn't exist
2. Update an existing Event Hub's configuration

You can configure:
- Partition count (number of partitions for parallel processing)
- Message retention time (how long messages are retained in hours)

Note: Some properties like partition count cannot be changed after creation.
This is a potentially long-running operation that waits for completion.

### Example CLI commands

Basic usage:

```azurecli
azmcp eventhubs eventhub update
```

With parameters:

```azurecli
azmcp eventhubs eventhub update --resource-group <resource-group> --namespace <namespace> --eventhub <eventhub> --partition-count <partition-count> --message-retention-in-hours <message-retention-in-hours> --status <status>
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
| `--partition-count` | string | The number of partitions for the event hub. Must be between 1 and 32 (or higher based on namespace tier). |
| `--message-retention-in-hours` | string | The message retention time in hours. Minimum is 1 hour, maximum depends on the namespace tier. |
| `--status` | string | The status of the event hub (Active, Disabled, etc.). Note: Status may be read-only in some operations. |

---
