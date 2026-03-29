---

title: Azure MCP Server tools for Azure Virtual Machines
description: Use Azure MCP Server tools to manage compute resources such as virtual machines, managed disks, and virtual machine scale sets with natural language prompts from your IDE.
ms.date: 03/27/2026
ms.service: azure-mcp-server
ms.topic: concept-article
tool_count: 12
mcp-cli.version: 2.0.0-beta.33+8fab340d1e64d47701d891b7e81b5def64bbc9f6
author: diberry
ms.author: diberry
ai-usage: ai-generated
ms.custom: build-2025
content_well_notification:
  - AI-contribution
---

# Azure MCP Server tools for Azure Virtual Machines

The Azure MCP Server lets you manage virtual machines and related compute resources, including create, delete, get, and update operations for compute disks, virtual machines, and virtual machine scale sets, with natural language prompts.

Azure Virtual Machines is a service that provides on-demand, scalable compute capacity in Azure. For more information, see [Azure Virtual Machines documentation](/azure/virtual-machines/).

[!INCLUDE [tip-about-params](../includes/tools/parameter-consideration.md)]


## Create compute disk

<!-- @mcpcli compute disk create -->

This tool creates a new Azure managed disk in the specified resource group. It is part of the Model Context Protocol (MCP) server. You can create an empty disk by specifying the size in GB, a disk from a source such as a snapshot, another managed disk, or a VHD blob URI, a disk from a Shared Image Gallery image version, or a disk that is ready for upload by specifying the upload type and upload size in bytes. If you don't specify location, the tool uses the resource group's location.

You can configure disk size, storage SKU, OS type, availability zone, hypervisor generation, tags, encryption settings, performance tier, shared disk behavior, on-demand bursting, and IOPS or throughput limits for UltraSSD disks. You can set the network access policy to `AllowAll`, `AllowPrivate`, or `DenyAll`, and you can associate a disk access resource during creation. The tool supports the `Upload` and `UploadWithSecurityData` upload types for disks that you plan to upload.

Example prompts include:

