### [MCP Server](#tab/mcp-server)

This tool executes `loadtesting testrun get` via MCP Server.

See parameters below.

### [CLI](#tab/cli)

Get load test run details by testrun ID, or list all test runs by test ID.
Returns execution details including status, start/end times, progress, metrics, and artifacts.
Does not return test configuration or resource details.

### Example CLI commands

Basic usage:

```azurecli
azmcp loadtesting testrun get
```

With parameters:

```azurecli
azmcp loadtesting testrun get --test-resource-name <test-resource-name> --resource-group <resource-group> --testrun-id <testrun-id> --test-id <test-id>
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
| `--test-resource-name` | string | - | The name of the load test resource for which you want to fetch the details. |
| `--resource-group` | string | - | The name of the Azure resource group. This is a logical container for Azure resources. |
| `--testrun-id` | string | - | The ID of the load test run for which you want to fetch the details. |
| `--test-id` | string | - | The ID of the load test for which you want to fetch the details. |

---
