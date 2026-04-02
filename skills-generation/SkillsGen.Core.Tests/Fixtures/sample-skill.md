---
name: azure-storage
display_name: Azure Storage
description: "Work with Azure Storage accounts, blobs, queues, tables, and file shares. USE FOR: creating storage accounts, managing blobs, configuring access policies, monitoring storage metrics, troubleshooting connectivity. DO NOT USE FOR: Azure Cosmos DB operations, Azure SQL Database queries, non-Azure storage providers."
---

# Azure Storage

The Azure Storage skill helps you manage Azure Storage accounts and their sub-resources including blob containers, file shares, queues, and tables.

## Use for

- Creating and configuring storage accounts
- Managing blob containers and objects
- Setting up shared access signatures (SAS)
- Configuring storage access policies
- Monitoring storage metrics and diagnostics

## Do not use for

- Azure Cosmos DB operations
- Azure SQL Database management
- Non-Azure storage providers

## Services

| Service | UseWhen | McpTools | Cli |
|---------|---------|----------|-----|
| Azure Blob Storage | Store unstructured data like documents, images, videos | storage_blob_list, storage_blob_upload | az storage blob |
| Azure File Shares | Share files across applications using SMB protocol | storage_file_list | az storage file |
| Azure Queue Storage | Decouple components with message queuing | storage_queue_list | az storage queue |
| Azure Table Storage | Store structured NoSQL data | - | az storage table |

## MCP Tools

| Tool | Command | Purpose | ToolPage |
|------|---------|---------|----------|
| storage_account_list | storage account list | List all storage accounts in a subscription | /azure/storage/account-list |
| storage_blob_list | storage blob list | List blobs in a container | /azure/storage/blob-list |
| storage_blob_upload | storage blob upload | Upload a file to blob storage | /azure/storage/blob-upload |
| storage_file_list | storage file list | List files in a share | - |
| storage_queue_list | storage queue list | List queues in a storage account | - |

## Workflow

1. Authenticate with Azure using `az login`
2. Select the target subscription
3. List existing storage accounts or create a new one
4. Configure access policies and networking rules
5. Upload or manage blob data
6. Monitor storage metrics

## Decision Guidance

### Storage Type Selection

| Option | BestFor | Tradeoff |
|--------|---------|----------|
| Blob Storage | Large unstructured data (images, videos, backups) | Higher latency for small frequent reads |
| File Shares | Shared file access across VMs | SMB protocol overhead |
| Queue Storage | Message decoupling between services | 64KB message size limit |
| Table Storage | Simple key-value NoSQL data | Limited query capabilities |

### Access Tier

- **Hot**: Frequently accessed data (tradeoff: higher storage cost)
- **Cool**: Infrequently accessed data, stored 30+ days (tradeoff: higher access cost)
- **Archive**: Rarely accessed, stored 180+ days (tradeoff: hours to rehydrate)

## Prerequisites

- Azure subscription
- Azure CLI installed
- Storage account contributor role
- GitHub Copilot with MCP extension

## Related Skills

- [Azure Deploy](/skills/azure-deploy) for deploying storage infrastructure
- @azure-diagnostics for monitoring storage health
- [Azure RBAC](/skills/azure-rbac) for managing storage access control
