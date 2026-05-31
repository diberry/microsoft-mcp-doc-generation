---

title: Azure MCP Server tools for Azure Compute
description: Use Azure MCP Server tools to manage compute resources such as virtual machines, managed disks, and virtual machine scale sets with natural language prompts from your IDE.
ms.date: 05/29/2026
ms.service: azure-mcp-server
ms.topic: concept-article
tool_count: 13
mcp-cli.version: 3.0.0-beta.13+cd8d1e8f9924440b33e3e908c390c1599700ccba
author: diberry
ms.author: diberry
ms.reviewer: mbaldwin
ai-usage: ai-generated
ms.custom: build-2025
content_well_notification:
  - AI-contribution
---

# Azure MCP Server tools for Azure Compute

The Azure Model Context Protocol (MCP) Server lets you manage Azure Compute resources, including: create, delete, get, power-state, and update, with natural language prompts.

Azure Compute provides a range of on-demand, scalable compute options—including virtual machines, containers, and serverless functions—to run and scale your applications and workloads in Azure. Use it to deploy VMs for lift-and-shift migrations, orchestrate containers with Azure Kubernetes Service, or build event-driven microservices with Azure Functions. For more information, see [Azure Compute documentation](/azure/compute/).

[!INCLUDE [tip-about-params](../includes/tools/parameter-consideration.md)]


## Managed disk: Create

Creates a new managed disk in the specified resource group, using Azure Managed Disks. You can create an empty disk by specifying size in gigabytes. You can create a disk from a source such as a snapshot, another managed disk, or a blob URI. You can create a disk from a Shared Image Gallery image version. You can create a disk that's ready for upload by specifying upload type and upload size in bytes. If you don't specify location, the resource group location applies.

#### [MCP Server](#tab/mcp-server)

<!-- @mcpcli compute disk create -->

Example prompts include:

- "Create a 128 GB managed disk named 'disk-128' in resource group 'rg-prod'."
- "Create a new Premium_LRS disk called 'disk-premium-256' in resource group 'rg-prod' with 256 GB."
- "Create a managed disk 'disk-eastus' in resource group 'rg-apps' in location 'eastus'."
- "Create a managed disk 'disk-from-snap' in resource group 'rg-prod' from snapshot '/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-prod/providers/Microsoft.Compute/snapshots/snap1'."
- "Create a managed disk 'disk-from-blob' in resource group 'rg-backup' from blob 'https://contoso.blob.core.windows.net/vhds/osdisk.vhd'."
- "Create a 64 GB Standard_LRS Linux disk named 'data-disk-64' in resource group 'rg-linux' in zone '1'."
- "Create a managed disk 'disk-gallery-os' in resource group 'rg-images' from gallery image '/subscriptions/11111111-1111-1111-1111-111111111111/resourceGroups/rg-images/providers/Microsoft.Compute/galleries/myGallery/images/myImage/versions/1.0.0'."
- "Create a disk ready for upload named 'disk-upload' in resource group 'rg-upload' with upload size bytes '20972032' and upload type 'Upload'."
- "Create a Trusted Launch upload disk named 'disk-trusted' in resource group 'rg-secure' with upload type 'UploadWithSecurityData' and security type 'TrustedLaunch'."
- "Create an UltraSSD_LRS disk named 'disk-ultra' in resource group 'rg-performance' with 256 GB, disk iops read write '10000', and disk mbps read write '500'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Disk name** |  Required | The name of the disk. |
| **Resource group** |  Required | The name of the Azure resource group. This resource group is a logical container for Azure resources. |
| **Disk access** |  Optional | Resource ID of the disk access resource for using private endpoints on disks. |
| **Disk encryption set** |  Optional | Resource ID of the disk encryption set to use for enabling encryption at rest. |
| **Disk iops read write** |  Optional | The number of IOPS allowed for this disk. Only settable for UltraSSD disks. |
| **Disk mbps read write** |  Optional | The bandwidth allowed for this disk in MBps. Only settable for UltraSSD disks. |
| **Enable bursting** |  Optional | Enable on-demand bursting beyond the provisioned performance target of the disk. Does not apply to Ultra disks. Accepted values: `true`, `false`. |
| **Encryption type** |  Optional | Encryption type of the disk. Accepted values: `EncryptionAtRestWithCustomerKey`, `EncryptionAtRestWithPlatformAndCustomerKeys`, `EncryptionAtRestWithPlatformKey`. |
| **Gallery image reference** |  Optional | Resource ID of a Shared Image Gallery image version to use as the source for the disk. Format: /subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.Compute/galleries/{gallery}/images/{image}/versions/{version}. |
| **Gallery image reference lun** |  Optional | LUN (Logical Unit Number) of the data disk in the gallery image version. If specified, the disk is created from the data disk at this LUN. If not specified, the disk is created from the OS disk of the image. |
| **Hyper v generation** |  Optional | The hypervisor generation of the Virtual Machine. Applicable to OS disks only. Accepted values: `V1`, `V2`. |
| **Location** |  Optional | The Azure region/location. Defaults to the resource group's location if not specified. |
| **Max shares** |  Optional | The maximum number of VMs that can attach to the disk at the same time. Value greater than one indicates a shared disk. |
| **Network access policy** |  Optional | Policy for accessing the disk through network. Accepted values: `AllowAll`, `AllowPrivate`, `DenyAll`. |
| **Os type** |  Optional | The Operating System type of the disk. Accepted values: `Linux`, `Windows`. |
| **Security type** |  Optional | Security type of the managed disk. Accepted values: `ConfidentialVM_DiskEncryptedWithCustomerKey`, `ConfidentialVM_DiskEncryptedWithPlatformKey`, `ConfidentialVM_VMGuestStateOnlyEncryptedWithPlatformKey`, `Standard`, `TrustedLaunch`. Required when `--upload-type` is UploadWithSecurityData. |
| **Size gb** |  Optional | Size of the disk in GB. Max size: 4095 GB. |
| **SKU** |  Optional | Underlying storage SKU. Accepted values: `Premium_LRS`, `PremiumV2_LRS`, `Premium_ZRS`, `StandardSSD_LRS`, `StandardSSD_ZRS`, `Standard_LRS`, `UltraSSD_LRS`. |
| **Source** |  Optional | Source to create the disk from, including a resource ID of a snapshot or disk, or a blob URI of a VHD. When a source is provided, `--size-gb` is optional and defaults to the source size. |
| **Tags** |  Optional | Space-separated tags in 'key=value' format. Use '' to clear existing tags. |
| **Tier** |  Optional | Performance tier of the disk (for example, `P10`, `P15`, `P20`, `P30`, `P40`, `P50`, `P60`, `P70`, `P80`). Applicable to Premium SSD disks only. |
| **Upload size bytes** |  Optional | The size in bytes (including the VHD footer of 512 bytes) of the content to be uploaded. Required when `--upload-type` is specified. |
| **Upload type** |  Optional | Type of upload for the disk. Accepted values: `Upload`, `UploadWithSecurityData`. When specified, the disk is created in a ReadyToUpload state. |
| **Zone** |  Optional | Availability zone into which to provision the resource. |

