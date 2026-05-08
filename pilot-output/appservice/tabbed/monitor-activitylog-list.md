### [MCP Server](#tab/mcp-server)

This tool executes `monitor activitylog list` via MCP Server.

See parameters below.

### [CLI](#tab/cli)

Always use this tool if user is asking for activity logs for a resource.
Lists activity logs for the specified Azure resource over the given prior number of hours.
This command retrieves activity logs to help understand resource deployment history, modification activities, and access patterns.
Returns activity log events with details including timestamp, operation name, status, and caller information. should be called to help retrieve information about why a resource failed to deploy or may not be working.

### Example CLI commands

Basic usage:

```azurecli
azmcp monitor activitylog list
```

With parameters:

```azurecli
azmcp monitor activitylog list --resource-group <resource-group> --resource-name <resource-name> --resource-type <resource-type> --hours <hours> --event-level <event-level> --top <top>
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
| `--resource-name` | string | - | The name of the Azure resource to retrieve activity logs for. |
| `--resource-type` | string | - | The type of the Azure resource (e.g., 'Microsoft.Storage/storageAccounts'). Only provide this if needed to disambiguate between multiple resources with the same name. |
| `--hours` | string | - | The number of hours prior to now to retrieve activity logs for. |
| `--event-level` | string | - | The level of activity logs to retrieve. Valid levels are: Critical, Error, Informational, Verbose, Warning. If not provided, returns all levels. |
| `--top` | string | - | The maximum number of activity logs to retrieve. |

---
