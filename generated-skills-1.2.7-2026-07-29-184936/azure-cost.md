---
title: Azure skill for Azure Cost Optimization
description: This skill helps you query your Azure costs and forecast spending across subscriptions and resource groups. You'll spot cost spikes, find orphaned resources, and get rightsizing and storage optimizations to reduce waste and lower bills.
ms.topic: reference
ms.date: 7/30/2026
author: diberry
ms.author: diberry
ms.service: azure-mcp-server
---

# Azure skill for Azure Cost Optimization

This skill helps you query your Azure costs and forecast spending across subscriptions and resource groups. You'll spot cost spikes, find orphaned resources, and get rightsizing and storage optimizations to reduce waste and lower bills. Use `azure-cost` when I need to analyze and forecast spend across subscriptions and resource groups—for example to investigate unexpected cost spikes, find. Eliminate orphaned or idle resources, rightsize VMs, or pinpoint storage or Azure Kubernetes Service (AKS) areas to reduce my bill.

**Skill** `azure-cost` | [Source code](https://github.com/microsoft/azure-skills/blob/main/skills/azure-cost/SKILL.md)

## What it provides

See your Azure spending across subscriptions and resource groups with detailed cost breakdowns by resource, service, and AKS, plus historical views and spending forecasts to predict future bills. Quickly spot cost spikes and orphaned resources and get actionable recommendations for rightsizing VMs and optimizing storage to reduce waste and lower your bill.

## Prerequisites

- **Azure authentication**—Sign in with `az login` or use a service principal.
- **Azure subscription**—An active Azure subscription is required.
- **Cost Management Reader** role at Subscription scope—
- **Monitoring Reader** role at Subscription scope—
- **GitHub Copilot**
- **Azure command-line interface (CLI)** (v2.60.0+)—Install: `curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash`

## When to use this skill

Use the **Azure Cost Optimization** skill when you need to:

- Manage and configure Azure costs, Azure bill, cost breakdown, and forecast spending in Azure
- Optimize costs in Azure
- Manage and configure reduce spending, orphaned resources, rightsize VMs, and cost spike in Azure
- Manage and configure reduce storage costs and AKS cost in Azure

### When not to use this skill

- Deploying resources in Azure
- Manage and configure diagnostics in Azure

## Example prompts

Try these prompts to activate this skill:

- "How do I work with azure costs?"
- "How do I work with azure bill?"
- "How do I work with cost breakdown?"
- "How much am I spending?"
- "How do I work with forecast spending?"
- "How do I optimize costs?"
- "How do I work with reduce spending?"
- "How do I work with orphaned resources?"

## Related content

- [Cost Management + Billing](/azure/cost-management-billing/)
- [Analyze costs](/azure/cost-management-billing/costs/cost-analysis-common-uses)
- [Azure pricing calculator](https://azure.microsoft.com/pricing/calculator/)
- [Cost optimization](/azure/cost-management-billing/costs/cost-mgt-best-practices)
- [Azure Model Context Protocol (MCP) Server overview](/azure/developer/azure-mcp-server/overview)
- [Skill source code](https://github.com/microsoft/azure-skills/blob/main/skills/azure-cost/SKILL.md)