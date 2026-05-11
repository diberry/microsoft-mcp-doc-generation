---
title: Azure MCP Server tools for Azure Backup
description: Use Azure MCP Server tools to manage backup vaults, policies, and protected items through AI-powered natural language interactions.
author: diberry
ms.author: diberry
ms.reviewer: mbaldwin
ms.service: azure-mcp-server
ms.topic: how-to
ms.date: 2026-05-11 08:01:31 UTC
content_well_notification:
  - AI-contribution
ai-usage: ai-generated
ms.custom: build-2025

#customer intent: As an Azure Backup user, I want to manage backup vaults, policies, and protected items using natural language conversations so that I can quickly verify configurations and troubleshoot issues without navigating portals.

---

# Azure MCP Server tools for Azure Backup

Manage backup vaults, policies, and protected items using natural language conversations with AI assistants through the Azure MCP Server.

Azure Backup provides cloud-native backup and restore capabilities to protect and recover Azure resources. It centralizes backup policies, vault management, and recovery point retention for VMs, disks, databases, and file shares. Azure MCP Server tools use the Model Context Protocol (MCP) and Microsoft Entra ID authentication, and rely on role-based access control (RBAC) to authorize operations. While the Azure portal and Azure CLI are powerful, the Azure MCP Server provides a more intuitive way to interact with your Azure Backup resources through conversational AI.

## What is the Azure MCP Server?

[!INCLUDE [mcp-introduction](~/includes/mcp-introduction.md)]

For Azure Backup users, this means you can:

- Create and manage Recovery Services vaults and vault settings
- Create and retrieve backup policies with schedule and retention rules
- Enable and configure backup protection for resources and workload items
- Retrieve job, backup status, and recovery point details to monitor operations
- Configure vault governance and disaster recovery features like immutability and CRR

## Prerequisites

To use the Azure MCP Server with Azure Backup, you need:

- **Azure subscription**: An active Azure subscription. [Create one for free](https://azure.microsoft.com/pricing/purchase-options/azure-account?cid=msft_learn).
- **Recovery Services vault**: An existing Recovery Services vault (RSV or DPP) is required for most data-plane operations and for storing protected items and recovery points.
- **Protected resource (VM, disk, database)**: The resource you plan to protect must exist; provide its ARM resource ID or discover it using protectable item discovery for workload items.
- **Microsoft Entra ID**: A Microsoft Entra ID account with appropriate role assignments is required for DefaultAzureCredential authentication and RBAC-based access to backup resources.
- **Azure permissions**: Appropriate roles to perform the operations you want:
  - Backup Reader - Required for read-only operations such as azurebackup backup status, job get, policy get, protectableitem list, protecteditem get, recoverypoint get, vault get, and governance find-unprotected.
  - Backup Contributor - Required for write and management operations such as azurebackup vault create, vault update, policy create, protecteditem protect, protecteditem undelete, governance immutability, governance soft-delete, and disasterrecovery enable-crr.

[!INCLUDE [mcp-prerequisites](~/includes/mcp-prerequisites.md)]

## Where can you use Azure MCP Server?

[!INCLUDE [mcp-usage-contexts](~/includes/mcp-usage-contexts.md)]

## Available tools for Azure Backup

Azure MCP Server provides the following tools for Azure Backup operations:

| Tool | Description |
| --- | --- |
| `azurebackup disasterrecovery enable-crr` | Enable Cross-Region Restore (CRR) on a GRS-enabled Recovery Services vault. |
| `azurebackup governance immutability` | Set immutability state for a Recovery Services vault, including irreversible Locked state. |
| `azurebackup governance soft-delete` | Configure backup vault soft delete state and optional retention period in days. |
| `azurebackup policy create` | Create a backup policy with schedule, retention rules, and workload type settings. |
| `azurebackup vault create` | Create a new Recovery Services or Backup vault, specifying type and location. |
| `azurebackup vault update` | Update vault settings such as redundancy, immutability, soft delete, and identity. |
| `azurebackup backup status` | Check whether a datasource is protected and return vault and policy details. |
| `azurebackup governance find-unprotected` | Scan subscription to find resources not protected by any backup policy. |
| `azurebackup job get` | Retrieve backup job details or list vault jobs, including status and times. |
| `azurebackup policy get` | Get backup policy details or list policies in a specified vault. |
| `azurebackup protectableitem list` | List protectable workload items in an RSV vault by workload or container. |
| `azurebackup protecteditem get` | Retrieve protected item details or list protected items in a vault. |
| `azurebackup protecteditem protect` | Enable or configure backup protection for a datasource using a policy. |
| `azurebackup protecteditem undelete` | Undelete a soft-deleted protected item to restore active protection. |
| `azurebackup recoverypoint get` | Retrieve recovery point details or list recovery points for a protected item. |
| `azurebackup vault get` | Get vault information or list all backup vaults in the subscription. |

For detailed information about each tool, including parameters and examples, see [Azure Backup tools for Azure MCP Server](../tool-family/azurebackup.md).

## Get started

Ready to use Azure MCP Server with your Azure Backup resources?

1. **Set up your environment**: Choose an AI assistant or development tool that supports MCP. For setup and authentication instructions, see the links in the [Where can you use Azure MCP Server?](#where-can-you-use-azure-mcp-server) section above.

1. **Start exploring**: Ask your AI assistant questions about your Azure Backup resources or request operations. Try prompts like:
   - "Check the backup status of virtual machine 'prod-web-01' in East US using its ARM resource ID '/subscriptions/{subId}/resourceGroups/prod-rg/providers/Microsoft.Compute/virtualMachines/prod-web-01' and location 'eastus'."
   - "Create a Recovery Services vault named 'prod-backup-vault' in East US with vault-type 'rsv' and Standard SKU."
   - "Protect VM 'prod-web-01' in East US by enabling protection in vault 'prod-backup-vault' using policy 'daily-30d' and providing the VM ARM resource ID."
   - "Set immutability to 'Locked' on vault 'prod-backup-vault' in East US and understand that 'Locked' is irreversible."
   - "Scan subscription for unprotected virtual machines in resource group 'prod-rg' with tag 'environment:prod' and return their resource IDs."

1. **Learn more**: Review the [Azure Backup tools reference](../tool-family/azurebackup.md) for all available capabilities and detailed parameter information.

## Best practices

When using Azure MCP Server with Azure Backup:

- **Use least-privilege RBAC for operations**: Assign Backup Reader for users who only run read commands, and assign Backup Contributor only to operators who run vault create, vault update, policy create, or protecteditem protect.
- **Verify protection before making changes**: Run azurebackup backup status or azurebackup governance find-unprotected to confirm protection state before enabling or altering protection for a resource.
- **Monitor asynchronous jobs**: Use azurebackup job get to poll and confirm the status of long-running operations such as protecteditem protect and protecteditem undelete.
- **Confirm immutability intent**: When using azurebackup governance immutability, validate the target state because setting immutability to 'Locked' is irreversible and can't be undone.


## Related content

* [Azure MCP Server overview](../overview.md)
* [Get started with Azure MCP Server](../get-started.md)
* [Azure Backup tools reference](../tool-family/azurebackup.md)