#### [Azure MCP CLI](#tab/azure-mcp-cli)

**Example CLI command**

```console
azmcp compute disk create \
  --resource-group <resource-group> \
  --disk-name <disk-name> \
  [--source <source>] \
  [--location <location>] \
  [--size-gb <size-gb>] \
  [--sku <sku>] \
  [--os-type <os-type>] \
  [--zone <zone>] \
  [--hyper-v-generation <hyper-v-generation>] \
  [--max-shares <max-shares>] \
  [--network-access-policy <network-access-policy>] \
  [--enable-bursting <enable-bursting>] \
  [--tags <tags>] \
  [--disk-encryption-set <disk-encryption-set>] \
  [--encryption-type <encryption-type>] \
  [--disk-access <disk-access>] \
  [--tier <tier>] \
  [--gallery-image-reference <gallery-image-reference>] \
  [--gallery-image-reference-lun <gallery-image-reference-lun>] \
  [--disk-iops-read-write <disk-iops-read-write>] \
  [--disk-mbps-read-write <disk-mbps-read-write>] \
  [--upload-type <upload-type>] \
  [--upload-size-bytes <upload-size-bytes>] \
  [--security-type <security-type>]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--resource-group` | string | Yes | The name of the Azure resource group. This is a logical container for Azure resources. |
| `--disk-name` | string | Yes | The name of the disk |
| `--source` | string | No | Source to create the disk from, including a resource ID of a snapshot or disk, or a blob URI of a VHD. When a source is provided, --size-gb is optional and defaults to the source size. |
| `--location` | string | No | The Azure region/location. Defaults to the resource group's location if not specified. |
| `--size-gb` | string | No | Size of the disk in GB. Max size: 4095 GB. |
| `--sku` | string | No | Underlying storage SKU. Accepted values: Premium_LRS, PremiumV2_LRS, Premium_ZRS, StandardSSD_LRS, StandardSSD_ZRS, Standard_LRS, UltraSSD_LRS. |
| `--os-type` | string | No | The Operating System type of the disk. Accepted values: Linux, Windows. |
| `--zone` | string | No | Availability zone into which to provision the resource. |
| `--hyper-v-generation` | string | No | The hypervisor generation of the Virtual Machine. Applicable to OS disks only. Accepted values: V1, V2. |
| `--max-shares` | string | No | The maximum number of VMs that can attach to the disk at the same time. Value greater than one indicates a shared disk. |
| `--network-access-policy` | string | No | Policy for accessing the disk via network. Accepted values: AllowAll, AllowPrivate, DenyAll. |
| `--enable-bursting` | string | No | Enable on-demand bursting beyond the provisioned performance target of the disk. Does not apply to Ultra disks. Accepted values: true, false. |
| `--tags` | string | No | Space-separated tags in 'key=value' format. Use '' to clear existing tags. |
| `--disk-encryption-set` | string | No | Resource ID of the disk encryption set to use for enabling encryption at rest. |
| `--encryption-type` | string | No | Encryption type of the disk. Accepted values: EncryptionAtRestWithCustomerKey, EncryptionAtRestWithPlatformAndCustomerKeys, EncryptionAtRestWithPlatformKey. |
| `--disk-access` | string | No | Resource ID of the disk access resource for using private endpoints on disks. |
| `--tier` | string | No | Performance tier of the disk (e.g., P10, P15, P20, P30, P40, P50, P60, P70, P80). Applicable to Premium SSD disks only. |
| `--gallery-image-reference` | string | No | Resource ID of a Shared Image Gallery image version to use as the source for the disk. Format: /subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.Compute/galleries/{gallery}/images/{image}/versions/{version}. |
| `--gallery-image-reference-lun` | string | No | LUN (Logical Unit Number) of the data disk in the gallery image version. If specified, the disk is created from the data disk at this LUN. If not specified, the disk is created from the OS disk of the image. |
| `--disk-iops-read-write` | string | No | The number of IOPS allowed for this disk. Only settable for UltraSSD disks. |
| `--disk-mbps-read-write` | string | No | The bandwidth allowed for this disk in MBps. Only settable for UltraSSD disks. |
| `--upload-type` | string | No | Type of upload for the disk. Accepted values: Upload, UploadWithSecurityData. When specified, the disk is created in a ReadyToUpload state. |
| `--upload-size-bytes` | string | No | The size in bytes (including the VHD footer of 512 bytes) of the content to be uploaded. Required when --upload-type is specified. |
| `--security-type` | string | No | Security type of the managed disk. Accepted values: ConfidentialVM_DiskEncryptedWithCustomerKey, ConfidentialVM_DiskEncryptedWithPlatformKey, ConfidentialVM_VMGuestStateOnlyEncryptedWithPlatformKey, Standard, TrustedLaunch. Required when --upload-type is UploadWithSecurityData. |

---

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ✅ | Idempotent: ❌ | Open World: ❌ | Read Only: ❌ | Secret: ❌ | Local Required: ❌

## Managed disk: Delete

Deletes an Azure managed disk from the specified resource group. This operation is idempotent, and returns `Deleted = true` if the disk is removed, or `Deleted = false` if the disk isn't found. The disk must not be attached to a virtual machine. Detach the disk before you delete it. Delete unused disks to avoid ongoing storage charges.

