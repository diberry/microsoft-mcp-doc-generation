---

title: Azure MCP Server tools for Azure Storage
description: Use Azure MCP Server tools to manage Azure Storage resources such as storage accounts, blob containers, blobs, and tables with natural language prompts from your IDE.
ms.date: 05/31/2026
ms.service: azure-mcp-server
ms.topic: concept-article
tool_count: 7
mcp-cli.version: 3.0.0-beta.13+cd8d1e8f9924440b33e3e908c390c1599700ccba
author: diberry
ms.author: diberry
ms.reviewer: mbaldwin
ai-usage: ai-generated
ms.custom: build-2025
content_well_notification:
  - AI-contribution
---

# Azure MCP Server tools for Azure Storage

The Azure MCP Server lets you manage Azure Storage resources, including: create, get, list, and upload, with natural language prompts.

Azure Storage is an Azure service that provides cloud-based capabilities for your applications. For more information, see [Azure Storage documentation](/azure/storage/).

[!INCLUDE [tip-about-params](../includes/tools/parameter-consideration.md)]


## Account: Create

Creates an Azure Storage account in the specified resource group and location. Returns the storage account's properties, including name, location, SKU, access tier, replication, network settings, primary endpoints, access keys, and configuration details.

#### [MCP Server](#tab/mcp-server)

<!-- @mcpcli storage account create -->

Example prompts include:

- "Create a new storage account with account name 'testaccount123' in location 'eastus' and resource group 'rg-prod'."
- "Create a storage account with account name 'premiumacct01' location 'westus2' resource group 'rg-prod' SKU 'Premium_LRS'."
- "Create a new storage account with account name 'datalakeacct' location 'eastus2' resource group 'rg-datalake' enable hierarchical namespace 'true'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Account name** |  Required | The name of the Azure Storage account to create. Must be globally unique, 3-24 characters, lowercase letters and numbers only. |
| **Location** |  Required | The Azure region where the storage account is created (for example, `'eastus'`, `'westus2'`). |
| **Resource group** |  Required | The name of the Azure resource group. This resource group is a logical container for Azure resources. |
| **Access tier** |  Optional | The default access tier for blob storage. Valid values: `Hot`, `Cool`. |
| **Enable hierarchical namespace** |  Optional | Whether to enable hierarchical namespace (Data Lake Storage Gen2) for the storage account. |
| **SKU** |  Optional | The storage account SKU. Valid values: `Standard_LRS`, `Standard_GRS`, `Standard_RAGRS`, `Standard_ZRS`, `Premium_LRS`, `Premium_ZRS`, `Standard_GZRS`, `Standard_RAGZRS`. |



Examples

- "Create storage account 'mystorageacct' in resource group 'rg-prod' in location 'eastus' with SKU 'Standard_LRS'."
- "Create storage account 'mystorageft' in resource group 'rg-backup' in location 'westus2' with replication 'GeoZRS' and access tier 'Hot'."

#### [Azure MCP CLI](#tab/azure-mcp-cli)

**Example CLI command**

```console
azmcp storage account create \
  --account <account> \
  --location <location> \
  --resource-group <resource-group> \
  [--sku <sku>] \
  [--access-tier <access-tier>] \
  [--enable-hierarchical-namespace <enable-hierarchical-namespace>]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--account` | string | Yes | The name of the Azure Storage account to create. Must be globally unique, 3-24 characters, lowercase letters and numbers only. |
| `--location` | string | Yes | The Azure region where the storage account will be created (e.g., 'eastus', 'westus2'). |
| `--sku` | string | No | The storage account SKU. Valid values: Standard_LRS, Standard_GRS, Standard_RAGRS, Standard_ZRS, Premium_LRS, Premium_ZRS, Standard_GZRS, Standard_RAGZRS. |
| `--access-tier` | string | No | The default access tier for blob storage. Valid values: Hot, Cool. |
| `--enable-hierarchical-namespace` | string | No | Whether to enable hierarchical namespace (Data Lake Storage Gen2) for the storage account. |
| `--resource-group` | string | Yes | The name of the Azure resource group. This is a logical container for Azure resources. |

---

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ✅ | Idempotent: ❌ | Open World: ❌ | Read Only: ❌ | Secret: ❌ | Local Required: ❌

## Account: Get details

