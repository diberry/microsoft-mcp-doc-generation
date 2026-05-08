### [MCP Server](#tab/mcp-server)

This tool executes `storage blob container create` via MCP Server.

See parameters below.

### [CLI](#tab/cli)

Create/provision a new Azure Storage blob container in a storage account.

Required: --account, --container, --subscription
Optional: --tenant

Returns: container name, lastModified, eTag, leaseStatus, publicAccessLevel, hasImmutabilityPolicy, hasLegalHold.
Creates a logical container for organizing blobs within a storage account.

### Example CLI commands

Basic usage:

```azurecli
azmcp storage blob container create
```

With parameters:

```azurecli
azmcp storage blob container create --account <account> --container <container>
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
| `--account` | string | The name of the Azure Storage account. This is the unique name you chose for your storage account (e.g., 'mystorageaccount'). |
| `--container` | string | The name of the container to access within the storage account. |

---
