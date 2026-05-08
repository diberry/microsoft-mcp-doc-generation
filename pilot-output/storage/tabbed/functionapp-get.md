### [MCP Server](#tab/mcp-server)

This tool executes `functionapp get` via MCP Server.

See parameters below.

### [CLI](#tab/cli)

Gets Azure Function App details. Lists all Function Apps in the subscription or resource group.  If function app name and resource group
is specified, retrieves the details of that specific function app.  Returns the details of Azure Function Apps, including its name,
location, status, and app service plan name.

### Example CLI commands

Basic usage:

```azurecli
azmcp functionapp get
```

With parameters:

```azurecli
azmcp functionapp get --resource-group <resource-group> --function-app <function-app>
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
| `--function-app` | string | The name of the Function App. |

---