Retrieves detailed properties for one or more Azure Storage accounts, including account name, location, SKU, kind, hierarchical namespace status, HTTPS-only enforcement, and blob public access configuration. If no account name is provided, the command returns details for all storage accounts in the subscription.

#### [MCP Server](#tab/mcp-server)

<!-- @mcpcli storage account get -->

Example prompts include:

- "Show details for storage account 'mystorageacct' including location, SKU, and kind."
- "Get properties of storage account 'companydata2024', including hierarchical namespace and HTTPS-only status."
- "List all storage accounts in my subscription and include location and SKU."
- "Show whether hierarchical namespace is enabled for storage account 'fileshareprod'."
- "Show all storage accounts in my subscription with HTTPS-only and public blob access settings."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Account name** |  Optional | The name of the Azure Storage account. This is the unique name you chose for your storage account (for example, `'mystorageaccount'`). |

#### [Azure MCP CLI](#tab/azure-mcp-cli)

**Example CLI command**

```console
azmcp storage account get \
  [--account <account>] \
  [--resource-group <resource-group>]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--account` | string | No | The name of the Azure Storage account. This is the unique name you chose for your storage account (e.g., 'mystorageaccount'). |
| `--resource-group` | string | No | The name of the Azure resource group. This is a logical container for Azure resources. |

---

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌

## Container: Create container

Creates a new Azure Storage blob container in a storage account. Returns the container name, lastModified, eTag, leaseStatus, publicAccessLevel, hasImmutabilityPolicy, and hasLegalHold. Creates a logical container for organizing blobs within the storage account.

#### [MCP Server](#tab/mcp-server)

<!-- @mcpcli storage blob container create -->

Example prompts include:

- "Create the storage container 'mycontainer' in storage account 'mystorageaccount'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Account name** |  Required | The name of the Azure Storage account. This is the unique name you chose for your storage account (for example, `'mystorageaccount'`). |
| **Container name** |  Required | The name of the container to access within the storage account. |

#### [Azure MCP CLI](#tab/azure-mcp-cli)

**Example CLI command**

```console
azmcp storage blob container create \
  --account <account> \
  --container <container>
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--account` | string | Yes | The name of the Azure Storage account. This is the unique name you chose for your storage account (e.g., 'mystorageaccount'). |
| `--container` | string | Yes | The name of the container to access within the storage account. |

---

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ✅ | Idempotent: ❌ | Open World: ❌ | Read Only: ❌ | Secret: ❌ | Local Required: ❌

## Container: Get container details

Lists blob containers in a storage account. Shows details for a specific container when the container name is provided. When no container is specified, lists all containers and can filter results by prefix. If a container is specified, the prefix is ignored.

#### [MCP Server](#tab/mcp-server)

<!-- @mcpcli storage blob container get -->

Example prompts include:

- "Show the properties of container 'app-data' in storage account 'mystorageaccount'."
- "List all blob containers in storage account 'mystorageaccount'."
- "List all blob containers in storage account 'mystorageaccount' with prefix 'logs'."
- "What containers are in storage account 'companydata2024'?"

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Account name** |  Required | The name of the Azure Storage account. This is the unique name you chose for your storage account (for example, `'mystorageaccount'`). |
| **Container name** |  Optional | The name of the container to access within the storage account. |
| **Prefix** |  Optional | The prefix to filter containers when listing containers in a storage account. Only containers whose names start with the specified prefix is listed. |

#### [Azure MCP CLI](#tab/azure-mcp-cli)

**Example CLI command**

```console
azmcp storage blob container get \
  --account <account> \
  [--container <container>] \
  [--prefix <prefix>]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--account` | string | Yes | The name of the Azure Storage account. This is the unique name you chose for your storage account (e.g., 'mystorageaccount'). |
| `--container` | string | No | The name of the container to access within the storage account. |
| `--prefix` | string | No | The prefix to filter containers when listing containers in a storage account. Only containers whose names start with the specified prefix will be listed. |

---

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌

## Blob: Get blob details

Lists blobs in a container or gets details for a specific blob in an Azure Storage account. If no blob is specified, lists all blobs in the container and optionally filters by `prefix`. When a `blob` is specified, the `prefix` is ignored.

#### [MCP Server](#tab/mcp-server)

<!-- @mcpcli storage blob get -->

Example prompts include:

