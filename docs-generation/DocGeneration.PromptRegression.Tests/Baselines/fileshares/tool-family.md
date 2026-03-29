---

title: Azure MCP Server tools for Azure File Shares
description: Use Azure MCP Server tools to manage Azure File Shares (managed SMB and NFS file shares on Azure Files) with natural language prompts from your IDE.
ms.date: 03/27/2026
ms.service: azure-mcp-server
ms.topic: concept-article
tool_count: 14
mcp-cli.version: 2.0.0-beta.33+8fab340d1e64d47701d891b7e81b5def64bbc9f6
author: diberry
ms.author: diberry
ai-usage: ai-generated
ms.custom: build-2025
content_well_notification:
  - AI-contribution
---

# Azure MCP Server tools for Azure File Shares

The Azure MCP Server lets you manage Azure File Shares, including check name availability (check-name-availability), create, delete, get, limits, rec (recommendations), update, and usage, with natural language prompts.

Azure File Shares is a fully managed file share service in Azure Storage that provides SMB and NFS file shares for cloud and on-premises workloads. For more information, see [Azure File Shares documentation](/azure/storage/files/).


[!INCLUDE [tip-about-params](../includes/tools/parameter-consideration.md)]


## Check fileshare

<!-- @mcpcli fileshares fileshare check-name-availability -->

This tool checks whether a file share name is available in Azure Files. It returns whether the name is available and provides details when it isn't. This tool is part of the Model Context Protocol (MCP) server.

Example prompts include:

- "Is file share name 'backup-share' available in location 'EastUS'?"
- "Check if name 'projectdata' is available in location 'WestEurope'."
- "Is name 'logs' available in location 'JapanEast' for subscription 'Contoso Subscription'?"
- "Verify availability of file share name 'mediafiles' in location 'SoutheastAsia'."
- "Please check file share name 'prod-backups' in location 'CentralUS' for subscription 'a1b2c3d4-5678-90ab-cdef-1234567890ab'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Location** |  Required | The Azure region/location name (for example, `EastUS`, `WestEurope`). |
| **Name** |  Required | The name of the file share. |
| **Subscription** |  Optional | Specifies the Azure subscription to use. Accepts either a subscription ID (GUID) or display name. If not, specified, the AZURE_SUBSCRIPTION_ID environment variable is used instead. |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌


## Create fileshare

<!-- @mcpcli fileshares fileshare create -->

This tool creates an Azure Files share in a resource group. This tool is part of the Model Context Protocol (MCP) server. It provisions a high-performance, fully managed file share and exposes it over the `NFS` protocol. You specify the location, name, and resource group for the share. You can also set subnet access, media tier, provisioned throughput and IOPS, redundancy, tags, and subscription. The tool returns the created share's resource properties.

Example: Create a share named 'fileshare-prod' in location 'EastUS' within resource group 'rg-storage'.

Example prompts include:

- "Create a new file share named 'fileshare-prod' in location 'EastUS' within resource group 'rg-prod'."
- "Create file share 'media-share' in resource group 'rg-media' at location 'WestEurope' with provisioned storage in gib '500'."
- "Set up a high-performance file share 'backup-01' in location 'SouthCentralUS' for resource group 'rg-backup' with media tier 'SSD'."
- "Create an NFS file share 'dev-nfs' in resource group 'rg-dev' at location 'EastUS2' with mount name 'dev-mount' and Nfs root squash 'NoRootSquash'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Location** |  Required | The Azure region/location name (for example, `EastUS`, `WestEurope`). |
| **Name** |  Required | The name of the file share. |
| **Resource group** |  Required | The name of the Azure resource group. This resource group is a logical container for Azure resources. |
| **Allowed subnets** |  Optional | Comma-separated list of subnet IDs allowed to access the file share. |
| **Media tier** |  Optional | The storage media tier (for example, `SSD`). |
| **Mount name** |  Optional | The mount name of the file share as seen by end users. |
| **Nfs root squash** |  Optional | NFS root squash setting (NoRootSquash, RootSquash, or AllSquash). |
| **Protocol** |  Optional | The file sharing protocol (for example, `NFS`). |
| **Provisioned io per sec** |  Optional | The provisioned IO operations per second. |
| **Provisioned storage in gib** |  Optional | The desired provisioned storage size of the share in GiB. |
| **Provisioned throughput mib per sec** |  Optional | The provisioned throughput in MiB per second. |
| **Public network access** |  Optional | Public network access setting (Enabled or Disabled). |
| **Redundancy** |  Optional | The redundancy level (for example, `Local`, `Zone`). |
| **Subscription** |  Optional | Specifies the Azure subscription to use. Accepts either a subscription ID (GUID) or display name. If not, specified, the AZURE_SUBSCRIPTION_ID environment variable is used instead. |
| **Tags** |  Optional | Resource tags as JSON (for example, `{&quot;key1&quot;:&quot;value1&quot;,&quot;key2&quot;:&quot;value2&quot;}`). |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ✅ | Idempotent: ❌ | Open World: ❌ | Read Only: ❌ | Secret: ❌ | Local Required: ❌