- "Create a 128 GB managed disk named 'data-disk-128' in resource group 'rg-prod'."
- "Create a new Premium_LRS disk called 'premium-disk-256' in resource group 'rg-prod' with 256 GB."
- "Create a managed disk 'os-disk-1' in resource group 'rg-eastus' in eastus."
- "Create a managed disk 'disk-from-snap' in resource group 'rg-backup' from snapshot '/subscriptions/11111111-1111-1111-1111-111111111111/resourceGroups/rg-backup/providers/Microsoft.Compute/snapshots/snap1'."
- "Create a managed disk 'disk-from-blob' in resource group 'rg-storage' from blob 'https://mystorageacct.blob.core.windows.net/vhds/osdisk.vhd'."
- "Create a 64 GB Standard_LRS Linux disk named 'linux-data-64' in resource group 'rg-prod' in zone '1'."
- "Create a managed disk 'tagged-disk' in resource group 'rg-prod' with tags 'env=prod' 'team=infra'."
- "Create a 128 GB Premium_LRS disk named 'perf-disk-128' in resource group 'rg-performance' with performance tier 'P30'."
- "Create a disk 'encrypted-disk' in resource group 'rg-sec' with customer-managed encryption using disk encryption set '/subscriptions/22222222-2222-2222-2222-222222222222/resourceGroups/rg-sec/providers/Microsoft.Compute/diskEncryptionSets/des1'."
- "Create a managed disk 'from-gallery' in resource group 'rg-images' from gallery image version '/subscriptions/33333333-3333-3333-3333-333333333333/resourceGroups/rg-images/providers/Microsoft.Compute/galleries/gallery1/images/ubuntuImage/versions/1.0.0'."
- "Create a data disk 'data-from-lun0' in resource group 'rg-images' from gallery image version '/subscriptions/33333333-3333-3333-3333-333333333333/resourceGroups/rg-images/providers/Microsoft.Compute/galleries/gallery1/images/dataImage/versions/1.0.0' with gallery image reference lun '0'."
- "Create a disk ready for upload named 'upload-disk' in resource group 'rg-upload' with upload size bytes '20972032'."
- "Create a Trusted Launch upload disk named 'trusted-upload' in resource group 'rg-sec' with upload type 'UploadWithSecurityData' and security type 'TrustedLaunch'."
- "Create an UltraSSD_LRS disk named 'ultra-disk-256' in resource group 'rg-ultra' with 256 GB, disk IOPS read write '10000', and disk MBPS read write '500'."
- "Create a shared managed disk named 'shared-disk-512' in resource group 'rg-shared' with 512 GB and max shares '3'."
- "Create a managed disk 'private-disk' in resource group 'rg-private' with network access policy 'DenyAll' and disk access '/subscriptions/44444444-4444-4444-4444-444444444444/resourceGroups/rg-private/providers/Microsoft.Compute/diskAccesses/da1'."
- "Create a 128 GB managed disk named 'burst-disk-128' in resource group 'rg-burst' with enable bursting 'true'."
- "Create a managed disk 'cmk-disk' in resource group 'rg-sec' with encryption type 'EncryptionAtRestWithPlatformAndCustomerKeys'."
- "Create a V2 hypervisor generation disk named 'v2-os-disk' in resource group 'rg-v2' with 128 GB and hyper v generation 'V2'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Disk name** |  Required | The name of the disk. |
| **Resource group** |  Required | The name of the Azure resource group. This resource group is a logical container for Azure resources. |
| **Disk access** |  Optional | Resource ID of the disk access resource for using private endpoints on disks. |
| **Disk encryption set** |  Optional | Resource ID of the disk encryption set to use for enabling encryption at rest. |
| **Disk iops read write** |  Optional | The number of IOPS allowed for this disk. Only settable for UltraSSD disks. |
| **Disk mbps read write** |  Optional | The bandwidth allowed for this disk in MBps. Only settable for UltraSSD disks. |
| **Enable bursting** |  Optional | Enable on-demand bursting beyond the provisioned performance target of the disk. Doesn't apply to Ultra disks. Accepted values: `true`, `false`. |
| **Encryption type** |  Optional | Encryption type of the disk. Accepted values: `EncryptionAtRestWithCustomerKey`, `EncryptionAtRestWithPlatformAndCustomerKeys`, `EncryptionAtRestWithPlatformKey`. |
| **Gallery image reference** |  Optional | Resource ID of a Shared Image Gallery image version to use as the source for the disk. Format: /subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.Compute/galleries/{gallery}/images/{image}/versions/{version}. |
| **Gallery image reference lun** |  Optional | LUN (Logical Unit Number) of the data disk in the gallery image version. If specified, the disk is created from the data disk at this LUN. If not, specified, the disk is created from the OS disk of the image. |
| **Hyper v generation** |  Optional | The hypervisor generation of the Virtual Machine. Applicable to OS disks only. Accepted values: V1, V2. |
| **Location** |  Optional | The Azure region/location. Defaults to the resource group's location if not specified. |
| **Max shares** |  Optional | The maximum number of VMs that can attach to the disk at the same time. Value greater than one indicates a shared disk. |
| **Network access policy** |  Optional | Policy for accessing the disk through network. Accepted values: `AllowAll`, `AllowPrivate`, `DenyAll`. |
| **Os type** |  Optional | The Operating System type of the disk. Accepted values: `Linux`, `Windows`. |
| **Security type** |  Optional | Security type of the managed disk. Accepted values: `ConfidentialVM_DiskEncryptedWithCustomerKey`, `ConfidentialVM_DiskEncryptedWithPlatformKey`, `ConfidentialVM_VMGuestStateOnlyEncryptedWithPlatformKey`, `Standard`, `TrustedLaunch`. Required when `--upload-type` is UploadWithSecurityData. |
| **Size gb** |  Optional | Size of the disk in GB. Max size: 4095 GB. |
| **SKU** |  Optional | Underlying storage SKU. Accepted values: `Premium_LRS`, `PremiumV2_LRS`, `Premium_ZRS`, `StandardSSD_LRS`, `StandardSSD_ZRS`, `Standard_LRS`, `UltraSSD_LRS`. |
| **Source** |  Optional | Source to create the disk from, including a resource ID of a snapshot or disk, or a blob URI of a VHD. When a source is provided, `--size-gb` is optional and defaults to the source size. |
| **Subscription** |  Optional | Specifies the Azure subscription to use. Accepts either a subscription ID (GUID) or display name. If not, specified, the AZURE_SUBSCRIPTION_ID environment variable is used instead. |
| **Tags** |  Optional | Space-separated tags in 'key=value' format. Use an empty string to clear existing tags. |
| **Tier** |  Optional | Performance tier of the disk (for example, `P10`, `P15`, `P20`, `P30`, `P40`, `P50`, `P60`, `P70`, `P80`). Applicable to Premium SSD disks only. |
| **Upload size bytes** |  Optional | The size in bytes (including the VHD footer of 512 bytes) of the content to be uploaded. Required when `--upload-type` is specified. |
| **Upload type** |  Optional | Type of upload for the disk. Accepted values: `Upload`, `UploadWithSecurityData`. When specified, the disk is created in a ReadyToUpload state. |
| **Zone** |  Optional | Availability zone into which to provision the resource. |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ✅ | Idempotent: ❌ | Open World: ❌ | Read Only: ❌ | Secret: ❌ | Local Required: ❌

