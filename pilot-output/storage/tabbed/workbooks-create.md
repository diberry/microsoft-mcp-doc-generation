### [MCP Server](#tab/mcp-server)

This tool executes `workbooks create` via MCP Server.

See parameters below.

### [CLI](#tab/cli)

Create a new workbook in the specified resource group and subscription.
You can set the display name and serialized data JSON content for the workbook.
Returns the created workbook information upon successful completion.

### Example CLI commands

Basic usage:

```azurecli
azmcp workbooks create
```

With parameters:

```azurecli
azmcp workbooks create --resource-group <resource-group> --display-name <display-name> --serialized-content <serialized-content> --source-id <source-id>
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
| `--display-name` | string | The display name of the workbook. |
| `--serialized-content` | string | The serialized JSON content of the workbook. |
| `--source-id` | string | The linked resource ID for the workbook. By default, this is 'azure monitor'. |

---
