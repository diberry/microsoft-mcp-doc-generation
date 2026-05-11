Creates a new Azure managed disk in the specified resource group. Supports creating empty disks (specify --size-gb), disks from a source such as a snapshot, another managed disk, or a blob URI (specify --source), disks from a Shared Image Gallery image version (specify --gallery-image-reference), or disks ready for upload (specify --upload-type and --upload-size-bytes). If location is not specified, defaults to the resource group's location. Supports configuring disk size, storage SKU (e.g., Premium_LRS, Standard_LRS, UltraSSD_LRS), OS type, availability zone, hypervisor generation, tags, encryption settings, performance tier, shared disk, on-demand bursting, and IOPS/throughput limits for UltraSSD disks. Create a disk with network access policy DenyAll, AllowAll, or AllowPrivate and associate a disk access resource during creation.

### Example CLI commands

Basic usage:

```azurecli
azmcp compute disk create
```

With parameters:

```azurecli
azmcp compute disk create --resource-group <resource-group> --disk-name <disk-name> --source <source> --location <location> --size-gb <size-gb> --sku <sku> --os-type <os-type> --zone <zone> --hyper-v-generation <hyper-v-generation> --max-shares <max-shares> --network-access-policy <network-access-policy> --enable-bursting <enable-bursting> --tags <tags> --disk-encryption-set <disk-encryption-set> --encryption-type <encryption-type> --disk-access <disk-access> --tier <tier> --gallery-image-reference <gallery-image-reference> --gallery-image-reference-lun <gallery-image-reference-lun> --disk-iops-read-write <disk-iops-read-write> --disk-mbps-read-write <disk-mbps-read-write> --upload-type <upload-type> --upload-size-bytes <upload-size-bytes> --security-type <security-type>
```

| Parameter | Type | Description |
|-----------|------|-------------|
| `--tenant` | string | The Microsoft Entra ID tenant ID or name. This can be either the GUID identifier or the display name of your Entra ID tenant. |
| `--auth-method` | string | Authentication method to use. Options: 'credential' (Azure CLI/managed identity), 'key' (access key), or 'connectionString'. |
| `--retry-delay` | string | Initial delay in seconds between retry attempts. For exponential backoff, this value is used as the base. |
| `--retry-max-delay` | string | Maximum delay in seconds between retries, regardless of the retry strategy. |
| `--retry-max-retries` | string | Maximum number of retry attempts for failed operations before giving up. |
| `--retry-mode` | string | Retry strategy to use. 'fixed' uses consistent delays, 'exponential' increases delay between attempts. |
| `--retry-network-timeout` | string | Network operation timeout in seconds. Operations taking longer than this will be cancelled. |
| `--subscription` | string | Specifies the Azure subscription to use. Accepts either a subscription ID (GUID) or display name. If not specified, the AZURE_SUBSCRIPTION_ID environment variable will be used instead. |
| `--resource-group` | string | The name of the Azure resource group. This is a logical container for Azure resources. |
| `--disk-name` | string | The name of the disk |
| `--source` | string | Source to create the disk from, including a resource ID of a snapshot or disk, or a blob URI of a VHD. When a source is provided, --size-gb is optional and defaults to the source size. |
| `--location` | string | The Azure region/location. Defaults to the resource group's location if not specified. |
| `--size-gb` | string | Size of the disk in GB. Max size: 4095 GB. |
| `--sku` | string | Underlying storage SKU. Accepted values: Premium_LRS, PremiumV2_LRS, Premium_ZRS, StandardSSD_LRS, StandardSSD_ZRS, Standard_LRS, UltraSSD_LRS. |
| `--os-type` | string | The Operating System type of the disk. Accepted values: Linux, Windows. |
| `--zone` | string | Availability zone into which to provision the resource. |
| `--hyper-v-generation` | string | The hypervisor generation of the Virtual Machine. Applicable to OS disks only. Accepted values: V1, V2. |
| `--max-shares` | string | The maximum number of VMs that can attach to the disk at the same time. Value greater than one indicates a shared disk. |
| `--network-access-policy` | string | Policy for accessing the disk via network. Accepted values: AllowAll, AllowPrivate, DenyAll. |
| `--enable-bursting` | string | Enable on-demand bursting beyond the provisioned performance target of the disk. Does not apply to Ultra disks. Accepted values: true, false. |
| `--tags` | string | Space-separated tags in 'key=value' format. Use '' to clear existing tags. |
| `--disk-encryption-set` | string | Resource ID of the disk encryption set to use for enabling encryption at rest. |
| `--encryption-type` | string | Encryption type of the disk. Accepted values: EncryptionAtRestWithCustomerKey, EncryptionAtRestWithPlatformAndCustomerKeys, EncryptionAtRestWithPlatformKey. |
| `--disk-access` | string | Resource ID of the disk access resource for using private endpoints on disks. |
| `--tier` | string | Performance tier of the disk (e.g., P10, P15, P20, P30, P40, P50, P60, P70, P80). Applicable to Premium SSD disks only. |
| `--gallery-image-reference` | string | Resource ID of a Shared Image Gallery image version to use as the source for the disk. Format: /subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.Compute/galleries/{gallery}/images/{image}/versions/{version}. |
| `--gallery-image-reference-lun` | string | LUN (Logical Unit Number) of the data disk in the gallery image version. If specified, the disk is created from the data disk at this LUN. If not specified, the disk is created from the OS disk of the image. |
| `--disk-iops-read-write` | string | The number of IOPS allowed for this disk. Only settable for UltraSSD disks. |
| `--disk-mbps-read-write` | string | The bandwidth allowed for this disk in MBps. Only settable for UltraSSD disks. |
| `--upload-type` | string | Type of upload for the disk. Accepted values: Upload, UploadWithSecurityData. When specified, the disk is created in a ReadyToUpload state. |
| `--upload-size-bytes` | string | The size in bytes (including the VHD footer of 512 bytes) of the content to be uploaded. Required when --upload-type is specified. |
| `--security-type` | string | Security type of the managed disk. Accepted values: ConfidentialVM_DiskEncryptedWithCustomerKey, ConfidentialVM_DiskEncryptedWithPlatformKey, ConfidentialVM_VMGuestStateOnlyEncryptedWithPlatformKey, Standard, TrustedLaunch. Required when --upload-type is UploadWithSecurityData. |