## Create virtual machine

<!-- @mcpcli compute vm create -->

Create, deploy, or provision a single Azure Virtual Machine (VM) by using the Model Context Protocol (MCP) server. This tool launches a new `Linux` or `Windows` VM and supports SSH public key or password authentication.

If you don't specify networking resources, this tool creates a virtual network (VNet), subnet, network security group (NSG), network interface (NIC), and public IP. For `Linux` VMs, provide an SSH public key or a path to a public key file. For `Windows` VMs, provide an admin password or use an SSH key when supported.

The tool defaults to `Standard_DS1_v2` VM size and `Ubuntu 24.04 LTS` image when you don't specify size or image. You must supply the required parameters listed in the table below.

This tool creates a single VM and doesn't provision scale sets.

Example prompts include:

- "Create a new Linux VM named 'webvm01' in resource group 'rg-prod' with admin username 'azureuser' in location 'eastus'."
- "Create a virtual machine with size 'Standard_DS1_v2' in resource group 'rg-dev' named 'appvm01' with admin username 'adminuser' in location 'westus'."
- "Create a Windows VM with admin password 'P@ssw0rd123!' in resource group 'rg-windows' named 'winvm01' and admin username 'winadmin' in location 'centralus'."
- "Create VM 'dbvm01' in location 'eastus2' with SSH public key '~/.ssh/id_rsa.pub' and admin username 'dbadmin' in resource group 'rg-db'."
- "Deploy a new VM with OS disk size '128' and OS disk type 'Premium_LRS' in resource group 'rg-storage' named 'storvm01' with admin username 'storageadmin' in location 'southcentralus'."
- "Create a VM with size 'Standard_E4s_v3' and no public IP in resource group 'rg-secure' named 'securevm01' with admin username 'secadmin' in location 'eastus'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Admin username** |  Required | The admin username for the VM. Required for VM creation. |
| **Location** |  Required | The Azure region/location. Defaults to the resource group's location if not specified. |
| **Resource group** |  Required | The name of the Azure resource group. This resource group is a logical container for Azure resources. |
| **VM name** |  Required | The name of the virtual machine. |
| **Admin password** |  Optional | The admin password for Windows VMs or when SSH key is not provided for Linux VMs. |
| **Image** |  Optional | The OS image to use. Can be URN (publisher:offer:SKU:version) or alias like 'Ubuntu2404', 'Win2022Datacenter'. Defaults to Ubuntu 24.04 LTS. |
| **Network security group** |  Optional | Name of the network security group to use or create. |
| **No public IP** |  Optional | Do not create or assign a public IP address. |
| **Os disk size gb** |  Optional | OS disk size in GB. Defaults based on image requirements. |
| **Os disk type** |  Optional | OS disk type: 'Premium_LRS', 'StandardSSD_LRS', 'Standard_LRS'. Defaults based on VM size. |
| **Os type** |  Optional | The Operating System type of the disk. Accepted values: `Linux`, `Windows`. |
| **Public IP address** |  Optional | Name of the public IP address to use or create. |
| **Source address prefix** |  Optional | Source IP address range for NSG inbound rules (for example, `'203.0.113.0/24'` or a specific IP). Defaults to '*' (any source). |
| **Ssh public key** |  Optional | SSH public key for Linux VMs. Can be the key content or path to a file. |
| **Subnet** |  Optional | Name of the subnet within the virtual network. |
| **Subscription** |  Optional | Specifies the Azure subscription to use. Accepts either a subscription ID (GUID) or display name. If not, specified, the AZURE_SUBSCRIPTION_ID environment variable is used instead. |
| **Virtual network** |  Optional | Name of an existing virtual network to use. If not, specified, a new one is created. |
| **VM size** |  Optional | The VM size (for example, `Standard_D2s_v3`, `Standard_B2s`). Defaults to Standard_DS1_v2 if not specified. |
| **Zone** |  Optional | Availability zone into which to provision the resource. |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ✅ | Idempotent: ❌ | Open World: ❌ | Read Only: ❌ | Secret: ✅ | Local Required: ❌

