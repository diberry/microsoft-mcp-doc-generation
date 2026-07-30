---
title: Azure skill for Azure Upgrade
description: Use the azure-upgrade skill to assess and plan workload and SDK upgrades across Azure. It evaluates plans and SKUs, including Consumption to Flex Consumption and Azure Functions hosting plan changes. It guides App Service to Container Apps migration, Azure Cache for Redis to Azure Managed Redis, and modernizes Java SDKs from com.microsoft.azure to com.azure.
ms.topic: reference
ms.date: 7/30/2026
ms.custom: skill-version-1.0.0
author: diberry
ms.author: diberry
ms.service: azure-mcp-server
---

# Azure skill for Azure Upgrade

To assess and plan workload and SDK upgrades across Azure, use the `azure-upgrade` skill. It evaluates plans and SKUs, including Consumption to Flex Consumption and Azure Functions hosting plan changes. It guides App Service to Container Apps migration, Azure Cache for Redis to Azure Managed Redis, and modernizes Java SDKs from com.microsoft.azure to com.azure. Use `azure-upgrade` when you need an assessment and actionable plan for Azure hosting plan or SKU changes (including Consumption to Flex), platform migrations like App Service to Container Apps. Legacy Redis to managed Redis, or modernization of Java SDKs from com.microsoft.azure to com.azure.

**Skill** `azure-upgrade` | [Source code](https://github.com/microsoft/azure-skills/blob/main/skills/azure-upgrade/SKILL.md)

## What it provides

You get targeted assessments and actionable upgrade plans for workloads and SKUs across Azure—covering Consumption to Flex Consumption, Azure Functions hosting. SKU changes, and App Service to Container Apps migrations. You also receive migration guidance for Azure Cache for Redis to Azure Managed Redis and concrete steps to modernize legacy Java SDKs from com.microsoft.azure to com.azure, helping you reduce risk. Effort.

## Prerequisites

- **Azure authentication**—Sign in with `az login` or use a service principal.
- **Azure subscription**—An active Azure subscription is required.
- **GitHub Copilot**
- **Python** (v3.10+)—Install: `https://python.org`
- **python3.10+**

## When to use this skill

Use the **Azure Upgrade** skill when you need to:

- Upgrade Consumption to Flex Consumption
- Upgrade Azure Functions plan
- Manage and configure change hosting plan and function app SKU in Azure
- Migrate App Service to Container Apps
- Modernize legacy Azure Java SDKs (com.microsoft.azure to com.azure)
- Migrate Azure Cache for Redis (Acr/Acre) to Azure Managed Redis (Amr)

## Example prompts

Try these prompts to activate this skill:

- "Upgrade my function app from Consumption to Flex Consumption"
- "Move my function app to a better plan"
- "Is my function app ready for Flex Consumption?"
- "Automate the steps to upgrade my Functions plan"
- "Upgrade my Azure Functions SKU"
- "Change my function app hosting plan"
- "Migrate my Azure Functions from Consumption to Flex Consumption"
- "Assess my function app for upgrade readiness"

## Related content

- [Azure maintenance and updates](/azure/virtual-machines/maintenance-and-updates)
- [virtual machine (VM) image upgrades](/azure/virtual-machines/updates-maintenance-overview)
- [Azure service updates](https://azure.microsoft.com/updates/)
- [Upgrade strategies](/azure/architecture/best-practices/)
- [Azure Model Context Protocol (MCP) Server overview](/azure/developer/azure-mcp-server/overview)
- [Skill source code](https://github.com/microsoft/azure-skills/blob/main/skills/azure-upgrade/SKILL.md)