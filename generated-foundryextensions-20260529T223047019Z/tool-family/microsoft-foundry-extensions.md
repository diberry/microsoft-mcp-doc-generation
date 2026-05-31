---

title: Azure MCP Server tools for Microsoft Foundry Extensions
description: Use Azure MCP Server tools to manage Microsoft Foundry Extensions resources such as chat completions, embeddings, models, and knowledge indexes with natural language prompts from your IDE.
ms.date: 05/29/2026
ms.service: azure-mcp-server
ms.topic: concept-article
tool_count: 7
mcp-cli.version: 3.0.0-beta.13+cd8d1e8f9924440b33e3e908c390c1599700ccba
author: diberry
ms.author: diberry
ms.reviewer: mbaldwin
ai-usage: ai-generated
ms.custom: build-2025
content_well_notification:
  - AI-contribution
---

# Azure MCP Server tools for Microsoft Foundry Extensions

The Azure Model Context Protocol (MCP) Server lets you manage Microsoft Foundry Extensions resources, including: chat-completions-create, create-completion, embeddings-create, get, list, models-list, and schema, with natural language prompts.

Microsoft Foundry Extensions is an Azure service that provides cloud-based capabilities for your applications. For more information, see [Microsoft Foundry Extensions documentation](/azure/foundryextensions/).

[!INCLUDE [tip-about-params](../includes/tools/parameter-consideration.md)]


## Knowledge index: List

Retrieves a list of knowledge indexes in Microsoft Foundry.

#### [MCP Server](#tab/mcp-server)

<!-- @mcpcli foundryextensions knowledge index list -->

Example prompts include:

- "List all knowledge indexes in my Microsoft Foundry project with endpoint 'https://contoso.services.ai.azure.com/api/projects/my-project'."
- "Show me the knowledge indexes in my Microsoft Foundry project using endpoint 'https://foundry-prod.services.ai.azure.com/api/projects/support-knowledge'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Endpoint** |  Required | The endpoint URL for the Microsoft Foundry project/service. The endpoint follows this pattern https://&lt;foundry-resource-name&gt;.services.AI.azure.com/api/projects/&lt;project-name&gt;. |

#### [Azure MCP CLI](#tab/azure-mcp-cli)

**Example CLI command**

```console
azmcp foundryextensions knowledge index list \
  --endpoint <endpoint>
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--endpoint` | string | Yes | The endpoint URL for the Microsoft Foundry project/service. The endpoint follows this pattern https://<foundry-resource-name>.services.ai.azure.com/api/projects/<project-name>. |

---

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌

## Knowledge index: Schema

Retrieves the detailed schema configuration of a specific knowledge index in Microsoft Foundry.

#### [MCP Server](#tab/mcp-server)

<!-- @mcpcli foundryextensions knowledge index schema -->

Example prompts include:

- "Show me the schema for knowledge index 'support-kb-index' at endpoint 'https://contoso-foundry.services.AI.azure.com/api/projects/project-alpha'."
- "Get the schema configuration for knowledge index 'products-index' from endpoint 'https://acme-foundry.services.AI.azure.com/api/projects/retail-catalog'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Endpoint** |  Required | The endpoint URL for the Microsoft Foundry project/service. The endpoint follows this pattern https://&lt;foundry-resource-name&gt;.services.AI.azure.com/api/projects/&lt;project-name&gt;. |
| **Index name** |  Required | The name of the knowledge index. |

#### [Azure MCP CLI](#tab/azure-mcp-cli)

**Example CLI command**

```console
azmcp foundryextensions knowledge index schema \
  --endpoint <endpoint> \
  --index <index>
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--endpoint` | string | Yes | The endpoint URL for the Microsoft Foundry project/service. The endpoint follows this pattern https://<foundry-resource-name>.services.ai.azure.com/api/projects/<project-name>. |
| `--index` | string | Yes | The name of the knowledge index. |

---

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌

## Openai: Chat completions create

Create chat completions with Azure OpenAI in Microsoft Foundry. Send messages to an Azure OpenAI chat model deployed in your Microsoft Foundry resource, and receive AI-generated conversational responses. The tool supports multi-turn conversations, message history, system instructions, and response customization. You can use those features to build interactive dialogues, generate assistant replies, and prototype conversational experiences.

#### [MCP Server](#tab/mcp-server)

<!-- @mcpcli foundryextensions openai chat-completions-create -->

Example prompts include:

