---

title: Azure MCP Server tools for Azure Storage
description: Use Azure MCP Server tools to manage Azure Storage resources.
ms.service: azure-mcp-server
ms.topic: concept-article
tool_count: 1
author: diberry
ms.author: diberry
ms.date: 05/15/2026

---

# Azure MCP Server tools for Azure Storage

The Azure MCP Server lets you manage Azure Storage resources.


## Account list

Lists all storage accounts in the subscription.

#### [Azure MCP CLI](#tab/azure-mcp-cli)

**Example CLI command**

```console
azmcp storage account list \
  --subscription <subscription>
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--subscription` | string | Yes | The Azure subscription ID. |

#### [MCP Server](#tab/mcp-server)

<!-- @mcpcli storage account list -->

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Subscription** |  Required | The Azure subscription ID. |

---

## Related content

- [What are the Azure MCP Server tools?](index.md)
