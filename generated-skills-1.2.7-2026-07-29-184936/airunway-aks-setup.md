---
title: Azure skill for AIRunway AKS Setup
description: This skill guides you through setting up AI Runway on an Azure Kubernetes Service (AKS) cluster, from cluster checks to running a model. It verifies cluster readiness, installs the AI Runway controller, assesses GPU capability, configures providers, and deploys an initial model for inference.
ms.topic: reference
ms.date: 7/30/2026
author: diberry
ms.author: diberry
ms.service: azure-mcp-server
---

# Azure skill for AIRunway Azure Kubernetes Service (AKS) Setup

This skill guides you through setting up AI Runway on an Azure Kubernetes Service (AKS) cluster, from cluster checks to running a model. It verifies cluster readiness, installs the AI Runway controller, assesses GPU capability, configures providers, and deploys an initial model for inference. Use the airunway-aks-setup skill when you are onboarding an AKS cluster to host AI Runway and need an end-to-end setup that verifies cluster. GPU readiness, installs the AI Runway controller, configures providers, and deploys a test model for inference.

**Skill** `airunway-aks-setup` | [Source code](https://github.com/microsoft/azure-skills/blob/main/skills/airunway-aks-setup/SKILL.md)

## What it provides

You get a guided AKS setup that checks cluster readiness, installs the AI Runway controller, and configures required providers so your cluster is ready for model serving. It also evaluates GPU capabilities and deploys an initial model so you can run inference (including vLLM and other GPU workloads) on your AKS cluster.

## Prerequisites

- **Azure authentication**—Sign in with `az login` or use a service principal.
- **Azure subscription**—An active Azure subscription is required.
- **GitHub Copilot**
- **Azure command-line interface (CLI)** (v2.60.0+)—Install: `curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash`
- **Azure Kubernetes Service cluster**—AKS cluster for container orchestration

## When to use this skill

Use the **AIRunway AKS Setup** skill when you need to:

- Setup AI Runway in Azure
- Manage and configure onboard AKS cluster in Azure
- Install AI Runway in Azure
- Manage and configure airunway setup in Azure
- Deploy model to AKS
- Manage and configure Gpu inference on AKS and Kaito setup on AKS in Azure
- Run LLM on AKS
- Manage and configure vLLM on AKS in Azure
- Set up model serving on AKS
- Manage and configure AI Runway controller in Azure

## Example prompts

Try these prompts to activate this skill:

- "How do I work with setup Ai Runway?"
- "How do I onboard AKS cluster?"
- "How do I install Ai Runway?"
- "How do I work with airunway setup?"
- "How do I deploy model to AKS?"
- "How do I work with gpu inference on AKS?"
- "How do I work with kaito setup on AKS?"
- "How do I run Llm on AKS?"

## Related content

- [Azure Model Context Protocol (MCP) Server overview](/azure/developer/azure-mcp-server/overview)
- [Skill source code](https://github.com/microsoft/azure-skills/blob/main/skills/airunway-aks-setup/SKILL.md)