- "Create a chat completion with message array '[{"role":"user","content":"Hello, how are you today?"}]' for deployment 'chat-deploy' in resource group 'rg-foundry' and resource 'foundry-openai'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Deployment name** |  Required | The name of the deployment. |
| **Message array** |  Required | Array of messages in the conversation (JSON format). Each message should have 'role' and 'content' properties. |
| **Resource group** |  Required | The name of the Azure resource group. This resource group is a logical container for Azure resources. |
| **Resource name** |  Required | The name of the Azure OpenAI resource. |
| **Frequency penalty** |  Optional | Penalizes new tokens based on their frequency (-2.0 to 2.0). Default is 0. |
| **Max tokens** |  Optional | The maximum number of tokens to generate in the completion. |
| **Presence penalty** |  Optional | Penalizes new tokens based on presence (-2.0 to 2.0). Default is 0. |
| **Seed** |  Optional | If specified, the system will make a best effort to sample deterministically. |
| **Stop** |  Optional | Up to 4 sequences where the API will stop generating further tokens. |
| **Stream** |  Optional | Whether to stream back partial progress. Default is `false`. |
| **Temperature** |  Optional | Controls randomness in the output. Lower values make it more deterministic. |
| **Top p** |  Optional | Controls diversity through nucleus sampling (0.0 to 1.0). Default is 1.0. |
| **User name** |  Optional | Optional user identifier for tracking and abuse monitoring. |

#### [Azure MCP CLI](#tab/azure-mcp-cli)

**Example CLI command**

```console
azmcp foundryextensions openai chat-completions-create \
  --resource-group <resource-group> \
  --resource-name <resource-name> \
  --deployment <deployment> \
  --message-array <message-array> \
  [--max-tokens <max-tokens>] \
  [--temperature <temperature>] \
  [--top-p <top-p>] \
  [--frequency-penalty <frequency-penalty>] \
  [--presence-penalty <presence-penalty>] \
  [--stop <stop>] \
  [--stream <stream>] \
  [--seed <seed>] \
  [--user <user>]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--resource-group` | string | Yes | The name of the Azure resource group. This is a logical container for Azure resources. |
| `--resource-name` | string | Yes | The name of the Azure OpenAI resource. |
| `--deployment` | string | Yes | The name of the deployment. |
| `--message-array` | string | Yes | Array of messages in the conversation (JSON format). Each message should have 'role' and 'content' properties. |
| `--max-tokens` | string | No | The maximum number of tokens to generate in the completion. |
| `--temperature` | string | No | Controls randomness in the output. Lower values make it more deterministic. |
| `--top-p` | string | No | Controls diversity via nucleus sampling (0.0 to 1.0). Default is 1.0. |
| `--frequency-penalty` | string | No | Penalizes new tokens based on their frequency (-2.0 to 2.0). Default is 0. |
| `--presence-penalty` | string | No | Penalizes new tokens based on presence (-2.0 to 2.0). Default is 0. |
| `--stop` | string | No | Up to 4 sequences where the API will stop generating further tokens. |
| `--stream` | string | No | Whether to stream back partial progress. Default is false. |
| `--seed` | string | No | If specified, the system will make a best effort to sample deterministically. |
| `--user` | string | No | Optional user identifier for tracking and abuse monitoring. |

---

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ❌ | Idempotent: ❌ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌

## Openai: Create completion

Create text completions with Azure OpenAI in Microsoft Foundry. Send a prompt or question to an Azure OpenAI model deployed in your Microsoft Foundry resource, and receive generated text answers. You can use completions to draft content, answer questions, or produce summaries. Adjust model settings to control output randomness and length. Requires `Resource name`, `Deployment name`, and `Prompt text`.

#### [MCP Server](#tab/mcp-server)

<!-- @mcpcli foundryextensions openai create-completion -->

Example prompts include:

- "Create a completion with deployment 'gpt-35' and prompt 'What is Azure?' in resource group 'rg-foundry-prod' using resource name 'foundry-openai-prod'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Deployment name** |  Required | The name of the deployment. |
| **Prompt text** |  Required | The prompt text to send to the completion model. |
| **Resource group** |  Required | The name of the Azure resource group. This resource group is a logical container for Azure resources. |
| **Resource name** |  Required | The name of the Azure OpenAI resource. |
| **Max tokens** |  Optional | The maximum number of tokens to generate in the completion. |
| **Temperature** |  Optional | Controls randomness in the output. Lower values make it more deterministic. |

#### [Azure MCP CLI](#tab/azure-mcp-cli)

**Example CLI command**

```console
azmcp foundryextensions openai create-completion \
  --resource-group <resource-group> \
  --resource-name <resource-name> \
  --deployment <deployment> \
  --prompt-text <prompt-text> \
  [--max-tokens <max-tokens>] \
  [--temperature <temperature>]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--resource-group` | string | Yes | The name of the Azure resource group. This is a logical container for Azure resources. |
| `--resource-name` | string | Yes | The name of the Azure OpenAI resource. |
| `--deployment` | string | Yes | The name of the deployment. |
| `--prompt-text` | string | Yes | The prompt text to send to the completion model. |
| `--max-tokens` | string | No | The maximum number of tokens to generate in the completion. |
| `--temperature` | string | No | Controls randomness in the output. Lower values make it more deterministic. |

---

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ❌ | Idempotent: ❌ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌

## Openai: Embeddings create

Generate vector embeddings from text with the Azure OpenAI Service in Microsoft Foundry. You can use embeddings for semantic search, similarity comparisons, clustering, or machine learning. Specify the `deployment-name`, `resource-name`, and `resource-group`, and provide the `input-text` to produce numerical vector representations of the text. Requires `resource-name`, `deployment-name`, and `input-text`.

