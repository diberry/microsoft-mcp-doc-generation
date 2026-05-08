### [MCP Server](#tab/mcp-server)

This tool executes `appservice webapp settings update-appsettings` via MCP Server.

See parameters below.

### [CLI](#tab/cli)

Updates the application setting for an App Service web app. Three types of updating are available:

- Add: adds a new application setting with the specified name and value. If the application setting already exists, the operation will fail and return an error message.
- Set: sets the value of an application setting. If the application setting does not exist, this is equivalent to add. If the application setting already exists, the value will be overwritten.
- Delete: deletes an application setting with the specified name. If the application setting does not exist, nothing happens.

For add and set update types, both the application setting name and value are required. For delete update type, only the application setting name is required.

### Example CLI commands

Basic usage:

```azurecli
azmcp appservice webapp settings update-appsettings
```

With parameters:

```azurecli
azmcp appservice webapp settings update-appsettings --resource-group <resource-group> --app <app> --setting-name <setting-name> --setting-value <setting-value> --setting-update-type <setting-update-type>
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
| `--app` | string | - | The name of the Azure App Service (e.g., my-webapp). |
| `--setting-name` | string | - | The name of the application setting. |
| `--setting-value` | string | - | The value of the application setting. Required for add and set update types. |
| `--setting-update-type` | string | - | The type of update to perform on the application setting. Valid values are: add, set, delete. |

---
