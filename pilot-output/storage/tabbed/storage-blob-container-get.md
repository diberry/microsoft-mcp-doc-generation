### [MCP Server](#tab/mcp-server)

This tool executes `storage blob container get` via MCP Server.

See parameters below.

### [CLI](#tab/cli)

Show/list containers in a storage account. Use this tool to list all blob containers in the storage account or
show details for a specific Storage container. If no container specified, shows all containers in the storage
account, optionally filtering on a prefix. The prefix is ignored if a container is specified.

Required: --account, --subscription
Optional: --container, --tenant, --prefix

Returns: container name, lastModified, leaseStatus, publicAccess, metadata, and container properties.
Do not use this tool to list blobs in a container.

### Example CLI commands

Basic usage:

```azurecli
azmcp storage blob container get
```

With parameters:

```azurecli
azmcp storage blob container get --account <account> --container <container> --prefix <prefix>
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
| `--account` | string | - | The name of the Azure Storage account. This is the unique name you chose for your storage account (e.g., 'mystorageaccount'). |
| `--container` | string | - | The name of the container to access within the storage account. |
| `--prefix` | string | - | The prefix to filter containers when listing containers in a storage account. Only containers whose names start with the specified prefix will be listed. |

---
