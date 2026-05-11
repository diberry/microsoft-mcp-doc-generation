### [MCP Server](#tab/mcp-server)

This tool executes `search index query` via MCP Server.

See parameters below.

### [CLI](#tab/cli)

Queries/searches documents in an Azure AI Search index with a given query, returning the results of the
query/search.

### Example CLI commands

Basic usage:

```azurecli
azmcp search index query
```

With parameters:

```azurecli
azmcp search index query --service <service> --index <index> --query <query>
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
| `--query` | string | The search query to execute against the Azure AI Search index. |

---
