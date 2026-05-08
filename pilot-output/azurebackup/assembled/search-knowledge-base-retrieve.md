Execute a retrieval operation using a specific Azure AI Search knowledge base, effectively searching and querying the underlying
data sources as needed to find relevant information. Provide either a --query for single-turn retrieval or one or more
conversational --messages in role:content form (e.g. user:What policies apply?). Specifying both --query and --messages is not
allowed.

Required arguments:
- service
- knowledge-base

### Example CLI commands

Basic usage:

```azurecli
azmcp search knowledge base retrieve
```

With parameters:

```azurecli
azmcp search knowledge base retrieve --service <service> --knowledge-base <knowledge-base> --query <query> --messages <messages>
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
| `--knowledge-base` | string | - | The name of the knowledge base within the Azure AI Search service. |
| `--query` | string | - | Natural language query for retrieval when a conversational message history isn't provided. |
| `--messages` | string | - | Conversation history messages passed to the knowledge base. Able to specify multiple --messages entries. Each entry formatted as role:content, where role is `user` or `assistant` (e.g., user:How many docs?). |

