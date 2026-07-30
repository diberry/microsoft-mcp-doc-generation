---
title: Azure skill for Azure Compute
description: The azure-compute skill helps you choose and prepare virtual machines (VMs) and Virtual Machine Scale Sets (Virtual machine scale set (VMSS)) for your workloads. It recommends sizes, compares pricing and capacity options, suggests scale and autoscale patterns, and guides on capacity reservation (CRG), machine enrollment (EMM), and monitoring. Use it when creating, sizing, or estimating costs for GPU, HPC, web, or dev/test workloads.
ms.topic: reference
ms.date: 7/30/2026
author: diberry
ms.author: diberry
ms.service: azure-mcp-server
---

# Azure skill for Azure Compute

The `azure-compute` skill helps you choose and prepare virtual machines (VMs) and Virtual Machine Scale Sets (Virtual machine scale set (VMSS)) for your workloads. It recommends sizes, compares pricing and capacity options, suggests scale and autoscale patterns, and guides on capacity reservation (CRG), machine enrollment (EMM), and monitoring. Use it when creating, sizing, or estimating costs for GPU, HPC, web, or `dev/test` workloads. Use `azure-compute` when you’re creating, sizing, or estimating costs for VMs or virtual machine (VM) scale sets—especially for GPU, HPC, web, or `dev/test` workloads—and need guidance on instance selection, pricing. Capacity trade-offs, autoscale patterns, capacity reservations, machine enrollment, or monitoring.

**Skill** `azure-compute` | [Source code](https://github.com/microsoft/azure-skills/blob/main/skills/azure-compute/SKILL.md)

## What it provides

When creating, sizing, or estimating costs for GPU, HPC, web, or `dev/test` workloads, `azure-compute` helps you pick the right Azure VMs. Virtual Machine Scale Sets by recommending sizes, comparing pricing and capacity options, and suggesting scale and autoscale patterns. It also guides you through capacity reservations, machine enrollment, monitoring, and Virtual machine scale set (VMSS) orchestration so you can reserve. Guarantee capacity, optimize costs, and ensure the right performance for production and test environments.

## Prerequisites

- **Azure authentication**—Sign in with `az login` or use a service principal.
- **Azure subscription**—An active Azure subscription is required.
- **GitHub Copilot**
- **Azure command-line interface (CLI) with Bicep** (v2.60.0+)—Install: `az bicep install`
- **Terraform** (v1.5+)—Install: `https://developer.hashicorp.com/terraform/install`

## When to use this skill

Use the **Azure Compute** skill when you need to:

- Create / provision / deploy / spin-up VM
- Recommend VM size in Azure
- Compare VM pricing in Azure
- Manage and configure Virtual machine scale set (VMSS) in Azure
- Scale set in Azure
- Manage and configure autoscale, burstable, lightweight server, and website in Azure
- Manage and configure back end, machine learning, Hpc simulation, and `dev/test` in Azure
- Manage and configure workload, family, load balancer, and Flexible orchestration in Azure
- Manage and configure Uniform orchestration, cost estimate, capacity reservation (Crg), and reserve in Azure
- Manage and configure guarantee capacity, pre-provision, Crg association, and Crg disassociation in Azure

## Example prompts

Try these prompts to activate this skill:

- "Help me choose a VM for my workload"
- "What VM size should I use for a web server?"
- "Compare Azure VM families for machine learning workloads"
- "How much will a Standard D4s v3 VM cost?"
- "Help me set up autoscale for my VM scale set"
- "I can't connect to my VM through RDP, help me troubleshoot"
- "How do I reset the password on my Azure VM?"
- "What's the difference between Flexible and Uniform orchestration?"

## Automatic activation

> [!NOTE]

## Related content

- [Virtual Machines overview](/azure/virtual-machines/)
- [VM quickstart](/azure/virtual-machines/windows/quick-create-portal)
- [Virtual Machines pricing](https://azure.microsoft.com/pricing/details/virtual-machines/)
- [VM sizing and performance](/azure/virtual-machines/sizing-guidance)
- [Azure Model Context Protocol (MCP) Server overview](/azure/developer/azure-mcp-server/overview)
- [Skill source code](https://github.com/microsoft/azure-skills/blob/main/skills/azure-compute/SKILL.md)