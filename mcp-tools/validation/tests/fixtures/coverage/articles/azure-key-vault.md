---
title: Azure Key Vault tools for the Azure MCP Server
description: Use Azure Key Vault tools in the Azure MCP Server.
ms.date: 05/13/2026
ms.custom: devx-track-azurecli, mcp
ms.reviewer: diberry
mcp-cli.version: 3.0.0-beta.10
---

# Azure Key Vault tools

Use the Azure Key Vault tools to manage secrets, keys, and certificates.

## Get a secret

<!-- keyvault secret get -->

Get a secret from a Key Vault.

Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅

### Parameters

| Parameter | Required or optional | Description |
|-----------|----------------------|-------------|
| **Vault name** | Required | The name of the Key Vault. |
