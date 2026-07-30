---
title: Azure skill for Azure Kusto (Data Explorer)
description: Use this skill to query and analyze telemetry, logs, and time series in Azure Data Explorer (Kusto/ADX) using Kusto Query Language (KQL). You're able to spot anomalies, investigate incidents, and extract insights from IoT and application telemetry.
ms.topic: reference
ms.date: 7/30/2026
author: diberry
ms.author: diberry
ms.service: azure-mcp-server
---

# Azure skill for Azure Kusto (Data Explorer)

To query and analyze telemetry, logs, and time series in Azure Data Explorer (Kusto/ADX) using Kusto Query Language (KQL), use this skill. You're able to spot anomalies, investigate incidents, and extract insights from IoT and application telemetry. Use the `azure-kusto` skill when you need to run Kusto (KQL) queries against Azure Data Explorer to analyze time-series and log telemetry—such as detecting anomalies, investigating incidents. Extracting operational insights from IoT and application data.

**Skill** `azure-kusto` | [Source code](https://github.com/microsoft/azure-skills/blob/main/skills/azure-kusto/SKILL.md)

## What it provides

With `azure-kusto`, you can run Kusto Query Language (KQL) against Azure Data Explorer and Log Analytics to analyze telemetry, logs, and time-series data from ADX clusters, IoT devices, and applications. Quickly surface anomalies, investigate incidents, and extract trends and summaries to drive diagnostics, monitoring, and operational decisions.

## Prerequisites

- **Azure authentication**—Sign in with `az login` or use a service principal.
- **Azure subscription**—An active Azure subscription is required.
- **GitHub Copilot**
- **Azure command-line interface (CLI)** (v2.60.0+)—Install: `curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash`

## When to use this skill

Use the **Azure Kusto (Data Explorer)** skill when you need to:

- Manage and configure KQL queries, Kusto database queries, Azure Data Explorer, and ADX clusters in Azure
- Manage and configure log analytics, time series data, IoT telemetry, and anomaly detection in Azure

## Example prompts

Try these prompts to activate this skill:

- "Query my Kusto database for events in the last hour"
- "Write a KQL query to analyze telemetry data from my ADX cluster"
- "Show me the schema for tables in my Azure Data Explorer cluster"
- "Analyze IoT sensor data for anomalies in the last 24 hours"
- "Create a time series chart of application logs from my Kusto database"
- "What tables are available in my Azure Data Explorer cluster?"
- "Aggregate request latency metrics by service using KQL"

## Related content

- [Azure Data Explorer (Kusto) overview](/azure/data-explorer/)
- [Kusto Query Language (KQL)](/azure/data-explorer/kusto/query/)
- [Azure Data Explorer pricing](https://azure.microsoft.com/pricing/details/data-explorer/)
- [Log Analytics Workspace queries](/azure/azure-monitor/logs/log-analytics-tutorial)
- [Azure Model Context Protocol (MCP) Server overview](/azure/developer/azure-mcp-server/overview)
- [Skill source code](https://github.com/microsoft/azure-skills/blob/main/skills/azure-kusto/SKILL.md)