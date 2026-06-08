---

title: Azure MCP Server tools for Azure Cosmos DB
description: Use Azure MCP Server tools to manage globally distributed, multi-model NoSQL databases with natural language prompts from your IDE.
ms.date: 05/31/2026
ms.service: azure-mcp-server
ms.topic: concept-article
tool_count: 2
mcp-cli.version: 3.0.0-beta.13+cd8d1e8f9924440b33e3e908c390c1599700ccba
author: diberry
ms.author: diberry
ms.reviewer: mbaldwin
ai-usage: ai-generated
ms.custom: build-2025
content_well_notification:
  - AI-contribution
---

# Azure MCP Server tools for Azure Cosmos DB

The Azure MCP Server lets you manage Azure Cosmos DB resources, including: list and query, with natural language prompts.

Azure Cosmos DB is an Azure service that provides cloud-based capabilities for your applications. For more information, see [Azure Cosmos DB documentation](/azure/cosmos/).

[!INCLUDE [tip-about-params](../includes/tools/parameter-consideration.md)]


## Get Cosmos DB accounts

Lists Azure Cosmos DB accounts, databases, or containers. By default, the tool lists all accounts in the subscription. Specify an account to list its databases, or specify an account and a database to list containers in that database.

#### [MCP Server](#tab/mcp-server)

<!-- @mcpcli cosmos list -->

Example prompts include:

- "List all cosmosdb accounts in my subscription."
- "Show me my cosmosdb accounts."
- "Show me the cosmosdb accounts in my subscription."
- "List all the databases in the cosmosdb account 'cosmos-prod'."
- "Show me the databases in the cosmosdb account 'dev-cosmos'."
- "List all the containers in the database 'orders-db' for the cosmosdb account 'cosmos-prod'."
- "Show me the containers in the database 'analytics-db' for the cosmosdb account 'dev-cosmos'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Account name** |  Optional | The name of the Cosmos DB account (optional). When not specified, lists all accounts in the subscription. Specify this to list databases, or combine with `--database` to list containers. |
| **Database name** |  Optional | The name of the database (optional). Requires `--account` to be specified. When provided, lists containers within this database. |

#### [Azure MCP CLI](#tab/azure-mcp-cli)

**Example CLI command**

```console
azmcp cosmos list \
  [--account <account>] \
  [--database <database>]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--account` | string | No | The name of the Cosmos DB account (optional). When not specified, lists all accounts in the subscription. Specify this to list databases, or combine with --database to list containers. |
| `--database` | string | No | The name of the database (optional). Requires --account to be specified. When provided, lists containers within this database. |

---

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌

## Query database container item

Lists items from an Azure Cosmos DB container. Specify the account name, database name, and container name to target the container. Optionally provide a SQL query to filter results and return only the items that match.

#### [MCP Server](#tab/mcp-server)

<!-- @mcpcli cosmos database container item query -->

Example prompts include:

- "List items that contain the word 'invoice' in container 'my-container' in database 'my-database' for account 'my-cosmos-account'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Account name** |  Required | The name of the Cosmos DB account to query (for example, `my-cosmos-account`). |
| **Container name** |  Required | The name of the container to query (for example, `my-container`). |
| **Database name** |  Required | The name of the database to query (for example, `my-database`). |
| **Query** |  Optional | SQL query to execute against the container. Uses Cosmos DB SQL syntax. |

#### [Azure MCP CLI](#tab/azure-mcp-cli)

**Example CLI command**

```console
azmcp cosmos database container item query \
  --account <account> \
  --database <database> \
  --container <container> \
  [--query <query>]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--account` | string | Yes | The name of the Cosmos DB account to query (e.g., my-cosmos-account). |
| `--database` | string | Yes | The name of the database to query (e.g., my-database). |
| `--container` | string | Yes | The name of the container to query (e.g., my-container). |
| `--query` | string | No | SQL query to execute against the container. Uses Cosmos DB SQL syntax. |

---

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌

## Related content

- [What are the Azure MCP Server tools?](index.md)
- [Get started using Azure MCP Server](../get-started.md)
- [Azure Cosmos DB documentation](/azure/cosmos-db/)
