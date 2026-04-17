---

title: Azure MCP Server tools for Azure AppLens
description: Use Azure MCP Server tools to manage diagnostics and troubleshooting for Azure App Service resources with natural language prompts from your IDE.
ms.date: 03/27/2026
ms.service: azure-mcp-server
ms.topic: concept-article
tool_count: 1
mcp-cli.version: 2.0.0-beta.33+8fab340d1e64d47701d891b7e81b5def64bbc9f6
author: diberry
ms.author: diberry
ai-usage: ai-generated
ms.custom: build-2025
content_well_notification:
  - AI-contribution
---

# Azure MCP Server tools for Azure AppLens

The Azure MCP Server lets you manage Azure AppLens diagnostics, including diagnose, with natural language prompts.

Azure AppLens is a diagnostics and troubleshooting feature for Azure App Service and related resources; for more information, see [Azure AppLens documentation](/azure/app-service/).

[!INCLUDE [tip-about-params](../includes/tools/parameter-consideration.md)]


## Diagnose applens resource

<!-- @mcpcli applens resource diagnose -->

This tool is part of the Model Context Protocol (MCP) tools. Get diagnostic help from App Lens for Azure applications and services to identify issues with performance, slowness, failures, errors, availability, or application state. This tool asks App Lens to analyze the specified resource and returns analysis, insights, and recommended solutions. Only the resource name and the `Question` are required; the `Subscription`, `Resource group`, and `Resource type` are optional and help narrow results when multiple resources share the same name.

Example prompts include:

- "Please help me diagnose issues with my app using app lens; question 'Why are requests failing frequently?' resource 'webapp-prod'."
- "Use app lens to check why my app is slow; question 'Why is API response time increased?' resource 'order-api'."
- "What does app lens say is wrong with my service; question 'What are the top errors causing failures?' resource 'service-backend'?"

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Question** |  Required | User question. |
| **Resource** |  Required | The name of the resource to investigate or diagnose. |
| **Resource group** |  Optional | Azure resource group name. Provide this when disambiguating between multiple resources of the same name. |
| **Resource type** |  Optional | Resource type. Provide this when disambiguating between multiple resources of the same name. |
| **Subscription** |  Optional | Azure subscription ID or name. Provide this when disambiguating between multiple resources of the same name. |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌

## Related content

- [What are the Azure MCP Server tools?](index.md)
- [Get started using Azure MCP Server](../get-started.md)
- [Azure App Service diagnostics documentation](/azure/app-service/troubleshoot-diagnostic-tools)