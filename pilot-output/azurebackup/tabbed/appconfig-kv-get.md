### [MCP Server](#tab/mcp-server)

This tool executes `appconfig kv get` via MCP Server.

See parameters below.

### [CLI](#tab/cli)

Gets key-values in an App Configuration store. This command can either retrieve a specific key-value by its key
and optional label, or list key-values if no key is provided. Listing key-values can optionally be filtered by a
key filter and label filter. Each key-value includes its key, value, label, content type, ETag, last modified time,
and lock status.

### Example CLI commands

Basic usage:

```azurecli
azmcp appconfig kv get
```

With parameters:

```azurecli
azmcp appconfig kv get --account <account> --key <key> --label <label> --key-filter <key-filter> --label-filter <label-filter>
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
| `--account` | string | - | The name of the App Configuration store (e.g., my-appconfig). |
| `--key` | string | - | The name of the key to access within the App Configuration store. |
| `--label` | string | - | The label to apply to the configuration key. Labels are used to group and organize settings. |
| `--key-filter` | string | - | Specifies the key filter, if any, to be used when retrieving key-values. The filter can be an exact match, for example a filter of 'foo' would get all key-values with a key of 'foo', or the filter can include a '*' character at the end of the string for wildcard searches (e.g., 'App*'). If omitted all keys will be retrieved. |
| `--label-filter` | string | - | Specifies the label filter, if any, to be used when retrieving key-values. The filter can be an exact match, for example a filter of 'foo' would get all key-values with a label of 'foo', or the filter can include a '*' character at the end of the string for wildcard searches (e.g., 'Prod*'). This filter is case-sensitive. If omitted, all labels will be retrieved. |

---