## Create fileshare snapshot

<!-- @mcpcli fileshares fileshare snapshot create -->

This tool is part of the Model Context Protocol (MCP) server. It creates a snapshot of an Azure Files share. Snapshots are read-only, point-in-time copies that you use for backup and recovery. Provide the required parameters in the parameter table to identify the file share and snapshot. Optionally, include `Metadata` as a JSON object and specify a `Subscription` to target. On success, the tool returns snapshot properties and metadata.

Example prompts include:

- "Create a snapshot named 'snap-20260327' of file share 'backupshare' in resource group 'rg-prod'."
- "Create a snapshot 'daily-snap' for file share 'fileshare-prod' in resource group 'rg-backup' with metadata '{"createdBy":"ops","env":"prod"}'."
- "Can you create snapshot 'snap-qa-01' of file share 'qa-files' in resource group 'rg-testing'?"

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **File share name** |  Required | The name of the parent file share. |
| **Resource group** |  Required | The name of the Azure resource group. This resource group is a logical container for Azure resources. |
| **Snapshot name** |  Required | The name of the snapshot. |
| **Metadata** |  Optional | Custom metadata for the snapshot as a JSON object (for example, `{&quot;key1&quot;:&quot;value1&quot;,&quot;key2&quot;:&quot;value2&quot;}`). |
| **Subscription** |  Optional | Specifies the Azure subscription to use. Accepts either a subscription ID (GUID) or display name. If not, specified, the AZURE_SUBSCRIPTION_ID environment variable is used instead. |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ✅ | Idempotent: ❌ | Open World: ❌ | Read Only: ❌ | Secret: ❌ | Local Required: ❌ |

## Delete fileshare

<!-- @mcpcli fileshares fileshare delete -->

This tool, part of the Model Context Protocol (MCP) server, deletes a file share in Azure Files. You specify the file share name and the resource group, and you can optionally specify the subscription. The tool removes the file share and its contents from the storage account and returns a confirmation on success. You need permission to delete the file share, such as the Storage File Data SMB Share Contributor role or another role that includes delete permissions.

Example prompts include:

- "Delete file share 'backup-share' from resource group 'rg-prod'."
- "Remove file share with name 'logs-share' from resource group 'rg-web-prod'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Name** |  Required | The name of the file share. |
| **Resource group** |  Required | The name of the Azure resource group. This resource group is a logical container for Azure resources. |
| **Subscription** |  Optional | Specifies the Azure subscription to use. Accepts either a subscription ID (GUID) or display name. If not, specified, the AZURE_SUBSCRIPTION_ID environment variable is used instead. |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ✅ | Idempotent: ❌ | Open World: ❌ | Read Only: ❌ | Secret: ❌ | Local Required: ❌

Examples

- Delete file share 'project-files' in resource group 'rg-prod-storage'.

## Delete fileshare snapshot

<!-- @mcpcli fileshares fileshare snapshot delete -->

This tool is part of the Model Context Protocol (MCP) tools. It permanently deletes a snapshot of an Azure Files file share. Deletion can't be undone.

You identify the snapshot by specifying `File share name`, `Resource group`, and `Snapshot name`. You can optionally specify `Subscription`. Ensure you have Microsoft Entra ID role-based access control (RBAC) permissions on the storage account.

For example, "Delete snapshot '2026-03-01T120000Z' from file share 'proj-reports' in resource group 'rg-prod'."

Example prompts include:

- "Delete snapshot 'snap-20260301' from file share 'project-files' in resource group 'rg-prod'."
- "Can you remove snapshot 'backup-2026-03' from file share 'media-share' in resource group 'rg-media'?"

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **File share name** |  Required | The name of the parent file share. |
| **Resource group** |  Required | The name of the Azure resource group. This resource group is a logical container for Azure resources. |
| **Snapshot name** |  Required | The name of the snapshot. |
| **Subscription** |  Optional | Specifies the Azure subscription to use. Accepts either a subscription ID (GUID) or display name. If not, specified, the AZURE_SUBSCRIPTION_ID environment variable is used instead. |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ✅ | Idempotent: ❌ | Open World: ❌ | Read Only: ❌ | Secret: ❌ | Local Required: ❌

## Get fileshare

<!-- @mcpcli fileshares fileshare get -->

Get details of a specific Azure Files file share or list all file shares in a subscription or resource group. If you specify `name`, this tool returns the specified file share and its properties. If you don't specify `name`, this tool lists all file shares in the `subscription` or `resource group`.

Example prompts include:

- "Show all file shares in my subscription."
- "List the file shares in resource group 'rg-dev'."
- "Get details of file share 'archive-share' in resource group 'rg-prod'."
- "Show me the file share 'project-files' in resource group 'webapp-prod'."
- "What file shares exist in resource group 'data-rg'?"

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Name** |  Optional | The name of the file share. |
| **Resource group** |  Optional | The name of the Azure resource group. This resource group is a logical container for Azure resources. |
| **Subscription** |  Optional | Specifies the Azure subscription to use. Accepts either a subscription ID (GUID) or display name. If not, specified, the AZURE_SUBSCRIPTION_ID environment variable is used instead. |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌

## Get fileshare private endpoint connection

<!-- @mcpcli fileshares fileshare peconnection get -->

This tool is part of the Model Context Protocol (MCP) server. It returns details for a specific private endpoint connection on an Azure Storage file share, or it lists all private endpoint connections for a file share. If you provide a `connection name`, the tool returns that connection. If you omit a `connection name`, the tool lists all private endpoint connections for the specified `file share name` and `resource group`.

Example prompts include:

- "List all private endpoint connections for file share 'datafiles' in resource group 'rg-storage-prod'."
- "Show me the private endpoint connections for file share 'sharebackups' in resource group 'rg-files-dev'."
- "Get private endpoint connection 'pe-connection-01' for file share 'mediafiles' in resource group 'rg-media-prod'."
- "What private endpoint connections exist for file share 'archive-share' in resource group 'rg-archive'?"

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **File share name** |  Required | The name of the file share. |
| **Resource group** |  Required | The name of the Azure resource group. This resource group is a logical container for Azure resources. |
| **Connection name** |  Optional | The name of the private endpoint connection. |
| **Subscription** |  Optional | Specifies the Azure subscription to use. Accepts either a subscription ID (GUID) or display name. If not, specified, the AZURE_SUBSCRIPTION_ID environment variable is used instead. |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌

## Get fileshare snapshot

<!-- @mcpcli fileshares fileshare snapshot get -->

This tool, part of the Model Context Protocol (MCP) server, gets details of a specific file share snapshot or lists all snapshots for a file share. If you provide a snapshot name, this tool returns that snapshot. Otherwise, it lists all snapshots for the file share.

Example prompts include:

- "List all snapshots for file share 'datafiles' in resource group 'rg-storage-prod'."
- "Show snapshots for file share 'backup-share' in resource group 'rg-backup'."
- "Get snapshot 'snapshot-2026-03-01' for file share 'logs-share' in resource group 'rg-logs-prod'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **File share name** |  Required | The name of the parent file share. |
| **Resource group** |  Required | The name of the Azure resource group that contains the file share. |
| **Snapshot name** |  Optional | The name of the snapshot. |
| **Subscription** |  Optional | The Azure subscription to use. Accepts either a subscription ID (GUID) or display name. If not, specified, the AZURE_SUBSCRIPTION_ID environment variable is used. |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌

## Get fileshares limits

<!-- @mcpcli fileshares limits -->

This tool returns Azure Files share limits for a subscription and location. It is part of the Model Context Protocol (MCP) tools. The response lists quota and limit values that apply to file shares in the specified Azure region, and helps you plan capacity and provisioning.

Example: Get file share limits for location 'eastus' and subscription 'Contoso Subscription'.

Example prompts include:

