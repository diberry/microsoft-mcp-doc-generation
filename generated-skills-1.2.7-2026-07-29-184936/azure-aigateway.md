---
title: Azure skill for Azure AI Gateway
description: This skill turns Azure API Management into an AI gateway that connects AI models, Model Context Protocol (MCP) tools, and agents. You use it to enforce governance, content safety, token limits, semantic caching, load balancing, rate limiting, and jailbreak detection. It's also for adding and testing backends like Azure OpenAI and AI Foundry, importing OpenAPI, and monitoring token metrics and costs.
ms.topic: reference
ms.date: 7/30/2026
author: diberry
ms.author: diberry
ms.service: azure-mcp-server
---

# Azure skill for Azure AI Gateway

This skill turns Azure API Management into an AI gateway that connects AI models, Model Context Protocol (MCP) tools, and agents. You use it to enforce governance, content safety, token limits, semantic caching, load balancing, rate limiting, and jailbreak detection. It's also for adding and testing backends like Azure OpenAI and AI Foundry, importing OpenAPI, and monitoring token metrics and costs. Use `azure-aigateway` when you need to turn Azure API Management into a central AI gateway to connect and test model backends. MCP/OpenAPI tools while enforcing governance and content safety (including jailbreak detection), managing token and rate limits, applying semantic caching and load balancing, and monitoring token usage and costs.

**Skill** `azure-aigateway` | [Source code](https://github.com/microsoft/azure-skills/blob/main/skills/azure-aigateway/SKILL.md)

## What it provides

You get a managed AI gateway in Azure API Management that enforces model governance and content safety while applying token limits, semantic caching, load balancing, rate limiting. Jailbreak detection across AI models, tools, and agents. You can add and test backends like Azure OpenAI and AI Foundry, import OpenAPI specs, configure model policies and backends. Track token usage and costs to control performance and spending.

## Prerequisites

- **Azure authentication**—Sign in with `az login` or use a service principal.
- **Azure subscription**—An active Azure subscription is required.
- **GitHub Copilot**
- **Azure command-line interface (CLI)** (v2.60.0+)—Install: `curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash`
- **Azure CLI (az) for configuration and testing**

## When to use this skill

Use the **Azure AI Gateway** skill when you need to:

- Manage and configure semantic caching, token limit, content safety, and load balancing in Azure
- Manage and configure AI model governance, MCP rate limiting, and jailbreak detection in Azure
- Add Azure OpenAI back end
- Add AI Foundry model
- Test AI gateway in Azure
- Manage and configure LLM policies in Azure
- Configure AI back end in Azure
- Manage and configure token metrics, AI cost control, convert API to MCP, and import OpenAPI to gateway in Azure

## Example prompts

Try these prompts to activate this skill:

- "How do I work with semantic caching?"
- "How do I work with token limit?"
- "How do I work with content safety?"
- "How do I work with load balancing?"
- "How do I work with AI model governance?"
- "How do I work with mCP rate limiting?"
- "How do I work with jailbreak detection?"
- "How do I add Azure OpenAI back end?"

## Related content

- [Azure API Management as an AI Gateway](/azure/api-management/api-management-gateway-overview)
- [API Management pricing](https://azure.microsoft.com/pricing/details/api-management/)
- [API Management policies](/azure/api-management/policies/)
- [API Management security](/azure/api-management/api-management-security-controls)
- [Azure MCP Server overview](/azure/developer/azure-mcp-server/overview)
- [Skill source code](https://github.com/microsoft/azure-skills/blob/main/skills/azure-aigateway/SKILL.md)