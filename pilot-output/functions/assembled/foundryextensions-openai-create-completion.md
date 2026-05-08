Create text completions using Azure OpenAI in Microsoft Foundry. Send a prompt or question to Azure OpenAI models
deployed in your Microsoft Foundry resource and receive generated text answers. Use this when you need to create
completions, get AI-generated content, generate answers to questions, or produce text completions from Azure
OpenAI based on any input prompt. Supports customization with temperature and max tokens.
Requires resource-name, deployment-name, and prompt-text.

### Example CLI commands

Basic usage:

```azurecli
azmcp foundryextensions openai create-completion
```

With parameters:

```azurecli
azmcp foundryextensions openai create-completion --resource-group <resource-group> --resource-name <resource-name> --deployment <deployment> --prompt-text <prompt-text> --max-tokens <max-tokens> --temperature <temperature>
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
| `--deployment` | string | The name of the deployment. |
| `--prompt-text` | string | The prompt text to send to the completion model. |
| `--max-tokens` | string | The maximum number of tokens to generate in the completion. |
| `--temperature` | string | Controls randomness in the output. Lower values make it more deterministic. |

