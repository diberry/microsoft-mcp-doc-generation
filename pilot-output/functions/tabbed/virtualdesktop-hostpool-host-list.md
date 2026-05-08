### [MCP Server](#tab/mcp-server)

This tool executes `virtualdesktop hostpool host list` via MCP Server.

See parameters below.

### [CLI](#tab/cli)

List all SessionHosts in a hostpool. This command retrieves all Azure Virtual Desktop SessionHost objects available
in the specified --subscription and hostpool. Results include SessionHost details and are
returned as a JSON array.

### Example CLI commands

Basic usage:

```azurecli
azmcp virtualdesktop hostpool host list
```

With parameters:

```azurecli
azmcp virtualdesktop hostpool host list --resource-group <resource-group> --hostpool <hostpool> --hostpool-resource-id <hostpool-resource-id>
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
| `--hostpool` | string | - | The name of the Azure Virtual Desktop host pool. This is the unique name you chose for your hostpool. |
| `--hostpool-resource-id` | string | - | The Azure resource ID of the host pool. When provided, this will be used instead of searching by name. |

---
