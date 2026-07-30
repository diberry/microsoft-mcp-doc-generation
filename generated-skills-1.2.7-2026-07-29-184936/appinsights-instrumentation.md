---
title: Azure skill for App Insights Instrumentation
description: Azure skill for appinsights-instrumentation

This skill helps you instrument web apps with Azure Application Insights, so you collect telemetry for diagnostics and performance monitoring. You get guidance on the Application Insights SDK, common telemetry patterns, configuration options, and application performance monitoring (APM) best practices.
ms.topic: reference
ms.date: 7/30/2026
author: diberry
ms.author: diberry
ms.service: azure-mcp-server
---

# Azure skill for App Insights Instrumentation

Azure skill for appinsights-instrumentation

This skill helps you instrument web apps with Azure Application Insights, so you collect telemetry for diagnostics and performance monitoring. You get guidance on the Application Insights SDK, common telemetry patterns, configuration options, and application performance monitoring (APM) best practices. Reach for the appinsights-instrumentation skill when you need to add or improve Application Insights telemetry in a web app — for example, when onboarding or configuring the SDK, implementing reliable telemetry patterns, or diagnosing. Optimizing performance using APM best practices.

**Skill** `appinsights-instrumentation` | [Source code](https://github.com/microsoft/azure-skills/blob/main/skills/appinsights-instrumentation/SKILL.md)

## What it provides

You get clear, actionable guidance to instrument your web apps with Azure Application Insights, including how to add the Application Insights SDK. Capture telemetry such as requests, dependencies, errors, logs, and custom metrics for diagnostics and performance monitoring. The skill also provides common telemetry patterns, configuration options, instrumentation examples, and APM best practices—like linking requests across services and tuning data collection—so you can quickly troubleshoot issues. Improve app performance.

## Prerequisites

- **Azure authentication**—Sign in with `az login` or use a service principal.
- **Azure subscription**—An active Azure subscription is required.
- **GitHub Copilot**
- **PowerShell** (v7.4+)—Install: `winget install Microsoft.PowerShell`
- **Azure command-line interface (CLI) with Bicep** (v2.60.0+)—Install: `az bicep install`

### Environment requirements

- An ASP.NET Core app hosted in Azure
- A Node.js app hosted in Azure

## When to use this skill

Use the **App Insights Instrumentation** skill when you need to:

- Manage and configure App Insights SDK, telemetry patterns, Application Insights guidance, and instrumentation examples in Azure
- Manage and configure Apm best practices in Azure

## Example prompts

Try these prompts to activate this skill:

- "How to instrument app?"
- "How do I work with app Insights SDK?"
- "How do I work with telemetry patterns?"
- "What is App Insights?"
- "How do I work with application Insights guidance?"
- "How do I work with instrumentation examples?"
- "How do I work with apm best practices?"

## Related content

- [Application Insights overview](/azure/azure-monitor/app/app-insights-overview)
- [Application Insights quickstart](/azure/azure-monitor/app/app-insights-overview)
- [Instrumentation guides](/azure/azure-monitor/app/codeless-overview)
- [Application Insights best practices](/azure/azure-monitor/app/best-practices)
- [Azure Model Context Protocol (MCP) Server overview](/azure/developer/azure-mcp-server/overview)
- [Skill source code](https://github.com/microsoft/azure-skills/blob/main/skills/appinsights-instrumentation/SKILL.md)