### [MCP Server](#tab/mcp-server)

This tool executes `azurebackup governance find-unprotected` via MCP Server.

See parameters below.

### [CLI](#tab/cli)

Scans the subscription to find Azure resources that are not currently protected by any
backup policy. Optionally filter by resource type, resource group, or tags.

### Example CLI commands

Basic usage:

```azurecli
azmcp azurebackup governance find-unprotected
```

With parameters:

```azurecli
azmcp azurebackup governance find-unprotected --resource-type-filter <resource-type-filter> --resource-group <resource-group> --tag-filter <tag-filter>
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
| `--resource-type-filter` | string | Resource types to filter (comma-separated). |
| `--resource-group` | string | The name of the Azure resource group. This is a logical container for Azure resources. |
| `--tag-filter` | string | Tag-based filter in key=value format (e.g., 'environment=production'). |

---
