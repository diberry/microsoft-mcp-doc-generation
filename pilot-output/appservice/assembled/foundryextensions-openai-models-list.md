List all available Azure OpenAI models and deployments in a Microsoft Foundry resource. This tool retrieves information
about Azure OpenAI models deployed in your Microsoft Foundry resource including model names, versions, capabilities,
and deployment status. Use this when you need to see what OpenAI models are available, check model deployments,
or list Azure OpenAI models in your foundry resource. Returns model information as JSON array. Requires resource-name.

### Example CLI commands

Basic usage:

```azurecli
azmcp foundryextensions openai models-list
```

With parameters:

```azurecli
azmcp foundryextensions openai models-list --resource-group <resource-group> --resource-name <resource-name>
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
| `--resource-group` | string | The name of the Azure resource group. This is a logical container for Azure resources. |
| `--resource-name` | string | The name of the Azure OpenAI resource. |

