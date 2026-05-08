### [MCP Server](#tab/mcp-server)

This tool executes `managedlustre fs sku get` via MCP Server.

See parameters below.

### [CLI](#tab/cli)

Retrieves the available Azure Managed Lustre SKU, including increments, bandwidth, scale targets and zonal support. If a location is specified, the results will be filtered to that location.

### Example CLI commands

Basic usage:

```azurecli
azmcp managedlustre fs sku get
```

With parameters:

```azurecli
azmcp managedlustre fs sku get --location <location>
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
| `--location` | string | - | Azure region/region short name (use Azure location token, lowercase). Examples: uaenorth, swedencentral, eastus. |

---