#### [MCP Server](#tab/mcp-server)

<!-- @mcpcli compute disk delete -->

Example prompts include:

- "Delete managed disk 'db-disk-01' from resource group 'rg-prod'."
- "Remove disk 'webapp-os-disk' in resource group 'web-rg'."
- "In resource group 'backup-rg', delete the managed disk 'backup-disk-2026'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Disk name** |  Required | The name of the disk. |
| **Resource group** |  Required | The name of the Azure resource group. This resource group is a logical container for Azure resources. |

#### [Azure MCP CLI](#tab/azure-mcp-cli)

**Example CLI command**

```console
azmcp compute disk delete \
  --resource-group <resource-group> \
  --disk-name <disk-name>
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--resource-group` | string | Yes | The name of the Azure resource group. This is a logical container for Azure resources. |
| `--disk-name` | string | Yes | The name of the disk |

---

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ✅ | Idempotent: ✅ | Open World: ❌ | Read Only: ❌ | Secret: ✅ | Local Required: ❌

## Managed disk: List or get

Lists available Azure managed disks, or returns details for a specific disk. Shows all disks in a subscription or in a resource group, including disk size, SKU, provisioning state, and OS type. Supports wildcard patterns in disk names, for example `win_OsDisk*`. If you provide a `disk name` without a `resource group`, the tool searches the entire subscription. If you provide a `resource group`, the tool limits the search to that resource group.

#### [MCP Server](#tab/mcp-server)

<!-- @mcpcli compute disk get -->

Example prompts include:

- "Show all managed disks in my subscription."
- "Show me all disks in resource group 'rg-prod'."
- "Get details of disk 'win_OsDisk1' in resource group 'rg-prod'."
- "Show the disk sizes in resource group 'rg-storage'."
- "Which managed disks are available in my subscription?"
- "Get information about disk 'data-disk-001'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Disk name** |  Optional | The name of the disk. |

#### [Azure MCP CLI](#tab/azure-mcp-cli)

**Example CLI command**

```console
azmcp compute disk get \
  [--resource-group <resource-group>] \
  [--disk-name <disk-name>]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--resource-group` | string | No | The name of the Azure resource group. This is a logical container for Azure resources. |
| `--disk-name` | string | No | The name of the disk |

---

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌

## Managed disk: Update

Update properties of an existing Azure managed disk. If you don't specify the resource group, the command locates the disk by the `Disk name` within the subscription.

#### [MCP Server](#tab/mcp-server)

<!-- @mcpcli compute disk update -->

Example prompts include:

- "Update disk 'disk-prod-01' to size gb '256'."
- "Change the SKU of disk 'db-disk-01' to SKU 'Premium_LRS'."
- "Update disk 'appdisk-burst' with enable bursting 'true'."
- "Set the max shares on disk 'shared-disk-01' to max shares '2'."
- "Change the network access policy of disk 'secure-disk-01' to network access policy 'DenyAll'."
- "Update disk 'config-disk-01' with tags 'env=staging'."
- "Set the IOPS limit on ultra disk 'ultra-disk-01' to disk IOPS read write '10000'."
- "Update the throughput of disk 'ultra-disk-02' to disk MBps read write '500'."
- "Change the performance tier of disk 'perf-disk-01' to tier 'P40'."
- "Set disk access on disk 'private-disk-01' to disk access '/subscriptions/0000/resourceGroups/rg-prod/providers/Microsoft.Compute/diskAccesses/da1' with network access policy 'AllowPrivate'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Disk name** |  Required | The name of the disk. |
| **Disk access** |  Optional | Resource ID of the disk access resource for using private endpoints on disks. |
| **Disk encryption set** |  Optional | Resource ID of the disk encryption set to use for enabling encryption at rest. |
| **Disk iops read write** |  Optional | The number of IOPS allowed for this disk. Only settable for UltraSSD disks. |
| **Disk mbps read write** |  Optional | The bandwidth allowed for this disk in MBps. Only settable for UltraSSD disks. |
| **Enable bursting** |  Optional | Enable on-demand bursting beyond the provisioned performance target of the disk. Does not apply to Ultra disks. Accepted values: `true`, `false`. |
| **Encryption type** |  Optional | Encryption type of the disk. Accepted values: `EncryptionAtRestWithCustomerKey`, `EncryptionAtRestWithPlatformAndCustomerKeys`, `EncryptionAtRestWithPlatformKey`. |
| **Max shares** |  Optional | The maximum number of VMs that can attach to the disk at the same time. Value greater than one indicates a shared disk. |
| **Network access policy** |  Optional | Policy for accessing the disk through network. Accepted values: `AllowAll`, `AllowPrivate`, `DenyAll`. |
| **Size gb** |  Optional | Size of the disk in GB. Max size: 4095 GB. |
| **SKU** |  Optional | Underlying storage SKU. Accepted values: `Premium_LRS`, `PremiumV2_LRS`, `Premium_ZRS`, `StandardSSD_LRS`, `StandardSSD_ZRS`, `Standard_LRS`, `UltraSSD_LRS`. |
| **Tags** |  Optional | Space-separated tags in 'key=value' format. Use '' to clear existing tags. |
| **Tier** |  Optional | Performance tier of the disk (for example, `P10`, `P15`, `P20`, `P30`, `P40`, `P50`, `P60`, `P70`, `P80`). Applicable to Premium SSD disks only. |

#### [Azure MCP CLI](#tab/azure-mcp-cli)

**Example CLI command**

```console
azmcp compute disk update \
  --disk-name <disk-name> \
  [--resource-group <resource-group>] \
  [--size-gb <size-gb>] \
  [--sku <sku>] \
  [--disk-iops-read-write <disk-iops-read-write>] \
  [--disk-mbps-read-write <disk-mbps-read-write>] \
  [--max-shares <max-shares>] \
  [--network-access-policy <network-access-policy>] \
  [--enable-bursting <enable-bursting>] \
  [--tags <tags>] \
  [--disk-encryption-set <disk-encryption-set>] \
  [--encryption-type <encryption-type>] \
  [--disk-access <disk-access>] \
  [--tier <tier>]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--resource-group` | string | No | The name of the Azure resource group. This is a logical container for Azure resources. |
