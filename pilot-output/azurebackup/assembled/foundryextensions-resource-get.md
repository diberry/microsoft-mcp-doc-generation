Gets detailed information about Microsoft Foundry (Cognitive Services) resources, including endpoint URL,
location, SKU, and all deployed models with their configuration. If a specific resource name is provided,
returns details for that resource only. If no resource name is provided, lists all Microsoft Foundry resources
in the subscription or resource group. Use this tool when users need endpoint information, want to discover
available AI resources, or need to see all models deployed on AI resources.

### Example CLI commands

Basic usage:

```azurecli
azmcp foundryextensions resource get
```

With parameters:

```azurecli
azmcp foundryextensions resource get --resource-group <resource-group> --resource-name <resource-name>
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

