---
title: Azure MCP Server tools for Azure Cloud Architect
description: Use Azure MCP Server tools to manage architecture designs and guidance through AI-powered natural language interactions.
author: diberry
ms.author: diberry
ms.service: azure-mcp-server
ms.topic: how-to
ms.date: 2026-03-25 06:36:50 UTC
content_well_notification:
  - AI-contribution
ai-usage: ai-generated
ms.custom: build-2025

#customer intent: As an Azure Cloud Architect user, I want to manage architecture designs and guidance using natural language conversations so that I can quickly verify configurations and troubleshoot issues without navigating portals.

---

# Azure MCP Server tools for Azure Cloud Architect

Manage architecture designs and guidance using natural language conversations with AI assistants through the Azure MCP Server.

Azure Cloud Architect is a design advisory service that recommends cloud architecture patterns and solution layouts to meet business goals and constraints. This toolset is designed for use with Model Context Protocol (MCP) Server, uses Microsoft Entra ID for authentication, and relies on role-based access control (RBAC) for permissioned access. While the Azure portal and Azure CLI are powerful, the Azure MCP Server provides a more intuitive way to interact with your Azure Cloud Architect resources through conversational AI.

## What is the Azure MCP Server?

[!INCLUDE [mcp-introduction](~/includes/mcp-introduction.md)]

For Azure Cloud Architect users, this means you can:

- Generate architecture recommendations and diagrams for cloud solutions

## Prerequisites

To use the Azure MCP Server with Azure Cloud Architect, you need:

- **Azure subscription**: An active Azure subscription. [Create one for free](https://azure.microsoft.com/free/).
- **Microsoft Entra ID account**: An account with Microsoft Entra ID is required for authentication with the MCP Server and to apply RBAC permissions.
- **Project requirements document**: A summary of business goals, compliance constraints, expected traffic, and integration points to ground the design recommendations.
- **Azure permissions**: Appropriate roles to perform the operations you want:
  - Reader - Required for read-only access to subscription and resource metadata that the design tool references when evaluating existing deployments.
  - Cognitive Services User - Required to invoke the Cloud Architect design model and retrieve generated architecture recommendations and artifacts.

[!INCLUDE [mcp-prerequisites](~/includes/mcp-prerequisites.md)]

## Where can you use Azure MCP Server?

[!INCLUDE [mcp-usage-contexts](~/includes/mcp-usage-contexts.md)]

## Available tools for Azure Cloud Architect

Azure MCP Server provides the following tools for Azure Cloud Architect operations:

| Tool | Description |
| --- | --- |
| `cloudarchitect design` | Recommend multi-tier cloud architecture designs with diagrams, tables, and confidence tracking. |

For detailed information about each tool, including parameters and examples, see [Azure Cloud Architect tools for Azure MCP Server](../tool-family/cloudarchitect.md).

## Get started

Ready to use Azure MCP Server with your Azure Cloud Architect resources?

1. **Set up your environment**: Choose an AI assistant or development tool that supports MCP. For setup and authentication instructions, see the links in the [Where can you use Azure MCP Server?](#where-can-you-use-azure-mcp-server) section above.

1. **Start exploring**: Ask your AI assistant questions about your Azure Cloud Architect resources or request operations. Try prompts like:
   - "Draft a reference architecture for 'ContosoShop' in East US for 2,000 concurrent users, PCI DSS compliance, CDN, caching, and high availability."
   - "Plan a migration for 'ContosoBank' moving 50 VMs and two SQL servers to East US with a zero-downtime target and data residency constraints."
   - "Create a multi-tenant SaaS reference architecture for 'InvoicePro' in West Europe for 10,000 tenants with tenant isolation and per-tenant encryption."

1. **Learn more**: Review the [Azure Cloud Architect tools reference](../tool-family/cloudarchitect.md) for all available capabilities and detailed parameter information.

## Best practices

When using Azure MCP Server with Azure Cloud Architect:

- **Iterate with clarifying questions**: Use cloudarchitect design to ask 1&#8211;2 clarifying questions, review the response, and then repeat until the confidence score meets your threshold.
- **Provide a scoped requirements document**: Include a concise project requirements document when you run cloudarchitect design so the tool tailors recommendations to actual constraints, such as compliance, throughput, and cost targets.
- **Validate context before requesting designs**: Use the Reader role to confirm subscription and resource context, then run cloudarchitect design so the recommendations reflect your current environment and constraints.

## Related content

* [Azure MCP Server overview](../overview.md)
* [Get started with Azure MCP Server](../get-started.md)
* [Azure Cloud Architect tools reference](../tool-family/cloudarchitect.md)
* [Azure Well-Architected Framework](/azure/architecture/framework/)
