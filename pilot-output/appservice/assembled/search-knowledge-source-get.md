Gets the details of Azure AI Search knowledge sources. A knowledge source may point directly at an
existing Azure AI Search index, or may represent external data (e.g. a blob storage container) that has been
indexed in Azure AI Search internally. These knowledge sources are used by knowledge bases during retrieval.
If a specific knowledge source name is not provided, the command will return details for all knowledge sources
within the specified service.

Required arguments:
- service

### Example CLI commands

Basic usage:

```azurecli
azmcp search knowledge source get
```

With parameters:

```azurecli
azmcp search knowledge source get --service <service> --knowledge-source <knowledge-source>
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
| `--service` | string | - | The name of the Azure AI Search service (e.g., my-search-service). |
| `--knowledge-source` | string | - | The name of the knowledge source within the Azure AI Search service. |