| `--disk-name` | string | Yes | The name of the disk |
| `--size-gb` | string | No | Size of the disk in GB. Max size: 4095 GB. |
| `--sku` | string | No | Underlying storage SKU. Accepted values: Premium_LRS, PremiumV2_LRS, Premium_ZRS, StandardSSD_LRS, StandardSSD_ZRS, Standard_LRS, UltraSSD_LRS. |
| `--disk-iops-read-write` | string | No | The number of IOPS allowed for this disk. Only settable for UltraSSD disks. |
| `--disk-mbps-read-write` | string | No | The bandwidth allowed for this disk in MBps. Only settable for UltraSSD disks. |
| `--max-shares` | string | No | The maximum number of VMs that can attach to the disk at the same time. Value greater than one indicates a shared disk. |
| `--network-access-policy` | string | No | Policy for accessing the disk via network. Accepted values: AllowAll, AllowPrivate, DenyAll. |
| `--enable-bursting` | string | No | Enable on-demand bursting beyond the provisioned performance target of the disk. Does not apply to Ultra disks. Accepted values: true, false. |
| `--tags` | string | No | Space-separated tags in 'key=value' format. Use '' to clear existing tags. |
| `--disk-encryption-set` | string | No | Resource ID of the disk encryption set to use for enabling encryption at rest. |
| `--encryption-type` | string | No | Encryption type of the disk. Accepted values: EncryptionAtRestWithCustomerKey, EncryptionAtRestWithPlatformAndCustomerKeys, EncryptionAtRestWithPlatformKey. |
| `--disk-access` | string | No | Resource ID of the disk access resource for using private endpoints on disks. |
| `--tier` | string | No | Performance tier of the disk (e.g., P10, P15, P20, P30, P40, P50, P60, P70, P80). Applicable to Premium SSD disks only. |

---

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ✅ | Idempotent: ✅ | Open World: ❌ | Read Only: ❌ | Secret: ❌ | Local Required: ❌

## Virtual machine: Create

Create a single Azure virtual machine (VM) with an operating system disk. You can create a Linux or Windows VM and configure SSH public key or password authentication. If you don't specify networking resources, the command creates a virtual network (VNet), subnet, network security group (NSG), network interface (NIC), and public IP address. The command uses the `Standard_D2s_v5` VM size by default when you don't provide a size. You must specify an image, for example `Ubuntu2404` or `Win2022Datacenter`, a marketplace URN in the form `publisher:offer:sku:version`, or a Shared Image Gallery image ID that starts with `/sharedGalleries/`. For Linux VMs that use SSH, specify the path to your public key file, for example `~/.ssh/id_rsa.pub`, so the public key installs on the VM.

#### [MCP Server](#tab/mcp-server)

<!-- @mcpcli compute vm create -->

Example prompts include:

- "Create a new Linux VM named 'web-vm-01' in resource group 'rg-prod' with admin username 'azureuser', image 'Ubuntu2404', location 'eastus'."
- "Create a virtual machine with VM name 'app-server' and VM size 'Standard_D2s_v5' in resource group 'rg-staging' with admin username 'adminuser', image 'Canonical:UbuntuServer:24_04-lts:latest', location 'westeurope'."
- "Create a Windows VM named 'win-web-01' in resource group 'rg-windows' with admin username 'winadmin', image 'Win2022Datacenter', location 'centralus', admin password '\<secure-password\>'."
- "Create VM 'dev-vm-01' in location 'eastus2' with resource group 'rg-dev', admin username 'devuser', image 'Ubuntu2404', and SSH public key '~/.ssh/id_rsa.pub'."
- "Deploy a new VM named 'db-server' in resource group 'rg-database' with admin username 'dbadmin', image 'Ubuntu2404', location 'eastus', OS disk size GB '128', OS disk type 'Premium_LRS'."
- "Create a VM named 'batch-worker' in resource group 'rg-batch' with admin username 'batchadmin', image 'Canonical:UbuntuServer:20_04-lts:latest', location 'southcentralus', VM size 'Standard_E4s_v3', no public IP."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Admin username** |  Required | The admin username for the VM. Required for VM creation. |
| **Image** |  Required | The OS image to use. Can be a URN (publisher:offer:SKU:version), a shared gallery image ID (starting with '/sharedGalleries/'), or an alias such as `Ubuntu2404` or `Win2022Datacenter`. |
| **Location** |  Required | The Azure region/location. |
| **Resource group** |  Required | The name of the Azure resource group. This resource group is a logical container for Azure resources. |
| **VM name** |  Required | The name of the virtual machine. |
| **Admin password** |  Optional | The admin password for Windows VMs or when SSH key is not provided for Linux VMs. |
| **Network security group name** |  Optional | Name of the network security group to use or create. |
| **No public IP** |  Optional | Do not create or assign a public IP address. |
| **Os disk size gb** |  Optional | OS disk size in GB. Defaults based on image requirements. |
| **Os disk type** |  Optional | OS disk type: `Premium_LRS`, `StandardSSD_LRS`, `Standard_LRS`. Defaults based on VM size. |
| **Os type** |  Optional | The Operating System type of the disk. Accepted values: `Linux`, `Windows`. |
| **Public IP address name** |  Optional | Name of the public IP address to use or create. |
| **Source address prefix** |  Optional | Source IP address range for NSG inbound rules (for example, `'203.0.113.0/24'` or a specific IP). Defaults to '*' (any source). |
| **Ssh public key** |  Optional | SSH public key for Linux VMs. Can be the key content or path to a file. |
| **Subnet name** |  Optional | Name of the subnet within the virtual network. |
| **Virtual network name** |  Optional | Name of an existing virtual network to use. If not specified, a new one is created. |
| **VM size** |  Optional | The VM size (for example, `Standard_D2s_v3`, `Standard_B2s`). Defaults to Standard_D2s_v5 if not specified. |
| **Zone** |  Optional | Availability zone into which to provision the resource. |

#### [Azure MCP CLI](#tab/azure-mcp-cli)

**Example CLI command**

