---
title: Azure MCP Server tools for Azure Deploy
description: Use Azure MCP Server tools to manage deployment plans, pipelines, and application logs through AI-powered natural language interactions.
author: diberry
ms.author: diberry
ms.service: azure-mcp-server
ms.topic: how-to
ms.date: 2026-03-25 05:41:10 UTC
content_well_notification:
  - AI-contribution
ai-usage: ai-generated
ms.custom: build-2025

#customer intent: As an Azure Deploy user, I want to manage deployment plans, pipelines, and application logs using natural language conversations so that I can quickly verify configurations and troubleshoot issues without navigating portals.

---

# Azure MCP Server tools for Azure Deploy

Manage deployment plans, pipelines, and application logs using natural language conversations with AI assistants through the Azure MCP Server.

Azure Deploy is a set of capabilities that help you plan, generate, and troubleshoot Azure application deployments. It provides architecture diagrams, deployment plans, CI/CD pipeline guidance, IaC rules, and access to deployment logs for applications deployed with the Azure Developer CLI, and it complements Model Context Protocol (MCP) workflows. While the Azure portal and Azure CLI are powerful, the Azure MCP Server provides a more intuitive way to interact with your Azure Deploy resources through conversational AI.

## What is the Azure MCP Server?

[!INCLUDE [mcp-introduction](~/includes/mcp-introduction.md)]

For Azure Deploy users, this means you can:

- Generate architecture diagrams from an application topology
- Retrieve application logs for azd-deployed Container Apps, App Services, and Function Apps
- Retrieve IaC rules and best practices for Bicep and Terraform files
- Generate CI/CD pipeline files and guidance for GitHub Actions or Azure DevOps
- Create a formatted deployment plan for a chosen hosting service and IaC tool

## Prerequisites

To use the Azure MCP Server with Azure Deploy, you need:

- **Azure subscription**: An active Azure subscription. [Create one for free](https://azure.microsoft.com/free/).
- **Azure Developer CLI environment**: An azd project workspace is required so tools can detect topology and configuration, and you must be signed in with Microsoft Entra ID for DefaultAzureCredential authentication.
- **Log Analytics workspace**: An existing Log Analytics workspace is required to retrieve application logs for Container Apps, App Services, and Function Apps deployed with azd.
- **Application source code repository**: A repository or local workspace is required for scanning service dependencies, generating architecture diagrams, and producing pipeline or IaC guidance.
- **Azure permissions**: Appropriate roles to perform the operations you want:
  - Reader - Required for reading resource metadata and configurations when scanning resources, generating architecture diagrams, and producing deployment plans under role-based access control (RBAC).
  - Monitoring Reader - Required for reading Log Analytics workspace data and metrics to retrieve application logs with deploy app logs get.

[!INCLUDE [mcp-prerequisites](~/includes/mcp-prerequisites.md)]

## Where can you use Azure MCP Server?

[!INCLUDE [mcp-usage-contexts](~/includes/mcp-usage-contexts.md)]

## Available tools for Azure Deploy

Azure MCP Server provides the following tools for Azure Deploy operations:

| Tool | Description |
| --- | --- |
| `deploy app logs get` | Retrieve azd application logs from the associated Log Analytics workspace. |
| `deploy architecture diagram generate` | Generate Azure architecture diagrams from detected application topology. |
| `deploy iac rules get` | Retrieve Bicep and Terraform IaC rules, compatibility, and best practices. |
| `deploy pipeline guidance get` | Generate CI/CD pipeline files and guidance for GitHub Actions or Azure DevOps. |
| `deploy plan get` | Generate a step-by-step deployment plan for chosen hosting and IaC. |

For detailed information about each tool, including parameters and examples, see [Azure Deploy tools for Azure MCP Server](../tool-family/deploy.md).

## Get started

Ready to use Azure MCP Server with your Azure Deploy resources?

1. **Set up your environment**: Choose an AI assistant or development tool that supports MCP. For setup and authentication instructions, see the links in the [Where can you use Azure MCP Server?](#where-can-you-use-azure-mcp-server) section above.

1. **Start exploring**: Ask your AI assistant questions about your Azure Deploy resources or request operations. Try prompts like:
   - "Show application logs for azd-deployed Container App 'my-orders-app' in East US for the last 2 hours, filtered for error level."
   - "Generate an architecture diagram for my app by scanning the workspace at 'github.com/contoso/order-service' and include Container Apps, Azure SQL, and Event Grid."
   - "Show Bicep and Terraform rules for deploying Container Apps with azd, including resource naming and azd compatibility tips."
   - "Create a GitHub Actions workflow that provisions infrastructure with azd for staging and deploys the app to Container Apps in East US."
   - "Create a deployment plan to deploy 'my-orders-app' to Container Apps in East US using azd and Bicep with staging and production environments."

1. **Learn more**: Review the [Azure Deploy tools reference](../tool-family/deploy.md) for all available capabilities and detailed parameter information.

## Best practices

When using Azure MCP Server with Azure Deploy:

- **Use Microsoft Entra ID authentication**: Before running deploy app logs get or deploy pipeline guidance get, ensure your user or managed identity has RBAC permissions and DefaultAzureCredential is configured so the tools access resources securely.
- **Scan the workspace before generating diagrams**: Run the workspace scan and provide an AppTopology or repository path before you call deploy architecture diagram generate so detected services and connection details are accurate.
- **Validate IaC rules early**: Run deploy iac rules get against your Bicep or Terraform drafts before committing so you catch azd compatibility and Azure configuration issues early.
- **Confirm CI preferences before generation**: Ask whether the team prefers GitHub Actions or Azure DevOps, and identify existing resources before calling deploy pipeline guidance get so generated pipelines match your environment and tooling.

## Related content

* [Azure MCP Server overview](../overview.md)
* [Get started with Azure MCP Server](../get-started.md)
* [Azure Deploy tools reference](../tool-family/deploy.md)
