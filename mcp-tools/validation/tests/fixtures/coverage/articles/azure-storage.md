---
title: Azure Storage tools for the Azure MCP Server
description: Use Azure Storage tools in the Azure MCP Server.
ms.date: 05/13/2026
ms.custom: devx-track-azurecli, mcp
ms.reviewer: diberry
mcp-cli.version: 3.0.0-beta.10
---

# Azure Storage tools

Use the Azure Storage tools to manage blobs, accounts, and containers.

## List storage accounts

<!-- storage account list -->

List all storage accounts in the subscription.

Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅

### Parameters

| **Account name** | Optional | The storage account name to filter by |

## Upload a blob

<!-- storage blob upload -->

Upload a blob to a container.

Destructive: ✅ | Idempotent: ❌ | Open World: ❌ | Read Only: ❌

### Parameters

| **Resource group** | Required | The resource group containing the storage account |
| **Account name** | Required | The name of the storage account |
| **Container name** | Required | The name of the container |
| **Blob name** | Required | The name of the blob to upload |
