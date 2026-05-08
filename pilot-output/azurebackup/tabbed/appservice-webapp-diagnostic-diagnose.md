### [MCP Server](#tab/mcp-server)

This tool executes `appservice webapp diagnostic diagnose` via MCP Server.

See parameters below.

### [CLI](#tab/cli)

Runs a specific diagnostic detector on an App Service Web App to troubleshoot issues with performance, availability,
configuration, or errors. Returns detailed analysis results including insights and recommendations. Use this to investigate
why a web app is slow, failing, restarting, or unhealthy. Requires a detector ID from 'azmcp appservice webapp diagnostic list'.
Supports optional time range filtering for historical analysis.

### Example CLI commands

Basic usage:

```azurecli
azmcp appservice webapp diagnostic diagnose
```

With parameters:

```azurecli
azmcp appservice webapp diagnostic diagnose --resource-group <resource-group> --app <app> --detector-id <detector-id> --start-time <start-time> --end-time <end-time> --interval <interval>
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
| `--app` | string | The name of the Azure App Service (e.g., my-webapp). |
| `--detector-id` | string | The ID of the diagnostic detector to run. Use the 'id' field from 'azmcp appservice webapp diagnostic list' output (e.g., LinuxContainerRecycle, LinuxMemoryDrillDown). |
| `--start-time` | string | The start time in ISO format (e.g., 2023-01-01T00:00:00Z). |
| `--end-time` | string | The end time in ISO format (e.g., 2023-01-01T00:00:00Z). |
| `--interval` | string | The time interval (e.g., PT1H for 1 hour, PT5M for 5 minutes). |

---
