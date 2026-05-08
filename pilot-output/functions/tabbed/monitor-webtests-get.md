### [MCP Server](#tab/mcp-server)

This tool executes `monitor webtests get` via MCP Server.

See parameters below.

### [CLI](#tab/cli)

Gets details for a specific web test or lists all web tests.
When --webtest-resource is provided, returns detailed information about a single web test.
When --webtest-resource is omitted, returns a list of all web tests in the subscription (optionally filtered by resource group).

### Example CLI commands

Basic usage:

```azurecli
azmcp monitor webtests get
```

With parameters:

```azurecli
azmcp monitor webtests get --webtest-resource <webtest-resource> --resource-group <resource-group>
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
| `--webtest-resource` | string | - | The name of the Web Test resource to operate on. |
| `--resource-group` | string | - | The name of the Azure resource group. This is a logical container for Azure resources. |

---
