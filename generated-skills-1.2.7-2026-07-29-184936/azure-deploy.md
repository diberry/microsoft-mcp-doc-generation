---
title: Azure skill for Azure Deploy
description: Use this skill to deploy applications that already include a .azure/deployment-plan.md and are validated by azure-validate. The skill runs azd, Terraform, and Azure Resource Manager deployment commands. It includes built-in error recovery, so you can push validated changes to Azure.
ms.topic: reference
ms.date: 7/30/2026
author: diberry
ms.author: diberry
ms.service: azure-mcp-server
---

# Azure skill for Azure Deploy

To deploy applications that already include a, use this skill.azure/deployment-plan.md and are validated by `azure-validate`. The skill runs `azd`, Terraform, and Azure Resource Manager deployment commands. It includes built-in error recovery, so you can push validated changes to Azure. Use `azure-deploy` when you have a validated deployment plan (a repo with .azure/deployment-plan.md that passed `azure-validate`) and need to run `azd`, Azure Resource Manager (ARM)/Bicep, or Terraform deployment commands to reliably push changes. Recover from errors as you go live or publish to production.

**Skill** `azure-deploy` | [Source code](https://github.com/microsoft/azure-skills/blob/main/skills/azure-deploy/SKILL.md)

## What it provides

To push validated application changes to Azure using `azd`, Terraform, or Azure Resource Manager/Bicep commands, based on the, use `azure-deploy`.azure/deployment-plan.md in your repo and validation by `azure-validate`. It includes automated error recovery and rollback so you can reliably run `azd` up, `azd` deploy, terraform apply, or publish updates to production.

## Prerequisites

- **Azure authentication**—Sign in with `az login` or use a service principal.
- **Azure subscription**—An active Azure subscription is required.
- **GitHub Copilot**
- **PowerShell** (v7.4+)—Install: `winget install Microsoft.PowerShell`
- **Bash**
- **Azure command-line interface (CLI)** (v2.60.0+)—Install: `curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash`

## When to use this skill

Use the **Azure Deploy** skill when you need to:

- Run `azd` up in Azure
- Run `azd` deploy in Azure
- Execute deployment in Azure
- Manage and configure push to production, push to cloud, go live, and ship it in Azure
- Manage and configure bicep deploy, terraform apply, publish to Azure, and launch on Azure in Azure

### When not to use this skill

- Create and deploy in Azure
- Build and deploy in Azure
- Create a new app
- Set up infrastructure in Azure
- Create and deploy to Azure using Terraform

## Deployment workflow

This skill is the final step in the deployment workflow:

- **`azure-prepare`** — generates infrastructure files and .azure/deployment-plan.md
- **`azure-validate`** — validates the deployment plan and infrastructure before deploying
- **`azure-deploy`** (this skill) — executes the deployment

## Example prompts

Try these prompts to activate this skill:

- "Execute deployment to Azure production"
- "Deploy and provision my Azure infrastructure"
- "Push my deploy to Azure production"
- "Ship and deploy my Azure app"
- "Run the Azure deployment now"
- "Deploy my Azure Functions app to cloud using the Azure Developer CLI."
- "Deploy my serverless function app to Azure"
- "Deploy Azure Functions to production"

## Related content

- [Azure Resource Manager templates](/azure/azure-resource-manager/templates/)
- [Bicep templates](/azure/azure-resource-manager/bicep/)
- [Terraform on Azure](/azure/developer/terraform/)
- [Azure CLI deployment](/azure/azure-resource-manager/management/)
- [Azure Model Context Protocol (MCP) Server overview](/azure/developer/azure-mcp-server/overview)
- [Skill source code](https://github.com/microsoft/azure-skills/blob/main/skills/azure-deploy/SKILL.md)