```console
azmcp compute vm create \
  --resource-group <resource-group> \
  --vm-name <vm-name> \
  --location <location> \
  --admin-username <admin-username> \
  --image <image> \
  [--admin-password <admin-password>] \
  [--ssh-public-key <ssh-public-key>] \
  [--vm-size <vm-size>] \
  [--os-type <os-type>] \
  [--virtual-network <virtual-network>] \
  [--subnet <subnet>] \
  [--public-ip-address <public-ip-address>] \
  [--network-security-group <network-security-group>] \
  [--no-public-ip <no-public-ip>] \
  [--source-address-prefix <source-address-prefix>] \
  [--zone <zone>] \
  [--os-disk-size-gb <os-disk-size-gb>] \
  [--os-disk-type <os-disk-type>]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--resource-group` | string | Yes | The name of the Azure resource group. This is a logical container for Azure resources. |
| `--vm-name` | string | Yes | The name of the virtual machine |
| `--location` | string | Yes | The Azure region/location. Defaults to the resource group's location if not specified. |
| `--admin-username` | string | Yes | The admin username for the VM. Required for VM creation |
| `--admin-password` | string | No | The admin password for Windows VMs or when SSH key is not provided for Linux VMs |
| `--ssh-public-key` | string | No | SSH public key for Linux VMs. Can be the key content or path to a file |
| `--image` | string | Yes | The OS image to use. Can be a URN (publisher:offer:sku:version), a shared gallery image ID (starting with '/sharedGalleries/'), or an alias such as 'Ubuntu2404' or 'Win2022Datacenter'. |
| `--vm-size` | string | No | The VM size (e.g., Standard_D2s_v3, Standard_B2s). Defaults to Standard_D2s_v5 if not specified |
| `--os-type` | string | No | The Operating System type of the disk. Accepted values: Linux, Windows. |
| `--virtual-network` | string | No | Name of an existing virtual network to use. If not specified, a new one will be created |
| `--subnet` | string | No | Name of the subnet within the virtual network |
| `--public-ip-address` | string | No | Name of the public IP address to use or create |
| `--network-security-group` | string | No | Name of the network security group to use or create |
| `--no-public-ip` | string | No | Do not create or assign a public IP address |
| `--source-address-prefix` | string | No | Source IP address range for NSG inbound rules (e.g., '203.0.113.0/24' or a specific IP). Defaults to '*' (any source) |
| `--zone` | string | No | Availability zone into which to provision the resource. |
| `--os-disk-size-gb` | string | No | OS disk size in GB. Defaults based on image requirements |
| `--os-disk-type` | string | No | OS disk type: 'Premium_LRS', 'StandardSSD_LRS', 'Standard_LRS'. Defaults based on VM size |

---

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ✅ | Idempotent: ❌ | Open World: ❌ | Read Only: ❌ | Secret: ✅ | Local Required: ❌

## Virtual machine: Delete

Delete an Azure virtual machine (VM) permanently. This operation removes the VM and its data irreversibly. Associated resources, such as disks, network interfaces, and public IP addresses, aren't deleted automatically; delete them separately if you want to remove them.

#### [MCP Server](#tab/mcp-server)

<!-- @mcpcli compute vm delete -->

Example prompts include:

- "Delete VM 'web-vm-01' in resource group 'rg-prod'."
- "Remove virtual machine 'backend-vm' from resource group 'rg-services'."
- "Destroy VM 'test-vm-02' in resource group 'rg-staging'."
- "Force delete VM 'db-vm' in resource group 'rg-databases' using force-deletion."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Resource group** |  Required | The name of the Azure resource group. This resource group is a logical container for Azure resources. |
| **VM name** |  Required | The name of the virtual machine. |
| **Force deletion** |  Optional | Force delete the resource even if it is in a running or failed state (passes forceDeletion=`true` to the Azure API). |

#### [Azure MCP CLI](#tab/azure-mcp-cli)

**Example CLI command**

```console
azmcp compute vm delete \
  --resource-group <resource-group> \
  --vm-name <vm-name> \
  [--force-deletion <force-deletion>]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--resource-group` | string | Yes | The name of the Azure resource group. This is a logical container for Azure resources. |
| `--vm-name` | string | Yes | The name of the virtual machine |
| `--force-deletion` | string | No | Force delete the resource even if it is in a running or failed state (passes forceDeletion=true to the Azure API) |

---

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ✅ | Idempotent: ✅ | Open World: ❌ | Read Only: ❌ | Secret: ✅ | Local Required: ❌

## Virtual machine: List or get

List or get Azure Virtual Machines (VMs) in a subscription or in a specific resource group. You can show all VMs, or get a single VM by name. The command returns read-only VM details, including name, location, VM size, provisioning state, OS type, and network interfaces. Use the instance view option to check a VM's runtime status and power state, and to view its provisioning state. Use it to inspect VM configuration, properties, and runtime status.

#### [MCP Server](#tab/mcp-server)

<!-- @mcpcli compute vm get -->

Example prompts include:

- "List all virtual machines across my subscription."
- "Show all VMs in my subscription."
- "What virtual machines do I have in my subscription?"
- "Get all virtual machines in resource group 'rg-prod'."
- "Show VMs in resource group 'rg-webapp'."
- "What VMs are in resource group 'rg-backend'?"
- "Get details for virtual machine 'db-server-01' in resource group 'rg-prod'."
- "Get virtual machine 'api-vm' with instance view in resource group 'rg-api'."
- "What is the power state of virtual machine 'web-vm-prod' in resource group 'rg-webapp'?"
- "Show me the current status of VM 'orphan-vm'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Instance view** |  Optional | Include instance view details (only available when retrieving a specific VM). |
| **VM name** |  Optional | The name of the virtual machine. |

#### [Azure MCP CLI](#tab/azure-mcp-cli)

**Example CLI command**

```console
azmcp compute vm get \
  [--resource-group <resource-group>] \
  [--vm-name <vm-name>] \
  [--instance-view <instance-view>]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--resource-group` | string | No | The name of the Azure resource group. This is a logical container for Azure resources. |
| `--vm-name` | string | No | The name of the virtual machine |
| `--instance-view` | string | No | Include instance view details (only available when retrieving a specific VM) |

---

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌

## Vm: Power state

Change the running state of an Azure virtual machine (VM) by choosing a `Power action` such as `deallocate`, `start`, `stop`, or `restart`.

