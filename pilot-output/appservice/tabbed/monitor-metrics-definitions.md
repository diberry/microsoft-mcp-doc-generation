### [MCP Server](#tab/mcp-server)

This tool executes `monitor metrics definitions` via MCP Server.

See parameters below.

### [CLI](#tab/cli)

List available metric definitions for an Azure resource. Returns metadata about the metrics available for the resource.

### Example CLI commands

Basic usage:

```azurecli
azmcp monitor metrics definitions
```

With parameters:

```azurecli
azmcp monitor metrics definitions --resource-group <resource-group> --resource-type <resource-type> --resource <resource> --metric-namespace <metric-namespace> --search-string <search-string> --limit <limit>
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
| `--resource-type` | string | The Azure resource type (e.g., 'Microsoft.Storage/storageAccounts', 'Microsoft.Compute/virtualMachines'). If not specified, will attempt to infer from resource name. |
| `--resource` | string | The name of the Azure resource to query metrics for. |
| `--metric-namespace` | string | The metric namespace to query. Obtain this value from the azmcp-monitor-metrics-definitions command. |
| `--search-string` | string | A string to filter the metric definitions by. Helpful for reducing the number of records returned. Performs case-insensitive matching on metric name and description fields. |
| `--limit` | string | The maximum number of metric definitions to return. Defaults to 10. |

---
