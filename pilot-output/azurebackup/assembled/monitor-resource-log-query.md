Query diagnostic and activity logs for a SPECIFIC Azure resource in a Log Analytics workspace using Kusto Query Language (KQL). 
Use this tool when the user mentions a specific resource name or Resource ID in their request (e.g., "show logs for resource 'app-monitor'"). 
This tool filters logs to only show data from the specified resource.

When to use: User asks for logs from a specific resource by name or ID.
When NOT to use: User asks for general workspace-wide logs without mentioning a specific resource.

Required arguments: resource ID or resource name, table name, KQL query
Optional: hours, limit

### Example CLI commands

Basic usage:

```azurecli
azmcp monitor resource log query
```

With parameters:

```azurecli
azmcp monitor resource log query --resource-id <resource-id> --table <table> --query <query> --hours <hours> --limit <limit>
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
| `--resource-id` | string | The Azure Resource ID to query logs. Example: /subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.OperationalInsights/workspaces/<ws> |
| `--table` | string | The name of the table to query. This is the specific table within the workspace. |
| `--query` | string | The KQL query to execute against the Log Analytics workspace. You can use predefined queries by name:
- 'recent': Shows most recent logs ordered by TimeGenerated
- 'errors': Shows error-level logs ordered by TimeGenerated
Otherwise, provide a custom KQL query. |
| `--hours` | string | The number of hours to query back from now. |
| `--limit` | string | The maximum number of results to return. |

