---
title: Azure MCP Server tools for Azure AppLens
description: Use Azure MCP Server tools to manage application diagnostics and insights through AI-powered natural language interactions.
author: diberry
ms.author: diberry
ms.service: azure-mcp-server
ms.topic: how-to
ms.date: 2026-03-27 06:29:11 UTC
content_well_notification:
  - AI-contribution
ai-usage: ai-generated
ms.custom: build-2025

#customer intent: As an Azure AppLens user, I want to manage application diagnostics and insights using natural language conversations so that I can quickly verify configurations and troubleshoot issues without navigating portals.

---

# Azure MCP Server tools for Azure AppLens

Manage application diagnostics and insights using natural language conversations with AI assistants through the Azure MCP Server.

Azure AppLens provides diagnostic analysis for Azure applications and services to help identify performance issues, availability problems, and errors. It returns analysis, actionable insights, and recommended remediation steps that guide your troubleshooting for existing resources. While the Azure portal and Azure CLI are powerful, the Azure MCP Server provides a more intuitive way to interact with your Azure AppLens resources through conversational AI.

## What is the Azure MCP Server?

[!INCLUDE [mcp-introduction](~/includes/mcp-introduction.md)]

For Azure AppLens users, this means you can:

- Run diagnostic analysis for an application or service and receive insights and recommended solutions

## Prerequisites

To use the Azure MCP Server with Azure AppLens, you need:

- **Azure subscription**: An active Azure subscription. [Create one for free](https://azure.microsoft.com/free/).
- **Target resource**: An existing Azure resource such as an App Service, Function App, or VM is required so AppLens can analyze runtime behavior and logs.
- **Microsoft Entra ID identity**: A user or managed identity authenticated via DefaultAzureCredential is required, and it must have permission to read the target resource and its diagnostic data.
- **Azure permissions**: Appropriate roles to perform the operations you want:
  - Reader - Required to run applens resource diagnose and read diagnostic data, telemetry, and resource metadata for the target resource.
  - Website Contributor - Required if you perform follow-up management actions on App Service configuration outside diagnostics; applens resource diagnose itself uses Reader permissions.

[!INCLUDE [mcp-prerequisites](~/includes/mcp-prerequisites.md)]

## Where can you use Azure MCP Server?

[!INCLUDE [mcp-usage-contexts](~/includes/mcp-usage-contexts.md)]

## Available tools for Azure AppLens

Azure MCP Server provides the following tools for Azure AppLens operations:

| Tool | Description |
| --- | --- |
| `applens resource diagnose` | Run AppLens diagnostic analysis for an app or resource to get insights. |

For detailed information about each tool, including parameters and examples, see [Azure AppLens tools for Azure MCP Server](../tool-family/applens.md).

## Get started

Ready to use Azure MCP Server with your Azure AppLens resources?

1. **Set up your environment**: Choose an AI assistant or development tool that supports MCP. For setup and authentication instructions, see the links in the [Where can you use Azure MCP Server?](#where-can-you-use-azure-mcp-server) section above.

1. **Start exploring**: Ask your AI assistant questions about your Azure AppLens resources or request operations. Try prompts like:
   - "Diagnose the performance of web app 'contoso-web' in East US that reports slow requests since 2026-03-01, include recent deployment details and error traces."
   - "Diagnose repeated 500 errors for function app 'contoso-func' that started after a package update, include exception messages and affected endpoints."
   - "Diagnose intermittent availability for web app 'contoso-portal' in West Europe that shows brief outages over the past 48 hours."

1. **Learn more**: Review the [Azure AppLens tools reference](../tool-family/applens.md) for all available capabilities and detailed parameter information.

## Best practices

When using Azure MCP Server with Azure AppLens:

- **Use managed identities**: Assign a Microsoft Entra ID managed identity or user account and use DefaultAzureCredential when you run applens resource diagnose so you don't store credentials locally.
- **Run diagnostics before manual checks**: Use applens resource diagnose first to gather correlation analysis and recommended steps, and then run targeted metric or log queries only if you need more detail.
- **Provide detailed context in queries**: Include timestamps, error messages, deployment names, and recent configuration changes when you call applens resource diagnose to improve the relevance of returned insights.

## Related content

* [Azure MCP Server overview](../overview.md)
* [Get started with Azure MCP Server](../get-started.md)
* [Azure AppLens tools reference](../tool-family/applens.md)
