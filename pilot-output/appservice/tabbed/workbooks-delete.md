### [MCP Server](#tab/mcp-server)

This tool executes `workbooks delete` via MCP Server.

See parameters below.

### [CLI](#tab/cli)

Delete one or more workbooks by their Azure resource IDs.
This command soft deletes workbooks: they will be retained for 90 days.
If needed, you can restore them from the Recycle Bin through the Azure Portal.

BATCH: Accepts multiple --workbook-ids values. Partial failures are reported per-workbook.
Individual failures do not fail the entire batch operation.

To learn more, visit: https://learn.microsoft.com/azure/azure-monitor/visualize/workbooks-manage

### Example CLI commands

Basic usage:

```azurecli
azmcp workbooks delete
```

With parameters:

```azurecli
azmcp workbooks delete --workbook-ids <workbook-ids>
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
| `--workbook-ids` | string | - | The Azure Resource IDs of the workbooks to operate on (supports multiple values for batch operations). |

---
