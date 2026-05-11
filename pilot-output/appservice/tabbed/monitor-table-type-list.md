### [MCP Server](#tab/mcp-server)

This tool executes `monitor table type list` via MCP Server.

See parameters below.

### [CLI](#tab/cli)

List available table types in a Log Analytics workspace. Returns table type names.

### Example CLI commands

Basic usage:

```azurecli
azmcp monitor table type list
```

With parameters:

```azurecli
azmcp monitor table type list --resource-group <resource-group> --workspace <workspace>
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

---
