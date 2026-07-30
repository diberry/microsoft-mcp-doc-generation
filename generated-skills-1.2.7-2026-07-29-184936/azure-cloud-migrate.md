---
title: Azure skill for Azure Cloud Migration
description: This skill assesses your cross-cloud workloads, generates migration reports, and converts code to prepare your apps for Azure. You'll get support for AWS Lambda to Azure Functions, Beanstalk/Heroku/App Engine to App Service, and Fargate, Kubernetes, Cloud Run, or Spring Boot to Container Apps.
ms.topic: reference
ms.date: 7/30/2026
ms.custom: skill-version-1.0.2
author: diberry
ms.author: diberry
ms.service: azure-mcp-server
---

# Azure skill for Azure Cloud Migration

This skill assesses your cross-cloud workloads, generates migration reports, and converts code to prepare your apps for Azure. You'll get support for AWS Lambda to Azure Functions, Beanstalk/Heroku/App Engine to App Service, and Fargate, Kubernetes, Cloud Run, or Spring Boot to Container Apps. Use `azure-cloud-migrate` when you're moving serverless, PaaS, or containerized applications from AWS, Google Cloud, or Heroku to Azure and need automated workload assessment, migration reports. Code/configuration conversion to Azure Functions, App Service, or Container Apps.

**Skill** `azure-cloud-migrate` | [Source code](https://github.com/microsoft/azure-skills/blob/main/skills/azure-cloud-migrate/SKILL.md)

## What it provides

You get an assessment of your cross‑cloud workloads with detailed migration reports that show required changes and estimated effort, plus code conversion to prepare your apps for Azure. It supports direct migrations such as AWS Lambda to Azure Functions; Elastic Beanstalk, Heroku, and App Engine to Azure App Service;. Fargate, ECS/Kubernetes/GKE/EKS, Cloud Run, or Spring Boot workloads to Azure Container Apps.

## Prerequisites

- **Azure authentication**—Sign in with `az login` or use a service principal.
- **Azure subscription**—An active Azure subscription is required.
- **GitHub Copilot**
- **Azure command-line interface (CLI)** (v2.60.0+)—Install: `curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash`
- **Azure Kubernetes Service cluster**—Azure Kubernetes Service (AKS) cluster for container orchestration

## When to use this skill

Use the **Azure Cloud Migration** skill when you need to:

- Migrate Lambda to Functions
- Manage and configure AWS to Azure in Azure
- Migrate Beanstalk in Azure
- Migrate Heroku in Azure
- Migrate App Engine in Azure
- Manage and configure Cloud Run migration, Fargate to Aca, and Ecs/Kubernetes/Gke/Eks to Container Apps in Azure
- Spring Boot to Container Apps
- Manage and configure cross-cloud migration in Azure

## Example prompts

Try these prompts to activate this skill:

- "How do I migrate my AWS Lambda functions to Azure Functions?"
- "I want to migrate from AWS to Azure"
- "Can you do a Lambda migration assessment for my project?"
- "Convert my serverless functions to Azure"
- "Generate a migration readiness report for my Lambda functions"
- "Help me migrate code to Azure Functions"
- "Assess my AWS Lambda project for Azure migration"
- "I need to move my Lambda workloads to Azure Functions"

## Related content

- [Azure Migrate overview](/azure/migrate/migrate-services-overview)
- [Migration strategies](/azure/cloud-adoption-framework/migrate/)
- [Database migration](/azure/dms/)
- [Migration best practices](/azure/migrate/best-practices-assessment)
- [Azure Model Context Protocol (MCP) Server overview](/azure/developer/azure-mcp-server/overview)
- [Skill source code](https://github.com/microsoft/azure-skills/blob/main/skills/azure-cloud-migrate/SKILL.md)