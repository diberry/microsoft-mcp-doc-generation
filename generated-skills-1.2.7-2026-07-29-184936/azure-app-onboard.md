---
title: Azure skill for Azure App Onboard
description: azure-app-onboard takes your app idea or existing code and prepares it for Azure. You get service recommendations, infrastructure-as-code scaffolding, cost estimates, and a deploy workflow with pre-deploy approval. It handles migrating existing apps with minimal code changes, and it doesn't force a rewrite.
ms.topic: reference
ms.date: 7/30/2026
author: diberry
ms.author: diberry
ms.service: azure-mcp-server
---

# Azure skill for Azure App Onboard

`azure-app-onboard` takes your app idea or existing code and prepares it for Azure. You get service recommendations, infrastructure-as-code scaffolding, cost estimates, and a deploy workflow with pre-deploy approval. It handles migrating existing apps with minimal code changes, and it doesn't force a rewrite. I should use `azure-app-onboard` when I have an app idea, starter project, or existing code and want Azure service recommendations, infrastructure as code (IaC) scaffolding, cost estimates, and a migration-friendly, deployable workflow that gets my app onto Azure with minimal code changes.

**Skill** `azure-app-onboard` | [Source code](https://github.com/microsoft/azure-skills/blob/main/skills/azure-app-onboard/SKILL.md)

## What it provides

Get tailored Azure service recommendations, cost estimates, and ready-to-run deployment templates and automation so you can plan, estimate, and deploy your app with confidence. Whether you have existing code or no code yet, you’ll get a starter project or migration path that minimizes code changes. A deploy workflow with one-click deploy and pre-deploy approval.

## Prerequisites

- **Azure authentication**—Sign in with `az login` or use a service principal.
- **Azure subscription**—An active Azure subscription is required.
- **GitHub Copilot**
- **PowerShell** (v7.4+)—Install: `winget install Microsoft.PowerShell`
- **Node.js** (vLTS+)—Install: `https://nodejs.org`
- **Bash**
- **Azure Kubernetes Service cluster**—Azure Kubernetes Service (AKS) cluster for container orchestration

## When to use this skill

Use the **Azure App Onboard** skill when you need to:

- Bring your app to Azure
- Plan my app in Azure
- Manage and configure cost to run in Azure
- Deploy my app to cloud
- Deploy all my services
- Plan my Azure deployment
- Deploy my new app to Azure
- Manage and configure one-click deploy in Azure
- I have an app and want it on Azure
- Migrate my app to Azure

### When not to use this skill

- Running `azd` up (use `azure-deploy`)
- Optimizing existing costs (use `azure-cost`)
- Code readiness checks only (use `azure-app-onboard-prereq`)

## Example prompts

Try these prompts to activate this skill:

- "How do I bring your app to Azure?"
- "How do I plan my app?"
- "How do I work with cost to run?"
- "Is my code ready to deploy?"
- "How do I deploy my app to cloud?"
- "How do I deploy all my services?"
- "What Azure services do I need?"
- "How do I plan my Azure deployment?"

## Related content

- [Azure Model Context Protocol (MCP) Server overview](/azure/developer/azure-mcp-server/overview)
- [Skill source code](https://github.com/microsoft/azure-skills/blob/main/skills/azure-app-onboard/SKILL.md)