#### [MCP Server](#tab/mcp-server)

<!-- @mcpcli compute vm power-state -->

Example prompts include:

- "Apply power action 'start' for VM 'web-vm-prod' in resource group 'rg-prod'."
- "Apply power action 'stop' for VM 'api-staging' in resource group 'rg-staging'."
- "Apply power action 'deallocate' for VM 'batch-node-01' in resource group 'rg-compute' to release compute resources."
- "Apply power action 'restart' for VM 'db-primary' in resource group 'rg-database'."
- "Apply power action 'stop' for VM 'legacy-app' in resource group 'rg-legacy' and skip the OS shutdown."
- "Apply power action 'start' for VM 'ci-runner' in resource group 'rg-dev' without waiting for completion."
- "Apply power action 'deallocate' for VM 'analytics-worker' in resource group 'rg-analytics' to stop billing for compute resources."
- "Can you apply power action 'stop' to VM 'test-vm' in resource group 'rg-test'?"

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Power action** |  Required | The power action to apply to the VM (not the current power state). Accepted values: start, stop, deallocate, restart. |
| **Resource group** |  Required | The name of the Azure resource group. This resource group is a logical container for Azure resources. |
| **VM name** |  Required | The name of the virtual machine. |
| **No wait** |  Optional | Return immediately without waiting for the operation to complete. |
| **Skip shutdown** |  Optional | Skip the graceful OS shutdown and force power off. Only compatible with the 'stop' state. |

#### [Azure MCP CLI](#tab/azure-mcp-cli)

**Example CLI command**

```console
azmcp compute vm power-state \
  --resource-group <resource-group> \
  --vm-name <vm-name> \
  --power-action <power-action> \
  [--no-wait <no-wait>] \
  [--skip-shutdown <skip-shutdown>]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--resource-group` | string | Yes | The name of the Azure resource group. This is a logical container for Azure resources. |
| `--vm-name` | string | Yes | The name of the virtual machine |
| `--power-action` | string | Yes | The power action to apply to the VM (not the current power state). Accepted values: start, stop, deallocate, restart. |
| `--no-wait` | string | No | Return immediately without waiting for the operation to complete. |
| `--skip-shutdown` | string | No | Skip the graceful OS shutdown and force power off. Only compatible with the 'stop' state. |

---

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ✅ | Idempotent: ✅ | Open World: ❌ | Read Only: ❌ | Secret: ❌ | Local Required: ❌

## Virtual machine: Update

Update an existing Azure virtual machine (VM) configuration. The `update` command changes VM settings such as tags, size, boot diagnostics, and user data. Resizing may require you to deallocate the VM before you change to some sizes. The command doesn't change the VM's power state, create new VMs, or update virtual machine scale sets.

#### [MCP Server](#tab/mcp-server)

<!-- @mcpcli compute vm update -->

Example prompts include:

- "Add tags to VM 'webserver01' in resource group 'rg-prod'."
- "Update virtual machine 'appvm02' in resource group 'rg-staging' with tags 'environment=production'."
- "Enable boot diagnostics for VM 'db-vm' in resource group 'rg-database' with boot diagnostics 'true'."
- "Change the size of VM 'backend01' in resource group 'rg-prod' to VM size 'Standard_D4s_v3'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Resource group** |  Required | The name of the Azure resource group. This resource group is a logical container for Azure resources. |
| **VM name** |  Required | The name of the virtual machine. |
| **Boot diagnostics** |  Optional | Enable or disable boot diagnostics: `true` or `false`. |
| **License type** |  Optional | License type for Azure Hybrid Benefit: `Windows_Server`, `Windows_Client`, `RHEL_BYOS`, `SLES_BYOS`, or `None` to disable. |
| **Tags** |  Optional | Space-separated tags in 'key=value' format. Use '' to clear existing tags. |
| **User data** |  Optional | Base64-encoded user data for the VM. Use to update custom data scripts. |
| **VM size** |  Optional | The VM size (for example, `Standard_D2s_v3`, `Standard_B2s`). Defaults to Standard_D2s_v5 if not specified. |

#### [Azure MCP CLI](#tab/azure-mcp-cli)

**Example CLI command**

```console
azmcp compute vm update \
  --resource-group <resource-group> \
  --vm-name <vm-name> \
  [--vm-size <vm-size>] \
  [--tags <tags>] \
  [--license-type <license-type>] \
  [--boot-diagnostics <boot-diagnostics>] \
  [--user-data <user-data>]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--resource-group` | string | Yes | The name of the Azure resource group. This is a logical container for Azure resources. |
| `--vm-name` | string | Yes | The name of the virtual machine |
| `--vm-size` | string | No | The VM size (e.g., Standard_D2s_v3, Standard_B2s). Defaults to Standard_D2s_v5 if not specified |
| `--tags` | string | No | Space-separated tags in 'key=value' format. Use '' to clear existing tags. |
| `--license-type` | string | No | License type for Azure Hybrid Benefit: 'Windows_Server', 'Windows_Client', 'RHEL_BYOS', 'SLES_BYOS', or 'None' to disable |
| `--boot-diagnostics` | string | No | Enable or disable boot diagnostics: 'true' or 'false' |
| `--user-data` | string | No | Base64-encoded user data for the VM. Use to update custom data scripts |

---

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ✅ | Idempotent: ✅ | Open World: ❌ | Read Only: ❌ | Secret: ❌ | Local Required: ❌

## Virtual machine scale set: Create

Create and deploy a new Azure Virtual Machine Scale Set (VMSS) to run multiple identical virtual machine instances. Use a scale set to enable horizontal scaling, load balancing, and high availability across instances. You specify the initial instance count and the upgrade policy, such as `Manual`, `Automatic`, or `Rolling`, at creation.

#### [MCP Server](#tab/mcp-server)

<!-- @mcpcli compute vmss create -->

Example prompts include:

- "Create a virtual machine scale set with VMSS name 'webapp-vmss' in resource group 'rg-prod' using image 'Ubuntu2404', admin username 'azureadmin', and location 'eastus'."
- "Create a VMSS with instance count '3', VMSS name 'api-vmss' in resource group 'rg-staging' with image 'Win2022Datacenter', admin username 'adminuser', and location 'westus2'."
- "Deploy a virtual machine scale set with VMSS name 'batch-vmss' in resource group 'rg-batch' using image 'Canonical:UbuntuServer:22_04-lts:latest', admin username 'vmadmin', location 'centralus', upgrade policy 'Rolling', and instance count '5'."
- "Create a Linux VMSS with VMSS name 'linux-scale' in resource group 'rg-linux' using image 'Ubuntu2404', admin username 'azureuser', location 'eastus2', os type 'Linux', and SSH public key '~/.ssh/id_rsa.pub'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Admin username** |  Required | The admin username for the VM. Required for VM creation. |
| **Image** |  Required | The OS image to use. Can be a URN (publisher:offer:SKU:version), a shared gallery image ID (starting with '/sharedGalleries/'), or an alias such as `Ubuntu2404` or `Win2022Datacenter`. |
| **Location** |  Required | The Azure region/location. |
| **Resource group** |  Required | The name of the Azure resource group. This resource group is a logical container for Azure resources. |
| **Virtual machine scale set (VMSS) name** |  Required | The name of the virtual machine scale set. |
| **Admin password** |  Optional | The admin password for Windows VMs or when SSH key is not provided for Linux VMs. |
| **Instance count** |  Optional | Number of VM instances in the scale set. Default is 2. |
| **Os disk size gb** |  Optional | OS disk size in GB. Defaults based on image requirements. |
| **Os disk type** |  Optional | OS disk type: `Premium_LRS`, `StandardSSD_LRS`, `Standard_LRS`. Defaults based on VM size. |
| **Os type** |  Optional | The Operating System type of the disk. Accepted values: `Linux`, `Windows`. |
| **Ssh public key** |  Optional | SSH public key for Linux VMs. Can be the key content or path to a file. |
| **Subnet name** |  Optional | Name of the subnet within the virtual network. |
| **Upgrade policy** |  Optional | Upgrade policy mode: `Automatic`, `Manual`, or `Rolling`. Default is `Manual`. |
| **Virtual network name** |  Optional | Name of an existing virtual network to use. If not specified, a new one is created. |
| **VM size** |  Optional | The VM size (for example, `Standard_D2s_v3`, `Standard_B2s`). Defaults to Standard_D2s_v5 if not specified. |
| **Zone** |  Optional | Availability zone into which to provision the resource. |

#### [Azure MCP CLI](#tab/azure-mcp-cli)

**Example CLI command**

```console
azmcp compute vmss create \
  --resource-group <resource-group> \
  --vmss-name <vmss-name> \
  --location <location> \
  --admin-username <admin-username> \
  --image <image> \
  [--admin-password <admin-password>] \
  [--ssh-public-key <ssh-public-key>] \
  [--vm-size <vm-size>] \
  [--os-type <os-type>] \
  [--instance-count <instance-count>] \
  [--upgrade-policy <upgrade-policy>] \
  [--virtual-network <virtual-network>] \
  [--subnet <subnet>] \
  [--zone <zone>] \
  [--os-disk-size-gb <os-disk-size-gb>] \
  [--os-disk-type <os-disk-type>]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--resource-group` | string | Yes | The name of the Azure resource group. This is a logical container for Azure resources. |
| `--vmss-name` | string | Yes | The name of the virtual machine scale set |
| `--location` | string | Yes | The Azure region/location. Defaults to the resource group's location if not specified. |
| `--admin-username` | string | Yes | The admin username for the VM. Required for VM creation |
| `--admin-password` | string | No | The admin password for Windows VMs or when SSH key is not provided for Linux VMs |
| `--ssh-public-key` | string | No | SSH public key for Linux VMs. Can be the key content or path to a file |
| `--image` | string | Yes | The OS image to use. Can be a URN (publisher:offer:sku:version), a shared gallery image ID (starting with '/sharedGalleries/'), or an alias such as 'Ubuntu2404' or 'Win2022Datacenter'. |
| `--vm-size` | string | No | The VM size (e.g., Standard_D2s_v3, Standard_B2s). Defaults to Standard_D2s_v5 if not specified |
| `--os-type` | string | No | The Operating System type of the disk. Accepted values: Linux, Windows. |
| `--instance-count` | string | No | Number of VM instances in the scale set. Default is 2 |
| `--upgrade-policy` | string | No | Upgrade policy mode: 'Automatic', 'Manual', or 'Rolling'. Default is 'Manual' |
| `--virtual-network` | string | No | Name of an existing virtual network to use. If not specified, a new one will be created |
| `--subnet` | string | No | Name of the subnet within the virtual network |
| `--zone` | string | No | Availability zone into which to provision the resource. |
| `--os-disk-size-gb` | string | No | OS disk size in GB. Defaults based on image requirements |
| `--os-disk-type` | string | No | OS disk type: 'Premium_LRS', 'StandardSSD_LRS', 'Standard_LRS'. Defaults based on VM size |

---

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ✅ | Idempotent: ❌ | Open World: ❌ | Read Only: ❌ | Secret: ✅ | Local Required: ❌

## Virtual machine scale set: Delete

Deletes an Azure Virtual Machine Scale Set (VMSS) and all its VM instances. This permanently removes the scale set and its instances, and the operation is irreversible. Use the force deletion option to remove a VMSS that is running or in a failed state.

#### [MCP Server](#tab/mcp-server)

<!-- @mcpcli compute vmss delete -->

Example prompts include:

- "Delete scale set 'web-vmss' in resource group 'rg-prod'."
- "Remove VMSS 'db-vmss' from resource group 'rg-databases'."
- "Destroy virtual machine scale set 'analytics-vmss' in resource group 'rg-analytics'."
- "Force delete VMSS 'faulty-vmss' in resource group 'rg-staging' using force-deletion."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Resource group** |  Required | The name of the Azure resource group. This resource group is a logical container for Azure resources. |
| **Virtual machine scale set (VMSS) name** |  Required | The name of the virtual machine scale set. |
| **Force deletion** |  Optional | Force delete the resource even if it is in a running or failed state (passes forceDeletion=`true` to the Azure API). |

#### [Azure MCP CLI](#tab/azure-mcp-cli)

**Example CLI command**

