---
title: Azure skill for Azure Compliance
description: Azure-compliance runs compliance and security audits with azqr and Key Vault expiration checks. You won't miss expired certificates, expiring secrets, orphaned resources, or policy and best-practice gaps before deployment.
ms.topic: reference
ms.date: 7/30/2026
author: diberry
ms.author: diberry
ms.service: azure-mcp-server
---

# Azure skill for Azure Compliance

Azure-compliance runs compliance and security audits with `azqr` and Key Vault expiration checks. You won't miss expired certificates, expiring secrets, orphaned resources, or policy and best-practice gaps before deployment. Use `azure-compliance` when you need a pre-deployment or routine compliance and security audit—especially before running `azqr`—to detect expiring or expired Key Vault items, orphaned resources, and policy or best-practice gaps.

**Skill** `azure-compliance` | [Source code](https://github.com/microsoft/azure-skills/blob/main/skills/azure-compliance/SKILL.md)

## What it provides

To help you remediate risks and stay deployment-ready, run compliance and security audits with `azqr` and perform Key Vault expiration checks so you don’t miss expired certificates, expiring secrets, orphaned resources, or policy. Best-practice gaps before deployment. Get actionable compliance assessments and prioritized findings.

## Prerequisites

- **Azure authentication**—Sign in with `az login` or use a service principal.
- **Azure subscription**—An active Azure subscription is required.
- **GitHub Copilot**
- **Azure command-line interface (CLI)** (v2.60.0+)—Install: `curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash`
- **Azure Key Vault**—Key vault for secrets and certificate management

## When to use this skill

Use the **Azure Compliance** skill when you need to:

- Manage and configure compliance scan and security audit in Azure
- Before running `azqr` (compliance cli tool)
- Manage and configure Azure best practices, Key Vault expiration check, expired certificates, and expiring secrets in Azure
- Manage and configure orphaned resources and compliance assessment in Azure

## Example prompts

Try these prompts to activate this skill:

- "Run `azqr` to check Azure compliance"
- "Check my Azure subscription for compliance issues"
- "Perform compliance assessment using Azure Quick Review"
- "Assess my Azure resources against best practices"
- "Review my Azure security posture"
- "Run compliance scan on my Azure subscription"
- "Identify orphaned resources in Azure"
- "Find resources that don't comply with best practices"

## Related content

- [Azure compliance offerings](/azure/compliance/)
- [Compliance assessment](/azure/governance/policy/overview)
- [Azure Policy](/azure/governance/policy/)
- [Security and compliance](/azure/security/fundamentals/overview)
- [Azure Model Context Protocol (MCP) Server overview](/azure/developer/azure-mcp-server/overview)
- [Skill source code](https://github.com/microsoft/azure-skills/blob/main/skills/azure-compliance/SKILL.md)