## Create virtual machine scale set

<!-- @mcpcli compute vmss create -->

This tool creates an Azure Virtual Machine Scale Set (VMSS) to run multiple identical virtual machine instances. A VMSS helps you scale horizontally, distribute load, and increase availability for stateless workloads. This tool is part of the Model Context Protocol (MCP) server. By default, the tool provisions 2 instances, uses `Standard_DS1_v2` VM size, and selects `Ubuntu 24.04 LTS` as the OS image. Provide an SSH public key or a path to a public key file for `Linux` VMs, or provide an admin password for `Windows` VMs. The tool accepts existing virtual network and subnet names, or it creates a new virtual network if none is provided. Example prompt: Create a VMSS named 'web-scale' in resource group 'prod-rg' in location 'eastus' with 3 instances and `Standard_D2s_v3` VM size.

Example prompts include:

- "Create a virtual machine scale set with VMSS name 'web-vmss' in resource group 'rg-prod' at location 'eastus' and admin username 'azureadmin'."
- "Create a VMSS with VMSS name 'api-scale' in resource group 'rg-backend' at location 'westeurope' with admin username 'adminuser' and instance count '3'."
- "Deploy a Linux VMSS with VMSS name 'web-frontend-vmss' in resource group 'rg-web' at location 'centralus' using admin username 'ubuntuadmin' and SSH public key '~/.ssh/id_rsa.pub'."
- "Create a VMSS with VMSS name 'db-scale-vmss' in resource group 'rg-data' at location 'eastus2' with admin username 'sqladmin', VM size 'Standard_D2s_v3', and upgrade policy 'Rolling'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Admin username** |  Required | The admin username for the VM. Required for VM creation. |
| **Location** |  Required | The Azure region/location. Defaults to the resource group's location if not specified. |
| **Resource group** |  Required | The name of the Azure resource group. This resource group is a logical container for Azure resources. |
| **Virtual machine scale set (VMSS) name** |  Required | The name of the virtual machine scale set. |
| **Admin password** |  Optional | The admin password for Windows VMs or when SSH key is not provided for Linux VMs. |
| **Image** |  Optional | The OS image to use. Can be URN (publisher:offer:SKU:version) or alias like 'Ubuntu2404', 'Win2022Datacenter'. Defaults to Ubuntu 24.04 LTS. |
| **Instance count** |  Optional | Number of VM instances in the scale set. Default is 2. |
| **Os disk size gb** |  Optional | OS disk size in GB. Defaults based on image requirements. |
| **Os disk type** |  Optional | OS disk type: 'Premium_LRS', 'StandardSSD_LRS', 'Standard_LRS'. Defaults based on VM size. |
| **Os type** |  Optional | The Operating System type of the disk. Accepted values: `Linux`, `Windows`. |
| **Ssh public key** |  Optional | SSH public key for Linux VMs. Can be the key content or path to a file. |
| **Subnet** |  Optional | Name of the subnet within the virtual network. |
| **Subscription** |  Optional | Specifies the Azure subscription to use. Accepts either a subscription ID (GUID) or display name. If not, specified, the AZURE_SUBSCRIPTION_ID environment variable is used instead. |
| **Upgrade policy** |  Optional | Upgrade policy mode: 'Automatic', 'Manual', or 'Rolling'. Default is 'Manual'. |
| **Virtual network** |  Optional | Name of an existing virtual network to use. If not, specified, a new one is created. |
| **VM size** |  Optional | The VM size (for example, `Standard_D2s_v3`, `Standard_B2s`). Defaults to Standard_DS1_v2 if not specified. |
| **Zone** |  Optional | Availability zone into which to provision the resource. |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ✅ | Idempotent: ❌ | Open World: ❌ | Read Only: ❌ | Secret: ✅ | Local Required: ❌

## Delete compute disk

<!-- @mcpcli compute disk delete -->

This tool deletes an Azure managed disk in the specified resource group. The operation is idempotent and returns `Deleted = true` if the disk is removed, or `Deleted = false` if the disk isn't found. Detach the disk from any virtual machine before you delete it.

Example prompts include:

