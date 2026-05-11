### [MCP Server](#tab/mcp-server)

This tool executes `redis create` via MCP Server.

See parameters below.

### [CLI](#tab/cli)

Create a new Azure Managed Redis resource in Azure. Use this command to provision a new Redis resource in your subscription.

### Example CLI commands

Basic usage:

```azurecli
azmcp redis create
```

With parameters:

```azurecli
azmcp redis create --resource <resource> --resource-group <resource-group> --sku <sku> --location <location> --access-keys-authentication <access-keys-authentication> --public-network-access <public-network-access> --modules <modules>
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
| `--resource` | string | The name of the Redis resource (e.g., my-redis). |
| `--resource-group` | string | The name of the Azure resource group. This is a logical container for Azure resources. |
| `--sku` | string | The SKU for the Redis resource. (Default: Balanced_B0) |
| `--location` | string | The location for the Redis resource (e.g. eastus). |
| `--access-keys-authentication` | string | Whether to enable access keys for authentication for the Redis resource. (Default: false) |
| `--public-network-access` | string | Whether to enable public network access for the Redis resource. (Default: false) |
| `--modules` | string | A list of modules to enable on the Azure Managed Redis resource (e.g., RedisBloom, RedisJSON). |

---
