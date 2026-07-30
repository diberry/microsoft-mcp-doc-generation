---
title: Azure skill for Azure Validate
description: Azure-validate runs pre-deployment checks that verify your configuration, infrastructure as code (Bicep or Terraform), and service settings. Use it to test role-based access control (RBAC), managed identity permissions, azure.yaml, Function App, or Container Apps readiness, so you don't hit issues during deployment.
ms.topic: reference
ms.date: 7/30/2026
author: diberry
ms.author: diberry
ms.service: azure-mcp-server
---

# Azure skill for Azure Validate

Azure-validate runs pre-deployment checks that verify your configuration, infrastructure as code (Bicep or Terraform), and service settings. Use it to test role-based access control (RBAC), managed identity permissions, azure.yaml, Function App, or Container Apps readiness, so you don't hit issues during deployment. Use `azure-validate` before you deploy infrastructure-as-code or publish Functions or Container Apps—particularly when running Bicep/Terraform or azure.yaml—to verify role assignments, managed identity permissions. Service configuration so you catch permission and readiness issues before deployment.

**Skill** `azure-validate` | [Source code](https://github.com/microsoft/azure-skills/blob/main/skills/azure-validate/SKILL.md)

## What it provides

`azure-validate` helps you catch deployment problems early by running pre-deployment checks on your configuration and infrastructure as code—validating Bicep or Terraform templates, azure.yaml, and what‑if changes. It also verifies RBAC role assignments and managed identity permissions and tests Function App and Container Apps readiness so you can fix issues before you deploy.

## Prerequisites

- **Azure authentication**—Sign in with `az login` or use a service principal.
- **Azure subscription**—An active Azure subscription is required.
- **GitHub Copilot**
- **PowerShell** (v7.4+)—Install: `winget install Microsoft.PowerShell`
- **Bash**

## When to use this skill

Use the **Azure Validate** skill when you need to:

- Validate my app in Azure
- Check deployment readiness in Azure
- Run preflight checks in Azure
- Verify configuration in Azure
- Check if ready to deploy
- Validate azure.yaml
- Validate Bicep in Azure
- Test before deploying in Azure
- Troubleshoot deployment errors in Azure
- Validate Azure Functions

## Deployment workflow

This skill is the second step in the deployment workflow:

- **`azure-prepare`** — generates infrastructure files and .azure/deployment-plan.md
- **`azure-validate`** (this skill) — validates the deployment plan and infrastructure before deploying
- **`azure-deploy`** — executes the deployment

## Example prompts

Try these prompts to activate this skill:

- "Check if my app is ready to deploy to Azure"
- "Validate my azure.yaml configuration"
- "Run preflight checks before Azure deployment"
- "Troubleshoot deployment errors"
- "Verify my infrastructure configuration before deploying"
- "Is my app ready for Azure deployment?"
- "Validate my Bicep configuration"
- "Validate my Bicep template before deploying to Azure"

## Related content

- [Azure Resource Manager validation](/azure/azure-resource-manager/templates/template-validation)
- [Template validation tutorial](/azure/azure-resource-manager/bicep/linter)
- [Azure Resource Manager (ARM) template best practices](/azure/azure-resource-manager/templates/best-practices)
- [Azure compliance validation](/azure/governance/blueprints/overview)
- [Azure Model Context Protocol (MCP) Server overview](/azure/developer/azure-mcp-server/overview)
- [Skill source code](https://github.com/microsoft/azure-skills/blob/main/skills/azure-validate/SKILL.md)