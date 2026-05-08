### [MCP Server](#tab/mcp-server)

This tool executes `quota region availability list` via MCP Server.

See parameters below.

### [CLI](#tab/cli)

Given a list of Azure resource types, this tool will return a list of regions where the resource types are available. Always get the user's subscription ID before calling this tool.

### Example CLI commands

Basic usage:

```azurecli
azmcp quota region availability list
```

With parameters:

```azurecli
azmcp quota region availability list --resource-types <resource-types> --cognitive-service-model-name <cognitive-service-model-name> --cognitive-service-model-version <cognitive-service-model-version> --cognitive-service-deployment-sku-name <cognitive-service-deployment-sku-name>
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
| `--resource-types` | string | - | Comma-separated list of Azure resource types to check available regions for. The valid Azure resource types. E.g. 'Microsoft.App/containerApps, Microsoft.Web/sites, Microsoft.CognitiveServices/accounts'. |
| `--cognitive-service-model-name` | string | - | Optional model name for cognitive services. Only needed when Microsoft.CognitiveServices is included in resource types. |
| `--cognitive-service-model-version` | string | - | Optional model version for cognitive services. Only needed when Microsoft.CognitiveServices is included in resource types. |
| `--cognitive-service-deployment-sku-name` | string | - | Optional deployment SKU name for cognitive services. Only needed when Microsoft.CognitiveServices is included in resource types. |

---
