### [MCP Server](#tab/mcp-server)

This tool executes `monitor workspace log query` via MCP Server.

See parameters below.

### [CLI](#tab/cli)

Query logs across an ENTIRE Log Analytics workspace using Kusto Query Language (KQL). 
Use this tool when the user wants to query all resources in a workspace or doesn't specify a particular resource name/ID (e.g., "show all errors in workspace", "query workspace logs", "what happened in my workspace").
This tool queries across all resources and tables in the workspace.

When to use: User asks for workspace-wide logs, all resources, or doesn't mention a specific resource.
When NOT to use: User mentions a specific resource name or Resource ID - use resource log query instead.

Requires workspace and resource group.
Optional: hours and limit.
query accepts KQL syntax.

### Example CLI commands

Basic usage:

```azurecli
azmcp monitor workspace log query
```

With parameters:

```azurecli
azmcp monitor workspace log query --resource-group <resource-group> --workspace <workspace> --table <table> --query <query> --hours <hours> --limit <limit>
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
| `--workspace` | string | The Log Analytics workspace ID or name. This can be either the unique identifier (GUID) or the display name of your workspace. |
| `--table` | string | The name of the table to query. This is the specific table within the workspace. |
| `--query` | string | The KQL query to execute against the Log Analytics workspace. You can use predefined queries by name:
- 'recent': Shows most recent logs ordered by TimeGenerated
- 'errors': Shows error-level logs ordered by TimeGenerated
Otherwise, provide a custom KQL query. |
| `--hours` | string | The number of hours to query back from now. |
| `--limit` | string | The maximum number of results to return. |

---