- "Delete the managed disk 'data-disk-01' in resource group 'rg-prod'."
- "Remove managed disk 'osdisk-2024' from resource group 'rg-staging'."
- "Delete disk 'db-disk-3' in resource group 'rg-production' in subscription '123e4567-e89b-12d3-a456-426614174000'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Disk name** |  Required | The name of the disk. |
| **Resource group** |  Required | The name of the Azure resource group. This resource group is a logical container for Azure resources. |
| **Subscription** |  Optional | Specifies the Azure subscription to use. Accepts either a subscription ID (GUID) or display name. If not, specified, the AZURE_SUBSCRIPTION_ID environment variable is used instead. |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ✅ | Idempotent: ✅ | Open World: ❌ | Read Only: ❌ | Secret: ✅ | Local Required: ❌

## Delete virtual machine

<!-- @mcpcli compute vm delete -->

This tool, part of the Model Context Protocol (MCP) server, deletes an Azure Virtual Machine (VM). The operation is irreversible; the VM data is permanently deleted. Use the `Force deletion` parameter to force deletion when the VM is running or in a failed state. Associated resources such as disks, network interfaces (NICs), and public IPs aren't deleted automatically. To delete a virtual machine scale set, use the VMSS delete tool.

Example: Delete VM 'web-prod-01' in resource group 'rg-prod'.

Example prompts include:

- "Delete VM 'vm-prod-01' in resource group 'rg-prod'."
- "Remove virtual machine 'web-vm-02' from resource group 'rg-web-prod'."
- "Destroy VM 'db-server-01' in resource group 'rg-databases'."
- "Force delete VM 'test-vm-03' in resource group 'rg-test' using force-deletion."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Resource group** |  Required | The name of the Azure resource group. This resource group is a logical container for Azure resources. |
| **VM name** |  Required | The name of the virtual machine. |
| **Force deletion** |  Optional | Force delete the resource even if it's in a running or failed state (passes forceDeletion=`true` to the Azure API). |
| **Subscription** |  Optional | Specifies the Azure subscription to use. Accepts either a subscription ID (GUID) or display name. If not, specified, the `AZURE_SUBSCRIPTION_ID` environment variable is used. |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ✅ | Idempotent: ✅ | Open World: ❌ | Read Only: ❌ | Secret: ✅ | Local Required: ❌

## Delete virtual machine scale set

<!-- @mcpcli compute vmss delete -->

This tool deletes an Azure Virtual Machine Scale Set (VMSS) and all its virtual machine instances. This operation is irreversible; it permanently deletes the scale set and all VMSS instances. Use the force deletion option to force delete the scale set even if it's in a running or failed state; the tool passes forceDeletion=`true` to the Azure API. To remove a single VM from a scale set, delete the VM resource separately.

Example prompts include:

- "Delete scale set 'web-vmss-prod' in resource group 'rg-prod'."
- "Remove VMSS 'backend-vmss' from resource group 'rg-apps'."
- "Destroy virtual machine scale set 'analytics-vmss' in resource group 'rg-analytics'."
- "Force delete VMSS 'test-vmss' in resource group 'rg-test' using force-deletion."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Resource group** |  Required | The name of the Azure resource group, a logical container for resources. |
| **Virtual machine scale set (VMSS) name** |  Required | The name of the virtual machine scale set. |
| **Force deletion** |  Optional | Force delete the resource even if it's in a running or failed state; the tool passes forceDeletion=`true` to the Azure API. |
| **Subscription** |  Optional | Specifies the Azure subscription to use. Accepts a subscription ID (GUID) or display name. If not, specified, the `AZURE_SUBSCRIPTION_ID` environment variable is used. |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ✅ | Idempotent: ✅ | Open World: ❌ | Read Only: ❌ | Secret: ✅ | Local Required: ❌

## Get compute disk

<!-- @mcpcli compute disk get -->

This tool lists Azure managed disks in a subscription or resource group, or returns detailed information for a specific disk. It returns disk size, SKU, provisioning state, and OS type. The tool supports wildcard patterns in disk names, for example `win_OsDisk*`. When you provide a `disk name` without a `resource group`, this tool searches across the `subscription`. When you specify a `resource group`, the tool limits the search to that `resource group`. Both parameters are optional.

Example prompts include:

- "List all managed disks in subscription 'Contoso Subscription'."
- "Show all disks in resource group 'rg-prod'."
- "Get details of disk 'osdisk-webapp' in resource group 'webapp-prod'."
- "Show disk sizes in resource group 'rg-storage'."
- "What managed disks are available?"
- "Get information about disk 'win-osdisk-01'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Disk name** |  Optional | The name of the disk. |
| **Resource group** |  Optional | The name of the Azure resource group. This resource group is a logical container for Azure resources. |
| **Subscription** |  Optional | Specifies the Azure subscription to use. Accepts either a subscription ID (GUID) or display name. If not, specified, the AZURE_SUBSCRIPTION_ID environment variable is used instead. |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌ |

