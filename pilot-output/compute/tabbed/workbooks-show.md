### [MCP Server](#tab/mcp-server)

This tool executes `workbooks show` via MCP Server.

See parameters below.

### [CLI](#tab/cli)

Retrieve full workbook details via ARM API (includes serializedData content).

USE FOR: Getting complete workbook definition including visualization JSON.
RETURNS: Full workbook properties, serializedData, tags, etag.

BATCH: Accepts multiple --workbook-ids values. Partial failures reported per-workbook.
PERFORMANCE: Use 'list' first for discovery, then 'show' for specific workbooks.

### Example CLI commands

Basic usage:

```azurecli
azmcp workbooks show
```

With parameters:

```azurecli
azmcp workbooks show --workbook-ids <workbook-ids>
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
| `--workbook-ids` | string | The Azure Resource IDs of the workbooks to operate on (supports multiple values for batch operations). |

---
