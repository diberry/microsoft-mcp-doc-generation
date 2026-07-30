---
title: Azure skill for Azure Quotas
description: This skill helps you check current Azure service quotas and resource usage across subscriptions and regions, so you can plan deployments and select regions. You can identify limits like vCPU limits, validate capacity before provisioning, and prepare requests to increase quotas when needed.
ms.topic: reference
ms.date: 7/30/2026
author: diberry
ms.author: diberry
ms.service: azure-mcp-server
---

# Azure skill for Azure Quotas

This skill helps you check current Azure service quotas and resource usage across subscriptions and regions, so you can plan deployments and select regions. You can identify limits like vCPU limits, validate capacity before provisioning, and prepare requests to increase quotas when needed. I should reach for `azure-quotas` whenever I need to verify current service limits and resource usage—such as available vCPUs and regional capacity—so I can validate capacity before provisioning or prepare. Troubleshoot quota increase requests.

**Skill** `azure-quotas` | [Source code](https://github.com/microsoft/azure-skills/blob/main/skills/azure-quotas/SKILL.md)

## What it provides

See current Azure service quotas and resource usage across subscriptions and regions—including exact vCPU limits and per-region capacity—so you can validate whether you have the capacity to deploy before provisioning. Identify resources that have reached their limits, find regions with available capacity, and prepare quota increase requests when you need to scale.

## Prerequisites

- **Azure authentication**—Sign in with `az login` or use a service principal.
- **Azure subscription**—An active Azure subscription is required.
- **Quota Request Operator** role at Subscription scope—
- **GitHub Copilot**
- **PowerShell** (v7.4+)—Install: `winget install Microsoft.PowerShell`
- **Bash**
- **Azure Storage account**—Storage account for blob, file, queue, or table data
- **Azure Cosmos DB account**—Cosmos DB account for NoSQL data

## When to use this skill

Use the **Azure Quotas** skill when you need to:

- Check quotas in Azure
- Manage and configure service limits and current usage in Azure
- Request quota increase in Azure
- Manage and configure quota exceeded in Azure
- Validate capacity in Azure
- Manage and configure regional availability in Azure
- Provisioning limits in Azure
- Manage and configure vCPU limit in Azure

## Example prompts

Try these prompts to activate this skill:

- "How do I check my Azure quota limits?"
- "What are the service limits for my Azure subscription?"
- "Check current usage for my compute quota"
- "I need to request a quota increase for VMs in East US"
- "My deployment failed with a quota exceeded error"
- "How do I validate deployment capacity before provisioning?"
- "Help me select a region based on quota availability"
- "Compare quotas across regions for Standard_D4s_v3"

## Related content

- [Azure subscription limits](/azure/azure-resource-manager/management/azure-subscription-service-limits)
- [Quota requests](/azure/quotas/)
- [Resource limits by service](/azure/azure-resource-manager/management/azure-subscription-service-limits)
- [Regional capacity limits](https://azure.microsoft.com/global-infrastructure/services/)
- [Azure Model Context Protocol (MCP) Server overview](/azure/developer/azure-mcp-server/overview)
- [Skill source code](https://github.com/microsoft/azure-skills/blob/main/skills/azure-quotas/SKILL.md)