## Get virtual machine

<!-- @mcpcli compute vm get -->

This Model Context Protocol (MCP) tool retrieves Azure Virtual Machines (VM) configuration and properties in a resource group. By default, the tool returns VM details such as name, location, size, provisioning state, and OS type. When you specify a `VM name` and request `instance view`, the response also includes the VM power state (for example, `running`, `stopped`, or `deallocated`). Specify a `resource group` or `subscription` to scope the results. Common tasks include listing all VMs in a resource group and getting detailed status for a specific VM.

Example prompts include:

- "List all virtual machines in my subscription."
- "Show me all VMs in my subscription."
- "What virtual machines do I have?"
- "List virtual machines in resource group 'rg-prod'."
- "Show me VMs in resource group 'webapp-dev'."
- "What VMs are in resource group 'rg-staging'?"
- "Get details for virtual machine 'appvm-01' in resource group 'prod-rg'."
- "Show me virtual machine 'dbserver-01' in resource group 'db-rg'."
- "What are the details of VM 'analytics-vm' in resource group 'analytics-rg'?"
- "Get virtual machine 'webvm-prod' with instance view in resource group 'rg-prod'."
- "Show me VM 'backend-vm' with runtime status in resource group 'backend-rg'."
- "What is the power state of virtual machine 'batch-vm' in resource group 'batch-rg'?"
- "Get VM 'cache-vm' status and provisioning state in resource group 'cache-rg'."
- "Show me the current status of VM 'jumpbox-01'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Instance view** |  Optional | Include instance view details (only available when retrieving a specific VM). |
| **Resource group** |  Optional | The name of the Azure resource group. This resource group is a logical container for Azure resources. |
| **Subscription** |  Optional | Specifies the Azure subscription to use. Accepts either a subscription ID (GUID) or display name. If not, specified, the AZURE_SUBSCRIPTION_ID environment variable is used instead. |
| **VM name** |  Optional | The name of the virtual machine. |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌

## Get virtual machine scale set

<!-- @mcpcli compute vmss get -->

This tool lists Azure Virtual Machine Scale Sets (VMSS) and their instances in a subscription or resource group. It returns scale set details, such as name, location, SKU, capacity, upgrade policy, and individual VM instance information. You can filter results by `resource group`, `subscription`, `Virtual machine scale set (VMSS) name`, or `instance ID`. This tool is part of the Model Context Protocol (MCP) server.

### Examples

- Get the VMSS named 'web-scale' in resource group 'rg-prod'
- Get instance '2' of VMSS 'web-scale' in resource group 'rg-prod'
- List VMSS in subscription '01234567-89ab-cdef-0123-456789abcdef'

Example prompts include:

- "List all virtual machine scale sets in my subscription."
- "List virtual machine scale sets in resource group 'rg-prod'."
- "What scale sets are in resource group 'rg-test'?"
- "Get details for virtual machine scale set 'webserver-vmss' in resource group 'rg-prod'."
- "Show me VMSS 'api-vmss' in resource group 'rg-dev'."
- "Show me instance '3' of VMSS 'batch-vmss' in resource group 'rg-prod'."
- "What is the status of instance '5' in scale set 'api-vmss'?"

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Instance ID** |  Optional | The instance ID of the virtual machine in the scale set. |
| **Resource group** |  Optional | The name of the Azure resource group. This resource group is a logical container for Azure resources. |
| **Subscription** |  Optional | Specifies the Azure subscription to use. Accepts either a subscription ID (GUID) or display name. If not, specified, the AZURE_SUBSCRIPTION_ID environment variable is used instead. |
| **Virtual machine scale set (VMSS) name** |  Optional | The name of the virtual machine scale set. |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌

## Update compute disk

<!-- @mcpcli compute disk update -->

This tool updates or modifies properties of an existing Azure managed disk that you previously created. This tool is part of the Model Context Protocol (MCP) server. If you don't specify the `Resource group`, this tool locates the disk by name within the `Subscription`. You can change the disk size (only increases), the storage `SKU`, `Disk iops read write` and `Disk mbps read write` (UltraSSD only), `Max shares` for shared attachments, `Enable bursting`, `Tags`, `Disk encryption set`, `Disk access`, `Tier`, and the `Network access policy`. Modify the network access policy to `DenyAll`, `AllowAll`, or `AllowPrivate`. This tool updates only the properties you specify; it leaves unspecified properties unchanged.

