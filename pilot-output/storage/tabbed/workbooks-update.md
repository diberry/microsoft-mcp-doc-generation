### [MCP Server](#tab/mcp-server)

This tool executes `workbooks update` via MCP Server.

See parameters below.

### [CLI](#tab/cli)

Updates properties of an existing Azure Workbook by adding new steps, modifying content, or changing the display name. Returns the updated workbook details.  Requires the workbook resource ID and either new serialized content or a new display name.

### Example CLI commands

Basic usage:

```azurecli
azmcp workbooks update
```

With parameters:

```azurecli
azmcp workbooks update --workbook-id <workbook-id> --display-name <display-name> --serialized-content <serialized-content>
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
| `--workbook-id` | string | The Azure Resource ID of the workbook to retrieve. |
| `--display-name` | string | The display name of the workbook. |
| `--serialized-content` | string | The JSON serialized content/data of the workbook. |

---
