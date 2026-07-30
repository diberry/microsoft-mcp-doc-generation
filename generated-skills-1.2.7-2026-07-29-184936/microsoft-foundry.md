---
title: Azure skill for Microsoft Foundry
description: Use the microsoft-foundry skill to author, test, and optimize Foundry AI agents across their lifecycle. You can scaffold, run, and deploy hosted agents with azd, create and batch-evaluate prompts, and perform continuous evaluation and monitoring. It's also helpful for curating datasets from traces, fine-tuning models with SFT, DPO, or RFT, and managing role-based access control (RBAC), quotas, and troubleshooting.
ms.topic: reference
ms.date: 7/30/2026
author: diberry
ms.author: diberry
ms.service: azure-mcp-server
---

# Azure skill for Microsoft Foundry

To author, test, and optimize Foundry AI agents across their lifecycle, use the microsoft-foundry skill. You can scaffold, run, and deploy hosted agents with `azd`, create and batch-evaluate prompts, and perform continuous evaluation and monitoring. It's also helpful for curating datasets from traces, fine-tuning models with SFT, DPO, or RFT, and managing role-based access control (RBAC), quotas, and troubleshooting. Reach for microsoft-foundry when you need to build, test, optimize, and run Azure Foundry AI agents end-to-end—scaffolding and deploying hosted agents with `azd`, iterating. Batch‑evaluating prompts and instructions, performing continuous evaluation and monitoring, curating trace datasets and fine‑tuning models (SFT/DPO/RFT), and managing RBAC, quotas, regions, and troubleshooting for production deployments.

**Skill** `microsoft-foundry` | [Source code](https://github.com/microsoft/azure-skills/blob/main/skills/microsoft-foundry/SKILL.md)

## What it provides

microsoft-foundry helps you author, test, and optimize Foundry AI agents end-to-end—scaffold, run, and deploy hosted agents with `azd`, add tools, invoke. Batch-evaluate prompts, and iterate on agent instructions and prompts. You also get continuous evaluation and monitoring, dataset curation from traces, fine-tuning workflows (SFT, DPO, RFT), deployment and quota/region management, RBAC. Role assignments, and troubleshooting for provisioning or deployment failures.

## Prerequisites

- **Azure authentication**—Sign in with `az login` or use a service principal.
- **Azure subscription**—An active Azure subscription is required.
- **GitHub Copilot**
- **PowerShell** (v7.4+)—Install: `winget install Microsoft.PowerShell`
- **Azure command-line interface (CLI) with Bicep** (v2.60.0+)—Install: `az bicep install`
- **Python** (v3.10+)—Install: `https://python.org`
- **Bash**
- **Azure Cosmos DB account**—Cosmos DB account for NoSQL data

## When to use this skill

Use the **Microsoft Foundry** skill when you need to:

- Manage and configure `azd` AI agent and `azd` provision/deploy in Azure
- Deploy agent in Azure
- Manage and configure hosted agent in Azure
- Create agent in Azure
- Add tool to agent
- Manage and configure invoke agent in Azure
- Evaluate agent in Azure
- Manage and configure continuous eval, continuous monitoring, and agent Ci/Cd in Azure
- Optimize prompt in Azure
- Manage and configure improve prompt in Azure

### When not to use this skill

- Manage and configure Azure Functions and App Service in Azure
- General Azure deploy (use `azure-deploy`)
- General Azure prep (use `azure-prepare`)

## Example prompts

Try these prompts to activate this skill:

- "How do I deploy an AI model from Microsoft Foundry catalog?"
- "Build a RAG application with Azure AI Foundry knowledge index"
- "Create an AI agent in Microsoft Foundry with web search"
- "Evaluate agent performance using Foundry evaluators"
- "Optimize my prompt for a Microsoft Foundry agent"
- "Improve my agent instructions in Azure AI Foundry"
- "Use a prompt optimizer on my Foundry system prompt"
- "Set up agent monitoring and continuous evaluation in Foundry"

## Related content

- [Azure AI Foundry](/azure/AI-studio/what-is-AI-studio)
- [Azure AI Foundry pricing](https://azure.microsoft.com/pricing/details/AI-studio/)
- [Azure OpenAI Service](/azure/AI-services/openai/)
- [Foundry model deployment](/azure/AI-studio/how-to/deploy-models-openai)
- [Azure Model Context Protocol (MCP) Server overview](/azure/developer/azure-mcp-server/overview)
- [Skill source code](https://github.com/microsoft/azure-skills/blob/main/skills/microsoft-foundry/SKILL.md)