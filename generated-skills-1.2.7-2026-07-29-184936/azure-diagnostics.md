---
title: Azure skill for Azure Diagnostics
description: Azure diagnostics helps you debug production issues across App Service, Functions, Container Apps, Azure Kubernetes Service, and virtual machines. It guides you through logs, Kusto Query Language (KQL), resource health, and network or messaging checks so you're able to find root cause and get suggested triage steps.
ms.topic: reference
ms.date: 7/30/2026
author: diberry
ms.author: diberry
ms.service: azure-mcp-server
---

# Azure skill for Azure Diagnostics

Azure diagnostics helps you debug production issues across App Service, Functions, Container Apps, Azure Kubernetes Service, and virtual machines. It guides you through logs, Kusto Query Language (KQL), resource health, and network or messaging checks so you're able to find root cause and get suggested triage steps. Use `azure-diagnostics` when you need to debug and triage production problems across App Service, Functions, Container Apps, Azure Kubernetes Service (AKS), or VMs—such as high CPU or deployment failures, crashed or pending pods. Node readiness issues, connectivity or credential problems to VMs or clusters, network/firewall blocks, image‑pull or cold‑start and health‑probe failures, or messaging failures in Event Hubs/Service Bus—by analyzing logs with KQL, checking resource health, and getting suggested remediation steps.

**Skill** `azure-diagnostics` | [Source code](https://github.com/microsoft/azure-skills/blob/main/skills/azure-diagnostics/SKILL.md)

## What it provides

Azure diagnostics helps you quickly find and fix production issues across App Service, Functions, Container Apps, AKS, and virtual machines by guiding you through relevant logs, Kusto Query Language (KQL) queries, resource health checks. Network and messaging diagnostics. It pinpoints root causes—such as high CPU, deployment or cold-start failures, pod CrashLoopBackOff or node-not-ready in AKS, virtual machine (VM) connectivity or password issues, network security group (NSG)/firewall blocks, image pull or health probe failures. Service Bus/Event Hubs messaging errors—and gives suggested triage steps to resolve them.

## Prerequisites

- **Azure authentication**—Sign in with `az login` or use a service principal.
- **Azure subscription**—An active Azure subscription is required.
- **GitHub Copilot**
- **Azure command-line interface (CLI)** (v2.60.0+)—Install: `curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash`
- **Azure Kubernetes Service cluster**—AKS cluster for container orchestration

## When to use this skill

Use the **Azure Diagnostics** skill when you need to:

- Debug production issues in Azure
- Troubleshoot app service in Azure
- Manage and configure app service high Cpu and app service deployment failure in Azure
- Troubleshoot container apps in Azure
- Troubleshoot functions in Azure
- Troubleshoot AKS in Azure
- Manage and configure VM Rdp, Linux SSH, VM black screen, and can't connect to VM in Azure
- Manage and configure reset VM password, Nsg or firewall blocking, kubectl can't connect, and kube-system/CoreDNS failures in Azure
- Manage and configure pod pending, crashloop, and node not ready in Azure
- Upgrade failures in Azure

## Example prompts

Try these prompts to activate this skill:

- "Debug my Azure Container App"
- "Troubleshoot production issues in my container app"
- "Diagnose errors in my Azure service"
- "Help me troubleshoot container apps on Azure"
- "Analyze logs with KQL for my app"
- "How do I analyze application logs?"
- "View application logs for my container"
- "Fix image pull failures in Container Apps"

## Related content

- [Azure Monitor overview](/azure/azure-monitor/overview)
- [Azure Monitor quickstart](/azure/azure-monitor/vm/monitor-vm-azure-monitor)
- [Diagnostic logging](/azure/azure-monitor/essentials/diagnostic-settings)
- [Troubleshooting guide](/azure/azure-monitor/troubleshooting)
- [Azure Model Context Protocol (MCP) Server overview](/azure/developer/azure-mcp-server/overview)
- [Skill source code](https://github.com/microsoft/azure-skills/blob/main/skills/azure-diagnostics/SKILL.md)