Example prompts include:

- "Update disk 'db-disk-01' in resource group 'rg-prod' to '256' GB."
- "Change the SKU of disk 'disk-web-01' to 'Premium_LRS'."
- "Resize disk 'backup-disk-01' in resource group 'rg-backup' to '512' GB."
- "Enable bursting on disk 'app-disk-01' with value 'true'."
- "Set the max shares on disk 'shared-disk-01' in resource group 'rg-shared' to '2'."
- "Change the network access policy of disk 'secure-disk-01' to 'DenyAll'."
- "Update disk 'staging-disk-01' in resource group 'rg-staging' with tags 'env=staging'."
- "Set the IOPS limit on Ultra disk 'ultra-disk-01' in resource group 'rg-ultra' to '10000'."
- "Update the throughput of disk 'perf-disk-01' in resource group 'rg-perf' to '500' MBps."
- "Change the performance tier of disk 'tiered-disk-01' in resource group 'rg-prod' to 'P40'."
- "Update disk 'encrypted-disk-01' in resource group 'rg-secure' to use disk encryption set '/subscriptions/12345678-1234-1234-1234-1234567890ab/resourceGroups/rg-secure/providers/Microsoft.Compute/diskEncryptionSets/des-prod'."
- "Change the encryption type of disk 'encdisk-01' in resource group 'rg-sec' to 'EncryptionAtRestWithPlatformAndCustomerKeys'."
- "Set disk access on disk 'private-disk-01' in resource group 'rg-private' to '/subscriptions/12345678-1234-1234-1234-1234567890ab/resourceGroups/rg-private/providers/Microsoft.Compute/diskAccesses/da-private' with network access policy 'AllowPrivate'."
- "Update disk 'dev-disk-01' to 'Standard_LRS' SKU with '512' GB size and tags 'env=dev'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Disk name** |  Required | The name of the disk. |
| **Disk access** |  Optional | Resource ID of the disk access resource that enables private endpoints for disks. |
| **Disk encryption set** |  Optional | Resource ID of the disk encryption set to use for enabling encryption at rest. |
| **Disk iops read write** |  Optional | The number of IOPS allowed for this disk. Only settable for UltraSSD disks. |
| **Disk mbps read write** |  Optional | The bandwidth allowed for this disk in MBps. Only settable for UltraSSD disks. |
| **Enable bursting** |  Optional | Enable on-demand bursting beyond the provisioned performance target of the disk. Does not apply to Ultra disks. Accepted values: `true`, `false`. |
| **Encryption type** |  Optional | Encryption type of the disk. Accepted values: `EncryptionAtRestWithCustomerKey`, `EncryptionAtRestWithPlatformAndCustomerKeys`, `EncryptionAtRestWithPlatformKey`. |
| **Max shares** |  Optional | The maximum number of VMs that can attach to the disk at the same time. A value greater than one indicates a shared disk. |
| **Network access policy** |  Optional | Policy for accessing the disk through network. Accepted values: `AllowAll`, `AllowPrivate`, `DenyAll`. |
| **Resource group** |  Optional | The name of the Azure resource group. This resource group is a logical container for Azure resources. |
| **Size gb** |  Optional | Size of the disk in GB. Max size: 4095 GB. |
| **SKU** |  Optional | Underlying storage SKU. Accepted values: `Premium_LRS`, `PremiumV2_LRS`, `Premium_ZRS`, `StandardSSD_LRS`, `StandardSSD_ZRS`, `Standard_LRS`, `UltraSSD_LRS`. |
| **Subscription** |  Optional | Specifies the Azure subscription to use. Accepts either a subscription ID (GUID) or display name. If not, specified, the `AZURE_SUBSCRIPTION_ID` environment variable is used instead. |
| **Tags** |  Optional | Space-separated tags in `key=value` format. Use `''` to clear existing tags. |
| **Tier** |  Optional | Performance tier of the disk (for example, `P10`, `P15`, `P20`, `P30`, `P40`, `P50`, `P60`, `P70`, `P80`). Applicable to Premium SSD disks only. |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ✅ | Idempotent: ✅ | Open World: ❌ | Read Only: ❌ | Secret: ❌ | Local Required: ❌

## Update virtual machine

<!-- @mcpcli compute vm update -->

