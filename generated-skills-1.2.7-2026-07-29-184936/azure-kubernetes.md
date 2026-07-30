---
title: Azure skill for Azure Kubernetes
description: This Azure skill helps you plan and prepare production-ready Azure Kubernetes Service (AKS) clusters. It guides SKU selection, networking, security, observability, autoscaling, upgrade strategy, and cost optimization, so you don't guess trade-offs and configuration decisions.
ms.topic: reference
ms.date: 7/30/2026
author: diberry
ms.author: diberry
ms.service: azure-mcp-server
---

# Azure skill for Azure Kubernetes

This Azure skill helps you plan and prepare production-ready Azure Kubernetes Service (AKS) clusters. It guides SKU selection, networking, security, observability, autoscaling, upgrade strategy, and cost optimization, so you don't guess trade-offs and configuration decisions. I should reach for the `azure-kubernetes` skill when I'm designing, provisioning, or operating a production AKS cluster and need prescriptive, trade-off-aware guidance to make concrete configuration decisions that ensure correct sizing, networking, security, observability, autoscaling, upgrade strategy. Cost-efficiency rather than guessing.

**Skill** `azure-kubernetes` | [Source code](https://github.com/microsoft/azure-skills/blob/main/skills/azure-kubernetes/SKILL.md)

## What it provides

You get clear, actionable guidance to plan and prepare production-ready Azure Kubernetes Service (AKS) clusters—covering SKU selection, secure networking, observability, autoscaling, upgrade strategy. Cost optimization so you don’t have to guess trade-offs or configurations. It helps you choose node SKUs and spot-node strategies, design AKS networking and security, enable observability, and apply pod rightsizing, cluster-autoscaler. Vertical Pod Autoscaler recommendations to improve performance and reduce cost.

## Prerequisites

- **Azure authentication**—Sign in with `az login` or use a service principal.
- **Azure subscription**—An active Azure subscription is required.
- **GitHub Copilot**
- **Azure command-line interface (CLI)** (v2.60.0+)—Install: `curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash`
- **Azure Key Vault**—Key vault for secrets and certificate management
- **Azure Kubernetes Service cluster**—AKS cluster for container orchestration

## When to use this skill

Use the **Azure Kubernetes** skill when you need to:

- Create AKS environment in Azure
- Provision AKS in Azure
- Enable AKS observability in Azure
- Design AKS networking in Azure
- Choose AKS SKU in Azure
- Secure AKS in Azure
- Optimize AKS in Azure
- Manage and configure AKS spot nodes, AKS cluster-autoscaler, rightsize AKS pod, and pod rightsizing in Azure
- Manage and configure over-provisioned AKS pod in Azure
- Pod resource requests and limits

## Example prompts

Try these prompts to activate this skill:

- "Help me create an AKS cluster"
- "I need to set up a new Kubernetes cluster on Azure"
- "Create a production-ready AKS cluster with best practices"
- "How do I provision an AKS cluster for my team?"
- "What networking options should I choose for AKS?"
- "AKS Day-0 checklist"
- "Plan AKS configuration for production"
- "Design AKS networking with private API server"

## Related content

- [Azure Kubernetes Service (AKS)](/azure/aks/)
- [AKS quickstart](/azure/aks/learn/quick-kubernetes-deploy-cli)
- [AKS pricing](https://azure.microsoft.com/pricing/details/kubernetes-service/)
- [AKS best practices](/azure/aks/best-practices)
- [Azure Model Context Protocol (MCP) Server overview](/azure/developer/azure-mcp-server/overview)
- [Skill source code](https://github.com/microsoft/azure-skills/blob/main/skills/azure-kubernetes/SKILL.md)