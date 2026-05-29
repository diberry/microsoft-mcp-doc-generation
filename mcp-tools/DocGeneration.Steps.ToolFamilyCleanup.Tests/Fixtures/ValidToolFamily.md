---

title: Azure MCP Server tools for Azure Backup
description: Use Azure MCP Server tools to manage Azure Backup resources with natural language prompts from your IDE.
ms.service: azure-mcp-server
ms.topic: concept-article
tool_count: 2
mcp-cli.version: 3.0.0-beta.10+abc123
author: diberry
ms.author: diberry
ms.date: 05/15/2026
ai-usage: ai-generated

---

# Azure MCP Server tools for Azure Backup

The Azure MCP Server lets you manage Azure Backup resources with natural language prompts.

[!INCLUDE [tip-about-params](../includes/tools/parameter-consideration.md)]


## Create vault

Creates a new backup vault.

#### [Azure MCP CLI](#tab/azure-mcp-cli)

**Example CLI command**

```console
azmcp azurebackup vault create \
  --resource-group <resource-group> \
  --vault <vault> \
  --location <location> \
  [--vault-type <vault-type>]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--resource-group` | string | Yes | The name of the Azure resource group. |
| `--vault` | string | Yes | The name of the backup vault. |
| `--location` | string | Yes | The Azure region. |
| `--vault-type` | string | No | The type of backup vault: 'rsv' or 'dpp'. |

#### [MCP Server](#tab/mcp-server)

<!-- @mcpcli azurebackup vault create -->

Example prompts include:

- "Create vault 'rsv-backup-prod' in resource group 'rg-prod' in location 'eastus'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Location** |  Required | The Azure region. |
| **Resource group** |  Required | The name of the Azure resource group. |
| **Vault name** |  Required | The name of the backup vault. |
| **Vault type** |  Optional | The type of backup vault: 'rsv' or 'dpp'. |

---

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ✅ | Idempotent: ❌ | Open World: ❌ | Read Only: ❌ | Secret: ❌ | Local Required: ❌

## List policies

Lists all protection policies in the specified vault.

#### [Azure MCP CLI](#tab/azure-mcp-cli)

**Example CLI command**

```console
azmcp azurebackup policy list \
  --vault-name <vault-name> \
  --resource-group <resource-group>
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--vault-name` | string | Yes | The name of the Recovery Services vault |
| `--resource-group` | string | Yes | The resource group containing the vault |

#### [MCP Server](#tab/mcp-server)

<!-- @mcpcli azurebackup policy list -->

Example prompts include:

- "List all backup policies in my vault"

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Vault name** |  Required | The name of the Recovery Services vault |
| **Resource group** |  Required | The resource group containing the vault |

---

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌

## Related content

- [What are the Azure MCP Server tools?](index.md)
- [Get started using Azure MCP Server](../get-started.md)
