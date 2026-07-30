---
title: Azure skill for Azure Resource Visualizer
description: This skill analyzes your Azure resource groups and produces detailed Mermaid architecture diagrams that map resource relationships. You'll use it to visualize topology, spot configuration gaps, and share clear architecture views with teammates.
ms.topic: reference
ms.date: 7/30/2026
author: diberry
ms.author: diberry
ms.service: azure-mcp-server
---

# Azure skill for Azure Resource Visualizer

This skill analyzes your Azure resource groups and produces detailed Mermaid architecture diagrams that map resource relationships. You'll use it to visualize topology, spot configuration gaps, and share clear architecture views with teammates. Use `azure-resource-visualizer` when you need to analyze an Azure resource group and generate Mermaid architecture diagrams that reveal resource topology. Relationships so you can spot configuration gaps, document architecture for change planning, or quickly onboard and align teammates.

**Skill** `azure-resource-visualizer` | [Source code](https://github.com/microsoft/azure-skills/blob/main/skills/azure-resource-visualizer/SKILL.md)

## What it provides

Visualize your Azure resource groups as detailed Mermaid architecture diagrams that map each resource and their relationships so you can quickly understand topology and connectivity. Use these shareable diagrams to spot configuration gaps, document your environment, and communicate clear architecture views with teammates.

## Prerequisites

- **Azure authentication**—Sign in with `az login` or use a service principal.
- **Azure subscription**—An active Azure subscription is required.
- **GitHub Copilot**
- **Azure command-line interface (CLI)** (v2.60.0+)—Install: `curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash`
- **Azure Key Vault**—Key vault for secrets and certificate management

## When to use this skill

Use the **Azure Resource Visualizer** skill when you need to:

- Create architecture diagram in Azure
- Visualize Azure resources
- Manage and configure generate Mermaid diagram in Azure
- Analyze resource group in Azure
- Manage and configure diagram my resources, architecture visualization, resource topology, and map Azure infrastructure in Azure

## Example prompts

Try these prompts to activate this skill:

- "Create an architecture diagram for my Azure resource group"
- "Generate a Mermaid diagram of my resource group"
- "Visualize my Azure resources"
- "Visualize the architecture of my Azure resources"
- "Architecture visualization for my Azure infrastructure"
- "Show me the relationships between my Azure resources"
- "Show resource relationships"
- "How are my Azure resources connected?"

## Related content

- [Azure Resource Graph](/azure/governance/resource-graph/overview)
- [Visualize resources](/azure/azure-resource-manager/management/manage-resource-groups-portal)
- [Resource topology visualization](/azure/governance/resource-graph/first-query-portal)
- [Query resources](/azure/governance/resource-graph/first-query-kusto-query-language)
- [Azure Model Context Protocol (MCP) Server overview](/azure/developer/azure-mcp-server/overview)
- [Skill source code](https://github.com/microsoft/azure-skills/blob/main/skills/azure-resource-visualizer/SKILL.md)