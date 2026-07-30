---
title: Azure skill for Azure Storage
description: Use the Azure Storage skill to get guidance on Blob Storage, File Shares, Queue Storage, Table Storage, and Data Lake for apps and analytics. It's focused on access tiers (hot, cool, cold, archive), helps you pick the right tier, and covers lifecycle management and storage account concepts.
ms.topic: reference
ms.date: 7/30/2026
author: diberry
ms.author: diberry
ms.service: azure-mcp-server
---

# Azure skill for Azure Storage

To get guidance on Blob Storage, File Shares, Queue Storage, Table Storage, and Data Lake for apps and analytics, use the Azure Storage skill. It's focused on access tiers (hot, cool, cold, archive), helps you pick the right tier, and covers lifecycle management and storage account concepts. Use the `azure-storage` skill when you're designing or operating application or analytics workloads that store or move objects, files, or analytic data. Need practical guidance on selecting storage services and access tiers (hot/cool/cold/archive), configuring lifecycle policies, or performing common tasks like uploads/downloads and account setup—it's not intended for SQL/Cosmos databases or Event Hubs/Service Bus messaging.

**Skill** `azure-storage` | [Source code](https://github.com/microsoft/azure-skills/blob/main/skills/azure-storage/SKILL.md)

## What it provides

Get practical guidance for choosing and managing Azure Storage—covering Blob Storage, File Shares, Queue and Table Storage, and Data Lake—so you can upload. Download data, design storage accounts, and implement lifecycle policies. Compare access tiers (hot, cool, cold, archive), learn when to use each, and apply tiering and lifecycle management to optimize cost and performance for apps and analytics.

### Azure services knowledge

| Service | When to use |
|---------|------------|
| Blob Storage | Objects, files, backups, static content |
| File Shares | SMB file shares, lift-and-shift |
| Queue Storage | Async messaging, task queues |
| Table Storage | NoSQL key-value (consider Cosmos DB) |
| Data Lake | Big data analytics, hierarchical namespace |

## Prerequisites

- **Azure authentication**—Sign in with `az login` or use a service principal.
- **Azure subscription**—An active Azure subscription is required.
- **GitHub Copilot**
- **Azure command-line interface (CLI)** (v2.60.0+)—Install: `curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash`
- **Azure Storage account**—Storage account for blob, file, queue, or table data

## When to use this skill

Use the **Azure Storage** skill when you need to:

- Manage and configure blob storage, file shares, queue storage, and table storage in Azure
- Manage and configure data lake in Azure
- Upload files in Azure
- Manage and configure download blobs, storage accounts, access tiers, and storage tiers in Azure
- Manage and configure hot cool cold archive, storage tier comparison, lifecycle management, and Azure Storage concepts in Azure

### When not to use this skill

- Manage and configure SQL databases and Cosmos Db (use `azure-prepare`) in Azure
- Messaging with Event Hubs or Service Bus (use `azure-messaging`)

## Example prompts

Try these prompts to activate this skill:

- "Upload a file to my Azure Blob Storage container"
- "Download a blob from my storage account"
- "List all containers in my Azure Storage account"
- "Set up lifecycle management to move blobs to archive tier"
- "Create a file share in my storage account"
- "What's the difference between hot, cool, and archive access tiers?"
- "Set up a queue storage for async processing"

## Related content

- [Azure Storage overview](/azure/storage/)
- [Azure Storage quickstart](/azure/storage/common/storage-quickstart-blobs-portal)
- [Azure Storage pricing](https://azure.microsoft.com/pricing/details/storage/)
- [Storage best practices](/azure/storage/common/storage-best-practices)
- [Azure Model Context Protocol (MCP) Server overview](/azure/developer/azure-mcp-server/overview)
- [Skill source code](https://github.com/microsoft/azure-skills/blob/main/skills/azure-storage/SKILL.md)