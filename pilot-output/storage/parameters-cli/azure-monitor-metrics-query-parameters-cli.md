---
ms.topic: include
ms.date: 05/08/2026
mcp-cli.version: 1.0.0-pilot
---
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
| `--metric-names` | string | The names of metrics to query (comma-separated). |
| `--start-time` | string | The start time for the query in ISO format (e.g., 2023-01-01T00:00:00Z). Defaults to 24 hours ago. |
| `--end-time` | string | The end time for the query in ISO format (e.g., 2023-01-01T00:00:00Z). Defaults to now. |
| `--interval` | string | The time interval for data points (e.g., PT1H for 1 hour, PT5M for 5 minutes). |
| `--aggregation` | string | The aggregation type to use (Average, Maximum, Minimum, Total, Count). |
| `--filter` | string | OData filter to apply to the metrics query. |
| `--metric-namespace` | string | The metric namespace to query. Obtain this value from the azmcp-monitor-metrics-definitions command. |
| `--max-buckets` | string | The maximum number of time buckets to return. Defaults to 50. |
