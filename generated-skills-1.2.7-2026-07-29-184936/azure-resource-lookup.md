---
title: Azure skill for Azure Resource Lookup
description: Azure Resource Lookup helps you find and list Azure resources across subscriptions and resource groups using tags, types, and Azure Resource Graph queries. Use it for inventory, tag analysis, orphaned resources and unattached disks, counting resources by type, and cross-subscription lookups; it isn't for deploying or cost optimization.
ms.topic: reference
ms.date: 7/30/2026
author: diberry
ms.author: diberry
ms.service: azure-mcp-server
---

# Azure skill for Azure Resource Lookup

Azure Resource Lookup helps you find and list Azure resources across subscriptions and resource groups using tags, types, and Azure Resource Graph queries. Use it for inventory, tag analysis, orphaned resources and unattached disks, counting resources by type, and cross-subscription lookups; it isn't for deploying or cost optimization. Use the `azure-resource-lookup` skill when you need to discover and inventory Azure resources across subscriptions or resource groups—such as locating resources by tag. Type, running Azure Resource Graph queries, finding orphaned or unattached resources, or counting resources for operational or governance tasks—rather than to deploy or modify resources (use `azure-deploy`), perform cost optimization (use `azure-cost`), or manage non‑Azure clouds.

**Skill** `azure-resource-lookup` | [Source code](https://github.com/microsoft/azure-skills/blob/main/skills/azure-resource-lookup/SKILL.md)

## What it provides

Find and list Azure resources across subscriptions and resource groups using tags, resource types, or Azure Resource Graph queries. Use it to inventory websites, web apps and app services, analyze tags, locate orphaned resources and unattached disks, count resources by type. Run cross‑subscription lookups; it’s not intended for deploying resources or cost optimization.

## Prerequisites

- **Azure authentication**—Sign in with `az login` or use a service principal.
- **Azure subscription**—An active Azure subscription is required.
- **GitHub Copilot**
- **Azure command-line interface (CLI)** (v2.60.0+)—Install: `curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash`
- **Azure Key Vault**—Key vault for secrets and certificate management
- **Azure Storage account**—Storage account for blob, file, queue, or table data
- **Azure Kubernetes Service cluster**—Azure Kubernetes Service (AKS) cluster for container orchestration
- **Azure Cosmos DB account**—Cosmos DB account for NoSQL data

## When to use this skill

Use the **Azure Resource Lookup** skill when you need to:

- List websites in Azure
- List web apps in Azure
- List app services in Azure
- Manage and configure resource inventory in Azure
- Find resources by tag
- Manage and configure tag analysis in Azure
- Orphaned resource discovery (not for cost analysis)
- Manage and configure unattached disks, count resources by type, and cross-subscription lookup in Azure
- And Azure Resource Graph queries

### When not to use this skill

- Manage and configure deploying/changing resources (use `azure-deploy`) and cost optimization (use `azure-cost`) in Azure

## Example prompts

Try these prompts to activate this skill:

- "List the websites in my subscription"
- "Show me the websites in my resource group"
- "List all virtual machines in my subscription"
- "Show me all VMs in resource group 'my-rg'"
- "List my Azure storage accounts"
- "List all my Azure Container Registries"
- "List the container apps in my subscription"
- "Show me the container apps in my resource group"

## Related content

- [Azure Resource Manager overview](/azure/azure-resource-manager/management/overview)
- [Resource naming and tagging](/azure/cloud-adoption-framework/ready/azure-best-practices/naming-and-tagging)
- [Resource organization](/azure/azure-resource-manager/management/manage-resource-groups-portal)
- [Resource providers](/azure/azure-resource-manager/management/resource-providers-and-types)
- [Azure Model Context Protocol (MCP) Server overview](/azure/developer/azure-mcp-server/overview)
- [Skill source code](https://github.com/microsoft/azure-skills/blob/main/skills/azure-resource-lookup/SKILL.md)