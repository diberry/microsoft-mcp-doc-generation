List/get/show Azure AI Search indexes in a Search service. Returns index properties such as fields,
description, and more. If a specific index name is not provided, the command will return details for all
indexes.

### Example CLI commands

Basic usage:

```azurecli
azmcp search index get
```

With parameters:

```azurecli
azmcp search index get --service <service> --index <index>
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
| `--service` | string | The name of the Azure AI Search service (e.g., my-search-service). |
| `--index` | string | The name of the search index within the Azure AI Search service. |

