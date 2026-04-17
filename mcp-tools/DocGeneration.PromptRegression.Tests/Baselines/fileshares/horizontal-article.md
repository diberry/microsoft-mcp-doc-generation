---
title: Azure MCP Server tools for Azure File Shares
description: Use Azure MCP Server tools to manage file shares, snapshots, and private endpoint connections through AI-powered natural language interactions.
author: diberry
ms.author: diberry
ms.service: azure-mcp-server
ms.topic: how-to
ms.date: 2026-03-26 19:35:19 UTC
content_well_notification:
  - AI-contribution
ai-usage: ai-generated
ms.custom: build-2025

#customer intent: As an Azure File Shares user, I want to manage file shares, snapshots, and private endpoint connections using natural language conversations so that I can quickly verify configurations and troubleshoot issues without navigating portals.

---

# Azure MCP Server tools for Azure File Shares

Manage file shares, snapshots, and private endpoint connections using natural language conversations with AI assistants through the Azure MCP Server.

[Azure File Shares](/azure/storage/files/files-overview) is a managed file storage service that provides fully managed SMB and NFS file shares hosted in Azure Storage accounts. It solves the need for shared, persistent file storage for lift-and-shift, legacy, and enterprise file workloads, and supports point-in-time snapshots, private endpoint isolation, and configurable performance. While the Azure portal and Azure CLI are powerful, the Azure MCP Server provides a more intuitive way to interact with your Azure File Shares resources through conversational AI.

## What is the Azure MCP Server?

[!INCLUDE [mcp-introduction](~/includes/mcp-introduction.md)]

For Azure File Shares users, this means you can:

- Create and configure managed file shares
- Create, update, and delete point-in-time snapshots for file shares
- Approve or reject private endpoint connection requests for file shares
- Check name availability and view usage and limits for file shares
- Get provisioning recommendations for file share sizing

## Prerequisites

To use the Azure MCP Server with Azure File Shares, you need:

- **Azure subscription**: An active Azure subscription. [Create one for free](https://azure.microsoft.com/free/).
- **Storage account**: A storage account hosts Azure File Shares; create one in the target subscription and region before creating file shares.
- **Resource group**: A resource group scopes storage account resources and access, so specify an existing resource group or create one for new file shares.
- **Virtual network**: A virtual network is required if you use private endpoints; configure a subnet that private endpoints can connect to before approving connections.
- **Microsoft Entra ID**: A Microsoft Entra ID identity (user, service principal, or managed identity) with role assignments is required because Model Context Protocol (MCP) Server uses DefaultAzureCredential and role-based access control (RBAC) for authentication and authorization.
- **Azure permissions**: Appropriate roles to perform the operations you want:
  - Storage Account Contributor - Required for fileshares fileshare create, fileshare update, fileshare delete, snapshot create, snapshot delete, snapshot update, and peconnection update operations that manage storage account resources.
  - Reader - Required for fileshares fileshare get, fileshare snapshot get, fileshare peconnection get, fileshares limits, fileshares usage, and fileshares rec operations that only read resource or subscription-level information.

[!INCLUDE [mcp-prerequisites](~/includes/mcp-prerequisites.md)]

## Where can you use Azure MCP Server?

[!INCLUDE [mcp-usage-contexts](~/includes/mcp-usage-contexts.md)]

## Available tools for Azure File Shares

Azure MCP Server provides the following tools for Azure File Shares operations:

| Tool | Description |
| --- | --- |
| `fileshares fileshare create` | Create a managed file share with NFS or SMB and performance options. |
| `fileshares fileshare delete` | Permanently delete a managed file share resource from a storage account. |
| `fileshares fileshare peconnection update` | Approve or reject private endpoint connection requests for a file share. |
| `fileshares fileshare snapshot create` | Create a read-only, point-in-time snapshot of a managed file share. |
| `fileshares fileshare snapshot delete` | Permanently delete a file share snapshot and free associated storage. |
| `fileshares fileshare snapshot update` | Update snapshot properties, tags, and retention metadata for a snapshot. |
| `fileshares fileshare update` | Update file share properties like provisioned storage, IOPS, throughput, and network. |
| `fileshares fileshare check-name-availability` | Check if a proposed file share name is available in the subscription. |
| `fileshares fileshare get` | Get details for a specific file share, or list file shares in scope. |
| `fileshares fileshare peconnection get` | View private endpoint connection details or list connections for a file share. |
| `fileshares fileshare snapshot get` | Get details for a specific snapshot, or list snapshots for a file share. |
| `fileshares limits` | Retrieve subscription and location-specific file share limits and quotas. |
| `fileshares rec` | Get provisioning recommendations for file share sizing based on storage needs. |
| `fileshares usage` | Get file share usage and consumption data for a subscription and location. |

For detailed information about each tool, including parameters and examples, see [Azure File Shares tools for Azure MCP Server](../tool-family/fileshares.md).

## Get started

Ready to use Azure MCP Server with your Azure File Shares resources?

1. **Set up your environment**: Choose an AI assistant or development tool that supports MCP. For setup and authentication instructions, see the links in the [Where can you use Azure MCP Server?](#where-can-you-use-azure-mcp-server) section above.

1. **Start exploring**: Ask your AI assistant questions about your Azure File Shares resources or request operations. Try prompts like:
   - "Create a file share named 'logs-share' in storage account 'prodstorageacct' in East US with NFS protocol, 5 TiB quota, and Premium performance."
   - "List all file shares in resource group 'rg-prod-eus' and show their provisioned sizes and network settings."
   - "Create a snapshot of file share 'logs-share' in storage account 'prodstorageacct' and tag it 'pre-deploy-2026-03-01'."
   - "Approve private endpoint connection 'pe-req-01' for file share 'secure-share' in storage account 'prodstorageacct' to enable private access from vnet-prod."

1. **Learn more**: Review the [Azure File Shares tools reference](../tool-family/fileshares.md) for all available capabilities and detailed parameter information.

## Best practices

When using Azure MCP Server with Azure File Shares:

- **Check name availability before create**: Run fileshares fileshare check-name-availability before creating a share to avoid naming conflicts and streamline provisioning.
- **Verify resource state before destructive actions**: Use fileshares fileshare get to list and inspect a file share before running fileshares fileshare delete to prevent accidental data loss.
- **Use snapshots for point-in-time copies**: Create snapshots with fileshares fileshare snapshot create, then confirm them with fileshares fileshare snapshot get to ensure successful capture and tagging.
- **Review private endpoint requests before approval**: Use fileshares fileshare peconnection get to inspect connection details, then use fileshares fileshare peconnection update to approve or reject with informed context.
- **Assign least-privilege RBAC for management operations**: Grant Storage Account Contributor role only to identities that run fileshares fileshare create, update, delete, and peconnection update, and grant Reader for users who only need to run fileshare get and usage commands.

## Related content

* [Azure MCP Server overview](../overview.md)
* [Get started with Azure MCP Server](../get-started.md)
* [Azure File Shares tools reference](../tool-family/fileshares.md)
* [Azure File Shares documentation](/azure/storage/files/files-overview)
* [Azure built-in roles](/azure/role-based-access-control/built-in-roles)