- "Get file share limits for location 'eastus'."
- "What are the file share limits in location 'westeurope'?"
- "Show file share limits for location 'centralus' in subscription '12345678-1234-1234-1234-1234567890ab'."
- "List file share limits in location 'southeastasia' for subscription 'Contoso-Prod'."
- "Retrieve file share limits for location 'canadacentral'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Location** |  Required | The Azure region/location name (for example, `eastus`, `westeurope`). |
| **Subscription** |  Optional | The Azure subscription to use. Accepts a subscription ID (GUID) or display name. If you don't specify it, the `AZURE_SUBSCRIPTION_ID` environment variable applies. |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌

## Get fileshares recommendations

<!-- @mcpcli fileshares rec -->

This tool, part of the Model Context Protocol (MCP), returns provisioning parameter recommendations for an Azure Files share based on the desired provisioned storage size. You provide `Location` and `Provisioned storage in gib`, and the tool suggests provisioning settings that balance performance and cost. The tool returns recommended parameters and, when applicable, warnings about regional limits or configuration conflicts.

Example: 'Recommend a file share in eastus with 512 GiB'

Example prompts include:

- "Get recommendations for a file share with location 'eastus' and provisioned storage in GiB '512'."
- "What provisioning parameters do you recommend for a file share in location 'westeurope' with provisioned storage in GiB '1024'?"
- "Show provisioning recommendations for a file share in location 'centralus' with provisioned storage in GiB '2048' under subscription 'Contoso Subscription'."
- "Recommend provisioning parameters for location 'uksouth' with provisioned storage in GiB '256'."
- "I need file share provisioning recommendations for location 'westus2' with provisioned storage in GiB '128' and subscription 'a1b2c3d4-5678-90ab-cdef-1234567890ab'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Location** |  Required | The Azure region/location name (for example, `eastus`, `westeurope`). |
| **Provisioned storage in gib** |  Required | The desired provisioned storage size of the share in GiB. |
| **Subscription** |  Optional | Specifies the Azure subscription to use. Accepts either a subscription ID (GUID) or display name. If not, specified, the AZURE_SUBSCRIPTION_ID environment variable is used instead. |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌

## Update fileshare private endpoint connection

<!-- @mcpcli fileshares fileshare peconnection update -->

This tool is part of the Model Context Protocol (MCP) server. It updates the state of a private endpoint connection for an Azure Files file share. You approve or reject private endpoint connection requests by setting the `Status` to `Approved`, `Rejected`, or `Pending`. Specify the `Connection name`, the `File share name`, and the `Resource group` to identify the connection. Optionally include a `Description` and a `Subscription` to target a different subscription.

Example prompt: "Update private endpoint connection 'pe-conn-01' for file share 'finance-share' in resource group 'rg-storage' to 'Approved' with description 'Approved for production access'."

Example prompts include:

- "Approve private endpoint connection 'peconn-01' for file share 'fileshare-data' in resource group 'rg-storage-prod' with status 'Approved'."
- "Reject private endpoint connection 'peconn-02' for file share 'backups' in resource group 'rg-backup' with status 'Rejected'."
- "Update private endpoint connection 'peconn-03' status to 'Pending' for file share 'sharedfiles' in resource group 'rg-dev'."
- "Change the status of private endpoint connection 'peconn-04' to 'Approved' for file share 'dailydata' in resource group 'rg-analytics'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Connection name** |  Required | The name of the private endpoint connection. |
| **File share name** |  Required | The name of the file share. |
| **Resource group** |  Required | The name of the Azure resource group. This resource group is a logical container for Azure resources. |
| **Status** |  Required | The connection status (Approved, Rejected, or Pending). |
| **Description** |  Optional | Description for the connection state change. |
| **Subscription** |  Optional | Specifies the Azure subscription to use. Accepts either a subscription ID (GUID) or display name. If not, specified, the AZURE_SUBSCRIPTION_ID environment variable is used instead. |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ✅ | Idempotent: ❌ | Open World: ❌ | Read Only: ❌ | Secret: ❌ | Local Required: ❌

## Update fileshare snapshot

<!-- @mcpcli fileshares fileshare snapshot update -->

This tool, part of the Model Context Protocol (MCP) server, updates properties and metadata of an Azure Files share snapshot, such as tags and retention policies. You can change custom metadata or adjust retention settings without recreating the snapshot. On success, the tool returns the updated snapshot resource.

Example prompts include:

