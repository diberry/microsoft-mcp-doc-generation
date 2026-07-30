---
title: Azure skill for Azure Enterprise Infrastructure Planner
description: azure-enterprise-infra-planner helps you design enterprise Azure infrastructure from workload descriptions. It's for cloud architects and platform engineers planning networking, identity, security, compliance, and multi-resource topologies. It generates Bicep or Terraform templates and deployment plans for hub-spoke networks, multi-region disaster recovery, virtual networks (VNets), firewalls, private endpoints, and subscription-scope deployments without relying on azd.
ms.topic: reference
ms.date: 7/30/2026
author: diberry
ms.author: diberry
ms.service: azure-mcp-server
---

# Azure skill for Azure Enterprise Infrastructure Planner

`azure-enterprise-infra-planner` helps you design enterprise Azure infrastructure from workload descriptions. It's for cloud architects and platform engineers planning networking, identity, security, compliance, and multi-resource topologies. It generates Bicep or Terraform templates and deployment plans for hub-spoke networks, multi-region disaster recovery, virtual networks (VNets), firewalls, private endpoints, and subscription-scope deployments without relying on `azd`. Use `azure-enterprise-infra-planner` when you're an architect or platform engineer designing enterprise-grade Azure landing zones, hub‑spoke or multi‑region DR topologies that need subscription-scoped Bicep/Terraform output and rigorous networking, identity, security, and compliance planning.

**Skill** `azure-enterprise-infra-planner` | [Source code](https://github.com/microsoft/azure-skills/blob/main/skills/azure-enterprise-infra-planner/SKILL.md)

## What it provides

You get deployment-ready Bicep or Terraform templates and step-by-step deployment plans that translate workload descriptions into enterprise-scale Azure infrastructure, so you can architect landing zones and subscription-scope deployments. It covers hub-spoke networking, multi-region disaster recovery, VNets, firewalls, private endpoints, and Azure Backup for virtual machine (VM) workloads; for app-centric workflows, prefer `azure-prepare`.

## Prerequisites

- **Azure authentication**—Sign in with `az login` or use a service principal.
- **Azure subscription**—An active Azure subscription is required.
- **GitHub Copilot**
- **Azure command-line interface (CLI)** (v2.60.0+)—Install: `curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash`

## When to use this skill

Use the **Azure Enterprise Infrastructure Planner** skill when you need to:

- Plan Azure infrastructure
- Manage and configure architect Azure landing zone in Azure
- Design hub-spoke network in Azure
- Plan multi-region Dr topology
- Set up VNets firewalls and private endpoints
- Manage and configure subscription-scope Bicep deployment in Azure
- Azure Backup for VM workloads'. Prefer `azure-prepare` For app-centric workflows

## Example prompts

Try these prompts to activate this skill:

- "Deploy a geo-redundant backup solution for on-premises SQL servers using Azure Backup, configure encryption-at-rest, and automate monthly DR tests."
- "Deploy 3-tier architecture with hardened OS images, virtual machine (VM) backups scheduled daily, and application-level redundancy for the business logic tier."
- "Configure a site recovery plan for disaster failover from East to West Azure region, replicate major VM workloads, and automate DNS failbacks."
- "Provision a jumpbox VM for secure management, establish NSGs for each tier, and connect tiers using internal Azure Load Balancer."
- "Spin up Linux VMs for each tier using Terraform, automate patch management through Azure Automation, and log traffic between subnets for compliance."
- "Deploy three distinct VM scale sets for a legacy app, route incoming HTTP/S through Application Gateway with Web Application Firewall (WAF), and encrypt all data disks."
- "Set up Azure Backup for critical VM workloads, create a long-term retention policy for compliance, and test backup restores quarterly."
- "Deploy disaster recovery for VMware VMs using Azure Site Recovery, configure runbooks for smooth failover, and maintain compliance audit trails."

## Related content

- [Azure landing zones](/azure/cloud-adoption-framework/ready/landing-zone/)
- [Cloud Adoption Framework](/azure/cloud-adoption-framework/)
- [Enterprise architecture](/azure/architecture/patterns/)
- [Network architecture](/azure/architecture/reference-architectures/dmz/)
- [Azure Model Context Protocol (MCP) Server overview](/azure/developer/azure-mcp-server/overview)
- [Skill source code](https://github.com/microsoft/azure-skills/blob/main/skills/azure-enterprise-infra-planner/SKILL.md)