#### [MCP Server](#tab/mcp-server)

<!-- @mcpcli foundryextensions openai embeddings-create -->

Example prompts include:

- "Generate embeddings for input text 'Azure OpenAI Service' using my Microsoft Foundry resource deployment 'text-embed-001' resource group 'rg-foundry' resource name 'foundry-openai'."
- "Create vector embeddings for input text 'How do I migrate VMs to Azure?' using my Microsoft Foundry resource deployment 'embed-deploy-prod' resource group 'rg-prod' resource name 'prod-openai'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Deployment name** |  Required | The name of the deployment. |
| **Input text** |  Required | The input text to generate embeddings for. |
| **Resource group** |  Required | The name of the Azure resource group. This resource group is a logical container for Azure resources. |
| **Resource name** |  Required | The name of the Azure OpenAI resource. |
| **Dimensions** |  Optional | The number of dimensions for the embedding output. Only supported in some models. |
| **Encoding format** |  Optional | The format to return embeddings in (float or base64). |
| **User name** |  Optional | Optional user identifier for tracking and abuse monitoring. |

#### [Azure MCP CLI](#tab/azure-mcp-cli)

**Example CLI command**

```console
azmcp foundryextensions openai embeddings-create \
  --resource-group <resource-group> \
  --resource-name <resource-name> \
  --deployment <deployment> \
  --input-text <input-text> \
  [--user <user>] \
  [--encoding-format <encoding-format>] \
  [--dimensions <dimensions>]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--resource-group` | string | Yes | The name of the Azure resource group. This is a logical container for Azure resources. |
| `--resource-name` | string | Yes | The name of the Azure OpenAI resource. |
| `--deployment` | string | Yes | The name of the deployment. |
| `--input-text` | string | Yes | The input text to generate embeddings for. |
| `--user` | string | No | Optional user identifier for tracking and abuse monitoring. |
| `--encoding-format` | string | No | The format to return embeddings in (float or base64). |
| `--dimensions` | string | No | The number of dimensions for the embedding output. Only supported in some models. |

---

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ❌ | Idempotent: ❌ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌

## Openai: Models list

List Azure OpenAI model deployments in a Microsoft Foundry resource. The command returns deployment names, model names, model versions, capabilities, and deployment status. Use it to show deployments, verify which OpenAI models are deployed, or find models available for inference in a specific Foundry resource. Requires `resource-name` and `resource-group`. For Foundry resource-level details, such as endpoint URL, location, or SKU, use the `resource get` command instead.

#### [MCP Server](#tab/mcp-server)

<!-- @mcpcli foundryextensions openai models-list -->

Example prompts include:

- "List all available OpenAI models in my Microsoft Foundry resource, resource group 'rg-prod', resource name 'foundry-prod'."
- "Show me the OpenAI model deployments in my Microsoft Foundry resource, resource group 'foundry-rg', resource name 'foundry-staging'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Resource group** |  Required | The name of the Azure resource group. This resource group is a logical container for Azure resources. |
| **Resource name** |  Required | The name of the Azure OpenAI resource. |

#### [Azure MCP CLI](#tab/azure-mcp-cli)

**Example CLI command**

```console
azmcp foundryextensions openai models-list \
  --resource-group <resource-group> \
  --resource-name <resource-name>
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--resource-group` | string | Yes | The name of the Azure resource group. This is a logical container for Azure resources. |
| `--resource-name` | string | Yes | The name of the Azure OpenAI resource. |

---

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌

## Resource: Get

Gets detailed information about Microsoft Foundry (Cognitive Services) resources, including endpoint URL, location, SKU, and provisioning state. If you provide a specific resource name, the tool returns details for that resource only. If you don't provide a resource name, the tool lists all Microsoft Foundry resources in the subscription or resource group. The tool helps you get endpoint information, discover available AI resources, and check Foundry account configuration. To list OpenAI model deployments within a resource, run the `openai models-list` command instead.

#### [MCP Server](#tab/mcp-server)

<!-- @mcpcli foundryextensions resource get -->

Example prompts include:

- "List all Microsoft Foundry resources in my subscription."
- "Show me the Microsoft Foundry resources in resource group 'rg-foundry-prod'."
- "Get details for Microsoft Foundry resource 'foundry-account-01' in resource group 'rg-foundry-prod'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Resource name** |  Optional | The name of the Azure OpenAI resource. |

#### [Azure MCP CLI](#tab/azure-mcp-cli)

**Example CLI command**

```console
azmcp foundryextensions resource get \
  [--resource-group <resource-group>] \
  [--resource-name <resource-name>]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--resource-group` | string | No | The name of the Azure resource group. This is a logical container for Azure resources. |
| `--resource-name` | string | No | The name of the Azure OpenAI resource. |

---

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌

## Related content

- [What are the Azure MCP Server tools?](index.md)
- [Get started using Azure MCP Server](../get-started.md)
- [Microsoft Foundry documentation](/azure/ai-foundry/)
