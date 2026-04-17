---
title: Azure MCP Server tools for Azure Virtual Machines
description: Use Azure MCP Server tools to manage virtual machines, managed disks, and scale sets through AI-powered natural language interactions.
author: diberry
ms.author: diberry
ms.service: azure-mcp-server
ms.topic: how-to
ms.date: 2026-03-26 18:48:30 UTC
content_well_notification:
  - AI-contribution
ai-usage: ai-generated
ms.custom: build-2025

#customer intent: As an Azure Virtual Machines user, I want to manage virtual machines, managed disks, and scale sets using natural language conversations so that I can quickly verify configurations and troubleshoot issues without navigating portals.

---

# Azure MCP Server tools for Azure Virtual Machines

Manage virtual machines, managed disks, and scale sets using natural language conversations with AI assistants through the Azure MCP Server.

[Azure Virtual Machines](/azure/virtual-machines/overview) is a compute service that provides on-demand, virtualized Windows and Linux servers for running applications, test environments, and lift-and-shift migrations. It helps you provision VM instances, managed disks, and scale sets with control over size, SKU, and availability. Use the Model Context Protocol (MCP) Server tools to manage VM lifecycle and disk resources through Microsoft Entra ID authentication and role-based access control (RBAC). While the Azure portal and Azure CLI are powerful, the Azure MCP Server provides a more intuitive way to interact with your Azure Virtual Machines resources through conversational AI.

## What is the Azure MCP Server?

[!INCLUDE [mcp-introduction](~/includes/mcp-introduction.md)]

For Azure Virtual Machines users, this means you can:

- Create and configure managed disks with size, SKU, encryption, and access policy
- Retrieve managed disk, virtual machine, and scale set configuration and state
- Create and provision virtual machines with networking and authentication options
- Update virtual machine and managed disk properties such as size, SKU, and tags
- Delete virtual machines, managed disks, and virtual machine scale sets
- Deploy and manage virtual machine scale sets with instance count and SKU

## Prerequisites

To use the Azure MCP Server with Azure Virtual Machines, you need:

- **Azure subscription**: An active Azure subscription. [Create one for free](https://azure.microsoft.com/free/).
- **Resource group**: A resource group is required to scope VM, disk, and scale set resources; create one in the target subscription and region before provisioning.
- **Virtual machine image**: A marketplace or custom image is used when creating VMs or VMSS instances; use a Shared Image Gallery image, marketplace SKU, or Linux/Windows image identifier.
- **Microsoft Entra ID account**: A Microsoft Entra ID account with the required RBAC assignments is needed for authentication via DefaultAzureCredential when using MCP Server tools.
- **Azure permissions**: Appropriate roles to perform the operations you want:
  - Virtual Machine Contributor - Required for create, update, and delete operations on virtual machines and scale sets such as compute vm create, compute vm update, compute vm delete, compute vmss create, compute vmss update, and compute vmss delete.
  - Reader - Required for read-only operations like compute vm get, compute disk get, and compute vmss get to view configuration and state.

[!INCLUDE [mcp-prerequisites](~/includes/mcp-prerequisites.md)]

## Where can you use Azure MCP Server?

[!INCLUDE [mcp-usage-contexts](~/includes/mcp-usage-contexts.md)]

## Available tools for Azure Virtual Machines

Azure MCP Server provides the following tools for Azure Virtual Machines operations:

| Tool | Description |
| --- | --- |
| `compute disk create` | Create a managed disk with SKU, size, encryption, and access policy. |
| `compute disk delete` | Delete a managed disk that is not attached to any VM. |
| `compute disk get` | List or retrieve managed disk details across subscription or resource group. |
| `compute disk update` | Update properties of an existing managed disk, like size or SKU. |
| `compute vm create` | Create and provision a VM with image, size, networking, and auth. |
| `compute vm delete` | Permanently delete a VM, without removing associated network or disk resources. |
| `compute vm get` | Get VM configuration and status, including power state with instance view. |
| `compute vm update` | Modify VM properties such as size, tags, and boot diagnostics. |
| `compute vmss create` | Deploy a VM scale set with instance count, SKU, and image. |
| `compute vmss delete` | Delete a VM scale set and all its VM instances permanently. |
| `compute vmss get` | List VM scale sets and instance details across subscription or group. |
| `compute vmss update` | Update a VM scale set configuration, capacity, or upgrade policy. |

For detailed information about each tool, including parameters and examples, see [Azure Virtual Machines tools for Azure MCP Server](../tool-family/compute.md).

## Get started

Ready to use Azure MCP Server with your Azure Virtual Machines resources?

1. **Set up your environment**: Choose an AI assistant or development tool that supports MCP. For setup and authentication instructions, see the links in the [Where can you use Azure MCP Server?](#where-can-you-use-azure-mcp-server) section above.

1. **Start exploring**: Ask your AI assistant questions about your Azure Virtual Machines resources or request operations. Try prompts like:
   - "Create a new Linux VM named 'app-prod-vm' in East US using Standard_DS2_v2, Ubuntu 24.04 LTS, SSH key '~/.ssh/id_rsa.pub', and tag environment=prod using the compute vm create tool."
   - "Resize VM 'app-prod-vm' in resource group 'rg-prod' to Standard_DS3_v2 and update tag tier=backend using compute vm update."
   - "Create a 128 GB managed disk named 'db-data-disk' in West Europe from snapshot 'db-snapshot-2026-03' with Premium_LRS using compute disk create."
   - "Create a VM scale set named 'web-ss' in East US with 3 instances, Standard_DS2_v2, Ubuntu 24.04, and load balancer enabled using compute vmss create."

1. **Learn more**: Review the [Azure Virtual Machines tools reference](../tool-family/compute.md) for all available capabilities and detailed parameter information.

## Best practices

When using Azure MCP Server with Azure Virtual Machines:

- **Use least-privilege RBAC**: Assign Virtual Machine Contributor only to principals that run compute vm create or compute vmss create, and assign Reader for users who only run compute vm get or compute disk get.
- **Verify state before destructive actions**: Use compute vm get and compute disk get to confirm power state and attachment status before running compute vm delete or compute disk delete.
- **Check power state when resizing**: Use compute vm get to check a VM's power state, deallocate if required, then run compute vm update to resize safely.

## Related content

* [Azure MCP Server overview](../overview.md)
* [Get started with Azure MCP Server](../get-started.md)
* [Azure Virtual Machines tools reference](../tool-family/compute.md)
* [Azure Virtual Machines documentation](/azure/virtual-machines/overview)
* [Virtual machine sizes](/azure/virtual-machines/sizes)
