### [MCP Server](#tab/mcp-server)

This tool executes `loadtesting testrun createorupdate` via MCP Server.

See parameters below.

### [CLI](#tab/cli)

Create or update a load test run execution.
Creates a new test run for a specified test in the load testing resource, or updates metadata and display properties of an existing test run.
When creating: Triggers a new test run execution based on the existing test configuration. Use testrun ID to specify the new run identifier. Create operations are NOT idempotent - each call starts a new test run with unique timestamps and execution state.
When updating: Modifies descriptive information (display name, description) of a completed or in-progress test run for better organization and documentation. Update operations are idempotent - repeated calls with same values produce the same result.
This does not modify the test plan configuration or create a new test/resource - only manages test run executions.

### Example CLI commands

Basic usage:

```azurecli
azmcp loadtesting testrun createorupdate
```

With parameters:

```azurecli
azmcp loadtesting testrun createorupdate --test-resource-name <test-resource-name> --resource-group <resource-group> --testrun-id <testrun-id> --test-id <test-id> --display-name <display-name> --description <description> --old-testrun-id <old-testrun-id>
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
| `--test-resource-name` | string | The name of the load test resource for which you want to fetch the details. |
| `--resource-group` | string | The name of the Azure resource group. This is a logical container for Azure resources. |
| `--testrun-id` | string | The ID of the load test run for which you want to fetch the details. |
| `--test-id` | string | The ID of the load test for which you want to fetch the details. |
| `--display-name` | string | The display name for the load test run. This is a user-friendly name to identify the test run. |
| `--description` | string | The description for the load test run. This provides additional context about the test run. |
| `--old-testrun-id` | string | The ID of an existing test run to update. If provided, the command will trigger a rerun of the given test run id. |

---
