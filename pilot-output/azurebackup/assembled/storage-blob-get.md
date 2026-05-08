List/get/show blobs in a blob container in Storage account. Use this tool to list the blobs in a container or
get details for a specific blob. If no blob specified, lists all blobs present in the container, optionally
filtering on a prefix. The prefix is ignored if a blob is specified.

Required: --account, --container, --subscription
Optional: --blob, --tenant, --prefix

Returns: blob name, size, lastModified, contentType, contentHash, metadata, and blob properties.
Do not use this tool to list containers in the storage account.

### Example CLI commands

Basic usage:

```azurecli
azmcp storage blob get
```

With parameters:

```azurecli
azmcp storage blob get --account <account> --container <container> --blob <blob> --prefix <prefix>
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
| `--blob` | string | The name of the blob to access within the container. This should be the full path within the container (e.g., 'file.txt' or 'folder/file.txt'). |
| `--prefix` | string | The prefix to filter blobs when listing blobs in a container. Only blobs whose names start with the specified prefix will be listed. |