- "List all blobs in container 'images' in storage account 'mystorageacct'."
- "Show properties for blob 'folder/file.txt' in container 'backups' in storage account 'companydata2024'."
- "Get details for blob 'logs/2026-05-31.log' in container 'prod-logs' in storage account 'acmeblobstore'."
- "List blobs in container 'media' in storage account 'mediastorage' with prefix 'images/'."
- "What blobs are in container 'site-assets' in storage account 'webstore'?"

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Account name** |  Required | The name of the Azure Storage account. This is the unique name you chose for your storage account (for example, `'mystorageaccount'`). |
| **Container name** |  Required | The name of the container to access within the storage account. |
| **Blob name** |  Optional | The name of the blob to access within the container. This should be the full path within the container (for example, `'file.txt'` or 'folder/file.txt'). |
| **Prefix** |  Optional | The prefix to filter blobs when listing blobs in a container. Only blobs whose names start with the specified prefix is listed. |

#### [Azure MCP CLI](#tab/azure-mcp-cli)

**Example CLI command**

```console
azmcp storage blob get \
  --account <account> \
  --container <container> \
  [--blob <blob>] \
  [--prefix <prefix>]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--blob` | string | No | The name of the blob to access within the container. This should be the full path within the container (e.g., 'file.txt' or 'folder/file.txt'). |
| `--prefix` | string | No | The prefix to filter blobs when listing blobs in a container. Only blobs whose names start with the specified prefix will be listed. |
| `--account` | string | Yes | The name of the Azure Storage account. This is the unique name you chose for your storage account (e.g., 'mystorageaccount'). |
| `--container` | string | Yes | The name of the container to access within the storage account. |

---

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌

## Blob: Upload

Uploads a local file to an Azure Storage blob if the blob doesn't already exist. Returns the blob's last modified time, ETag, and content hash to help verify the upload.

#### [MCP Server](#tab/mcp-server)

<!-- @mcpcli storage blob upload -->

Example prompts include:

- "Upload local file '/home/alice/report.pdf' to blob 'reports/2026/report.pdf' in container 'backup-data' in storage account 'mystorageaccount'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Account name** |  Required | The name of the Azure Storage account. This is the unique name you chose for your storage account (for example, `'mystorageaccount'`). |
| **Blob name** |  Required | The name of the blob to access within the container. This should be the full path within the container (for example, `'file.txt'` or 'folder/file.txt'). |
| **Container name** |  Required | The name of the container to access within the storage account. |
| **Local file path** |  Required | The local file path to read content from or to write content to. This should be the full path to the file on your local system. |

#### [Azure MCP CLI](#tab/azure-mcp-cli)

**Example CLI command**

```console
azmcp storage blob upload \
  --local-file-path <local-file-path> \
  --account <account> \
  --container <container> \
  --blob <blob>
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--local-file-path` | string | Yes | The local file path to read content from or to write content to. This should be the full path to the file on your local system. |
| `--account` | string | Yes | The name of the Azure Storage account. This is the unique name you chose for your storage account (e.g., 'mystorageaccount'). |
| `--container` | string | Yes | The name of the container to access within the storage account. |
| `--blob` | string | Yes | The name of the blob to access within the container. This should be the full path within the container (e.g., 'file.txt' or 'folder/file.txt'). |

---

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ❌ | Idempotent: ❌ | Open World: ❌ | Read Only: ❌ | Secret: ❌ | Local Required: ✅

## Table: List

Lists all tables in an Azure Storage account. Returns the table names for the specified storage account. It doesn't list tables in Azure Cosmos DB or Azure Data Explorer.

#### [MCP Server](#tab/mcp-server)

<!-- @mcpcli storage table list -->

Example prompts include:

- "List all tables in the storage account 'mystorageaccount'."
- "Show me the tables in the storage account 'companydata2024'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Account name** |  Required | The name of the Azure Storage account. This is the unique name you chose for your storage account (for example, `'mystorageaccount'`). |

#### [Azure MCP CLI](#tab/azure-mcp-cli)

**Example CLI command**

```console
azmcp storage table list \
  --account <account>
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--account` | string | Yes | The name of the Azure Storage account. This is the unique name you chose for your storage account (e.g., 'mystorageaccount'). |

---

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌

## Related content

- [What are the Azure MCP Server tools?](index.md)
- [Get started using Azure MCP Server](../get-started.md)
- [Azure Storage documentation](/azure/storage/)
