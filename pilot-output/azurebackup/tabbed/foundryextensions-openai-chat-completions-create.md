### [MCP Server](#tab/mcp-server)

This tool executes `foundryextensions openai chat-completions-create` via MCP Server.

See parameters below.

### [CLI](#tab/cli)

Create chat completions using Azure OpenAI in Microsoft Foundry. Send messages to Azure OpenAI chat models deployed
in your Microsoft Foundry resource and receive AI-generated conversational responses. Supports multi-turn conversations
with message history, system instructions, and response customization. Use this when you need to create chat
completions, have AI conversations, get conversational responses, or build interactive dialogues with Azure OpenAI.
Requires resource-name, deployment-name, and message-array.

### Example CLI commands

Basic usage:

```azurecli
azmcp foundryextensions openai chat-completions-create
```

With parameters:

```azurecli
azmcp foundryextensions openai chat-completions-create --resource-group <resource-group> --resource-name <resource-name> --deployment <deployment> --message-array <message-array> --max-tokens <max-tokens> --temperature <temperature> --top-p <top-p> --frequency-penalty <frequency-penalty> --presence-penalty <presence-penalty> --stop <stop> --stream <stream> --seed <seed> --user <user>
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
| `--message-array` | string | - | Array of messages in the conversation (JSON format). Each message should have 'role' and 'content' properties. |
| `--max-tokens` | string | - | The maximum number of tokens to generate in the completion. |
| `--temperature` | string | - | Controls randomness in the output. Lower values make it more deterministic. |
| `--top-p` | string | - | Controls diversity via nucleus sampling (0.0 to 1.0). Default is 1.0. |
| `--frequency-penalty` | string | - | Penalizes new tokens based on their frequency (-2.0 to 2.0). Default is 0. |
| `--presence-penalty` | string | - | Penalizes new tokens based on presence (-2.0 to 2.0). Default is 0. |
| `--stop` | string | - | Up to 4 sequences where the API will stop generating further tokens. |
| `--stream` | string | - | Whether to stream back partial progress. Default is false. |
| `--seed` | string | - | If specified, the system will make a best effort to sample deterministically. |
| `--user` | string | - | Optional user identifier for tracking and abuse monitoring. |

---
