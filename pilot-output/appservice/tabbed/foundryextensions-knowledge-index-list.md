### [MCP Server](#tab/mcp-server)

This tool executes `foundryextensions knowledge index list` via MCP Server.

See parameters below.

### [CLI](#tab/cli)

Retrieves a list of knowledge indexes from Microsoft Foundry.

This function is used when a user requests information about the available knowledge indexes in Microsoft Foundry. It provides an overview of the knowledge bases and search indexes that are currently deployed and available for use with AI agents and applications.

Requires the project endpoint URL (format: https://<resource>.services.ai.azure.com/api/projects/<project-name>).

Usage:
    Use this function when a user wants to explore the available knowledge indexes in Microsoft Foundry. This can help users understand what knowledge bases are currently operational and how they can be utilized for retrieval-augmented generation (RAG) scenarios.

Notes:
    - The indexes listed are knowledge indexes specifically created within Microsoft Foundry projects.
    - These indexes can be used with AI agents for knowledge retrieval and RAG applications.
    - The list may change as new indexes are created or existing ones are updated.

### Example CLI commands

Basic usage:

```azurecli
azmcp foundryextensions knowledge index list
```

With parameters:

```azurecli
azmcp foundryextensions knowledge index list --endpoint <endpoint>
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
| `--endpoint` | string | The endpoint URL for the Microsoft Foundry project/service. The endpoint follows this pattern https://<foundry-resource-name>.services.ai.azure.com/api/projects/<project-name>. |

---