This tool updates an existing Azure Virtual Machine (VM). It modifies VM properties such as size, tags, boot diagnostics, license type, and user data. This tool is part of the Model Context Protocol (MCP) server. You may need to deallocate the VM before resizing to certain sizes. You need role-based access control (RBAC) permissions for the subscription and resource group to update a VM. The tool returns the updated VM resource.

Example prompts include:

- "Add tags to VM 'webapp-prod' in resource group 'rg-prod'."
- "Update virtual machine 'app-server-01' in resource group 'rg-prod' with tags 'environment=production'."
- "Update VM 'db-vm-02' in resource group 'rg-databases' to enable boot diagnostics 'true'."
- "Change the size of VM 'compute-node-3' in resource group 'rg-compute' to vm size 'Standard_D4s_v3'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Resource group** |  Required | The name of the Azure resource group. This resource group is a logical container for Azure resources. |
| **VM name** |  Required | The name of the virtual machine. |
| **Boot diagnostics** |  Optional | Enable or disable boot diagnostics: `true` or `false`. |
| **License type** |  Optional | License type for Azure Hybrid Benefit: `Windows_Server`, `Windows_Client`, `RHEL_BYOS`, `SLES_BYOS`, or `None` to disable. |
| **Subscription** |  Optional | Specifies the Azure subscription to use. Accepts either a subscription ID (GUID) or display name. If not, specified, `AZURE_SUBSCRIPTION_ID` is used. |
| **Tags** |  Optional | Space-separated tags in `key=value` format. Provide an empty string to clear existing tags. |
| **User data** |  Optional | Base64-encoded user data for the VM, used for custom data scripts. |
| **VM size** |  Optional | The VM size (for example, `Standard_D2s_v3`, `Standard_B2s`). Defaults to `Standard_DS1_v2` if not specified. |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ✅ | Idempotent: ✅ | Open World: ❌ | Read Only: ❌ | Secret: ❌ | Local Required: ❌

## Update virtual machine scale set

<!-- @mcpcli compute vmss update -->

This Model Context Protocol (MCP) tool updates an existing Azure Virtual Machine Scale Set (VMSS). This tool scales instance count, resizes VMs, changes the upgrade policy, and updates tags on a scale set. Changes may require `update-instances` to roll out to existing VMs. To create a new VMSS, use the VMSS create tool. To update a single VM, use the VM update tool.

Example prompts include:

- "Update the capacity to 10 for VMSS 'web-vmss' in resource group 'rg-prod'."
- "Enable automatic OS upgrades on VMSS 'api-vmss' in resource group 'rg-staging'."
- "Set upgrade policy to 'Rolling' for VMSS 'compute-vmss' in resource group 'rg-scale'."
- "Add tags 'env=prod owner=team-ops' to scale set 'batch-vmss' in resource group 'rg-ops'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Resource group** |  Required | The name of the Azure resource group. This resource group is a logical container for Azure resources. |
| **Virtual machine scale set (VMSS) name** |  Required | The name of the virtual machine scale set. |
| **Capacity** |  Optional | Number of VM instances (capacity) in the scale set. |
| **Enable auto os upgrade** |  Optional | Enable automatic OS image upgrades. Requires health probes or the Application Health extension. |
| **Overprovision** |  Optional | Enable or disable overprovisioning. When enabled, Azure provisions more VMs than requested and deletes the extra VMs after deployment. |
| **Scale in policy** |  Optional | Scale-in policy to determine which VMs to remove: `Default`, `NewestVM`, or `OldestVM`. |
| **Subscription** |  Optional | Specifies the Azure subscription to use. Accepts either a subscription ID (GUID) or the display name. If not, specified, the `AZURE_SUBSCRIPTION_ID` environment variable is used. |
| **Tags** |  Optional | Space-separated tags in `key=value` format. Use `''` to clear existing tags. |
| **Upgrade policy** |  Optional | Upgrade policy mode: `Automatic`, `Manual`, or `Rolling`. Default is `Manual`. |
| **VM size** |  Optional | The VM size (for example, `Standard_D2s_v3`, `Standard_B2s`). Defaults to `Standard_DS1_v2` if not specified. |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ✅ | Idempotent: ✅ | Open World: ❌ | Read Only: ❌ | Secret: ❌ | Local Required: ❌

## Related content

- [What are the Azure MCP Server tools?](index.md)
- [Get started using Azure MCP Server](../get-started.md)
- [Azure Virtual Machines documentation](/azure/virtual-machines/)