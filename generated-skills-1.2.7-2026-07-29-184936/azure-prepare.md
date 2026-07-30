---
title: Azure skill for Azure Prepare
description: This skill prepares azd-based Azure projects for deployment when you plan to use the Azure Developer CLI (azd). It generates an azure.yaml file, infrastructure as code in Bicep or Terraform, and Dockerfiles aligned to the azd workflow. Don't use it for non-azd deployments, Python App Service code-only deploys (use python-appservice-deploy), or cross-cloud migration (use azure-cloud-migrate).
ms.topic: reference
ms.date: 7/30/2026
author: diberry
ms.author: diberry
ms.service: azure-mcp-server
---

# Azure skill for Azure Prepare

This skill prepares `azd`-based Azure projects for deployment when you plan to use the Azure Developer command-line interface (CLI) (`azd`). It generates an azure.yaml file, infrastructure as code in Bicep or Terraform, and Dockerfiles aligned to the `azd` workflow. Don't use it for non-`azd` deployments, Python App Service code-only deploys (use python-appservice-deploy), or cross-cloud migration (use `azure-cloud-migrate`). Use `azure-prepare` when you want to convert an existing app—such as a function or containerized service that uses triggers or managed identities—into an `azd`-ready project by generating azure.yaml, `azd`-aligned Dockerfiles. Bicep or Terraform infrastructure so you can deploy with the Azure Developer CLI, and avoid it for non-`azd` or cross-cloud migration scenarios.

**Skill** `azure-prepare` | [Source code](https://github.com/microsoft/azure-skills/blob/main/skills/azure-prepare/SKILL.md)

## What it provides

You get an `azd`-ready project that includes a generated azure.yaml, infrastructure-as-code templates in Bicep or Terraform, and Dockerfiles configured to the Azure Developer CLI workflow for straightforward deployment. Use this to prepare `azd`-based apps for deployment—it's not intended for non-`azd` deployments, Python App Service code-only deploys (use python-appservice-deploy), or cross-cloud migrations (use `azure-cloud-migrate`).

## Prerequisites

- **Azure authentication**—Sign in with `az login` or use a service principal.
- **Azure subscription**—An active Azure subscription is required.
- **GitHub Copilot**
- **PowerShell** (v7.4+)—Install: `winget install Microsoft.PowerShell`
- **Bash**

## When to use this skill

Use the **Azure Prepare** skill when you need to:

- Prepare app for `azd`

### When not to use this skill

- Manage and configure non-`azd` deployments in Azure
- Python App Service code-only deploys (use python-appservice-deploy)
- Create azure.yaml
- Set up `azd` infrastructure
- Modernize app for Azure with `azd`
- Deploy with `azd` in Azure
- Manage and configure function app, timer trigger, service bus trigger, and event-driven function in Azure
- Manage and configure managed identity, generate Bicep, and generate Terraform in Azure
- Create and deploy to Azure

## Deployment workflow

This skill is the first step in the deployment workflow:

- **`azure-prepare`** (this skill) — generates infrastructure files and .azure/deployment-plan.md
- **`azure-validate`** — validates the deployment plan and infrastructure before deploying
- **`azure-deploy`** — executes the deployment

## Example prompts

Try these prompts to activate this skill:

- "Create a dad joke generator and deploy to Azure"
- "Build a web app and host it on Azure"
- "I want to deploy my application to Azure"
- "Set up Azure infrastructure for my project"
- "Prepare my app for Azure deployment"
- "Create an API and run it on Azure"
- "Migrate my application to Azure"
- "Configure Azure hosting for my app"

## Related content

- [Azure Developer CLI (`azd`)](/azure/developer/azure-developer-cli/)
- [Azure Developer CLI quickstart](/azure/developer/azure-developer-cli/get-started)
- [Infrastructure as Code](/azure/developer/azure-developer-cli/make-`azd`-templates)
- [Azure development best practices](/azure/developer/intro/)
- [Azure Model Context Protocol (MCP) Server overview](/azure/developer/azure-mcp-server/overview)
- [Skill source code](https://github.com/microsoft/azure-skills/blob/main/skills/azure-prepare/SKILL.md)