```console
azmcp compute vmss delete \
  --resource-group <resource-group> \
  --vmss-name <vmss-name> \
  [--force-deletion <force-deletion>]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--resource-group` | string | Yes | The name of the Azure resource group. This is a logical container for Azure resources. |
| `--vmss-name` | string | Yes | The name of the virtual machine scale set |
| `--force-deletion` | string | No | Force delete the resource even if it is in a running or failed state (passes forceDeletion=true to the Azure API) |

---

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ✅ | Idempotent: ✅ | Open World: ❌ | Read Only: ❌ | Secret: ✅ | Local Required: ❌

## Virtual machine scale set: List or get

List or show Azure Virtual Machine Scale Sets (VMSS) and their virtual machine instances within a `subscription` or `resource-group`. You can list all scale sets, get a specific scale set by `name`, or get a specific VMSS instance by `instance-id`. The command returns scale set and instance properties such as name, location, SKU, capacity, upgrade policy, instance ID, and provisioning state.

#### [MCP Server](#tab/mcp-server)

<!-- @mcpcli compute vmss get -->

Example prompts include:

- "Show all virtual machine scale sets in my subscription."
- "List virtual machine scale sets for resource group 'rg-prod'."
- "Which scale sets are in resource group 'webapp-dev'?"
- "Get details for virtual machine scale set 'web-vmss' in resource group 'rg-prod'."
- "Display VMSS 'api-backend-vmss' from resource group 'rg-staging'."
- "Show instance '2' of virtual machine scale set 'api-backend-vmss' in resource group 'rg-staging'."
- "What's the status of instance '5' in scale set 'web-vmss'?"

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Instance ID** |  Optional | The instance ID of the virtual machine in the scale set. |
| **Virtual machine scale set (VMSS) name** |  Optional | The name of the virtual machine scale set. |

#### [Azure MCP CLI](#tab/azure-mcp-cli)

**Example CLI command**

```console
azmcp compute vmss get \
  [--resource-group <resource-group>] \
  [--vmss-name <vmss-name>] \
  [--instance-id <instance-id>]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--resource-group` | string | No | The name of the Azure resource group. This is a logical container for Azure resources. |
| `--vmss-name` | string | No | The name of the virtual machine scale set |
| `--instance-id` | string | No | The instance ID of the virtual machine in the scale set |

---

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌

## Virtual machine scale set: Update

Update, modify, or reconfigure an existing Azure Virtual Machine Scale Set (VMSS). You can adjust the instance count, resize virtual machines, change the upgrade policy, or update tags. Some changes require `update-instances` to roll out to existing VMs. Changes apply to the scale set resource as a whole, not to an individual virtual machine.

#### [MCP Server](#tab/mcp-server)

<!-- @mcpcli compute vmss update -->

Example prompts include:

- "Update the capacity of VMSS 'web-vmss' in resource group 'rg-prod' to capacity '10'."
- "Enable automatic OS upgrades on VMSS 'api-vmss' in resource group 'rg-staging'."
- "Change upgrade policy to 'Rolling' for VMSS 'compute-vmss' in resource group 'rg-prod'."
- "Add tags 'env=prod owner=teamA' to VMSS 'batch-vmss' in resource group 'rg-data'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Resource group** |  Required | The name of the Azure resource group. This resource group is a logical container for Azure resources. |
| **Virtual machine scale set (VMSS) name** |  Required | The name of the virtual machine scale set. |
| **Capacity** |  Optional | Number of VM instances (capacity) in the scale set. |
| **Enable auto os upgrade** |  Optional | Enable automatic OS image upgrades. Requires health probes or Application Health extension. |
| **Overprovision** |  Optional | Enable or disable overprovisioning. When enabled, Azure provisions more VMs than requested and deletes extra VMs after deployment. |
| **Scale in policy** |  Optional | Scale-in policy to determine which VMs to remove: `Default`, `NewestVM`, or `OldestVM`. |
| **Tags** |  Optional | Space-separated tags in 'key=value' format. Use '' to clear existing tags. |
| **Upgrade policy** |  Optional | Upgrade policy mode: `Automatic`, `Manual`, or `Rolling`. Default is `Manual`. |
| **VM size** |  Optional | The VM size (for example, `Standard_D2s_v3`, `Standard_B2s`). Defaults to Standard_D2s_v5 if not specified. |

#### [Azure MCP CLI](#tab/azure-mcp-cli)

**Example CLI command**

```console
azmcp compute vmss update \
  --resource-group <resource-group> \
  --vmss-name <vmss-name> \
  [--upgrade-policy <upgrade-policy>] \
  [--capacity <capacity>] \
  [--vm-size <vm-size>] \
  [--overprovision <overprovision>] \
  [--enable-auto-os-upgrade <enable-auto-os-upgrade>] \
  [--scale-in-policy <scale-in-policy>] \
  [--tags <tags>]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--resource-group` | string | Yes | The name of the Azure resource group. This is a logical container for Azure resources. |
| `--vmss-name` | string | Yes | The name of the virtual machine scale set |
| `--upgrade-policy` | string | No | Upgrade policy mode: 'Automatic', 'Manual', or 'Rolling'. Default is 'Manual' |
| `--capacity` | string | No | Number of VM instances (capacity) in the scale set |
| `--vm-size` | string | No | The VM size (e.g., Standard_D2s_v3, Standard_B2s). Defaults to Standard_D2s_v5 if not specified |
| `--overprovision` | string | No | Enable or disable overprovisioning. When enabled, Azure provisions more VMs than requested and deletes extra VMs after deployment |
| `--enable-auto-os-upgrade` | string | No | Enable automatic OS image upgrades. Requires health probes or Application Health extension |
| `--scale-in-policy` | string | No | Scale-in policy to determine which VMs to remove: 'Default', 'NewestVM', or 'OldestVM' |
| `--tags` | string | No | Space-separated tags in 'key=value' format. Use '' to clear existing tags. |

---

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ✅ | Idempotent: ✅ | Open World: ❌ | Read Only: ❌ | Secret: ❌ | Local Required: ❌

## Related content

- [What are the Azure MCP Server tools?](index.md)
- [Get started using Azure MCP Server](../get-started.md)
- [Azure Compute documentation](/azure/virtual-machines/)