- "Update snapshot 'snap-20260301' of file share 'exports-share' in resource group 'rg-backup' with metadata '{"retention":"30d","owner":"backup-team"}'."
- "Update metadata for snapshot 'snapshot-2026-03-01' of file share 'prod-files' in resource group 'rg-prod'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **File share name** |  Required | The name of the parent file share. |
| **Resource group** |  Required | The name of the Azure resource group. This resource group is a logical container for Azure resources. |
| **Snapshot name** |  Required | The name of the snapshot. |
| **Metadata** |  Optional | Custom metadata for the snapshot as a JSON object (for example, `{"key1":"value1","key2":"value2"}`). |
| **Subscription** |  Optional | Specifies the Azure subscription to use. Accepts either a subscription ID (GUID) or display name. If not, specified, the `AZURE_SUBSCRIPTION_ID` environment variable is used. |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ✅ | Idempotent: ❌ | Open World: ❌ | Read Only: ❌ | Secret: ❌ | Local Required: ❌



## Update fileshare

<!-- @mcpcli fileshares fileshare update -->

This tool updates an existing Azure Files managed file share resource. It updates mutable properties such as provisioned storage, provisioned IOPS, provisioned throughput, NFS settings, network access, and resource tags. You must have role-based access control (RBAC) permissions that allow updates to the file share. After the update, the tool returns the file share's updated metadata and provisioning status.

"Update file share 'share-prod' in resource group 'rg-production' to provisioned storage '2048' GiB and provisioned throughput '512' MiB/s."

"Update NFS settings for file share 'nfs-share' in resource group 'rg-nfs' and restrict access to subnets 'subnet-1,subnet-2'."

Example prompts include:

- "Update file share 'shared-data' in resource group 'rg-fileshare-prod'."
- "Update the provisioned storage for file share 'backup-share' in resource group 'rg-backup' to provisioned storage in gib '200'."
- "Modify file share 'project-files' in resource group 'rg-projects' to set provisioned IO per sec '3000', provisioned throughput MiB per sec '128', and public network access 'Disabled'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Name** |  Required | The name of the file share. |
| **Resource group** |  Required | The name of the Azure resource group. This resource group is a logical container for Azure resources. |
| **Allowed subnets** |  Optional | Comma-separated list of subnet IDs allowed to access the file share. |
| **Nfs root squash** |  Optional | NFS root squash setting (NoRootSquash, RootSquash, or AllSquash). |
| **Provisioned io per sec** |  Optional | The provisioned IO operations per second. |
| **Provisioned storage in gib** |  Optional | The desired provisioned storage size of the share in GiB. |
| **Provisioned throughput mib per sec** |  Optional | The provisioned throughput in MiB per second. |
| **Public network access** |  Optional | Public network access setting (Enabled or Disabled). |
| **Subscription** |  Optional | Specifies the Azure subscription to use. Accepts either a subscription ID (GUID) or display name. If not, specified, the AZURE_SUBSCRIPTION_ID environment variable is used instead. |
| **Tags** |  Optional | Resource tags as JSON (for example, `{&quot;key1&quot;:&quot;value1&quot;,&quot;key2&quot;:&quot;value2&quot;}`). |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ✅ | Idempotent: ❌ | Open World: ❌ | Read Only: ❌ | Secret: ❌ | Local Required: ❌

## Get fileshares usage

<!-- @mcpcli fileshares usage -->

This tool, part of the Model Context Protocol (MCP) server, retrieves file share usage data for a subscription and an Azure location. It returns usage metrics and quota information for file shares in the specified location, so you can monitor storage consumption and plan capacity. For example, "Get file share usage for subscription 'Contoso Subscription' and location 'eastus'".

Example prompts include:

- "Get file share usage for location 'eastus'."
- "Show file share usage in location 'westeurope' for subscription 'Prod Subscription'."
- "What is the file share usage for location 'uksouth'?"
- "Retrieve file share usage for location 'centralus' using subscription 'a1b2c3d4-5678-90ab-cdef-1234567890ab'."
- "List file share usage metrics for location 'southeastasia'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Location** |  Required | The Azure region/location name (for example, `eastus`, `westeurope`). |
| **Subscription** |  Optional | Specifies the Azure subscription to use. Accepts either a subscription ID (GUID) or display name. If not, specified, the AZURE_SUBSCRIPTION_ID environment variable is used instead. |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌

## Related content

- [What are the Azure MCP Server tools?](index.md)
- [Get started using Azure MCP Server](../get-started.md)
- [Azure Files documentation](/azure/storage/files/)