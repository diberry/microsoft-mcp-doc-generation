---
title: Azure skill for Entra App Registration
description: This skill guides Microsoft Entra ID app registration and OAuth 2.0 setup, so you can secure app authentication and authorization. You'll learn how to add API permissions, create service principals, and integrate the Microsoft Authentication Library (MSAL) for console and web app scenarios.
ms.topic: reference
ms.date: 7/30/2026
author: diberry
ms.author: diberry
ms.service: azure-mcp-server
---

# Azure skill for Entra App Registration

This skill guides Microsoft Entra ID app registration and OAuth 2.0 setup, so you can secure app authentication and authorization. You'll learn how to add API permissions, create service principals, and integrate the Microsoft Authentication Library (MSAL) for console and web app scenarios. Use this skill when you need to register an app in Microsoft Entra ID and configure OAuth 2.0 authentication and authorization—such as assigning API permissions, creating service principals. Integrating MSAL for web or console apps to obtain tokens—rather than for Key Vault secret management or general Azure resource security.

**Skill** `entra-app-registration` | [Source code](https://github.com/microsoft/azure-skills/blob/main/skills/entra-app-registration/SKILL.md)

## What it provides

Guides you through Microsoft Entra ID app registration and OAuth 2.0 configuration so you can secure authentication and authorization for your applications. You get step-by-step instructions to create app registrations and service principals, add API permissions, and use MSAL examples for both console and web app scenarios.

## Prerequisites

- **Azure authentication**—Sign in with `az login` or use a service principal.
- **Azure subscription**—An active Azure subscription is required.
- **GitHub Copilot**
- **Azure command-line interface (CLI) with Bicep** (v2.60.0+)—Install: `az bicep install`

## When to use this skill

Use the **Entra App Registration** skill when you need to:

- Create app registration in Azure
- Manage and configure register Microsoft Entra ID app in Azure
- Configure OAuth in Azure
- Set up authentication in Azure
- Add API permissions in Azure
- Manage and configure generate service principal, MSAL example, console app auth, and Entra ID setup in Azure
- Manage and configure Microsoft Entra ID authentication in Azure

### When not to use this skill

- Key Vault secrets (use `azure-keyvault-expiration-audit`)
- General Azure resource security guidance

## Example prompts

Try these prompts to activate this skill:

- "How do I create an app registration in Azure?"
- "Register a Microsoft Entra ID app for my web application"
- "Configure OAuth authentication for my application"
- "Set up authentication with Microsoft Entra ID"
- "Add API permissions to my Entra app registration"
- "Generate a service principal for Azure authentication"
- "Show me MSAL examples for Microsoft Entra ID authentication."
- "Create a console app with Microsoft Entra ID authentication"

## Related content

- [Microsoft Entra ID](/entra/identity-platform/)
- [App registration](/entra/identity-platform/quickstart-register-app)
- [Authentication and authorization](/entra/identity-platform/authentication-vs-authorization)
- [Security best practices](/entra/identity-platform/security-best-practices-for-app-registration)
- [Azure Model Context Protocol (MCP) Server overview](/azure/developer/azure-mcp-server/overview)
- [Skill source code](https://github.com/microsoft/azure-skills/blob/main/skills/entra-app-registration/SKILL.md)