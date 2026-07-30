---
title: Azure skill for Azure Messaging
description: This skill helps you troubleshoot Azure Messaging SDK issues for Event Hubs and Service Bus, resolving connection, authentication, and message-processing failures. Use it when you're facing AMQP errors, lock or session problems, timeouts or disconnects, duplicate events, or when you need logging and SDK configuration guidance.
ms.topic: reference
ms.date: 7/30/2026
author: diberry
ms.author: diberry
ms.service: azure-mcp-server
---

# Azure skill for Azure Messaging

This skill helps you troubleshoot Azure Messaging SDK issues for Event Hubs and Service Bus, resolving connection, authentication, and message-processing failures. Use it when you're facing `AMQP` errors, lock or session problems, timeouts or disconnects, duplicate events, or when you need logging and SDK configuration guidance. Use `azure-messaging` when you're troubleshooting Event Hubs or Service Bus SDK problems—such as `AMQP`/link errors, connection or authentication failures, send/receive timeouts or disconnects, message lock/session/renewal. Checkpointing failures, duplicate or missing events, or when you need SDK-specific logging and configuration guidance across languages.

**Skill** `azure-messaging` | [Source code](https://github.com/microsoft/azure-skills/blob/main/skills/azure-messaging/SKILL.md)

## What it provides

Help you diagnose and resolve Azure Messaging SDK issues for Event Hubs and Service Bus, including connection and authentication failures, `AMQP`, session. Link errors, message lock/renewal problems, and send/receive timeouts or disconnects. You get targeted, actionable guidance for duplicate events, checkpoint/offset and dead‑letter scenarios, enabling SDK logging, and configuring retries, timeouts, and batching across Python, Java, JavaScript, and .NET.

## Prerequisites

- **Azure authentication**—Sign in with `az login` or use a service principal.
- **Azure subscription**—An active Azure subscription is required.
- **GitHub Copilot**
- **Azure command-line interface (CLI)** (v2.60.0+)—Install: `curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash`

## When to use this skill

Use the **Azure Messaging** skill when you need to:

- Manage and configure event hub SDK error, service bus SDK issue, messaging connection failure, and `Amqp` error in Azure
- Manage and configure event processor host issue, message lock lost, message lock expired, and lock renewal in Azure
- Manage and configure lock renewal batch, send timeout, receiver disconnected, and SDK troubleshooting in Azure
- Manage and configure azure messaging SDK, event hub consumer, service bus queue issue, and topic subscription error in Azure
- Enable logging event hub
- Manage and configure service bus logging, `eventhub` python, `servicebus` java, and `eventhub` javascript in Azure
- Manage and configure `servicebus` dotnet and event hub checkpoint in Azure
- Event hub not receiving messages
- Manage and configure service bus dead letter, batch processing lock, session lock expired, and idle timeout in Azure
- Manage and configure connection inactive, link detach, slow reconnect, and session error in Azure

## Example prompts

Try these prompts to activate this skill:

- "event hub SDK error in my Python app"
- "my event hub consumer isn't receiving messages"
- "event hub checkpoint store failing"
- "`eventhub` python connection timeout"
- "`eventhub` javascript client disconnects"
- "service bus SDK issue with message lock lost"
- "service bus queue issue with dead letter"
- "`servicebus` java send timeout"

## Related content

- [Azure Service Bus](/azure/service-bus-messaging/)
- [Azure Event Hubs](/azure/event-hubs/)
- [Messaging services pricing](https://azure.microsoft.com/pricing/details/service-bus/)
- [Messaging best practices](/azure/architecture/reference-architectures/messaging/)
- [Azure Model Context Protocol (MCP) Server overview](/azure/developer/azure-mcp-server/overview)
- [Skill source code](https://github.com/microsoft/azure-skills/blob/main/skills/azure-messaging/SKILL.md)