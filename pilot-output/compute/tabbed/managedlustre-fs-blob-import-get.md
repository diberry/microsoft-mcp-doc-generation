### [MCP Server](#tab/mcp-server)

This tool executes `managedlustre fs blob import get` via MCP Server.

See parameters below.

### [CLI](#tab/cli)

Gets import job details or lists all import jobs for an Azure Managed Lustre filesystem. If job-name is provided, returns details for that specific job. If job-name is omitted, returns a list of all import jobs for the filesystem.
Required options:
- filesystem-name: The name of the AMLFS filesystem
Optional options:
- job-name: Name of specific import job to get (omit to list all jobs)

### Example CLI commands

Basic usage:

```azurecli
azmcp managedlustre fs blob import get
```

With parameters:

```azurecli
azmcp managedlustre fs blob import get --resource-group <resource-group> --filesystem-name <filesystem-name> --job-name <job-name>
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
| `--filesystem-name` | string | - | The name of the Azure Managed Lustre filesystem |
| `--job-name` | string | - | The name of the autoimport job. If not specified, a timestamped name will be generated. |

---
