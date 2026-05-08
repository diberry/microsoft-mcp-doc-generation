Retrieves the detailed schema configuration of a specific knowledge index from Microsoft Foundry.

This function provides comprehensive information about the structure and configuration of a knowledge index, including field definitions, data types, searchable attributes, and other schema properties. The schema information is essential for understanding how the index is structured and how data is indexed and searchable.

Usage:
    Use this function when you need to examine the detailed configuration of a specific knowledge index. This is helpful for troubleshooting search issues, understanding index capabilities, planning data mapping, or when integrating with the index programmatically.

Notes:
    - Returns the index schema.

### Example CLI commands

Basic usage:

```azurecli
azmcp foundryextensions knowledge index schema
```

With parameters:

```azurecli
azmcp foundryextensions knowledge index schema --endpoint <endpoint> --index <index>
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
| `--endpoint` | string | - | The endpoint URL for the Microsoft Foundry project/service. The endpoint follows this pattern https://<foundry-resource-name>.services.ai.azure.com/api/projects/<project-name>. |
| `--index` | string | - | The name of the knowledge index. |

