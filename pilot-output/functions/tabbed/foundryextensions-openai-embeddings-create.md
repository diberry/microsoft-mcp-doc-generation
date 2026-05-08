### [MCP Server](#tab/mcp-server)

This tool executes `foundryextensions openai embeddings-create` via MCP Server.

See parameters below.

### [CLI](#tab/cli)

Create embeddings using Azure OpenAI in Microsoft Foundry. Generate vector embeddings from text using Azure OpenAI
deployments in your Microsoft Foundry resource for semantic search, similarity comparisons, clustering, or machine
learning. Use this when you need to create foundry embeddings, generate vectors from text, or convert text to
numerical representations using Azure OpenAI. Requires resource-name, deployment-name, and input-text.

### Example CLI commands

Basic usage:

```azurecli
azmcp foundryextensions openai embeddings-create
```

With parameters:

```azurecli
azmcp foundryextensions openai embeddings-create --resource-group <resource-group> --resource-name <resource-name> --deployment <deployment> --input-text <input-text> --user <user> --encoding-format <encoding-format> --dimensions <dimensions>
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
| `--resource-name` | string | - | The name of the Azure OpenAI resource. |
| `--deployment` | string | - | The name of the deployment. |
| `--input-text` | string | - | The input text to generate embeddings for. |
| `--user` | string | - | Optional user identifier for tracking and abuse monitoring. |
| `--encoding-format` | string | - | The format to return embeddings in (float or base64). |
| `--dimensions` | string | - | The number of dimensions for the embedding output. Only supported in some models. |

---
