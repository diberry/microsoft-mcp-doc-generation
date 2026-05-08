### [MCP Server](#tab/mcp-server)

This tool executes `appconfig kv set` via MCP Server.

See parameters below.

### [CLI](#tab/cli)

Set a key-value setting in an App Configuration store. This command creates or updates a key-value setting
with the specified value. You must specify an account name, key, and value. Optionally, you can specify a
label otherwise the default label will be used. You can also specify a content type to indicate how the value
should be interpreted. You can add tags in the format 'key=value' to associate metadata with the setting.

### Example CLI commands

Basic usage:

```azurecli
azmcp appconfig kv set
```

With parameters:

```azurecli
azmcp appconfig kv set --account <account> --key <key> --label <label> --content-type <content-type> --value <value> --tags <tags>
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
| `--account` | string | The name of the App Configuration store (e.g., my-appconfig). |
| `--key` | string | The name of the key to access within the App Configuration store. |
| `--label` | string | The label to apply to the configuration key. Labels are used to group and organize settings. |
| `--content-type` | string | The content type of the configuration value. This is used to indicate how the value should be interpreted or parsed. |
| `--value` | string | The value to set for the configuration key. |
| `--tags` | string | The tags to associate with the configuration key. Tags should be in the format 'key=value'. Multiple tags can be specified. |

---
