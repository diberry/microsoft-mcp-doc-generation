---
title: Azure skill for Azure App Onboard Prereq
description: This skill checks your repository to see if your app's code, dependencies, and configuration are ready for Azure deployment before any infrastructure work. It evaluates build health, runtime and local service needs, stack compatibility, and deployment blockers, and tells you what to fix or add.
ms.topic: reference
ms.date: 7/30/2026
author: diberry
ms.author: diberry
ms.service: azure-mcp-server
---

# Azure skill for Azure App Onboard Prereq

This skill checks your repository to see if your app's code, dependencies, and configuration are ready for Azure deployment before any infrastructure work. It evaluates build health, runtime and local service needs, stack compatibility, and deployment blockers, and tells you what to fix or add. Use the `azure-app-onboard-prereq` skill before you provision infrastructure or start a deployment when you need a repo-level readiness check that identifies missing artifacts (for example a Dockerfile or required configs), build/runtime or dependency compatibility issues. Any other blockers you must fix to successfully deploy to Azure.

**Skill** `azure-app-onboard-prereq` | [Source code](https://github.com/microsoft/azure-skills/blob/main/skills/azure-app-onboard-prereq/SKILL.md)

## What it provides

Scans your repository and tells you whether your app is ready to deploy to Azure by evaluating build health, dependency compatibility, required runtimes or local services, and configuration. It highlights any deployment blockers and gives clear, prioritized recommendations—such as needing a Dockerfile, service bindings, framework support, or dependency updates—so you know exactly what to fix before deploying.

## Prerequisites

- **Azure authentication**—Sign in with `az login` or use a service principal.
- **Azure subscription**—An active Azure subscription is required.
- **GitHub Copilot**
- **Node.js** (vLTS+)—Install: `https://nodejs.org`

## When to use this skill

Use the **Azure App Onboard Prereq** skill when you need to:

- Evaluate my repo in Azure
- Scan my repo for issues
- Check if my app is ready for Azure
- Check my app configuration

## Example prompts

Try these prompts to activate this skill:

- "How do I evaluate my repo?"
- "Is my app ready to deploy?"
- "What does my app need to deploy?"
- "What do I need before deploying?"
- "Does my app need?"
- "Can I ship this to Azure?"
- "How do I scan my repo for issues?"
- "Is this app deployable?"

## Related content

- [Azure Model Context Protocol (MCP) Server overview](/azure/developer/azure-mcp-server/overview)
- [Skill source code](https://github.com/microsoft/azure-skills/blob/main/skills/azure-app-onboard-prereq/SKILL.md)